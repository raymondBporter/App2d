using App2d.Core.Geometry;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Shapes;

/// <summary>
/// The solid side of an infinite line: every point with dot(point, <see cref="Normal"/>) &lt;= <see cref="Offset"/>.
/// The normal points toward free space. Area and bounds are infinite.
/// </summary>
public sealed class HalfSpace2D : IShape2D
{
    /// <summary>Creates a half-space; the normal is normalized and the offset rescaled to match.</summary>
    /// <param name="outwardNormal">A finite, nonzero normal pointing toward free space.</param>
    /// <param name="offset">The finite signed boundary offset along <paramref name="outwardNormal"/>, in its original length units.</param>
    public HalfSpace2D(Vector2 outwardNormal, float offset)
    {
        ArgGuard.ThrowIfNotFiniteOrZero(outwardNormal);
        ArgGuard.ThrowIfNotFinite(offset);
        var normalLength = outwardNormal.Length();
        Normal = outwardNormal / normalLength;
        Offset = offset / normalLength;
    }

    /// <summary>The unit normal pointing toward free space.</summary>
    public Vector2 Normal { get; }

    /// <summary>The signed boundary offset along <see cref="Normal"/>.</summary>
    public float Offset { get; }

    /// <inheritdoc/>
    public float Area => float.PositiveInfinity;

    /// <inheritdoc/>
    public bool ContainsPoint(Vector2 localPoint) => Containment2D.HalfSpace(localPoint, Normal, Offset);

    /// <summary>Creates the half-space whose boundary passes through a point.</summary>
    /// <param name="pointOnBoundary">A finite point on the boundary line.</param>
    /// <param name="outwardNormal">A finite, nonzero normal pointing toward free space.</param>
    /// <returns>The half-space with that boundary and normal.</returns>
    public static HalfSpace2D FromPoint(Vector2 pointOnBoundary, Vector2 outwardNormal)
    {
        var normalized = Vector2.Normalize(outwardNormal);
        return new HalfSpace2D(normalized, Vector2.Dot(pointOnBoundary, normalized));
    }
}
