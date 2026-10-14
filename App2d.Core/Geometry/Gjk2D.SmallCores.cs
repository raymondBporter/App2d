using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Core.Geometry;

public static partial class Gjk2D
{
    // A few projection axes beat GJK/EPA for overlapping triangles, boxes and capsule spines.
    // This is one core-level optimization, shared by raw distances, shape distances and contacts.
    private static bool TrySmallCorePenetration(ConvexProxy2D first, ConvexProxy2D second, bool witnesses, out ConvexQueryResult2D result)
    {
        result = default;
        if (first.VertexCount is < 1 or > 4 || second.VertexCount is < 1 or > 4) return false;
        var origin = first.ReferencePoint;
        Span<Vector2> a = stackalloc Vector2[first.VertexCount];
        Span<Vector2> b = stackalloc Vector2[second.VertexCount];
        for (var i = 0; i < a.Length; i++) a[i] = first.Vertex(i, origin);
        for (var i = 0; i < b.Length; i++) b[i] = second.Vertex(i, origin);
        // A support proxy can contain an unordered point cloud. SAT edges require a convex perimeter.
        if (!HasConvexOrder(a) || !HasConvexOrder(b)) return false;
        var signedDistance = float.NegativeInfinity;
        var normal = Vector2.UnitX;
        if (!TestSmallAxes(a, a, b, ref signedDistance, ref normal) || !TestSmallAxes(b, a, b, ref signedDistance, ref normal)) return false;
        if (float.IsNegativeInfinity(signedDistance)) return false;
        if (!witnesses)
        {
            result = new(signedDistance - first.Radius - second.Radius, normal, default, default, 0f);
            return true;
        }

        var (aStart, aEnd) = SupportFace(a, -normal);
        var (bStart, bEnd) = SupportFace(b, normal);
        var translation = normal * -signedDistance;
        var closest = ClosestPoint2D.BetweenSegments(aStart + translation, aEnd + translation, bStart, bEnd);
        Span<Vertex> witness = stackalloc Vertex[1];
        witness[0] = new Vertex { First = closest.First - translation, Second = closest.Second, Weight = 1d };
        result = Result(witness, signedDistance, normal, first, second, origin, 0d);
        return true;
    }

    private static bool HasConvexOrder(ReadOnlySpan<Vector2> vertices)
    {
        if (vertices.Length < 3) return true;
        var winding = 0;
        for (var i = 0; i < vertices.Length; i++)
        {
            var turn = (vertices[(i + 1) % vertices.Length] - vertices[i]).CrossDouble(vertices[(i + 2) % vertices.Length] - vertices[(i + 1) % vertices.Length]);
            if (turn == 0d) continue;
            var sign = Math.Sign(turn);
            if (winding != 0 && sign != winding) return false;
            winding = sign;
        }
        return winding != 0;
    }

    private static bool TestSmallAxes(ReadOnlySpan<Vector2> edges, ReadOnlySpan<Vector2> first, ReadOnlySpan<Vector2> second, ref float signedDistance, ref Vector2 normal)
    {
        for (var i = 0; i < edges.Length; i++)
        {
            var edge = edges[(i + 1) % edges.Length] - edges[i];
            if (edge.LengthSquared() <= float.Epsilon) continue;
            var direction = Vector2.Normalize(edge);
            if (!TestSmallAxis(direction.PerpCcw, first, second, ref signedDistance, ref normal)) return false;
            if (edges.Length == 2 && !TestSmallAxis(direction, first, second, ref signedDistance, ref normal)) return false;
        }
        return true;
    }

    private static bool TestSmallAxis(Vector2 axis, ReadOnlySpan<Vector2> first, ReadOnlySpan<Vector2> second, ref float signedDistance, ref Vector2 normal)
    {
        var gap = Distance2D.SignedDistance(Projection2D.Polygon(first, axis), Projection2D.Polygon(second, axis), out var direction);
        if (gap > 0f) return false;
        if (gap > signedDistance) { signedDistance = gap; normal = axis * direction; }
        return true;
    }

    private static (Vector2 Start, Vector2 End) SupportFace(ReadOnlySpan<Vector2> vertices, Vector2 direction)
    {
        var start = SupportPoint2D.Polygon(direction, vertices);
        var end = start;
        var projection = Dot(start, direction);
        var scaleSquared = 0d;
        foreach (var vertex in vertices) scaleSquared = Math.Max(scaleSquared, Dot(vertex, vertex));
        var tolerance = RelativeTolerance * Math.Max(1e-20, Math.Sqrt(scaleSquared));
        var tangent = direction.PerpCcw;
        var min = Dot(start, tangent);
        var max = min;
        foreach (var vertex in vertices)
        {
            var along = Dot(vertex, direction);
            if (Math.Abs(along - projection) > tolerance) continue;
            var across = Dot(vertex, tangent);
            if (across < min) { min = across; start = vertex; }
            if (across > max) { max = across; end = vertex; }
        }
        return (start, end);
    }
}
