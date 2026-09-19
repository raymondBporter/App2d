using App2d.Core;
using App2d.Core.Constraints;
using System.Numerics;

namespace App2d.Noodle.Projectors;

/// <summary>Editor wrapper around Core's limited projection onto an axis.</summary>
internal sealed class AxisProjector2D
{
    public const string DisplayName = "AXIS / PRISMATIC PREVIEW";
    public const string DisplayEquation = "p = anchor + axis * t,  min <= t <= max";

    public AxisProjector2D(Vector2 axis, float minimumTranslation, float maximumTranslation)
    {
        ArgGuard.ThrowIfNotFiniteOrZero(axis);
        Axis = Vector2.Normalize(axis);
        Limits = ConstraintLimit1D.Between(minimumTranslation, maximumTranslation);
    }

    public Vector2 Axis { get; }
    public ConstraintLimit1D Limits { get; }
    public float MinimumTranslation => Limits.Minimum;
    public float MaximumTranslation => Limits.Maximum;

    public Vector2 Project(Vector2 anchor, Vector2 requestedPoint) =>
        ConstraintMath2D.ProjectPointToAxis(anchor, requestedPoint, Axis, Limits);
}
