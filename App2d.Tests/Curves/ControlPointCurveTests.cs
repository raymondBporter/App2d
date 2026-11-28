using App2d.Core.Geometry.Curves;
using App2d.Core.Mathematics.Curves;

namespace App2d.Tests.Curves;

/// <summary>Control points are a capability of the curve itself, in any dimension, with no type switch.</summary>
public sealed class ControlPointCurveTests
{
    public static TheoryData<ICurve2D, int> PointDefinedCurves => new()
    {
        { QuadraticBezier2D.Builder.Create(new(-1, 1), new(0, -1), new(1, 1)), 3 },
        { CubicBezier2D.Builder.Create(new(1, 2), new(3, 8), new(7, 5), new(9, -3)), 4 },
        { LineSegmentCurve2D.Create(new(1, 2), new(4, 6)), 2 },
        { PolylineCurve2D.Builder.Create([new(0, 0), new(1, 0), new(1, 1)]), 3 },
        { BSpline2D.Builder.Create([new(0, 0), new(1, 3), new(4, 3), new(5, 0), new(7, 1)], 3), 5 },
        { RoundedPolylineCurve2D.Builder.Create([new(0, 0), new(10, 0), new(10, 10)]), 3 }
    };

    [Theory]
    [MemberData(nameof(PointDefinedCurves))]
    public void EveryPointDefinedCurveMovesWithItsControlPoints(ICurve2D curve, int count)
    {
        var editable = Assert.IsType<IControlPointCurve<Vector2f, ICurve2D>>(curve, exactMatch: false);
        var shift = new Vector2f(1, 2);
        var moved = new Vector2f[editable.ControlPointCount];
        for (var i = 0; i < moved.Length; i++) moved[i] = editable.GetControlPoint(i) + shift;

        var result = editable.WithControlPoints(moved);

        Assert.Equal(count, editable.ControlPointCount);
        Assert.IsType(curve.GetType(), result);
        var readBack = Assert.IsType<IControlPointCurve<Vector2f, ICurve2D>>(result, exactMatch: false);
        for (var i = 0; i < moved.Length; i++) Assert.Equal(moved[i], readBack.GetControlPoint(i));
        Assert.InRange((result.Evaluate(.4f) - curve.Evaluate(.4f) - shift).Length(), 0, 1e-4f);
    }

    [Theory]
    [MemberData(nameof(PointDefinedCurves))]
    public void AWrongPointCountIsRejected(ICurve2D curve, int count)
    {
        var editable = (IControlPointCurve<Vector2f, ICurve2D>)curve;

        Assert.Throws<InvalidDataException>(() => editable.WithControlPoints(new Vector2f[count + 1]));
        Assert.Throws<InvalidDataException>(() => editable.WithControlPoints(new Vector2f[count - 1]));
    }

    [Fact]
    public void GenericBeziersKeepTheirOwnTypeInAnyDimension()
    {
        var cubic = new CubicBezier<Vector3f, float>(new(0, 0, 0), new(1, 0, 0), new(2, 1, 0), new(3, 1, 5));
        var scalar = new QuadraticBezier<double, double>(0d, 0d, 1d);

        Assert.Equal(4, cubic.ControlPointCount);
        Assert.Equal(cubic.Control2, cubic.GetControlPoint(2));
        Assert.Equal(new CubicBezier<Vector3f, float>(new(9, 0, 0), new(8, 0, 0), new(7, 0, 0), new(6, 0, 0)), cubic.WithControlPoints([new(9, 0, 0), new(8, 0, 0), new(7, 0, 0), new(6, 0, 0)]));
        Assert.Equal(new QuadraticBezier<double, double>(1d, 2d, 3d), scalar.WithControlPoints([1d, 2d, 3d]));
        Assert.Throws<InvalidDataException>(() => scalar.WithControlPoints([1d, 2d]));
        Assert.Throws<ArgumentOutOfRangeException>(() => cubic.GetControlPoint(4));
    }

    [Fact]
    public void ABSplineKeepsItsDegree()
    {
        var spline = BSpline2D.Builder.Create([new(0, 0), new(1, 3), new(4, 3), new(5, 0)], 2);

        var result = (BSpline2D)spline.WithControlPoints([new(0, 1), new(1, 4), new(4, 4), new(5, 1)]);

        Assert.Equal(2, result.Degree);
    }

    [Fact]
    public void ACurveNotDefinedByPointsHasNoControlPoints()
    {
        ICurve2D arc = Arc2D.Builder.Create(Vector2f.Zero, 1, 0, 1);

        Assert.False(arc is IControlPointCurve<Vector2f, ICurve2D>);
    }
}
