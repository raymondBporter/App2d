using App2d.Core.Characters.Geometry;
using App2d.Core.Constraints;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Validation;
using System.Numerics;
using System.Text.Json.Serialization;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// The declared setup kinds of a rig node (character-system migration, P3). A control states its kind; nothing infers
/// it from whether a nullable transform exists.
/// </summary>
public static class RigNodeKinds
{
    /// <summary>
    /// Legacy input: the rest position and rest angle are authored in the character frame (<see cref="ModelControl.Rest"/>,
    /// <see cref="ModelControl.RestAngle"/>) and the parent-local similarity is derived when the model resolves. Every
    /// shipped model authors its controls this way; the compiler turns them into canonical nodes without loss.
    /// </summary>
    public const string Point = "point";
    /// <summary>
    /// Canonical node: a parent-local similarity setup (<see cref="ModelControl.Local"/>), an optional bone length along
    /// local +X and an optional <see cref="RigLink"/> binding the origin to a position on the parent bone. The rest
    /// position is derived, never authored.
    /// </summary>
    public const string Bone = "bone";
    public static readonly string[] All = [Point, Bone];
}

/// <summary>
/// A parent-local similarity setup: translation, rotation, uniform scale, optional reflection (the local Y axis is
/// mirrored) and a drawing-layer offset. Composed poses stay similarities, so no child inherits shear.
/// </summary>
public sealed record RigSetup
{
    public float X { get; set; }
    public float Y { get; set; }
    /// <summary>Rotation in radians, in the parent's frame. Under a reflected parent the character-space turn is reversed.</summary>
    public float Rotation { get; set; }
    /// <summary>Uniform scale, positive. Bone length is a dimension, not a scale.</summary>
    public float Scale { get; set; } = 1;
    /// <summary>Mirrors the local Y axis. Children inherit the reflection.</summary>
    public bool Reflect { get; set; }
    /// <summary>Drawing-layer offset from the parent (character-migration-baseline.md, section 7.4).</summary>
    public float Layer { get; set; }

    [JsonIgnore] public Vector2 Translation => new(X, Y);
    [JsonIgnore] public Vector2 ScaleVector => new(Scale, Reflect ? -Scale : Scale);

    public void Validate(string field)
    {
        new Limit(-10000, 10000).Check(X, field + ".x");
        new Limit(-10000, 10000).Check(Y, field + ".y");
        new Limit(-1000, 1000).Check(Rotation, field + ".rotation");
        new Limit(.001f, 1000).Check(Scale, field + ".scale");
        new Limit(-32, 32).Check(Layer, field + ".layer");
    }
}

/// <summary>Orientation policies of a linked node.</summary>
public static class RigLinkOrientations
{
    /// <summary>The node turns with its parent (ordinary child).</summary>
    public const string Parent = "parent";
    /// <summary>The node keeps the character's axes while its origin follows the parent, like a locomotion-frame socket.</summary>
    public const string Character = "character";
    public static readonly string[] All = [Parent, Character];
}

/// <summary>
/// Binds a node's origin to a normalized position <see cref="T"/> along the parent bone (0 its origin, 1 its tip, from the
/// parent's final length) plus an offset in the parent's local axes. A linked node has no independent authored position.
/// </summary>
public sealed record RigLink
{
    public float T { get; set; } = 1;
    public float OffsetX { get; set; }
    public float OffsetY { get; set; }
    public string Orientation { get; set; } = RigLinkOrientations.Parent;

    [JsonIgnore] public Vector2 Offset => new(OffsetX, OffsetY);
    /// <summary>A plain tip binding: the origin is the parent's +X tip and the node turns with it.</summary>
    [JsonIgnore] public bool IsTip => T == 1 && OffsetX == 0 && OffsetY == 0 && Orientation == RigLinkOrientations.Parent;

    /// <summary>The bound origin in the parent's local axes, from the parent's final length.</summary>
    public Vector2 Origin(float parentLength) => new(parentLength * T + OffsetX, OffsetY);

    public void Validate(string field)
    {
        new Limit(-10, 10).Check(T, field + ".t");
        new Limit(-100, 100).Check(OffsetX, field + ".offsetX");
        new Limit(-100, 100).Check(OffsetY, field + ".offsetY");
        if (!RigLinkOrientations.All.Contains(Orientation))
        {
            throw new InvalidDataException($"{field}: unknown orientation '{Orientation}'.");
        }
    }
}

