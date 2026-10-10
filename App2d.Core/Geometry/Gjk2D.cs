// Simplex reduction adapted from Box2D 3.1.1.
// SPDX-FileCopyrightText: 2023 Erin Catto
// SPDX-License-Identifier: MIT
// The C# support proxies, double-precision calculations and 2D EPA extension are App2d adaptations.
// See THIRD-PARTY-NOTICES.md for the original license and source.
using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Core.Geometry;

/// <summary>The gap or negative escape depth, boundary witnesses, and normal moving the first convex set out of the second.</summary>
/// <param name="SignedDistance">Positive when separated, zero at contact, negative when penetrating.</param>
/// <param name="Normal">A unit direction from the second set toward the first, or an escape direction during penetration.</param>
/// <param name="PointOnFirst">The first boundary witness.</param>
/// <param name="PointOnSecond">The second boundary witness.</param>
/// <param name="ErrorBound">The remaining support gap at termination, in world units.</param>
public readonly record struct ConvexQueryResult2D(float SignedDistance, Vector2 Normal, Vector2 PointOnFirst, Vector2 PointOnSecond, float ErrorBound);

/// <summary>
/// Convex distance and overlap using GJK; penetrating cores use a 2D expanding polytope (EPA), with SAT for small cores.
/// Round radii are applied after the core query. Analytic support mappings are never sampled into perimeters.
/// </summary>
public static partial class Gjk2D
{
    private const int MaxGjkIterations = 64;
    private const int MaxEpaVertices = 1024;
    private const double RelativeTolerance = 2e-7;

    /// <summary>Queries separation, penetration, a normal and witnesses for two finite convex sets.</summary>
    /// <remarks>
    /// Queries allocate no managed memory. Iterations are bounded; <see cref="ConvexQueryResult2D.ErrorBound"/>
    /// reports the residual when an iteration or precision limit is reached. Tied escape directions are arbitrary.
    /// </remarks>
    public static ConvexQueryResult2D Query(ConvexProxy2D first, ConvexProxy2D second)
        => Query(first, second, witnesses: true);

    /// <summary>The gap or negative escape depth, skipping boundary witness construction where possible.</summary>
    public static float SignedDistance(ConvexProxy2D first, ConvexProxy2D second)
        => Query(first, second, witnesses: false).SignedDistance;

    internal static ConvexQueryResult2D Query(ConvexProxy2D first, ConvexProxy2D second, bool witnesses)
    {
        first.Validate();
        second.Validate();
        if (TryPointQuery(first, second, out var simple)) return simple;
        // A fixed geometric order makes the same tied escape direction win when arguments are reversed.
        if (Compare(first, second) <= 0) return QueryOrdered(first, second, witnesses: witnesses);
        var result = QueryOrdered(second, first, witnesses: witnesses);
        return result with { Normal = -result.Normal, PointOnFirst = result.PointOnSecond, PointOnSecond = result.PointOnFirst };
    }

    private static int Compare(ConvexProxy2D first, ConvexProxy2D second)
    {
        var order = Compare(first.Origin, second.Origin);
        if (order != 0) return order;
        ReadOnlySpan<Vector2> directions = [Vector2.UnitX, Vector2.UnitY, -Vector2.UnitX, -Vector2.UnitY];
        foreach (var direction in directions)
        {
            order = Compare(first.Support(direction, first.Origin), second.Support(direction, first.Origin));
            if (order != 0) return order;
        }
        return first.Radius.CompareTo(second.Radius);
    }

    private static int Compare(Vector2 first, Vector2 second)
    {
        var order = first.X.CompareTo(second.X);
        return order != 0 ? order : first.Y.CompareTo(second.Y);
    }

    internal static ConvexQueryResult2D QuerySeparation(ConvexProxy2D first, ConvexProxy2D second)
    {
        first.Validate();
        second.Validate();
        if (TryPointQuery(first, second, out var simple)) return simple;
        return QueryOrdered(first, second, penetration: false);
    }

