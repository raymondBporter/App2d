using App2d.Core.Geometry.Operations.Bounds;
using App2d.Core.Geometry.Operations.MassProperties;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class MassProperties2DTests
{
    [Fact]
    public void DiskUsesItsOwnCenterAndTheSolidDiskFormula()
    {
        var result = ShapeMassProperties2D.Calculate(Circle2D.Builder.Create(3f, new(4f, -2f)), 2d);
        Close(9d * Math.PI, result.Area);
        Close(18d * Math.PI, result.Mass);
        Assert.Equal(new Vector2d(4d, -2d), result.CenterOfMass);
        Close(81d * Math.PI, result.MomentOfInertia);
        Close(result.MomentOfInertia + result.Mass * 20d, result.MomentOfInertiaAbout(Vector2d.Zero));
    }

    [Fact]
    public void EllipseAndBothRectangleTypesHaveAnalyticMassProperties()
    {
        var ellipse = ShapeMassProperties2D.Calculate(Ellipse2D.Builder.Create(new(3f, 2f), new(10f, -20f)), 2d);
        Close(6d * Math.PI, ellipse.Area);
        Close(12d * Math.PI, ellipse.Mass);
        Close(39d * Math.PI, ellipse.MomentOfInertia);
        Assert.Equal(new Vector2d(10d, -20d), ellipse.CenterOfMass);

        var rectangle = ShapeMassProperties2D.Calculate(Rectangle2D.Builder.Create(new(7f, -22f), new(13f, -18f)), 3d);
        var axisAligned = ShapeMassProperties2D.Calculate(AxisAlignedRectangle2D.Builder.Create(new(7f, -22f), new(13f, -18f)), 3d);
        Assert.Equal(rectangle, axisAligned);
        Close(24d, rectangle.Area);
        Close(72d, rectangle.Mass);
        Close(312d, rectangle.MomentOfInertia);
        Assert.Equal(new Vector2d(10d, -20d), rectangle.CenterOfMass);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TriangleIntegralAcceptsBothWindings(bool reverse)
    {
        var a = new Vector2(0f, 0f);
        var b = new Vector2(6f, 0f);
        var c = new Vector2(0f, 3f);
        var triangle = Triangle2D.Builder.Create(a, reverse ? c : b, reverse ? b : c);
        var result = ShapeMassProperties2D.Calculate(triangle, 2d);
        Close(9d, result.Area);
        Close(18d, result.Mass);
        Assert.Equal(new Vector2d(2d, 1d), result.CenterOfMass);
        Close(45d, result.MomentOfInertia);
    }

    [Fact]
    public void ConcaveOutlineMatchesItsDisjointRectangleDecomposition()
    {
        var outline = SimplePolygon2D.Builder.Create([
            new(0f, 0f), new(4f, 0f), new(4f, 1f), new(1f, 1f), new(1f, 4f), new(0f, 4f)
        ]);
        var composite = CompositeShape2D.Builder.Create([
            Rectangle2D.Builder.Create(new(0f, 0f), new(4f, 1f)),
            Rectangle2D.Builder.Create(new(0f, 1f), new(1f, 4f))
        ]);
        var result = ShapeMassProperties2D.Calculate(outline);
        var pieces = ShapeMassProperties2D.Calculate(composite);
        Close(7d, result.Mass);
        Close(19d / 14d, result.CenterOfMass.X);
        Close(19d / 14d, result.CenterOfMass.Y);
        Close(793d / 42d, result.MomentOfInertia);
        Close(result.Mass, pieces.Mass);
        Close(result.CenterOfMass.X, pieces.CenterOfMass.X);
        Close(result.CenterOfMass.Y, pieces.CenterOfMass.Y);
        Close(result.MomentOfInertia, pieces.MomentOfInertia);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SmallPolygonFarFromTheOriginKeepsItsCentroidalInertia(bool reverse)
    {
        Vector2[] vertices = [new(100_000_000f, -100_000_000f), new(100_000_032f, -100_000_000f),
            new(100_000_032f, -99_999_984f), new(100_000_000f, -99_999_984f)];
        if (reverse) Array.Reverse(vertices);
        var polygon = ConvexPolygon2D.Builder.Create(vertices);
        var result = ShapeMassProperties2D.Calculate(polygon);
        Close(512d, result.Mass);
        Assert.Equal(new Vector2d(100_000_016d, -99_999_992d), result.CenterOfMass);
        Close(512d * (32d * 32d + 16d * 16d) / 12d, result.MomentOfInertia);
    }

    [Fact]
    public void CapsuleUsesRectangleAndSemicircleMassAndCollapsesExactlyToADisk()
    {
        var horizontal = ShapeMassProperties2D.Calculate(Capsule2D.Builder.Create(new(0f, 0f), new(6f, 0f), 2f));
        var vertical = ShapeMassProperties2D.Calculate(Capsule2D.Builder.Create(new(0f, 6f), new(0f, 0f), 2f));
        Close(24d + 4d * Math.PI, horizontal.Mass);
        Close(168d + 44d * Math.PI, horizontal.MomentOfInertia);
        Close(horizontal.MomentOfInertia, vertical.MomentOfInertia);
        Assert.Equal(new Vector2d(3d, 0d), horizontal.CenterOfMass);
        Assert.Equal(new Vector2d(0d, 3d), vertical.CenterOfMass);

        var point = new Vector2(5f, -2f);
        Assert.Equal(ShapeMassProperties2D.Calculate(Circle2D.Builder.Create(2f, point)),
            ShapeMassProperties2D.Calculate(Capsule2D.Builder.Create(point, point, 2f)));
    }

    [Fact]
    public void RoundedRectangleLimitsMatchRectangleCircleAndCapsule()
    {
        var rectangle = ShapeMassProperties2D.Calculate(Rectangle2D.Builder.CreateFromSize(10f, 6f));
        Assert.Equal(rectangle, ShapeMassProperties2D.Calculate(RoundedRectangle2D.Builder.FromSize(10f, 6f, 0f)));
        var circle = ShapeMassProperties2D.Calculate(Circle2D.Builder.Create(2f));
        Assert.Equal(circle, ShapeMassProperties2D.Calculate(RoundedRectangle2D.Builder.FromSize(4f, 4f, 2f)));
        var capsule = ShapeMassProperties2D.Calculate(Capsule2D.Builder.Create(new(-3f, 0f), new(3f, 0f), 2f));
        Assert.Equal(capsule, ShapeMassProperties2D.Calculate(RoundedRectangle2D.Builder.FromSize(10f, 4f, 2f)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CurvedFormulasAgreeWithIndependentAreaSampling(bool capsule)
    {
        IShape2D shape = capsule
            ? Capsule2D.Builder.Create(new(-2f, -1f), new(4f, 3f), 1.3f)
            : RoundedRectangle2D.Builder.FromSize(7f, 4f, 1.3f, new(2f, -1f));
        var result = ShapeMassProperties2D.Calculate(shape);
        var bounds = ShapeBounds2D.Calculate(shape);
        const int samples = 600;
        var stepX = bounds.Width / samples;
        var stepY = bounds.Height / samples;
        var origin = result.CenterOfMass;
        var first = Vector2d.Zero;
        var inertia = 0d;
        var count = 0;
        for (var y = 0; y < samples; y++)
        {
            for (var x = 0; x < samples; x++)
            {
                var point = new Vector2(bounds.Left + (x + 0.5f) * stepX, bounds.Bottom + (y + 0.5f) * stepY);
                if (!shape.ContainsPoint(point)) continue;
                var relative = new Vector2d(point) - origin;
                count++;
                first += relative;
                inertia += relative.LengthSquared();
            }
        }
        Close(result.Area, count * (double)stepX * stepY, 0.002);
        Close(result.MomentOfInertia, inertia * stepX * stepY, 0.002);
        Assert.InRange((first / count).Length(), 0d, 0.001d);
    }

    [Fact]
    public void ChangingDensityOrTotalMassDoesNotMoveTheCentroid()
    {
        var shape = Circle2D.Builder.Create(3f, new(2f, 5f));
        var unit = ShapeMassProperties2D.Calculate(shape);
        var dense = ShapeMassProperties2D.Calculate(shape, 3d);
        Close(unit.Mass * 3d, dense.Mass);
        Close(unit.MomentOfInertia * 3d, dense.MomentOfInertia);
        Assert.Equal(unit.CenterOfMass, dense.CenterOfMass);
        var chosen = unit.WithMass(4d);
        Close(4d, chosen.Mass);
        Close(18d, chosen.MomentOfInertia);
        Assert.Equal(unit.CenterOfMass, chosen.CenterOfMass);
    }

    [Fact]
    public void AdditiveOverlapIsExplicitAndUnionOrInfiniteMassIsRejected()
    {
        var disk = Circle2D.Builder.Create(2f);
        var single = ShapeMassProperties2D.Calculate(disk);
        var added = ShapeMassProperties2D.Calculate(CompositeShape2D.Builder.Create([disk, disk]));
        Close(single.Mass * 2d, added.Mass);
        Close(single.MomentOfInertia * 2d, added.MomentOfInertia);
        Assert.Throws<NotSupportedException>(() => ShapeMassProperties2D.Calculate(
            CompositeShape2D.Builder.Create([disk, disk], includeOverlap: false)));
        Assert.Throws<NotSupportedException>(() => ShapeMassProperties2D.Calculate(HalfSpace2D.Builder.Create(Vector2.UnitY, 0f)));
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void DensityAndChosenMassMustBeFiniteAndPositive(double value)
    {
        var shape = Circle2D.Builder.Create(2f);
        Assert.Throws<ArgumentOutOfRangeException>(() => ShapeMassProperties2D.Calculate(shape, value));
        Assert.Throws<ArgumentOutOfRangeException>(() => ShapeMassProperties2D.Calculate(shape).WithMass(value));
    }

    private static void Close(double expected, double actual, double relativeTolerance = 1e-10) =>
        Assert.InRange(Math.Abs(expected - actual), 0d, Math.Max(1d, Math.Abs(expected)) * relativeTolerance);
}
