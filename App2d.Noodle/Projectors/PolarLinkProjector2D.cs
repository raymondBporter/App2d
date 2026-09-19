using App2d.Core;
using App2d.Core.Constraints;
using System.Numerics;

namespace App2d.Noodle.Projectors;

/// <summary>Fixed-radius polar projection used to preview a limited bone hinge.</summary>
internal sealed class PolarLinkProjector2D
{
    public const string DisplayName = "POLAR LINK / HINGE PREVIEW";
    public const string DisplayEquation = "r = L,  min <= angle <= max";

    public PolarLinkProjector2D(float length, float minimumAngle, float maximumAngle)
    {
        ArgGuard.ThrowIfNotPositive(length);
        Length = length;
        AngleLimits = ConstraintLimit1D.Between(minimumAngle, maximumAngle);
    }

    public float Length { get; }
    public ConstraintLimit1D AngleLimits { get; }
    public float MinimumAngle => AngleLimits.Minimum;
    public float MaximumAngle => AngleLimits.Maximum;

    public Vector2 Project(Vector2 anchor, Vector2 requestedPoint) =>
        ConstraintMath2D.ProjectPointToPolarArc(anchor, requestedPoint, Length, AngleLimits);
}
