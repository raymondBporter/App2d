using App2d.Core.Geometry;
using System.Numerics;

namespace App2d.Core.Characters.Authored;

/// <summary>A convex collision region in XY: hurt, attack and movement shapes derived from an evaluated pose.</summary>
public sealed record EntityRegion(string Id, IReadOnlyList<Vector2> Points)
{
    public bool Overlaps(EntityRegion other, Vector2 position, Vector2 otherPosition) =>
        PolygonGeometry2D.OverlapsConvex(Points, other.Points, position, otherPosition);

    public static EntityRegion Box(string id, Vector2 center, Vector2 size)
    {
        var points = new Vector2[4];
        VertexGenerator2D.WriteRectangle(points, center - size / 2, center + size / 2);
        return new(id, points);
    }
}
