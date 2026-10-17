using App2d.Core.Geometry.Operations.Containment;
using App2d.Core.Geometry.Shapes;
using System.Numerics;

namespace App2d.Core.Shapes;

/// <summary>
/// The solid side of an infinite line: every point with dot(point, <see cref="Normal"/>) &lt;= <see cref="Offset"/>.
/// The normal points toward free space. Area and diameter are infinite; axis-aligned bounds retain the boundary edge.
/// </summary>
public readonly partial record struct HalfSpace2D : IShape2D
{
    /// <inheritdoc/>
    public string Kind => ShapeKinds2D.HalfSpace;

    /// <summary>Creates a half-space from normalized data. Builder.Create normalizes and validates input.</summary>
    /// <param name="outwardNormal">The unit normal pointing toward free space.</param>
    /// <param name="offset">The finite signed boundary offset along <paramref name="outwardNormal"/>.</param>
    public HalfSpace2D(Vector2 outwardNormal, float offset)
    {
        Normal = outwardNormal;
        Offset = offset;
    }

    /// <summary>The unit normal pointing toward free space.</summary>
    public Vector2 Normal { get; init; }

    /// <summary>The signed boundary offset along <see cref="Normal"/>.</summary>
    public float Offset { get; init; }

    /// <inheritdoc/>
    public float Area => float.PositiveInfinity;

    /// <inheritdoc/>
    public float Diameter => float.PositiveInfinity;

    /// <inheritdoc/>
    public bool ContainsPoint(Vector2 localPoint) => Containment2D.HalfSpace(localPoint, Normal, Offset);

}
