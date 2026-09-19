using App2d.Core;
using App2d.Core.Constraints;
using System.Numerics;

namespace App2d.Noodle.Projectors;

/// <summary>Editor wrapper around Core's annular distance projection.</summary>
internal sealed class DistanceProjector2D
{
    public DistanceProjector2D(float minimumDistance, float maximumDistance)
    {
        ArgGuard.ThrowIfNegativeOrNotFinite(minimumDistance);
        ArgGuard.ThrowIfNegativeOrNotFinite(maximumDistance);
        Limits = ConstraintLimit1D.Between(minimumDistance, maximumDistance);
    }

    public string Name => Limits.IsLocked ? "DISTANCE / ROD" : "DISTANCE RANGE";
    public string Equation => Limits.IsLocked ? "r = L" : "min <= r <= max";
    public ConstraintLimit1D Limits { get; }
    public float MinimumDistance => Limits.Minimum;
    public float MaximumDistance => Limits.Maximum;

    public Vector2 Project(Vector2 anchor, Vector2 requestedPoint) =>
        ConstraintMath2D.ProjectPointToDistance(anchor, requestedPoint, Limits);
}

