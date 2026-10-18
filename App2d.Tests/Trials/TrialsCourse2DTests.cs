using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.HeightFields;
using App2d.Trials;
using System.Numerics;

namespace App2d.Tests.Trials;

public sealed class TrialsCourse2DTests
{
    [Theory]
    [InlineData(12800f)]
    [InlineData(25600f)]
    [InlineData(12813f)]
    [InlineData(80f)]
    public void WidthDefinesExactEndpointsIncludingPartialFinalSpansAndSmallCourses(float width)
    {
        var course = TrialsCourse2D.Default with { Width = width };
        var curve = course.CreateCurve();
        Assert.Equal(course.XMin, curve.Evaluate(0f).X);
        Assert.Equal(course.XMin + width, curve.Evaluate(1f).X);
        Assert.True(curve.ControlPoints.Length >= 4);
        for (var index = 1; index < curve.ControlPoints.Length; index++)
            Assert.True(curve.ControlPoints[index].X > curve.ControlPoints[index - 1].X);
    }

    [Fact]
    public void ExtendingWidthPreservesExistingWaveScaleAndAuthoredRampPositions()
    {
        var original = TrialsCourse2D.Default;
        var extended = original with { Width = original.Width * 2f };
        var oldCurve = original.CreateCurve();
        var newCurve = extended.CreateCurve();
        for (var index = 0; index < oldCurve.ControlPoints.Length; index++)
        {
            var point = oldCurve.ControlPoints[index];
            if (point.X <= original.XMax - original.WaveFadeOutLength)
                Assert.Equal(point, newCurve.ControlPoints[index]);
        }
        Assert.Equal(new Vector2(extended.XMax, 0f), newCurve.ControlPoints[^1]);
        Assert.Contains(newCurve.ControlPoints.ToArray(), point => point.X > original.XMax && MathF.Abs(point.Y) > 20f);
        var oldField = new BSplineHeightField2D(oldCurve, original.YMin);
        var newField = new BSplineHeightField2D(newCurve, extended.YMin);
        foreach (var x in new[] { 0f, 1000f, 2280f, 5160f, 8000f })
            Assert.InRange(MathF.Abs(oldField.HeightAt(x) - newField.HeightAt(x)), 0f, 0.005f);
    }

    [Fact]
    public void FrequencyAmplitudesAndOverlappingRampProfilesCanBeReplacedPerMap()
    {
        var course = new TrialsCourse2D
        {
            Width = 5280f,
            XMin = 0f,
            YMin = -100f,
            WaveStartX = 0f,
            WaveFadeInLength = 80f,
            WaveFadeOutLength = 80f,
            FrequencyAmplitudes = [(1f / 640f, 10f)],
            Ramps =
            [
                PolylineCurve2D.Builder.Create([new(320, 0), new(480, 30), new(640, 0)]),
                PolylineCurve2D.Builder.Create([new(320, 0), new(480, 7), new(640, 0)])
            ]
        };
        var controls = course.CreateCurve().ControlPoints.ToArray();
        Assert.Equal(10f, controls.Single(point => point.X == 160f).Y, 4);
        Assert.Equal(27f, controls.Single(point => point.X == 480f).Y, 4);
        var wavesOnly = course with { Ramps = [] };
        Assert.Equal(-10f, wavesOnly.CreateCurve().ControlPoints.ToArray().Single(point => point.X == 480f).Y, 4);
        var flat = course with { FrequencyAmplitudes = [], Ramps = [] };
        Assert.All(flat.CreateCurve().ControlPoints.ToArray(), point => Assert.Equal(0f, point.Y));
    }

    [Fact]
    public void GameUsesConfiguredWidthForTheActualCollidableTerrain()
    {
        var course = TrialsCourse2D.Default with { Width = 25613f };
        using var game = new TrialsGame2D(course);
        Assert.Same(course, game.Course);
        Assert.Equal(course.XMin, game.Terrain.Ground.WorldBounds.Min.X);
        Assert.Equal(course.XMax, game.Terrain.Ground.WorldBounds.Max.X);
        var newlyAddedX = TrialsCourse2D.Default.XMax + 1500f;
        Assert.True(game.Terrain.Ground.Shape.ContainsPoint(new(newlyAddedX, -100f)));
        Assert.False(game.Terrain.Ground.Shape.ContainsPoint(new(course.XMax + 1f, -100f)));
        Assert.InRange(MathF.Abs(game.Terrain.HeightAt(newlyAddedX)), 0f, 80f);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void InvalidWidthsAreRejectedBeforeGeneratingGeometry(float width) =>
        Assert.ThrowsAny<ArgumentException>(() => (TrialsCourse2D.Default with { Width = width }).CreateCurve());

    [Fact]
    public void InvalidWaveAndRampDefinitionsAreRejected()
    {
        Assert.ThrowsAny<ArgumentException>(() => (TrialsCourse2D.Default with { FrequencyAmplitudes = [(-1f, 10f)] }).CreateCurve());
        Assert.ThrowsAny<ArgumentException>(() => (TrialsCourse2D.Default with { FrequencyAmplitudes = [(0.01f, float.NaN)] }).CreateCurve());
        Assert.Throws<ArgumentException>(() => (TrialsCourse2D.Default with
        {
            Ramps = [PolylineCurve2D.Builder.Create([new(0, 0), new(100, 50), new(50, 0)])]
        }).CreateCurve());
    }
}
