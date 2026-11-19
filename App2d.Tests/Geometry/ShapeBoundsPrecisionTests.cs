using App2d.Core.Geometry;
using App2d.Core.Geometry.Bounds;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Spatial;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class ShapeBoundsPrecisionTests
{
    [Theory]
    [InlineData(float.MaxValue, float.MaxValue)]
    [InlineData(float.MaxValue / 2, float.MaxValue)]
    [InlineData(-float.MaxValue, float.MaxValue)]
    [InlineData(float.Epsilon, float.Epsilon)]
    public void SharedRectangleAndCapsuleCentersKeepFiniteExtremesAndSubnormalPoints(float first, float second)
    {
        var a = new Vector2f(first, -second);
        var b = new Vector2f(second, -first);
        var expected = new Vector2f((float)(((double)first + second) / 2), (float)(-((double)first + second) / 2));
        var rectangle = Rect2f.FromPoints(a, b);
        Assert.Equal(expected, rectangle.Center);
        Assert.Equal(expected.X, rectangle.MidX);
        Assert.Equal(expected.Y, rectangle.MidY);
        var capsule = Capsule2D.Builder.Create(a, b, .5f);
        Assert.Equal(expected, capsule.Center);
        Assert.Equal(expected, Capsule2D.Builder.Create(b, a, .5f).Center);
        Assert.Equal(expected, ShapeBounds2D.CalculateBoundingCircle(capsule).Center);
    }

    [Theory]
    [InlineData(1f, 0f, 3f, float.NegativeInfinity, float.NegativeInfinity, 3f, float.PositiveInfinity)]
    [InlineData(-1f, 0f, 3f, -3f, float.NegativeInfinity, float.PositiveInfinity, float.PositiveInfinity)]
    [InlineData(0f, 1f, 3f, float.NegativeInfinity, float.NegativeInfinity, float.PositiveInfinity, 3f)]
    [InlineData(0f, -1f, 3f, float.NegativeInfinity, -3f, float.PositiveInfinity, float.PositiveInfinity)]
    public void HalfSpaceBoxesKeepEachAxisAlignedBoundary(float nx, float ny, float offset, float minX, float minY, float maxX, float maxY)
    {
        var halfSpace = HalfSpace2D.Builder.Create(new(nx, ny), offset);
        var bounds = ShapeBounds2D.Calculate(halfSpace);
        Assert.Equal(new Rect2f(new(minX, minY), new(maxX, maxY)), bounds);
        Assert.True(bounds.Contains(halfSpace.Normal * halfSpace.Offset));
        Assert.Equal(float.PositiveInfinity, halfSpace.Diameter);
    }

    [Fact]
    public void NearlyAxisAlignedHalfSpacesMustNotDropDistantPoints()
    {
        var halfSpace = HalfSpace2D.Builder.Create(new(1, .0000001f), 3);
        Assert.Equal(Rect2f.Unbounded, ShapeBounds2D.Calculate(halfSpace));
        Assert.True(halfSpace.ContainsPoint(new(1000, -1e12f)));
    }

    [Theory]
    [InlineData(2f)]
    [InlineData(1e30f)]
    [InlineData(1e-30f)]
    public void HalfSpaceBoundsDoNotDependOnTheInputNormalsLength(float scale)
    {
        var halfSpace = HalfSpace2D.Builder.Create(new(scale, 0), 3 * scale);
        Assert.Equal(Vector2f.UnitX, halfSpace.Normal);
        Assert.Equal(3f, halfSpace.Offset, 5);
        Assert.Equal(new Rect2f(new(float.NegativeInfinity), new(halfSpace.Offset, float.PositiveInfinity)), ShapeBounds2D.Calculate(halfSpace));
    }

    [Fact]
    public void HalfSpaceBoundsFollowReflectionShearAndCollapsedTransforms()
    {
        var halfSpace = HalfSpace2D.Builder.Create(Vector2f.UnitX, 3);
        var reflectedBounds = new Rect2f(new(4, float.NegativeInfinity), new(float.PositiveInfinity));
        var pose = Similarity2D.FromAxis(new(10, 20), -Vector2f.UnitX, mirror: true, scale: 2);
        Assert.Equal(reflectedBounds, ShapeBounds2D.CalculateTransformed(halfSpace, pose));

        var item = new SpatialObject2D(halfSpace);
        item.Transform.CopyFrom(Affine2D.FromMatrix(new(-2, 0, 0, 4, 10, 20)));
        Assert.Equal(reflectedBounds, item.WorldBounds);
        item.Transform.CopyFrom(Affine2D.FromMatrix(new(0, 0, 0, 2, 10, 20)));
        Assert.Equal(new Rect2f(new(10, float.NegativeInfinity), new(10, float.PositiveInfinity)), item.WorldBounds);

        var diagonal = new SpatialObject2D(HalfSpace2D.Builder.FromPoint(new(0, 2), new(1, 1)));
        diagonal.Transform.CopyFrom(Affine2D.FromMatrix(new(1, 1, 1, 0, 0, 0)));
        Assert.Equal(new Rect2f(new(float.NegativeInfinity), new(2, float.PositiveInfinity)), diagonal.WorldBounds);
    }

    [Fact]
    public void SimilarityCircleBoundsUseTheScaledRadius()
    {
        var circle = Circle2D.Builder.Create(2f);
        var pose = Similarity2D.FromAxis(new(10f, -4f), new(.8f, .6f), mirror: true, scale: 2f);

        Assert.Equal(new Rect2f(new(6f, -8f), new(14f, 0f)), ShapeBounds2D.CalculateTransformed(circle, pose));
    }

    [Fact]
    public void AffineCircleAndEllipseBoundsUseAnalyticExtents()
    {
        IShape2D[] shapes = [Circle2D.Builder.Create(2, new(1, -3)), Ellipse2D.Builder.Create(new(3, 1), new(-2, 4))];
        Matrix3x2[] matrices = [Matrix3x2.CreateRotation(.7f), new(2, .5f, -.6f, -3, 10, -20), new(0, 0, 0, 2, 3, 4)];
        foreach (var shape in shapes)
        {
            var item = new SpatialObject2D(shape);
            foreach (var matrix in matrices)
            {
                item.Transform.CopyFrom(Affine2D.FromMatrix(matrix));
                var bounds = item.WorldBounds;
                var points = new Vector2f[1024];
                shape.GetOutlineVerts(points, points.Length);
                foreach (var point in points)
                {
                    Assert.InRange(bounds.DistanceTo(Vector2f.Transform(point, matrix)), 0, .00002f);
                }
            }
        }
    }

    [Fact]
    public void ScalarBuildersKeepCentersValidationAndAxisAlignedIntent()
    {
        var center = new Vector2f(10, -5);
        var rectangle = Assert.IsType<Rectangle2D>(Rectangle2D.Builder.FromSize(4, 2, center).Validated());
        Assert.Equal(new Rect2f(new(8, -6), new(12, -4)), ShapeBounds2D.Calculate(rectangle));
        Assert.IsType<AxisAlignedRectangle2D>(AxisAlignedRectangle2D.Builder.Create(4, 2, center).Validated());
        Assert.Equal(rectangle.Center - rectangle.HalfSize, AxisAlignedRectangle2D.Builder.Create(4, 2, center).Min);
        Assert.Equal(rectangle.Center + rectangle.HalfSize, ShapeBounds2D.Calculate(RoundedRectangle2D.Builder.Create(4, 2, .5f, center)).Max);
        Assert.Throws<ArgumentOutOfRangeException>(() => Rectangle2D.Builder.Create(0, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => AxisAlignedRectangle2D.Builder.Create(2, float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => RoundedRectangle2D.Builder.Create(4, 2, 2));
    }
}