    private static bool TryPointQuery(ConvexProxy2D first, ConvexProxy2D second, out ConvexQueryResult2D result)
    {
        // Circles and point-versus-capsule cores already have a direct closest-feature solution.
        if (first.VertexCount == 1 && second.VertexCount is 1 or 2)
        {
            var origin = first.ReferencePoint;
            var a = first.Vertex(0, origin);
            var b = second.Vertex(0, origin);
            var fallback = Vector2.UnitX;
            if (second.VertexCount == 2)
            {
                var end = second.Vertex(1, origin);
                var edge = end - b;
                b = ClosestPoint2D.OnSegment(a, b, end);
                if (Dot(edge, edge) > 0d) fallback = Vector2.Normalize(new(-edge.Y, edge.X));
            }
            var delta = a - b;
            var distance = (float)Math.Sqrt(Dot(delta, delta));
            var normal = distance > 0f ? delta / distance : fallback;
            Span<Vertex> witness = stackalloc Vertex[1];
            witness[0] = new Vertex { First = a, Second = b, Difference = delta, Weight = 1d };
            result = Result(witness, distance, normal, first, second, origin, 0d);
            return true;
        }
        if (second.VertexCount == 1 && first.VertexCount == 2)
        {
            TryPointQuery(second, first, out result);
            result = result with { Normal = -result.Normal, PointOnFirst = result.PointOnSecond, PointOnSecond = result.PointOnFirst };
            return true;
        }
        result = default;
        return false;
    }

    private static ConvexQueryResult2D QueryOrdered(ConvexProxy2D first, ConvexProxy2D second, bool penetration = true, bool witnesses = true)
    {
        if (penetration && TrySmallCorePenetration(first, second, witnesses, out var small)) return small;
        var origin = first.ReferencePoint;
        Span<Vertex> simplex = stackalloc Vertex[3];
        simplex[0] = Support(first, second, Vector2.UnitX, origin);
        simplex[0].Weight = 1d;
        var count = 1;
        var scale = Math.Sqrt(Dot(simplex[0].Difference, simplex[0].Difference));
        var error = 0d;
        for (var iteration = 0; iteration < MaxGjkIterations; iteration++)
        {
            Reduce(simplex, ref count);
            var closest = Closest(simplex, count);
            var distance = Math.Sqrt(Dot(closest, closest));
            var tolerance = RelativeTolerance * Math.Max(scale, 1e-20);
            if (count == 3 || distance <= tolerance * .01d)
                return penetration ? Penetration(first, second, simplex[..count], origin, scale)
                    : Result(simplex[..count], 0f, Vector2.UnitX, first, second, origin, 0d);

            var direction = -closest / (float)distance;
            var vertex = Support(first, second, direction, origin);
            scale = Math.Max(scale, Math.Sqrt(Dot(vertex.Difference, vertex.Difference)));
            error = Math.Max(0d, distance + Dot(direction, vertex.Difference));
            var duplicate = false;
            for (var i = 0; i < count; i++) duplicate |= vertex.Difference == simplex[i].Difference;
            if (duplicate || error <= tolerance)
                return Result(simplex[..count], (float)distance, closest / (float)distance, first, second, origin, error);
            simplex[count++] = vertex;
        }

        Reduce(simplex, ref count);
        var last = Closest(simplex, count);
        var length = last.Length();
        return count == 3 || length == 0f
            ? (penetration ? Penetration(first, second, simplex[..count], origin, scale)
                : Result(simplex[..count], 0f, Vector2.UnitX, first, second, origin, 0d))
            : Result(simplex[..count], length, last / length, first, second, origin, error);
    }

    /// <summary>True for touching or overlapping sets.</summary>
    public static bool Intersects(ConvexProxy2D first, ConvexProxy2D second) => QuerySeparation(first, second).SignedDistance <= 0f;

    /// <summary>The nonnegative gap between filled convex sets. Overlapping cores skip EPA.</summary>
    public static float Distance(ConvexProxy2D first, ConvexProxy2D second) => Math.Max(0f, QuerySeparation(first, second).SignedDistance);

    private struct Vertex
    {
        public Vector2 First;
        public Vector2 Second;
        public Vector2 Difference;
        public double Weight;
    }

    private static Vertex Support(ConvexProxy2D first, ConvexProxy2D second, Vector2 direction, Vector2 origin)
    {
        var a = first.Support(direction, origin);
        var b = second.Support(-direction, origin);
        return new Vertex { First = a, Second = b, Difference = a - b };
    }

    private static double Dot(Vector2 a, Vector2 b) => (double)a.X * b.X + (double)a.Y * b.Y;

    private static Vector2 Closest(ReadOnlySpan<Vertex> vertices, int count)
    {
        double x = 0d, y = 0d;
        for (var i = 0; i < count; i++)
        {
            x += vertices[i].Weight * vertices[i].Difference.X;
            y += vertices[i].Weight * vertices[i].Difference.Y;
        }
        return new((float)x, (float)y);
    }

