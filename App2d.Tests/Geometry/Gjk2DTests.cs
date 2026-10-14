using App2d.Core.Geometry;
using App2d.Core.Mathematics;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class Gjk2DTests
{
    [Fact]
    public void SeparationHasBoundaryWitnessesAndRoundRadii()
    {
        ReadOnlySpan<Vector2> first = [new(0, 0)];
        ReadOnlySpan<Vector2> second = [new(3, 4)];
        var query = Gjk2D.Query(new(first, .5f), new(second, 1f));
        Assert.Equal(3.5f, query.SignedDistance, 6);
        AssertNear(new(-.6f, -.8f), query.Normal);
        AssertNear(new(.3f, .4f), query.PointOnFirst);
        AssertNear(new(2.4f, 3.2f), query.PointOnSecond);
        AssertNear(query.Normal * query.SignedDistance, query.PointOnFirst - query.PointOnSecond);
        Assert.False(Gjk2D.Intersects(new(first, .5f), new(second, 1f)));
    }

    [Fact]
    public void ContainmentAndCrossedSpinesIncludeCoreEscapeDepth()
    {
        ReadOnlySpan<Vector2> box = [new(-4, -3), new(4, -3), new(4, 3), new(-4, 3)];
        ReadOnlySpan<Vector2> point = [new(1, .5f)];
        var contained = Gjk2D.Query(new(point, .25f), new(box, .5f));
        Assert.Equal(-3.25f, contained.SignedDistance, 6);
        AssertNear(Vector2.UnitY, contained.Normal);
        AssertNear(contained.Normal * contained.SignedDistance, contained.PointOnFirst - contained.PointOnSecond);
        ReadOnlySpan<Vector2> horizontal = [new(-2, 0), new(2, 0)];
        ReadOnlySpan<Vector2> vertical = [new(0, -3), new(0, 3)];
        var crossed = Gjk2D.Query(new(horizontal, .5f), new(vertical, .5f));
        Assert.Equal(-3f, crossed.SignedDistance, 6);
        Assert.Equal(1f, MathF.Abs(crossed.Normal.X), 6);
        var escaped = Gjk2D.Query(new(horizontal, Similarity2D.FromTranslation(crossed.Normal * 3.001f), .5f), new(vertical, .5f));
        Assert.True(escaped.SignedDistance > 0f);
    }

    [Fact]
    public void TouchingDegenerateAndRepeatedVerticesRemainFinite()
    {
        ReadOnlySpan<Vector2> repeated = [new(0, 0), new(0, 0), new(2, 0), new(2, 0)];
        ReadOnlySpan<Vector2> point = [new(1, 0)];
        var overlap = Gjk2D.Query(new(repeated, .5f), new(point, .5f));
        Assert.Equal(-1f, overlap.SignedDistance, 6);
        AssertNear(1f, overlap.Normal.Length());
        ReadOnlySpan<Vector2> touching = [new(1, 1)];
        Assert.Equal(0f, Gjk2D.Query(new(repeated, .5f), new(touching, .5f)).SignedDistance);
        Assert.True(Gjk2D.Intersects(new(repeated, .5f), new(touching, .5f)));
        Assert.Equal(0f, Gjk2D.Query(new(point), new(point)).SignedDistance);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConvexProxy2D(Array.Empty<Vector2>()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConvexProxy2D(new Vector2[1], -1f));
    }

    [Fact]
    public void UnorderedVertexCloudFallsBackToSupportQuery()
    {
        ReadOnlySpan<Vector2> unordered = [new(-2, -1), new(2, 1), new(-2, 1), new(2, -1)];
        ReadOnlySpan<Vector2> point = [Vector2.Zero];
        var query = Gjk2D.Query(new(unordered), new(point, .2f));
        Assert.Equal(-1.2f, query.SignedDistance, 6);
        AssertNear(query.Normal * query.SignedDistance, query.PointOnFirst - query.PointOnSecond);
    }

    [Fact]
    public void PolygonQueriesAgreeWithIndependentSatAndSegmentReference()
    {
        var random = new Random(4621);
        for (var iteration = 0; iteration < 500; iteration++)
        {
            var first = Polygon(random, 3 + random.Next(7));
            var second = Polygon(random, 3 + random.Next(7));
            var firstRadius = (float)random.NextDouble() * .4f;
            var secondRadius = (float)random.NextDouble() * .4f;
            var reference = PolygonDistance(first, second) - firstRadius - secondRadius;
            var query = Gjk2D.Query(new(first, firstRadius), new(second, secondRadius));
            Assert.Equal(query.SignedDistance, Gjk2D.SignedDistance(new(first, firstRadius), new(second, secondRadius)));
            Assert.True(MathF.Abs(reference - query.SignedDistance) < .00003f, $"{iteration}: expected {reference}, got {query}");
            AssertNear(query.Normal * query.SignedDistance, query.PointOnFirst - query.PointOnSecond, .00004f);
            if (query.SignedDistance < 0f)
            {
                var pose = Similarity2D.FromTranslation(query.Normal * (-query.SignedDistance + .0001f));
                Assert.True(Gjk2D.Query(new(first, pose, firstRadius), new(second, secondRadius)).SignedDistance > 0f);
            }
        }
    }

    [Fact]
    public void AnalyticEllipseQueriesAgreeWithDenseSupportReference()
    {
        var random = new Random(9872);
        for (var iteration = 0; iteration < 80; iteration++)
        {
            var first = new Ellipse2D(new(.4f + 3f * (float)random.NextDouble(), .4f + 3f * (float)random.NextDouble()));
            var second = new Ellipse2D(new(.4f + 3f * (float)random.NextDouble(), .4f + 3f * (float)random.NextDouble()));
            var firstPose = Similarity2D.FromAxis(Vector2.Zero, Direction(random), mirror: iteration % 2 == 0, scale: .8f);
            var secondPose = Similarity2D.FromAxis(new((float)random.NextDouble() * 8f - 4f, (float)random.NextDouble() * 8f - 4f), Direction(random), scale: 1.2f);
            var query = ShapeConvexQuery2D.Query(first, firstPose, second, secondPose);
            var reverse = ShapeConvexQuery2D.Query(second, secondPose, first, firstPose);
            Assert.Equal(query.SignedDistance, reverse.SignedDistance);
            AssertNear(query.Normal, -reverse.Normal);
            var minimumSupport = float.PositiveInfinity;
            for (var sample = 0; sample < 16384; sample++)
            {
                var angle = sample * MathF.Tau / 16384;
                var normal = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                var a = firstPose.TransformPoint(first.GetSupportPoint(firstPose.TransposeTransformDirection(normal)));
                var b = secondPose.TransformPoint(second.GetSupportPoint(secondPose.TransposeTransformDirection(-normal)));
                minimumSupport = Math.Min(minimumSupport, Vector2.Dot(a - b, normal));
            }
            Assert.True(MathF.Abs(query.SignedDistance + minimumSupport) < .00004f, $"{iteration}: sampled {-minimumSupport}, got {query}");
            if (query.SignedDistance < 0f)
            {
                var escaped = firstPose.Translated(query.Normal * (-query.SignedDistance + .0001f));
                Assert.True(ShapeConvexQuery2D.Query(first, escaped, second, secondPose).SignedDistance > 0f);
            }
        }
    }

    [Fact]
    public void ThinContactBetweenOldEllipseSamplesIsDetected()
    {
        var first = new Ellipse2D(new(3, 1));
        var angle = MathF.PI / 64;
        var boundary = new Vector2(3f * MathF.Cos(angle), MathF.Sin(angle));
        var normal = Vector2.Normalize(boundary / new Vector2(9, 1));
        var second = new Ellipse2D(new(.0001f));
        var pose = Similarity2D.FromTranslation(boundary + normal * .00005f);
        var query = ShapeConvexQuery2D.Query(first, Similarity2D.Identity, second, pose);
        Assert.InRange(query.SignedDistance, -.000052f, -.000048f);
    }

    [Fact]
    public void CustomConvexSupportNeedsNoPerimeterRegistration()
    {
        var custom = new CustomDisk();
        var circle = new Circle2D(.5f);
        var pose = Similarity2D.FromTranslation(new(1.2f, 0));
        var query = ShapeConvexQuery2D.Query(custom, Similarity2D.Identity, circle, pose);
        Assert.InRange(query.SignedDistance, -.30001f, -.29999f);
        Assert.Equal(query.SignedDistance, ShapeDistance2D.SignedDistance(custom, Similarity2D.Identity, circle, pose));
    }

    [Fact]
    public void LargeCommonTranslationDoesNotEraseSmallLocalGeometry()
    {
        ReadOnlySpan<Vector2> segment = [new(-.001f, 0), new(.001f, 0)];
        var firstPose = Similarity2D.FromTranslation(new(1000000, 1000000));
        var secondPose = firstPose.Translated(new(0, .125f));
        var query = Gjk2D.Query(new(segment, firstPose, .02f), new(segment, secondPose, .03f));
        Assert.Equal(.075f, query.SignedDistance, 6);
    }

    [Theory]
    [InlineData(1f, 1e-30f, 4f, 1f)]
    [InlineData(1e-30f, 1f, 1f, 4f)]
    [InlineData(1000f, 1e-20f, 1000000f, .01f)]
    [InlineData(3.75f, 1e-10f, 4f, 1f)]
    public void EllipseRootRemainsStableNearMajorAxis(float px, float py, float rx, float ry)
    {
        var point = new Vector2(px, py);
        var radii = new Vector2(rx, ry);
        var closest = ClosestPoint2D.OnEllipsePerimeter(point, Vector2.Zero, radii);
        Assert.True(float.IsFinite(closest.X) && float.IsFinite(closest.Y));
        AssertNear(1f, (closest / radii).LengthSquared());
        var onAxis = ClosestPoint2D.OnEllipsePerimeter(rx >= ry ? new Vector2(px, 0) : new Vector2(0, py), Vector2.Zero, radii);
        Assert.InRange(Vector2.Distance(closest, onAxis), 0f, .001f);
    }

    private sealed class CustomDisk : IConvexShape2D
    {
        public string Kind => "customDisk";
        public float Area => MathF.PI;
        public bool ContainsPoint(Vector2 point) => point.LengthSquared() <= 1f;
        public Vector2 GetSupportPoint(Vector2 direction) => Vector2.Normalize(direction);
    }

    private static Vector2 Direction(Random random)
    {
        var angle = (float)random.NextDouble() * MathF.Tau;
        return new(MathF.Cos(angle), MathF.Sin(angle));
    }

    private static Vector2[] Polygon(Random random, int count)
    {
        var offset = new Vector2((float)random.NextDouble() * 8f - 4f, (float)random.NextDouble() * 8f - 4f);
        var startAngle = (float)random.NextDouble() * MathF.Tau;
        var vertices = new Vector2[count];
        for (var i = 0; i < count; i++)
        {
            var angle = startAngle + i * MathF.Tau / count;
            vertices[i] = offset + new Vector2(2f * MathF.Cos(angle), MathF.Sin(angle));
        }
        return vertices;
    }

    private static float PolygonDistance(ReadOnlySpan<Vector2> first, ReadOnlySpan<Vector2> second)
    {
        var separation = Math.Max(Axes(first, first, second), Axes(second, first, second));
        if (separation <= 0f) return separation;
        var squared = float.PositiveInfinity;
        for (var i = 0; i < first.Length; i++)
            for (var j = 0; j < second.Length; j++)
                squared = Math.Min(squared, Distance2D.DistanceSquaredBetweenSegments(first[i], first[(i + 1) % first.Length], second[j], second[(j + 1) % second.Length]));
        return MathF.Sqrt(squared);
    }

    private static float Axes(ReadOnlySpan<Vector2> edges, ReadOnlySpan<Vector2> first, ReadOnlySpan<Vector2> second)
    {
        var result = float.NegativeInfinity;
        for (var i = 0; i < edges.Length; i++)
        {
            var edge = edges[(i + 1) % edges.Length] - edges[i];
            var axis = Vector2.Normalize(new Vector2(-edge.Y, edge.X));
            result = Math.Max(result, Distance2D.SignedDistance(Projection2D.Polygon(first, axis), Projection2D.Polygon(second, axis)));
        }
        return result;
    }

    private static void AssertNear(Vector2 expected, Vector2 actual, float tolerance = .00001f) => Assert.True(Vector2.Distance(expected, actual) < tolerance, $"Expected {expected}, actual {actual}");
    private static void AssertNear(float expected, float actual) => Assert.InRange(MathF.Abs(expected - actual), 0f, .00001f);
}
