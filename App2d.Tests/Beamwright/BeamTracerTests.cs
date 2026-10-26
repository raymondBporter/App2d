using App2d.Beamwright;
using App2d.Beamwright.Optics;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Operations.Intersection;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Mathematics;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Tests.Beamwright;

public sealed class BeamTracerTests
{
    public static IEnumerable<object[]> ReferenceCases =>
        BeamwrightContent.ReadResource<ReferenceTrace[]>("reference-traces.json").Select(r => new object[] { r.Level, r.Solved });

    [Theory]
    [MemberData(nameof(ReferenceCases))]
    public void MatchesUpstreamCampaign(int index, bool solved)
    {
        var reference = BeamwrightContent.ReadResource<ReferenceTrace[]>("reference-traces.json").Single(r => r.Level == index && r.Solved == solved);
        var errors = CampaignVerification.Check(BeamwrightContent.Default, reference);
        Assert.True(errors.Count == 0, $"{BeamwrightContent.Default.Levels[index].Name}: {string.Join("; ", errors)}");
    }

    [Fact]
    public void DiscAndBendReuseExistingCompleteShapes()
    {
        var content = BeamwrightContent.Default;
        Assert.IsType<Circle2D>(content.Part("droplet").Geometry);
        Assert.IsType<Circle2D>(content.Part("cateye").Geometry);
        Assert.IsType<Rectangle2D>(content.Part("block").Geometry);
        Assert.IsType<SimplePolygon2D>(content.Part("bend").Geometry);
        foreach (var part in content.Pieces.Where(part => part.Shape == "seg"))
        {
            var line = Assert.IsType<LineSegmentCurve2D>(part.Geometry);
            Assert.True(GeometryRaycast2D.TryIntersect(new(0, -1), Vector2.UnitY, line, 2, out var hit));
            Assert.Equal(Vector2.Zero, hit.Point);
        }
    }

    [Fact]
    public void RefractionHandlesNormalIncidenceAndTotalInternalReflection()
    {
        Assert.True(Vector2.TryRefract(Vector2.UnitX, -Vector2.UnitX, 1, 1.5f, out var transmitted));
        Assert.Equal(Vector2.UnitX, transmitted);
        Assert.False(Vector2.TryRefract(Vector2.Normalize(new(1, 1)), -Vector2.UnitY, 1.5f, 1, out _));
        Assert.True(Vector2.TryRefract(Vector2.Normalize(new(1, 1)), -Vector2.UnitY, 1, 1.5f, out var bent));
        Assert.True(bent.X < bent.Y);
        Assert.Equal(1f, bent.Length(), 5);
    }

    [Fact]
    public void TracerReportsExhaustedSegmentBudget()
    {
        var level = BeamwrightContent.Default.Levels[0];
        var result = new BeamTracer(BeamwrightContent.Default).Trace(level, level.Solution, 1);
        Assert.Single(result.Segments);
        Assert.True(result.Truncated);
        Assert.False(result.Win);
    }

    [Fact]
    public void CircularOpticsTransmitAndReflectWithoutTurningIntoHollowRings()
    {
        var level = new BenchLevel
        {
            Sources = [new() { X = 0, Y = 2 }],
            Targets = [new() { X = 7, Y = 2, Need = 10 }]
        };
        var tracer = new BeamTracer(BeamwrightContent.Default);
        var droplet = tracer.Trace(level, [new() { Type = "droplet", X = 3, Y = 2 }]);
        Assert.True(droplet.Win);
        Assert.Contains(droplet.Segments, s => s.Event == "disperse");
        Assert.Contains(droplet.Segments, s => s.Event == "glassOut");
        Assert.All(droplet.Segments, s => Assert.True(float.IsFinite(s.End.X) && float.IsFinite(s.End.Y)));
        var catEye = tracer.Trace(level, [new() { Type = "cateye", X = 3, Y = 2 }]);
        Assert.Contains(catEye.Segments, s => s.Event == "retro");
        Assert.False(catEye.Win);
    }

    [Fact]
    public void FiberCanTrapLightByTotalInternalReflection()
    {
        var level = new BenchLevel { W = 9, H = 9, Sources = [new() { X = 1, Y = 5, Angle = -60 }] };
        var result = new BeamTracer(BeamwrightContent.Default).Trace(level, [new() { Type = "fiber", X = 3, Y = 2 }]);
        Assert.Contains(result.Segments, s => s.Event == "fiberTir");
        Assert.False(result.Truncated);
        Assert.False(result.Win);
    }

    [Fact]
    public void SegmentCastIsTwoSidedFiniteAndRespectsRayParameter()
    {
        var segment = new Segment<Vector2>(new(2, -1), new(2, 1));
        Assert.True(Raycast2D.TrySegment(Vector2.Zero, new(2, 0), segment, 1, out var hit));
        Assert.Equal(1f, hit.Distance);
        Assert.Equal(new Vector2(2, 0), hit.Point);
        Assert.Equal(-Vector2.UnitX, hit.Normal);
        Assert.True(Raycast2D.TrySegment(new(4, 0), -Vector2.UnitX, segment, 2, out _));
        Assert.False(Raycast2D.TrySegment(new(0, 2), Vector2.UnitX, segment, 5, out _));
        Assert.False(Raycast2D.TrySegment(Vector2.Zero, Vector2.UnitX, segment, 1, out _));
        Assert.False(Raycast2D.TrySegment(new(2, -2), Vector2.UnitY, segment, 5, out _));
        Assert.False(Raycast2D.TrySegment(Vector2.Zero, Vector2.UnitX, new(Vector2.One, Vector2.One), 5, out _));
    }
}