    private static void Reduce(Span<Vertex> s, ref int count)
    {
        if (count == 1) { s[0].Weight = 1d; return; }
        var a = s[0].Difference;
        var b = s[1].Difference;
        var ab = b - a;
        var abA = Dot(b, ab);
        var abB = -Dot(a, ab);
        if (count == 2)
        {
            if (abB <= 0d) { count = 1; s[0].Weight = 1d; }
            else if (abA <= 0d) { count = 1; s[0] = s[1]; s[0].Weight = 1d; }
            else { s[0].Weight = abA / (abA + abB); s[1].Weight = abB / (abA + abB); }
            return;
        }

        var c = s[2].Difference;
        var ac = c - a;
        var bc = c - b;
        var acA = Dot(c, ac);
        var acC = -Dot(a, ac);
        var bcB = Dot(c, bc);
        var bcC = -Dot(b, bc);
        var area = ab.CrossDouble(ac);
        var triangleA = area * b.CrossDouble(c);
        var triangleB = area * c.CrossDouble(a);
        var triangleC = area * a.CrossDouble(b);
        if (abB <= 0d && acC <= 0d) { count = 1; s[0].Weight = 1d; }
        else if (abA > 0d && abB > 0d && triangleC <= 0d)
        {
            count = 2; s[0].Weight = abA / (abA + abB); s[1].Weight = abB / (abA + abB);
        }
        else if (acA > 0d && acC > 0d && triangleB <= 0d)
        {
            count = 2; s[0].Weight = acA / (acA + acC); s[1] = s[2]; s[1].Weight = acC / (acA + acC);
        }
        else if (abA <= 0d && bcC <= 0d) { count = 1; s[0] = s[1]; s[0].Weight = 1d; }
        else if (acA <= 0d && bcB <= 0d) { count = 1; s[0] = s[2]; s[0].Weight = 1d; }
        else if (bcB > 0d && bcC > 0d && triangleA <= 0d)
        {
            count = 2; s[0] = s[1]; s[1] = s[2]; s[0].Weight = bcB / (bcB + bcC); s[1].Weight = bcC / (bcB + bcC);
        }
        else
        {
            var sum = triangleA + triangleB + triangleC;
            s[0].Weight = triangleA / sum; s[1].Weight = triangleB / sum; s[2].Weight = triangleC / sum;
        }
    }

    private static ConvexQueryResult2D Penetration(ConvexProxy2D first, ConvexProxy2D second, ReadOnlySpan<Vertex> simplex, Vector2 origin, double scale)
    {
        Span<Vertex> polygon = stackalloc Vertex[MaxEpaVertices];
        Span<Vertex> seeds = stackalloc Vertex[8];
        simplex.CopyTo(seeds);
        var seedCount = simplex.Length;
        seeds[seedCount++] = Support(first, second, Vector2.UnitX, origin);
        seeds[seedCount++] = Support(first, second, Vector2.UnitY, origin);
        seeds[seedCount++] = Support(first, second, -Vector2.UnitX, origin);
        seeds[seedCount++] = Support(first, second, -Vector2.UnitY, origin);
        if (simplex.Length == 2)
        {
            // Cardinal directions can all select the same diagonal endpoints when support projections tie.
            // Query both sides of the actual simplex segment to distinguish a thin core from a full solid.
            var edge = simplex[1].Difference - simplex[0].Difference;
            var perpendicular = Vector2.Normalize(new Vector2(-edge.Y, edge.X));
            seeds[seedCount++] = Support(first, second, perpendicular, origin);
            seeds[seedCount++] = Support(first, second, -perpendicular, origin);
        }
        var count = Hull(seeds[..seedCount], polygon);
        if (count < 3)
        {
            var normal = count == 2 ? new Vector2(-(polygon[1].Difference - polygon[0].Difference).Y, (polygon[1].Difference - polygon[0].Difference).X) : Vector2.UnitX;
            normal = Vector2.Normalize(normal);
            SetEdgeWeights(polygon, 0, count - 1);
            return Result(polygon[..count], 0f, normal, first, second, origin, 0d);
        }

        var edgeIndex = 0;
        var edgeDistance = 0d;
        var edgeNormal = Vector2.UnitX;
        var gap = 0d;
        for (;;)
        {
            edgeDistance = double.PositiveInfinity;
            for (var i = 0; i < count; i++)
            {
                var a = polygon[i].Difference;
                var edge = polygon[(i + 1) % count].Difference - a;
                var length = Math.Sqrt(Dot(edge, edge));
                if (length == 0d) continue;
                var normal = new Vector2((float)(edge.Y / length), (float)(-edge.X / length));
                var distance = Dot(normal, a);
                if (distance < edgeDistance) { edgeIndex = i; edgeDistance = distance; edgeNormal = normal; }
            }

            var vertex = Support(first, second, edgeNormal, origin);
            var upperDistance = Dot(vertex.Difference, edgeNormal);
            scale = Math.Max(scale, Math.Sqrt(Dot(vertex.Difference, vertex.Difference)));
            gap = Math.Max(0d, upperDistance - edgeDistance);
            var next = (edgeIndex + 1) % count;
            if (gap <= RelativeTolerance * Math.Max(scale, 1e-20) || count == polygon.Length ||
                vertex.Difference == polygon[edgeIndex].Difference || vertex.Difference == polygon[next].Difference)
            {
                // The support plane is an upper bound: translating by this depth reaches separation even if capped.
                edgeDistance = Math.Max(0d, upperDistance);
                break;
            }
            polygon[(edgeIndex + 1)..count].CopyTo(polygon[(edgeIndex + 2)..]);
            polygon[edgeIndex + 1] = vertex;
            count++;
        }

        var end = (edgeIndex + 1) % count;
        Span<Vertex> witnesses = stackalloc Vertex[2];
        witnesses[0] = polygon[edgeIndex]; witnesses[1] = polygon[end];
        SetEdgeWeights(witnesses, 0, 1);
        return Result(witnesses, -(float)edgeDistance, -edgeNormal, first, second, origin, gap);
    }

