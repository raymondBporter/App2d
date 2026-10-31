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
    /// Point control: the rest position and rest angle are authored in the character frame (<see cref="ModelControl.Rest"/>,
    /// <see cref="ModelControl.RestAngle"/>) and the parent-local similarity is derived when the model resolves.
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
