using App2d.Core.Animation;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Json;
using System.Text.Json;

namespace App2d.Tests.Geometry;

/// <summary>Geometry as an interpolated value: the whole of it by its own rule, or one part of it through a channel.</summary>
public sealed class GeometryInterpolationTests
{
    private static readonly Ellipse2D Open = Ellipse2D.Builder.Create(new(.025f, .04f), new(.18f, .07f));

    /// <summary>Holds everything from the earlier value and moves only the radius along the ellipse's own Y axis.</summary>
    private static Interpolator<Ellipse2D, float> Blink => Interpolation.Hold<Ellipse2D, float>().With(Ellipse2D.Channels.RadiusY, Interpolation.Linear<float>());

    [Fact]
    public void AnEllipseMovesResizesAndTurnsByItsOwnRule()
    {
        var turned = Ellipse2D.Builder.Create(new(.075f, .02f), new(.38f, -.13f), MathF.PI / 2);
        var half = Ellipse2D.Lerp(Open, turned, .5f);
        Assert.Equal(.05f, half.Radii.X, 6);
        Assert.Equal(.03f, half.Radii.Y, 6);
        Assert.Equal(.28f, half.Center.X, 6);
        Assert.Equal(-.03f, half.Center.Y, 6);
        Assert.Equal(MathF.PI / 4, half.AngleRadians, 5);
        Assert.Equal(Open, Ellipse2D.Lerp(Open, turned, 0));
        // An ellipse that only moves keeps its axis exactly: no angle is taken apart and put back.
        var slanted = Ellipse2D.Builder.Create(new(.02f, .03f), default, .3f);
        Assert.Equal(slanted.Axis, Ellipse2D.Lerp(slanted, slanted with { Center = new(1, 2) }, .37f).Axis);
    }

    [Fact]
    public void ABlinkDrivesOneRadiusAndLeavesTheRestOfTheEllipseAlone()
    {
        // The later value differs everywhere; only the one channel is read from it.
        var other = Ellipse2D.Builder.Create(new(.5f, .01f), new(9, 9), 1f) with { Radii = new(.5f, 0) };
        var half = Blink.Evaluate(Open, other, .5f);
        Assert.Equal(Open.Center, half.Center);
        Assert.Equal(Open.Axis, half.Axis);
        Assert.Equal(Open.Radii.X, half.Radii.X);
        Assert.Equal(.02f, half.Radii.Y, 6);
        // Fully shut is a radius of nothing: an interpolated value is not validated, so an eye can close.
        Assert.Equal(0f, Blink.Evaluate(Open, other, 1f).Radii.Y);
    }

    [Fact]
    public void ATrackOfEllipsesPlaysABlinkOrTheWholeShape()
    {
        var shut = Open with { Radii = new(Open.Radii.X, 0) };
        var blink = new Track<Ellipse2D>(Blink, new(0f, Open), new(.08f, shut), new(.16f, Open));
        Assert.Equal(.02f, blink.Sample(.04f).Radii.Y, 6);
        Assert.Equal(.02f, blink.Sample(.12f).Radii.Y, 6);
        Assert.Equal(Open.Center, blink.Sample(.12f).Center);
        Assert.Equal(Open, blink.Sample(1f));

        var glance = Track.Of<Ellipse2D>(new(0f, Open), new(1f, Open with { Center = new(.2f, .07f) }));
        Assert.Equal(.19f, glance.Sample(.5f).Center.X, 6);
        Assert.Equal(Open.Radii, glance.Sample(.5f).Radii);
    }

