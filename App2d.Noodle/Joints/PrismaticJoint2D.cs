using System.Numerics;

namespace App2d.Noodle.Joints;

/// <summary>A slider that permits translation only along one axis.</summary>
internal sealed class PrismaticJoint2D : IJoint2D
{
    public PrismaticJoint2D(Vector2 axis, float minimumTranslation, float maximumTranslation)
    {
        if (!float.IsFinite(axis.X) || !float.IsFinite(axis.Y) || axis.LengthSquared() <= float.Epsilon)
            throw new ArgumentException("The slider axis must be finite and non-zero.", nameof(axis));
        if (!float.IsFinite(minimumTranslation) || !float.IsFinite(maximumTranslation) ||
            minimumTranslation > maximumTranslation)
        {
            throw new ArgumentException("The translation limits must be finite and ordered.");
        }

        Axis = Vector2.Normalize(axis);
        MinimumTranslation = minimumTranslation;
        MaximumTranslation = maximumTranslation;
    }

    public string Name => "PRISMATIC";
    public string Constraint => "p = anchor + axis * t,  min <= t <= max";
    public Vector2 Axis { get; }
    public float MinimumTranslation { get; }
    public float MaximumTranslation { get; }

    public Vector2 Constrain(Vector2 parentAnchor, Vector2 requestedChildAnchor)
    {
        var translation = Vector2.Dot(requestedChildAnchor - parentAnchor, Axis);
        translation = Math.Clamp(translation, MinimumTranslation, MaximumTranslation);
        return parentAnchor + Axis * translation;
    }
}
