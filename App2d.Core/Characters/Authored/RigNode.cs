using App2d.Core.Characters.Geometry;
using App2d.Core.Mathematics;
using App2d.Core.Mathematics.Transforms;
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
    /// <summary>
    /// Legacy affine adapter: a full parent-local <see cref="Affine2D"/> setup (nonuniform scale, shear) evaluated by
    /// the explicit-bone path (<see cref="LegacyAffineRig"/>). Retained for the native wheel fixture and curve-face's
    /// scaled eyes until P11; the canonical similarity rig does not express inherited nonuniform scale or shear.
    /// </summary>
    public const string Affine = "affine";
    public static readonly string[] All = [Point, Bone, Affine];
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
/// One compiled rig node: the parent-local similarity setup every control resolves to, its dimension and link, and its
/// derived rest frame in the character frame. The evaluator composes <c>nodeInCharacter = nodeLocal * parentInCharacter</c>
/// from these; positions, directions and angles are read from the result.
/// </summary>
public sealed record RigNode(
    string Id,
    string? Parent,
    string Kind,
    RigSetup Setup,
    float Length,
    RigLink? Link,
    Affine2D? Affine,
    Matrix3x2 RestFrame,
    float RestAngle,
    float RestLayer,
    int Handedness)
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
    public static Dictionary<string, RigNode> Compile(CharacterModel model) => Compile(model, Order(model), null, new Dictionary<string, Vector3>(StringComparer.Ordinal));

    /// <summary>
    /// Compiles every control in <paramref name="order"/> from its final setup, length and link: the <paramref name="layout"/>
    /// after parameter bindings and explicit overrides wrote it, or the authored values when it is null. <paramref name="pins"/>
    /// are explicit character-frame rest overrides of point and affine nodes (a variant's <c>rest</c>): a pinned node's local
    /// translation is re-derived beneath its parent's final frame. A point control nobody moved keeps its authored rest bit for
    /// bit; one whose setup changed, or whose ancestor's did, derives its rest from the setup like a bone node.
    /// </summary>
    public static Dictionary<string, RigNode> Compile(CharacterModel model, IReadOnlyList<ModelControl> order, RigLayout? layout, IReadOnlyDictionary<string, Vector3> pins)
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
                case RigNodeKinds.Affine:
                {
                    var affine = control.Transform!;
                    float layer;
                    if (pins.TryGetValue(control.Id, out var pin))
                    {
                        if (!Matrix3x2.Invert(parentFrame, out var inverse))
                        {
                            throw new InvalidDataException($"Cannot move '{control.Id}' beneath a collapsed parent.");
                        }

                        var local = Vector2.Transform(new(pin.X, pin.Y), inverse);
                        affine = affine with { X = local.X, Y = local.Y };
                        layer = pin.Z - parentLayer;
                    }
                    else if (layout is not null)
                    {
                        var setup = layout.Setups[control.Id];
                        affine = affine with { X = setup.X, Y = setup.Y };
                        layer = setup.Layer;
                    }
                    else
                    {
                        layer = control.Rest.Z - parentLayer;
                    }
                    var frame = affine.Matrix * parentFrame;
                    node = new(control.Id, control.Parent, control.Node, new() { X = affine.X, Y = affine.Y, Rotation = affine.Rotation, Layer = layer }, control.Length, null, affine, frame,
                        control.RestAngle, parentLayer + layer, frame.GetDeterminant() < 0 ? -1 : 1);
                    break;
                }
                case RigNodeKinds.Bone:
                {
                    if (pins.ContainsKey(control.Id))
                    {
                        throw new InvalidDataException($"'{control.Id}' is a bone node; its rest is derived from its setup, length and link and cannot be pinned.");
                    }

                    var setup = layout?.Setups[control.Id] ?? control.Local ?? new RigSetup();
                    var link = layout is null ? control.Link : layout.Links[control.Id];
                    var length = layout?.Lengths[control.Id] ?? control.Length;
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
                    node = new(control.Id, control.Parent, control.Node, setup, length, link, null, frame, angle, parentLayer + setup.Layer, hand);
                    break;
                }
                default:
                {
                    // A point control's character-frame rest is authoritative unless a binding or override moved it or an
                    // ancestor; the local similarity is what reproduces it on the parent.
                    var pinned = pins.TryGetValue(control.Id, out var pin);
                    if (pinned)
                    {
                        derived.Add(control.Id);
                    }

                    if (pinned || !moved || layout is null)
                    {
                        var r = pinned ? pin : control.Rest.XYZ;
                        var frame = Matrix3x2.CreateRotation(control.RestAngle) * Matrix3x2.CreateTranslation(r.X, r.Y);
                        Vector2 translation;
                        if (parent is null)
                        {
                            translation = new(r.X, r.Y);
                        }
                        else if (parent.Kind == RigNodeKinds.Affine)
                        {
                            // The legacy affine adapter carries point children by rotation only; its scale and shear stay on its own frame.
                            translation = Rotation2D.Apply(new Vector2(r.X, r.Y) - new Vector2(parentFrame.M31, parentFrame.M32), -parentAngle);
                        }
                        else if (!new Frame2D(parentFrame).TryToLocal(new(r.X, r.Y), out translation))
                        {
                            throw new InvalidDataException($"Cannot place '{control.Id}' beneath a collapsed parent.");
                        }
                        var setup = new RigSetup { X = translation.X, Y = translation.Y, Rotation = parentHand * (control.RestAngle - parentAngle), Layer = r.Z - parentLayer };
                        node = new(control.Id, control.Parent, RigNodeKinds.Point, setup, control.Length, null, null, frame, control.RestAngle, r.Z, parentHand);
                    }
                    else
                    {
                        var setup = layout.Setups[control.Id];
                        var position = parent is null ? setup.Translation
                            : parent.Kind == RigNodeKinds.Affine ? new Vector2(parentFrame.M31, parentFrame.M32) + Rotation2D.Apply(setup.Translation, parentAngle)
                            : Vector2.Transform(setup.Translation, parentFrame);
                        var angle = parentAngle + parentHand * setup.Rotation;
                        var frame = Matrix3x2.CreateRotation(angle) * Matrix3x2.CreateTranslation(position.X, position.Y);
                        node = new(control.Id, control.Parent, RigNodeKinds.Point, setup, control.Length, null, null, frame, angle, parentLayer + setup.Layer, parentHand);
                    }
                    break;
                }
            }
            nodes.Add(control.Id, node);
        }

        return nodes;
    }
}
