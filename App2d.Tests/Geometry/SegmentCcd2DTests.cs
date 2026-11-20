using App2d.Core.Geometry.Intersection;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Mathematics.Transforms;

namespace App2d.Tests.Geometry;

public sealed class SegmentCcd2DTests
{
    private static readonly Segment<Vector2f, float> Horizontal = new(new(0, 0), new(1, 0));
    private static readonly Segment<Vector2f, float> Vertical = new(new(0, -1), new(0, 1));

    [Fact]
    public void InitialOverlapAndMissIncludePerpendicularAndCollapsedSegments()
    {
        AssertTime(0, Horizontal, default, default, Vertical, Pose(.5f, 0), Pose(.5f, 0));
        AssertTime(null, Horizontal, default, default, Vertical, Pose(2, 0), Pose(2, 0));
        AssertTime(0, Horizontal, default, default, Horizontal, Pose(.5f, 0), Pose(.5f, 0));
        var point = new Segment<Vector2f, float>(new(.5f, 0), new(.5f, 0));
        AssertTime(0, point, default, default, Horizontal, default, default);
        AssertTime(0, point, default, default, point, default, default);
        AssertTime(0, point, Pose(2, 3, .7f), Pose(4, 5, 1.2f), point, Pose(2, 3, .7f), Pose(4, 5, 1.2f));
    }

    [Theory]
    [InlineData(0, .5)]
    [InlineData(1, 1)]
    public void CollinearTranslationFindsFirstEntryIncludingTheFinalInstant(float end, double time)
        => AssertTime(time, Horizontal, default, default, Horizontal, Pose(2, 0), Pose(end, 0));

    [Fact]
    public void ParallelDistinctLinesAndContactsOutsideTheFiniteSegmentMiss()
    {
        AssertTime(null, Horizontal, default, default, Horizontal, Pose(2, .001f), Pose(0, .001f));
        AssertTime(null, Horizontal, default, default, Vertical, Pose(3, 3), Pose(3, -3));
        AssertTime(null, Horizontal, default, new(default, 2),
            new(new(2, 0), new(3, 0)), default, new(default, 2));
    }

    [Fact]
    public void FastCrossingIsFoundWithoutTimeSampling()
        => AssertTime(.4999995, Horizontal, default, default, Vertical, Pose(1_000_000, 0), Pose(-1_000_000, 0));

    [Fact]
    public void NonparallelContactAtTheFinalInstantIsIncluded()
        => AssertTime(1, Horizontal, default, default, Vertical, Pose(2, 0), Pose(1, 0));

