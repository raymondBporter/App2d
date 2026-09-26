using System.Numerics;

namespace App2d.Core.Characters;

/// <summary>A convex collision region in XY: hurt, attack and movement shapes derived from an evaluated pose.</summary>
public sealed record EntityRegion(string Id, IReadOnlyList<Vector2> Points)
{
    public bool Overlaps(EntityRegion other, Vector2 position, Vector2 otherPosition)
    {
        bool Separated(IReadOnlyList<Vector2> axes)
        {
            for (var i = 0; i < axes.Count; i++)
            {
                var edge = axes[(i + 1) % axes.Count] - axes[i]; var axis = new Vector2(-edge.Y, edge.X);
                if (axis.LengthSquared() < 1e-12f) continue;
                var minA = float.PositiveInfinity; var maxA = float.NegativeInfinity; var minB = minA; var maxB = maxA;
                foreach (var p in Points) { var d = Vector2.Dot(p + position, axis); minA = Math.Min(minA, d); maxA = Math.Max(maxA, d); }
                foreach (var p in other.Points) { var d = Vector2.Dot(p + otherPosition, axis); minB = Math.Min(minB, d); maxB = Math.Max(maxB, d); }
                if (maxA < minB || maxB < minA) return true;
            }
            return false;
        }
        return !Separated(Points) && !Separated(other.Points);
    }
    public static EntityRegion Box(string id, Vector2 center, Vector2 size) => new(id, [center + new Vector2(-size.X, -size.Y) / 2, center + new Vector2(size.X, -size.Y) / 2, center + size / 2, center + new Vector2(-size.X, size.Y) / 2]);
}