/// <summary>
/// Angular joint limits of a bone node (character-system migration, P4b): bounds on the node's parent-local rotation, in
/// radians, in the node's own local sense. Because the bound value is the local rotation, a reflected parent or a mirrored
/// actor does not invert the convention: the same limit folds a mirrored limb the same way relative to its parent. The
/// evaluator clamps an ordinary angle channel to the limits and records the state; a normalized bend channel maps 0..1 onto
/// [Min, Max] exactly at both ends, so changing the limits intentionally changes a normalized performance while angle keys
/// keep their authored values. The limit-aware IK solve (<see cref="BoneFrameIk"/>) returns the closest in-range pose with
/// its residual. Reuses <see cref="Interval{T}"/> for the interval semantics.
/// </summary>
public sealed record RigLimit
{
    public static readonly Limit Range = new(-1000, 1000);

    public float Min { get; set; } = -MathF.PI / 2;
    public float Max { get; set; } = MathF.PI / 2;

    /// <summary>A quarter turn either way, for a limit the author has just added.</summary>
    public static RigLimit Starter => new();

    [JsonIgnore] public Interval<float> Interval => new(ArgGuard.Finite(Min), ArgGuard.Finite(Max));
    [JsonIgnore] public float Span => Max - Min;

    public bool Contains(float rotation) => rotation >= Min && rotation <= Max;

    /// <summary>The nearest in-range rotation and the limit state for a local rotation.</summary>
    public ConstraintLimitEvaluation1D Evaluate(float rotation) => ConstraintMath1D.EvaluateLimit(Interval, rotation);

    /// <summary>A normalized bend through the range: 0 is exactly <see cref="Min"/>, 1 exactly <see cref="Max"/>; outside 0..1 is clamped.</summary>
    public float FromNormalized(float bend) => bend <= 0 ? Min : bend >= 1 ? Max : Min + bend * Span;

    /// <summary>The normalized position of a local rotation in the range (0 for a locked limit).</summary>
    public float ToNormalized(float rotation) => Span == 0 ? 0 : (rotation - Min) / Span;

    /// <summary>
    /// The representative of a wrapped angle that the range can hold, or the one nearest the range: a solved frame angle is
    /// known only modulo a turn, while a limit may span more than one.
    /// </summary>
    public float Unwrap(float rotation)
    {
        var best = rotation;
        var bestError = MathF.Abs(Evaluate(rotation).Error);
        for (var k = -2; k <= 2; k++)
        {
            var candidate = rotation + k * MathF.Tau;
            var error = MathF.Abs(Evaluate(candidate).Error);
            if (error < bestError - 1e-6f)
            {
                best = candidate;
                bestError = error;
            }
        }

        return best;
    }

    public void Validate(string field)
    {
        Range.Check(Min, field + ".min");
        Range.Check(Max, field + ".max");
        if (Min > Max)
        {
            throw new InvalidDataException($"{field}: min must not exceed max.");
        }
    }
}

/// <summary>
/// One compiled rig node: the parent-local similarity setup every control resolves to, its dimension, link and joint limits,
/// and its derived rest frame in the character frame. The evaluator composes <c>nodeInCharacter = nodeLocal * parentInCharacter</c>
/// from these; positions, directions and angles are read from the result.
/// </summary>
public sealed record RigNode(
    string Id,
    string? Parent,
    string Kind,
    RigSetup Setup,
    float Length,
    RigLink? Link,
    Matrix3x2 RestFrame,
    float RestAngle,
    float RestLayer,
    int Handedness,
    RigLimit? Limit = null)
{
    public Vector2 ScaleVector => Setup.ScaleVector;
    public float Rotation => Setup.Rotation;
    public float Layer => Setup.Layer;
    public bool CharacterOriented => Link?.Orientation == RigLinkOrientations.Character;
    /// <summary>The rest position in the character frame at the rest layer.</summary>
    public Vector3 Rest => new(RestFrame.M31, RestFrame.M32, RestLayer);
    /// <summary>The setup origin in the parent's local axes, before animation: the link's bound position or the setup translation.</summary>
    public Vector2 Origin(float parentLength) => Link is { } link ? link.Origin(parentLength) : Setup.Translation;
}

/// <summary>Compiles authored controls into canonical nodes and their rest frame graph.</summary>
/// <summary>Compiles authored controls into canonical nodes and their rest frame graph.</summary>
internal static class RigGraph
{
    /// <summary>Controls ordered so every parent precedes its children, keeping document order otherwise.</summary>
    public static List<ModelControl> Order(CharacterModel model)
    {
        var controls = model.Controls.ToDictionary(c => c.Id, StringComparer.Ordinal);
        var order = new List<ModelControl>();
        var placed = new HashSet<string>(StringComparer.Ordinal);
        void Place(ModelControl control)
        {
            if (!placed.Add(control.Id))
            {
                return;
            }

            if (control.Parent is not null)
            {
                Place(controls[control.Parent]);
            }

            order.Add(control);
        }
        foreach (var control in model.Controls)
        {
            Place(control);
        }

        return order;
    }

    /// <summary>Compiles from the authored controls alone: no variant, no parameter values, no overrides.</summary>
    public static Dictionary<string, RigNode> Compile(CharacterModel model) => Compile(model, Order(model), null);