    [Fact]
    public void ACurveAndASegmentMoveTheirPointsTogetherOrOneAtATime()
    {
        var flat = QuadraticBezier2D.Builder.Create(new(-.05f, -.12f), new(.06f, -.12f), new(.18f, -.12f));
        var smile = QuadraticBezier2D.Builder.Create(new(-.07f, -.1f), new(.06f, -.2f), new(.2f, -.1f));
        var half = QuadraticBezier2D.Lerp(flat, smile, .5f);
        Assert.Equal(-.06f, half.Start.X, 6);
        Assert.Equal(-.16f, half.Control.Y, 6);
        Assert.Equal(.19f, half.End.X, 6);
        // Only the middle point: the corners of the mouth stay where they were.
        var dip = Interpolation.Hold<QuadraticBezier2D, float>().With(QuadraticBezier2D.Channels.Control, Interpolation.Default<Vector2f, float>());
        Assert.Equal(flat with { Control = new(.06f, -.16f) }, dip.Evaluate(flat, smile, .5f));

        var brow = LineSegmentCurve2D.Builder.Create(new(0, 0), new(4, 0));
        var raised = LineSegmentCurve2D.Builder.Create(new(0, 2), new(4, 6));
        Assert.Equal(LineSegmentCurve2D.Builder.Create(new(0, 1), new(4, 3)), LineSegmentCurve2D.Lerp(brow, raised, .5f));
        var outer = Interpolation.Hold<LineSegmentCurve2D, float>().With(LineSegmentCurve2D.Channels.End, Interpolation.Default<Vector2f, float>());
        Assert.Equal(LineSegmentCurve2D.Builder.Create(new(0, 0), new(4, 3)), outer.Evaluate(brow, raised, .5f));
    }

    [Fact]
    public void APolygonMovesCornerToCornerAndNeedsTheSameNumberOfCorners()
    {
        var square = SimplePolygon2D.Builder.Create([new(0, 0), new(2, 0), new(2, 2), new(0, 2)]);
        var wide = SimplePolygon2D.Builder.Create([new(-2, 0), new(4, 0), new(4, 2), new(-2, 2)]);
        Assert.Equal([new(-1, 0), new(3, 0), new(3, 2), new(-1, 2)], SimplePolygon2D.Lerp(square, wide, .5f).Vertices.ToArray());
        Assert.Equal(8f, SimplePolygon2D.Lerp(square, wide, .5f).Area, 4);
        var triangle = SimplePolygon2D.Builder.Create([new(0, 0), new(2, 0), new(1, 2)]);
        Assert.Throws<ArgumentException>(() => SimplePolygon2D.Lerp(square, triangle, .5f));
    }

    [Fact]
    public void GeometryRulesAndTheirChannelsSurviveJson()
    {
        var other = Open with { Radii = new(.5f, 0), Center = new(9, 9) };
        var blink = JsonSerializer.Deserialize<Interpolator<Ellipse2D, float>>(JsonSerializer.Serialize(Blink, JsonProfiles.Mathematics), JsonProfiles.Mathematics);
        Assert.Equal(Blink.Evaluate(Open, other, .37f), blink.Evaluate(Open, other, .37f));
        Assert.Contains("radiusY", JsonSerializer.Serialize(Blink, JsonProfiles.Mathematics));

        var whole = JsonSerializer.Deserialize<Interpolator<Ellipse2D, float>>(JsonSerializer.Serialize(Interpolation.Default<Ellipse2D, float>()));
        Assert.Equal(Ellipse2D.Lerp(Open, other, .37f), whole.Evaluate(Open, other, .37f));

        var flat = QuadraticBezier2D.Builder.Create(new(0, 0), new(1, 0), new(2, 0));
        var bent = QuadraticBezier2D.Builder.Create(new(0, 0), new(1, 4), new(2, 0));
        var dip = Interpolation.Hold<QuadraticBezier2D, float>().With(QuadraticBezier2D.Channels.Control, Interpolation.Default<Vector2f, float>());
        var restored = JsonSerializer.Deserialize<Interpolator<QuadraticBezier2D, float>>(JsonSerializer.Serialize(dip, JsonProfiles.Mathematics), JsonProfiles.Mathematics);
        Assert.Equal(new Vector2f(1, 1), restored.Evaluate(flat, bent, .25f).Control);
    }
}
