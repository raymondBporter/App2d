using App2d.Core.Geometry;
using App2d.Core.Geometry.Operations.Containment;
using App2d.Core.Geometry.Operations.Convex;
using App2d.Core.Geometry.Operations.Diameter;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using System.Numerics;

namespace App2d.Core.Shapes;

/// <summary>A filled capsule in local space: every point within <see cref="Radius"/> of the spine segment.</summary>
public readonly partial record struct Capsule2D : IConvexShape2D
{
    /// <inheritdoc/>
    public string Kind => ShapeKinds2D.Capsule;

    /// <summary>Creates a capsule.</summary>
    /// <param name="start">The finite spine start; may equal <paramref name="end"/> for a circle.</param>
    /// <param name="end">The finite spine end.</param>
    /// <param name="radius">The finite, positive radius.</param>
    public Capsule2D(Vector2 start, Vector2 end, float radius)
    {
        Start = start;
        End = end;
        Radius = radius;
    }

    /// <summary>The spine start.</summary>
    public Vector2 Start
    {
        get;
    }

    /// <summary>The spine end.</summary>
    public Vector2 End
    {
        get;
    }

    /// <summary>The center of the capsule's spine, in either endpoint order.</summary>
    public Vector2 Center => Rect2D.FromPoints(Start, End).Center;

    /// <summary>The radius around the spine.</summary>
    public float Radius
    {
        get;
    }

    /// <inheritdoc/>
    public float Area => Area2D.Capsule(Start, End, Radius);

    /// <inheritdoc/>
    public float Diameter => Diameter2D.Capsule(Start, End, Radius);

    /// <inheritdoc/>
    public bool ContainsPoint(Vector2 localPoint) => Containment2D.Capsule(localPoint, Start, End, Radius);

    /// <inheritdoc/>
    public Vector2 GetSupportPoint(Vector2 localDirection) => SupportPoint2D.Capsule(localDirection, Start, End, Radius);
}
