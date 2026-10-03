using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry;

public static partial class Distance2D
{
    /// <summary>
    /// The signed distance between two convex cores, each optionally expanded by a round radius.
    /// A one-vertex core plus radius is a circle; two vertices plus radius form a capsule; larger cores are
    /// convex perimeters in either winding with nonzero area. Repeated adjacent vertices and point-like segments
    /// are supported. Overlapping cores use the separating-axis escape distance (including containment); disjoint
    /// cores use the true closest features. O((n+m)^2) time, no allocations.
    /// </summary>
    /// <param name="first">The first core, at least one vertex.</param>
    /// <param name="second">The second core, at least one vertex.</param>
    /// <param name="firstRadius">The round radius added to the first core.</param>
    /// <param name="secondRadius">The round radius added to the second core.</param>
    /// <returns>The gap when separated, or the negated minimum escape translation when overlapping.</returns>
    public static float SignedDistanceBetweenConvexPolygons(ReadOnlySpan<Vector2> first, ReadOnlySpan<Vector2> second, float firstRadius = 0f, float secondRadius = 0f)
    {
        ArgGuard.ThrowIfTooShort(first, 1);
        ArgGuard.ThrowIfTooShort(second, 1);
        var separation = float.NegativeInfinity;
        AccumulateAxes(first, first, second, ref separation);
        AccumulateAxes(second, first, second, ref separation);

        // Overlapping cores: SAT gives the shortest escape translation, including containment.
        // Disjoint cores: axis gaps are only lower bounds, so find the actual closest features.
        var coreDistance = separation <= 0f && !float.IsNegativeInfinity(separation) ? separation : PerimeterDistance(first, second);
        return coreDistance - (firstRadius + secondRadius);
    }

    /// <summary>The distance between two expanded convex cores; zero when touching or overlapping.</summary>
    /// <param name="first">The first core, at least one vertex.</param>
    /// <param name="second">The second core, at least one vertex.</param>
    /// <param name="firstRadius">The round radius added to the first core.</param>
    /// <param name="secondRadius">The round radius added to the second core.</param>
    /// <returns>The nonnegative gap.</returns>
    public static float DistanceBetweenConvexPolygons(ReadOnlySpan<Vector2> first, ReadOnlySpan<Vector2> second, float firstRadius = 0f, float secondRadius = 0f) => Math.Max(0f, SignedDistanceBetweenConvexPolygons(first, second, firstRadius, secondRadius));

    /// <summary>The signed distance between two capsules, including crossed spines.</summary>
    /// <param name="firstStart">The first spine start.</param>
    /// <param name="firstEnd">The first spine end.</param>
    /// <param name="firstRadius">The first radius.</param>
    /// <param name="secondStart">The second spine start.</param>
    /// <param name="secondEnd">The second spine end.</param>
    /// <param name="secondRadius">The second radius.</param>
    /// <returns>The gap when separated, or the negated minimum escape translation when overlapping.</returns>
    public static float SignedDistanceBetweenCapsules(Vector2 firstStart, Vector2 firstEnd, float firstRadius, Vector2 secondStart, Vector2 secondEnd, float secondRadius) => SignedDistanceBetweenConvexPolygons([firstStart, firstEnd], [secondStart, secondEnd], firstRadius, secondRadius);

    /// <summary>The distance between two filled capsules; zero when touching or overlapping.</summary>
    /// <param name="firstStart">The first spine start.</param>
    /// <param name="firstEnd">The first spine end.</param>
    /// <param name="firstRadius">The first radius.</param>
    /// <param name="secondStart">The second spine start.</param>
    /// <param name="secondEnd">The second spine end.</param>
    /// <param name="secondRadius">The second radius.</param>
    /// <returns>The nonnegative gap.</returns>
    public static float DistanceBetweenCapsules(Vector2 firstStart, Vector2 firstEnd, float firstRadius, Vector2 secondStart, Vector2 secondEnd, float secondRadius) => Math.Max(0f, SignedDistanceBetweenCapsules(firstStart, firstEnd, firstRadius, secondStart, secondEnd, secondRadius));

    private static void AccumulateAxes(ReadOnlySpan<Vector2> perimeter, ReadOnlySpan<Vector2> first, ReadOnlySpan<Vector2> second, ref float separation)
    {
        for (var i = 0; i < perimeter.Length; i++)
        {
            var edge = perimeter[(i + 1) % perimeter.Length] - perimeter[i];
            if (edge.LengthSquared() <= float.Epsilon) continue;
            var direction = Vector2.Normalize(edge);
            AccumulateAxis(direction.PerpCcw, first, second, ref separation);
            // A segment has no end-cap edges: include its tangent to detect collinear separation.
            if (perimeter.Length == 2) AccumulateAxis(direction, first, second, ref separation);
        }
    }

    private static void AccumulateAxis(Vector2 axis, ReadOnlySpan<Vector2> first, ReadOnlySpan<Vector2> second, ref float separation) => separation = Math.Max(separation, SignedDistance(Projection2D.Polygon(first, axis), Projection2D.Polygon(second, axis)));

    private static float PerimeterDistance(ReadOnlySpan<Vector2> first, ReadOnlySpan<Vector2> second)
    {
        var bestSquared = float.PositiveInfinity;
        for (var i = 0; i < first.Length; i++)
        {
            for (var j = 0; j < second.Length; j++)
                bestSquared = Math.Min(bestSquared, DistanceSquaredBetweenSegments(first[i], first[(i + 1) % first.Length], second[j], second[(j + 1) % second.Length]));
        }

        return MathF.Sqrt(bestSquared);
    }
}
