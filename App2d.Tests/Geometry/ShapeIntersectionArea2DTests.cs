using App2d.Core.Geometry;
using App2d.Core.Geometry.Operations.Area;
using App2d.Core.Geometry.Operations.Transforms;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class ShapeIntersectionArea2DTests
{
    [Fact]
    public void RandomPairsSatisfyInclusionExclusionSymmetryAndBounds()
    {
        var random = new Random(4471);
        for (var sample = 0; sample < 64; sample++)
        {
            var a = RandomShape(random, sample % 4);
            var b = RandomShape(random, (sample / 4) % 4);
            var intersection = ShapeArea2D.Intersection(a, b);
            Assert.InRange(Math.Abs(intersection + ShapeArea2D.Union([a, b]) - (a.Area + b.Area)), 0f, 1e-4f);
            Assert.Equal(intersection, ShapeArea2D.Intersection(b, a), 5);
            Assert.InRange(intersection, 0f, MathF.Min(a.Area, b.Area) + 1e-5f);
        }
    }

    [Fact]
    public void IdenticalContainedDisjointAndTangentEllipses()
    {
        const float angle = .4f;
        var ellipse = Ellipse2D.Builder.Create(new(2, 1), new(.5f, -.25f), angle);
        Assert.Equal(ellipse.Area, ShapeArea2D.Intersection(ellipse, Ellipse2D.Builder.Create(new(2, 1), new(.5f, -.25f), angle)), 5);
        var inner = Ellipse2D.Builder.Create(new(1, .5f), new(.5f, -.25f), angle);
        Assert.Equal(inner.Area, ShapeArea2D.Intersection(ellipse, inner), 5);
        Assert.Equal(0f, ShapeArea2D.Intersection(ellipse, Ellipse2D.Builder.Create(new(2, 1), new(10, 0), angle)));
        var upright = Ellipse2D.Builder.Create(new(2, 1));
        Assert.Equal(0f, ShapeArea2D.Intersection(upright, Ellipse2D.Builder.Create(new(2, 1), new(4, 0))), 5);
        Assert.Equal(0f, ShapeArea2D.Intersection(Circle2D.Builder.Create(1), Circle2D.Builder.Create(1, new(2, 0))), 5);
        var touchingInside = Ellipse2D.Builder.Create(new(1, .5f), new(1, 0));
        Assert.Equal(touchingInside.Area, ShapeArea2D.Intersection(upright, touchingInside), 5);
        Assert.Equal(touchingInside.Area, ShapeArea2D.Intersection(Rotated(upright, angle), Rotated(touchingInside, angle)), 5);
    }

    [Fact]
    public void CircleLensesAreExactEvenForThinSlivers()
    {
        Assert.InRange(Math.Abs(ShapeArea2D.Intersection(Circle2D.Builder.Create(1), Circle2D.Builder.Create(1, new(1, 0))) - ShapeArea2DTests.CircleLens(1, 1, 1)), 0d, 1e-6d);
        const float distance = 1.999f;
        var sliver = ShapeArea2D.Intersection(Circle2D.Builder.Create(1), Circle2D.Builder.Create(1, new(distance, 0)));
        Assert.InRange(Math.Abs(sliver - ShapeArea2DTests.CircleLens(1, 1, distance)), 0d, 1e-9d);
    }

    [Fact]
    public void RotatedEllipsePairsMatchTheOracleTheFourCrossingFormulaAndTheSeamLens()
    {
        var a = Ellipse2D.Builder.Create(new(2, 1), default, .3f);
        var b = Ellipse2D.Builder.Create(new(2, 1), new(1.5f, .4f), -.5f);
        Assert.InRange(Math.Abs(ShapeArea2D.Intersection(a, b) - (a.Area + b.Area - ShapeArea2DTests.CrossSectionArea([a, b]))), 0d, .0002d);
        var c = Ellipse2D.Builder.Create(new(2, .5f), default, .3f);
        var d = Ellipse2D.Builder.Create(new(2, .5f), default, .3f + MathF.PI / 2);
        Assert.Equal((float)(2 * Math.PI * 2 * .5 - 4 * 2 * .5 * Math.Atan2(2, .5)), ShapeArea2D.Intersection(c, d), 5);
        // B centered on A's +X vertex: the crossings straddle A's parameter seam.
        var seam = (float)(2 * 1 * ShapeArea2DTests.CircleLens(1, 1, 1));
        Assert.Equal(seam, ShapeArea2D.Intersection(Ellipse2D.Builder.Create(new(2, 1)), Ellipse2D.Builder.Create(new(2, 1), new(2, 0))), 5);
        Assert.Equal(seam, ShapeArea2D.Intersection(Ellipse2D.Builder.Create(new(2, 1), default, .3f), Ellipse2D.Builder.Create(new(2, 1), 2 * Polar2D.Direction(.3f), .3f)), 5);
    }

    [Fact]
    public void CapsuleAgainstRotatedEllipseUsesTheGroupSemantics()
    {
        var capsule = Capsule2D.Builder.Create(new(-1, 0), new(1, .5f), .4f);
        var ellipse = Ellipse2D.Builder.Create(new(1.5f, .7f), new(.3f, .2f), .6f);
        var expected = capsule.Area + ellipse.Area - ShapeArea2D.Union([capsule, ellipse]);
        Assert.Equal(expected, ShapeArea2D.Intersection(capsule, ellipse), 5);
        Assert.Equal(expected, ShapeArea2D.Intersection(ellipse, capsule), 5);
    }

    [Fact]
    public void IntersectionIsInvariantUnderTranslationRotationAndUniformScaling()
    {
        var a = Ellipse2D.Builder.Create(new(2, 1), default, .3f);
        var b = Ellipse2D.Builder.Create(new(1.5f, .75f), new(1.5f, .5f), -.8f);
        var expected = ShapeArea2D.Intersection(a, b);
        var offset = new Vector2(1 << 20, -(1 << 20));
        Assert.Equal(expected, ShapeArea2D.Intersection(Ellipse2D.Builder.Create(a.Radii, offset, .3f), Ellipse2D.Builder.Create(b.Radii, b.Center + offset, -.8f)), 5);
        Assert.Equal(expected, ShapeArea2D.Intersection(Rotated(a, 1.1f), Rotated(b, 1.1f)), 5);
        foreach (var scale in new[] { 1e-8f, 1e8f })
        {
            var scaled = ShapeArea2D.Intersection(Ellipse2D.Builder.Create(a.Radii * scale, default, .3f), Ellipse2D.Builder.Create(b.Radii * scale, b.Center * scale, -.8f));
            Assert.InRange(MathF.Abs(scaled / scale / scale - expected), 0f, 2e-6f);
        }
    }

    [Fact]
    public void ASlightlyLongAxisIntersectsItselfInTheEllipseArea()
    {
#pragma warning disable APP2D001 // Raw construction is the point: a validated axis may be 0.001 off unit length.
        var ellipse = new Ellipse2D { Radii = new(2, 1), Axis = new(1.0005f, 0) };
#pragma warning restore APP2D001
        Assert.Equal(Area2D.Ellipse(new(2, 1)), ShapeArea2D.Intersection(ellipse, ellipse), 5);
    }

    private static IConvexShape2D Rotated(IConvexShape2D shape, float angle)
    {
        Assert.True(Similarity2D.TryFromMatrix(Matrix3x2.CreateRotation(angle), out var pose));
        return (IConvexShape2D)ShapeTransforms2D.Posed(shape, pose);
    }

    private static IConvexShape2D RandomShape(Random random, int kind)
    {
        var center = new Vector2((float)random.NextDouble() * 2 - 1, (float)random.NextDouble() * 2 - 1);
        var turn = (float)random.NextDouble() * MathF.Tau;
        return kind switch
        {
            0 => Ellipse2D.Builder.Create(new(.2f + (float)random.NextDouble() * 2, .2f + (float)random.NextDouble() * 2), center, turn),
            1 => Circle2D.Builder.Create(.3f + (float)random.NextDouble(), center),
            2 => Rectangle2D.Builder.FromSize(.5f + (float)random.NextDouble(), .5f + (float)random.NextDouble(), center, turn),
            _ => Capsule2D.Builder.Create(center, center + Polar2D.Direction(turn) * (.5f + (float)random.NextDouble()), .2f + (float)random.NextDouble() * .5f)
        };
    }
}
