using App2d.Core.Geometry;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Shapes;

/// <summary>
/// The solid side of an infinite line: every point with dot(point, <see cref="Normal"/>) &lt;= <see cref="Offset"/>.
/// The normal points toward free space. Area and bounds are infinite.
/// </summary>
public sealed partial class HalfSpace2D : IShape2D
{
    /// <inheritdoc/>
    public string Kind => ShapeKinds2D.HalfSpace;

    /// <summary>Creates a half-space; the normal is normalized and the offset rescaled to match.</summary>
    /// <param name="outwardNormal">A finite, nonzero normal pointing toward free space.</param>
    /// <param name="offset">The finite signed boundary offset along <paramref name="outwardNormal"/>, in its original length units.</param>
    private HalfSpace2D(Vector2 normal, float offset)
    {
        Normal = normal;
        Offset = offset;
    }

    /// <summary>The unit normal pointing toward free space.</summary>
    public Vector2 Normal { get; }

    /// <summary>The signed boundary offset along <see cref="Normal"/>.</summary>
    public float Offset { get; }

    /// <inheritdoc/>
    public float Area => float.PositiveInfinity;

    /// <inheritdoc/>
    public bool ContainsPoint(Vector2 localPoint) => Containment2D.HalfSpace(localPoint, Normal, Offset);


}
