using App2d.Core.Mathematics;
using App2d.Core.Validation;

namespace App2d.Core.Geometry.Operations.MassProperties;

/// <summary>Uniform solid lamina in shape-local coordinates. Inertia is about the centroid, perpendicular to the plane.</summary>
public readonly record struct MassProperties2D(
    double Area,
    double Mass,
    Vector2d CenterOfMass,
    double MomentOfInertia)
{
    /// <summary>Preserves geometry and changes the uniform density to obtain the requested total mass.</summary>
    public MassProperties2D WithMass(double mass)
    {
        ArgGuard.Positive(mass);
        var inertia = ArgGuard.Positive(MomentOfInertia * (mass / Mass));
        return this with { Mass = mass, MomentOfInertia = inertia };
    }

    /// <summary>Parallel-axis theorem: I(point) = I(centroid) + mass * distance squared.</summary>
    public double MomentOfInertiaAbout(Vector2d point)
    {
        ArgGuard.ThrowIf(!point.IsFinite(), "The reference point must be finite.", nameof(point));
        return ArgGuard.Positive(MomentOfInertia + Mass * (point - CenterOfMass).LengthSquared());
    }
}