    /// <summary>
    /// Compiles every control in <paramref name="order"/> from its final setup, length and link: the <paramref name="layout"/>
    /// after parameter bindings and explicit overrides wrote it, or the authored values when it is null. A point control nobody
    /// moved keeps its authored rest bit for bit; one whose setup changed, or whose ancestor's did, derives its rest from the
    /// setup like a bone node. There is no absolute-position pin: a variant moves a node through its setup overrides (P6a).
    /// </summary>
    public static Dictionary<string, RigNode> Compile(CharacterModel model, IReadOnlyList<ModelControl> order, RigLayout? layout)
    {
        var nodes = new Dictionary<string, RigNode>(StringComparer.Ordinal);
        var derived = new HashSet<string>(StringComparer.Ordinal);
        foreach (var control in order)
        {
            var parent = control.Parent is null ? null : nodes[control.Parent];
            var parentFrame = parent?.RestFrame ?? Matrix3x2.Identity;
            var parentAngle = parent?.RestAngle ?? 0;
            var parentLayer = parent?.RestLayer ?? 0;
            var parentHand = parent?.Handedness ?? 1;
            var moved = layout?.Moved.Contains(control.Id) == true || parent is not null && derived.Contains(parent.Id);
            if (moved)
            {
                derived.Add(control.Id);
            }

            RigNode node;
            switch (control.Node)
            {
                case RigNodeKinds.Bone:
                    {
                        var setup = layout?.Setups[control.Id] ?? control.Local ?? new RigSetup();
                        var link = layout is null ? control.Link : layout.Links[control.Id];
                        var length = layout?.Lengths[control.Id] ?? control.Length;
                        var limit = layout is null ? control.Limit : layout.Limits[control.Id];
                        if (limit is not null && !limit.Contains(setup.Rotation))
                        {
                            throw new InvalidDataException($"Control '{control.Id}': its rest rotation {setup.Rotation:0.###} lies outside its joint limits [{limit.Min:0.###}, {limit.Max:0.###}]; widen the limits or turn the rest.");
                        }

                        var origin = link is null ? setup.Translation : link.Origin(parent?.Length ?? 0);
                        Matrix3x2 frame;
                        float angle;
                        int hand;
                        if (link?.Orientation == RigLinkOrientations.Character)
                        {
                            var at = Vector2.Transform(origin, parentFrame);
                            frame = LayeredFrame2D.Local(setup.ScaleVector, setup.Rotation, at, 0).Frame.Matrix;
                            angle = setup.Rotation;
                            hand = setup.Reflect ? -1 : 1;
                        }
                        else
                        {
                            frame = LayeredFrame2D.Local(setup.ScaleVector, setup.Rotation, origin, setup.Layer).Frame.Matrix * parentFrame;
                            angle = parentAngle + parentHand * setup.Rotation;
                            hand = parentHand * (setup.Reflect ? -1 : 1);
                        }
                        node = new(control.Id, control.Parent, control.Node, setup, length, link, frame, angle, parentLayer + setup.Layer, hand, limit);
                        break;
                    }
                default:
                    {
                        // A point control's character-frame rest is authoritative unless a binding or override moved it or an
                        // ancestor; the local similarity is what reproduces it on the parent.
                        if (!moved || layout is null)
                        {
                            var r = control.Rest.XYZ;
                            var frame = Matrix3x2.CreateRotation(control.RestAngle) * Matrix3x2.CreateTranslation(r.X, r.Y);
                            Vector2 translation;
                            if (parent is null)
                            {
                                translation = new(r.X, r.Y);
                            }
                            else if (!new Frame2D(parentFrame).TryToLocal(new(r.X, r.Y), out translation))
                            {
                                throw new InvalidDataException($"Cannot place '{control.Id}' beneath a collapsed parent.");
                            }
                            var setup = new RigSetup { X = translation.X, Y = translation.Y, Rotation = parentHand * (control.RestAngle - parentAngle), Layer = r.Z - parentLayer };
                            node = new(control.Id, control.Parent, RigNodeKinds.Point, setup, control.Length, null, frame, control.RestAngle, r.Z, parentHand);
                        }
                        else
                        {
                            var setup = layout.Setups[control.Id];
                            var position = parent is null ? setup.Translation : Vector2.Transform(setup.Translation, parentFrame);
                            var angle = parentAngle + parentHand * setup.Rotation;
                            var frame = Matrix3x2.CreateRotation(angle) * Matrix3x2.CreateTranslation(position.X, position.Y);
                            node = new(control.Id, control.Parent, RigNodeKinds.Point, setup, control.Length, null, frame, angle, parentLayer + setup.Layer, parentHand);
                        }
                        break;
                    }
            }
            nodes.Add(control.Id, node);
        }

        return nodes;
    }
}
