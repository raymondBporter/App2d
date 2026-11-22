using App2d.Core.Geometry.Containment;
using App2d.Core.Geometry.Convex;
using App2d.Core.Geometry.Measurements;

namespace App2d.Core.Geometry.Shapes;

/// <summary>A filled circle in local space.</summary>
public readonly partial record struct Circle2D : IConvexShape2D
{
    public const string Kind = "circle";

    /// <summary>Creates a circle.</summary>
    /// <param name="radius">The finite, positive radius.</param>
    /// <param name="center">The finite local center.</param>
    public Circle2D(float radius, Vector2f center = default)
    {
        Radius = radius;
        Center = center;
    }

    /// <summary>The radius.</summary>
    public float Radius { get; init; }

    /// <summary>The local center.</summary>
    public Vector2f Center { get; init; }

    /// <inheritdoc/>
    public float Area => Area2D.Circle(Radius);

    /// <inheritdoc/>
    public float Diameter => Diameter2D.Circle(Radius);

    /// <inheritdoc/>
    public bool ContainsPoint(Vector2f localPoint) => Containment2D.Circle(localPoint, Center, Radius);

    /// <inheritdoc/>
    public Vector2f GetSupportPoint(Vector2f localDirection) => SupportPoint2D.Circle(localDirection, Center, Radius);
}
