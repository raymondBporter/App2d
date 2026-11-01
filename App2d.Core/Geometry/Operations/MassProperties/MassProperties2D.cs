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
        Ensure.Positive(mass);
        var inertia = Ensure.Positive(MomentOfInertia * (mass / Mass));
        return this with { Mass = mass, MomentOfInertia = inertia };
    }

    /// <summary>Parallel-axis theorem: I(point) = I(centroid) + mass * distance squared.</summary>
    public double MomentOfInertiaAbout(Vector2d point)
    {
        Ensure.Finite(point);
        return Ensure.Positive(MomentOfInertia + Mass * (point - CenterOfMass).LengthSquared());
    }
}