    private static void SetEdgeWeights(Span<Vertex> vertices, int start, int end)
    {
        var edge = vertices[end].Difference - vertices[start].Difference;
        var squared = Dot(edge, edge);
        var t = squared == 0d ? 0d : Math.Clamp(-Dot(vertices[start].Difference, edge) / squared, 0d, 1d);
        vertices[start].Weight = 1d - t;
        if (end != start) vertices[end].Weight = t;
    }

    private static int Hull(Span<Vertex> seeds, Span<Vertex> hull)
    {
        // At most eight seeds: insertion sort avoids delegates and heap allocations.
        for (var i = 1; i < seeds.Length; i++)
        {
            var vertex = seeds[i]; var j = i;
            while (j > 0 && (seeds[j - 1].Difference.X > vertex.Difference.X ||
                (seeds[j - 1].Difference.X == vertex.Difference.X && seeds[j - 1].Difference.Y > vertex.Difference.Y)))
            { seeds[j] = seeds[j - 1]; j--; }
            seeds[j] = vertex;
        }
        var unique = 0;
        for (var i = 0; i < seeds.Length; i++)
            if (unique == 0 || seeds[i].Difference != seeds[unique - 1].Difference) seeds[unique++] = seeds[i];
        if (unique == 1) { hull[0] = seeds[0]; return 1; }
        var count = 0;
        for (var i = 0; i < unique; i++)
        {
            while (count >= 2 && (hull[count - 1].Difference - hull[count - 2].Difference).CrossDouble(seeds[i].Difference - hull[count - 1].Difference) <= 0d) count--;
            hull[count++] = seeds[i];
        }
        var lower = count;
        for (var i = unique - 2; i >= 0; i--)
        {
            while (count > lower && (hull[count - 1].Difference - hull[count - 2].Difference).CrossDouble(seeds[i].Difference - hull[count - 1].Difference) <= 0d) count--;
            hull[count++] = seeds[i];
        }
        return count - 1;
    }

    private static ConvexQueryResult2D Result(ReadOnlySpan<Vertex> vertices, float coreDistance, Vector2 normal,
        ConvexProxy2D first, ConvexProxy2D second, Vector2 origin, double error)
    {
        double ax = 0d, ay = 0d, bx = 0d, by = 0d;
        foreach (var vertex in vertices)
        {
            ax += vertex.Weight * vertex.First.X; ay += vertex.Weight * vertex.First.Y;
            bx += vertex.Weight * vertex.Second.X; by += vertex.Weight * vertex.Second.Y;
        }
        var pointA = new Vector2((float)ax, (float)ay) - normal * first.Radius + origin;
        var pointB = new Vector2((float)bx, (float)by) + normal * second.Radius + origin;
        return new(coreDistance - first.Radius - second.Radius, normal, pointA, pointB, (float)error);
    }
}