    [Fact]
    public void RotationalTangencyAtTheFinalInstantIsIncluded()
    {
        var wall = new Segment<Vector2f, float>(new(1, -.25f), new(1, .25f));
        AssertTime(1, Horizontal, new(default, -MathF.PI / 2), default, wall, default, default);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void RotationDetectsAnIsolatedTangencyWithoutASignChange(float radius)
    {
        var radial = new Segment<Vector2f, float>(new(radius - 1, 0), new(radius, 0));
        var wall = new Segment<Vector2f, float>(new(radius, -.25f), new(radius, .25f));
        AssertTime(.5, radial, new(default, -MathF.PI / 2), new(default, MathF.PI / 2), wall, default, default);
    }

    [Fact]
    public void UnwrappedLongTurnsKeepTheirDirection()
    {
        var wall = new Segment<Vector2f, float>(new(1, -.25f), new(1, .25f));
        AssertTime(.5, Horizontal, new(default, -3 * MathF.PI / 4), new(default, 3 * MathF.PI / 4), wall, default, default);
    }

    [Fact]
    public void CollapsedSegmentsUsePointSegmentAndPointPointContacts()
    {
        var point = new Segment<Vector2f, float>(default, default);
        AssertTime(.5, point, default, default, Vertical, Pose(1, 0), Pose(-1, 0));
        AssertTime(.5, point, default, default, point, Pose(-1, 0), Pose(1, 0));
        AssertTime(1, point, default, default, point, Pose(0, 1), default);
        AssertTime(null, point, default, default, point, Pose(-1, .01f), Pose(1, .01f));
    }

    [Theory]
    [InlineData(1e-16f)]
    [InlineData(1f)]
    [InlineData(1e16f)]
    public void GeometricScaleDoesNotTurnSeparatedParallelSegmentsIntoInitialContacts(float scale)
    {
        var segment = new Segment<Vector2f, float>(default, new(scale, 0));
        AssertTime(.5, segment, default, default, segment, Pose(2 * scale, 0), default);
        AssertTime(null, segment, default, default, segment, Pose(2 * scale, scale), Pose(0, scale));
    }

    [Fact]
    public void SamplingMatchesEndpointTransformsAndRationalHalfAngleProgress()
    {
        var start = Pose(2, -3, -.6f);
        var end = Pose(-4, 5, 2.1f);
        var point = new Vector2f(3, -2);
        foreach (var time in new[] { 0d, .13, .5, .82, 1 })
        {
            var expected = ReferencePoint(point, start, end, time);
            var actual = SegmentCcd2D.TransformPoint(point, start, end, time);
            Assert.InRange((actual - expected).Length(), 0, 5e-14);
        }
        var rational = SegmentCcd2D.TransformPoint(Vector2f.UnitX, default, new(default, MathF.PI), .25);
        Assert.True(rational.X > .7); // Linear angle interpolation would give cos(pi/4), about .707.
        Assert.True(rational.X < .9);
    }

    [Fact]
    public void IndependentRotationsAndTranslationsAgreeWithAnIndependentScan()
    {
        var random = new Random(821034);
        var hits = 0;
        var misses = 0;
        for (var sample = 0; sample < 48; sample++)
        {
            var a = new Segment<Vector2f, float>(new(-1.2f, -.1f), new(.9f, .2f));
            var b = new Segment<Vector2f, float>(new(-.8f, .3f), new(1.3f, -.2f));
            RigidTransform2D RandomPose() => Pose(Next(3), Next(3), Next(2.5f));
            float Next(float scale) => scale * (2 * random.NextSingle() - 1);
            var aStart = RandomPose();
            var aEnd = RandomPose();
            var bStart = RandomPose();
            var bEnd = RandomPose();
            var expected = ReferenceFirstContact(a, aStart, aEnd, b, bStart, bEnd);
            var actual = SegmentCcd2D.FirstContact(a, aStart, aEnd, b, bStart, bEnd);
            if (expected.HasValue)
            {
                hits++;
                Assert.True(actual.HasValue, $"Missed scanned contact in sample {sample} at {expected}.");
                Assert.InRange(Math.Abs(actual.Value - expected.Value), 0, 1e-9);
            }
            else
            {
                misses++;
                // The scan can miss a very short or isolated contact; verify any extra event directly.
                if (actual.HasValue) Assert.True(ReferenceIntersects(a, aStart, aEnd, b, bStart, bEnd, Math.Min(1, actual.Value + 1e-10)));
            }
            var swapped = SegmentCcd2D.FirstContact(b, bStart, bEnd, a, aStart, aEnd);
            Assert.Equal(actual.HasValue, swapped.HasValue);
            if (actual.HasValue) Assert.InRange(Math.Abs(actual.Value - swapped!.Value), 0, 1e-10);
        }
        Assert.True(hits > 5);
        Assert.True(misses > 5);
    }

    [Fact]
    public void NonfiniteInputsAndSingularHalfAnglePathsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SegmentCcd2D.FirstContact(Horizontal, Pose(float.NaN, 0), default, Vertical, default, default));
        Assert.Throws<ArgumentOutOfRangeException>(() => SegmentCcd2D.FirstContact(Horizontal, default, new(default, float.PositiveInfinity), Vertical, default, default));
        Assert.Throws<ArgumentException>(() => SegmentCcd2D.FirstContact(Horizontal, default, new(default, MathF.Tau), Vertical, default, default));
        Assert.Throws<ArgumentException>(() => SegmentCcd2D.FirstContact(Horizontal, default, new(default, MathF.Tau - 1e-6f), Vertical, default, default));
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = SegmentCcd2D.TransformPoint(default, default, default, -.1); });
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = SegmentCcd2D.TransformPoint(default, default, default, double.NaN); });
    }

    private static RigidTransform2D Pose(float x, float y, float angle = 0) => new(new(x, y), angle);

    private static void AssertTime(double? expected, Segment<Vector2f, float> a, RigidTransform2D aStart, RigidTransform2D aEnd,
        Segment<Vector2f, float> b, RigidTransform2D bStart, RigidTransform2D bEnd)
    {
        foreach (var (first, second) in new[] { (a, b), (a.Reversed(), b), (a, b.Reversed()) })
        {
            var actual = SegmentCcd2D.FirstContact(first, aStart, aEnd, second, bStart, bEnd);
            var swapped = SegmentCcd2D.FirstContact(second, bStart, bEnd, first, aStart, aEnd);
            if (!expected.HasValue)
            {
                Assert.Null(actual);
                Assert.Null(swapped);
                continue;
            }
            Assert.True(actual.HasValue, $"Expected contact at {expected}.");
            Assert.True(swapped.HasValue, $"Expected swapped contact at {expected}.");
            Assert.InRange(Math.Abs(actual.Value - expected.Value), 0, 1e-9);
            Assert.InRange(Math.Abs(swapped.Value - expected.Value), 0, 1e-9);
        }
    }

    // An independent oracle: sample the angular path with atan2/sin/cos and use orientation
    // predicates, then bisect the first scanned crossing. Structured tests cover isolated events
    // and intervals too short for this scan to discover.
    private static Vector2d ReferencePoint(Vector2f point, RigidTransform2D start, RigidTransform2D end, double time)
    {
        var real = (1 - time) * Math.Cos(start.Rotation * .5d) + time * Math.Cos(end.Rotation * .5d);
        var imaginary = (1 - time) * Math.Sin(start.Rotation * .5d) + time * Math.Sin(end.Rotation * .5d);
        var angle = 2 * Math.Atan2(imaginary, real);
        var x = (1 - time) * start.Translation.X + time * end.Translation.X;
        var y = (1 - time) * start.Translation.Y + time * end.Translation.Y;
        return new(x + point.X * Math.Cos(angle) - point.Y * Math.Sin(angle), y + point.X * Math.Sin(angle) + point.Y * Math.Cos(angle));
    }

    private static bool ReferenceIntersects(Segment<Vector2f, float> a, RigidTransform2D aStart, RigidTransform2D aEnd,
        Segment<Vector2f, float> b, RigidTransform2D bStart, RigidTransform2D bEnd, double time)
    {
        var p = ReferencePoint(a.Start, aStart, aEnd, time);
        var q = ReferencePoint(a.End, aStart, aEnd, time);
        var r = ReferencePoint(b.Start, bStart, bEnd, time);
        var s = ReferencePoint(b.End, bStart, bEnd, time);
        return p.Orientation(q, r) * p.Orientation(q, s) <= 0 && r.Orientation(s, p) * r.Orientation(s, q) <= 0;
    }

    private static double? ReferenceFirstContact(Segment<Vector2f, float> a, RigidTransform2D aStart, RigidTransform2D aEnd,
        Segment<Vector2f, float> b, RigidTransform2D bStart, RigidTransform2D bEnd)
    {
        if (ReferenceIntersects(a, aStart, aEnd, b, bStart, bEnd, 0)) return 0;
        const int steps = 4096;
        for (var i = 1; i <= steps; i++)
        {
            var high = (double)i / steps;
            if (!ReferenceIntersects(a, aStart, aEnd, b, bStart, bEnd, high)) continue;
            var low = (double)(i - 1) / steps;
            for (var j = 0; j < 48; j++)
            {
                var middle = .5 * (low + high);
                if (ReferenceIntersects(a, aStart, aEnd, b, bStart, bEnd, middle)) high = middle;
                else low = middle;
            }
            return high;
        }
        return null;
    }
}
