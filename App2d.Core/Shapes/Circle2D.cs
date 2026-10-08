using App2d.Core.Geometry;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Shapes;

/// <summary>A filled circle in local space.</summary>
public sealed class Circle2D : IConvexShape2D
{
    /// <inheritdoc/>
    public string Kind => ShapeKinds2D.Circle;

    /// <summary>Creates a circle.</summary>
    /// <param name="radius">The finite, positive radius.</param>
    /// <param name="center">The finite local center.</param>
    public Circle2D(float radius, Vector2 center = default)
    {
        ArgGuard.ThrowIfNotFiniteOrNotPositive(radius);
        ArgGuard.ThrowIfNotFinite(center);
        Radius = radius;
        Center = center;
    }

    /// <summary>The radius.</summary>
    public float Radius { get; }

    /// <summary>The local center.</summary>
    public Vector2 Center { get; }

    /// <inheritdoc/>
    public float Area => Area2D.Circle(Radius);

    /// <inheritdoc/>
    public bool ContainsPoint(Vector2 localPoint) => Containment2D.Circle(localPoint, Center, Radius);

    /// <inheritdoc/>
    public Vector2 GetSupportPoint(Vector2 localDirection) => SupportPoint2D.Circle(localDirection, Center, Radius);
}
