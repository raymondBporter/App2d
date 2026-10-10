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
    /// are supported. GJK finds closest features and EPA finds escape depth, including containment. No allocations.
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
        return Gjk2D.SignedDistance(new ConvexProxy2D(first, firstRadius), new ConvexProxy2D(second, secondRadius));
    }

    /// <summary>The distance between two expanded convex cores; zero when touching or overlapping.</summary>
    /// <param name="first">The first core, at least one vertex.</param>
    /// <param name="second">The second core, at least one vertex.</param>
    /// <param name="firstRadius">The round radius added to the first core.</param>
    /// <param name="secondRadius">The round radius added to the second core.</param>
    /// <returns>The nonnegative gap.</returns>
    public static float DistanceBetweenConvexPolygons(ReadOnlySpan<Vector2> first, ReadOnlySpan<Vector2> second, float firstRadius = 0f, float secondRadius = 0f) => Gjk2D.Distance(new ConvexProxy2D(first, firstRadius), new ConvexProxy2D(second, secondRadius));

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
    public static float DistanceBetweenCapsules(Vector2 firstStart, Vector2 firstEnd, float firstRadius, Vector2 secondStart, Vector2 secondEnd, float secondRadius) => DistanceBetweenConvexPolygons([firstStart, firstEnd], [secondStart, secondEnd], firstRadius, secondRadius);

}
