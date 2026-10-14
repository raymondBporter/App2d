using App2d.Core.Geometry;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Shapes;

/// <summary>A filled capsule in local space: every point within <see cref="Radius"/> of the spine segment.</summary>
public sealed class Capsule2D : IConvexShape2D
{
    /// <inheritdoc/>
    public string Kind => ShapeKinds2D.Capsule;

    /// <summary>Creates a capsule.</summary>
    /// <param name="start">The finite spine start; may equal <paramref name="end"/> for a circle.</param>
    /// <param name="end">The finite spine end.</param>
    /// <param name="radius">The finite, positive radius.</param>
    public Capsule2D(Vector2 start, Vector2 end, float radius)
    {
        ArgGuard.ThrowIfNotFinite(start);
        ArgGuard.ThrowIfNotFinite(end);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(radius);
        Start = start;
        End = end;
        Radius = radius;
    }

    /// <summary>The spine start.</summary>
    public Vector2 Start { get; }

    /// <summary>The spine end.</summary>
    public Vector2 End { get; }

    /// <summary>The radius around the spine.</summary>
    public float Radius { get; }

    /// <inheritdoc/>
    public float Area => Area2D.Capsule(Start, End, Radius);

    /// <inheritdoc/>
    public bool ContainsPoint(Vector2 localPoint) => Containment2D.Capsule(localPoint, Start, End, Radius);

    /// <inheritdoc/>
    public Vector2 GetSupportPoint(Vector2 localDirection) => SupportPoint2D.Capsule(localDirection, Start, End, Radius);
}
