using App2d.Core.Geometry.Curves;
using App2d.Core.Mathematics.Transforms;
using System.Numerics;

namespace App2d.Tests.Curves;

public sealed class GenericCurveTests
{
    [Fact]
    public void BeziersSupportScalarsAndAllSixVectorTypesWithNormalizedDerivatives()
    {
        Check<float, float>(1f);
        Check<double, double>(1d);
        Check<Vector2f, float>(Vector2f.UnitY);
        Check<Vector3f, float>(Vector3f.UnitZ);
        Check<Vector4f, float>(Vector4f.UnitW);
        Check<Vector2d, double>(Vector2d.UnitY);
        Check<Vector3d, double>(Vector3d.UnitZ);
        Check<Vector4d, double>(Vector4d.UnitW);
    }

    private static void Check<TValue, TReal>(TValue axis)
        where TValue : IAdditionOperators<TValue, TValue, TValue>, ISubtractionOperators<TValue, TValue, TValue>,
            IMultiplyOperators<TValue, TReal, TValue>, IAdditiveIdentity<TValue, TValue>
        where TReal : unmanaged, IFloatingPointIeee754<TReal>
    {
        var two = TReal.CreateChecked(2);
        var three = TReal.CreateChecked(3);
        var q = new QuadraticBezier<TValue, TReal>(TValue.AdditiveIdentity, axis, axis * two);
        var c = new CubicBezier<TValue, TReal>(TValue.AdditiveIdentity, axis, axis * two, axis * three);
        foreach (var parameter in new[] { -.5, 0, .25, .5, .75, 1, 1.5 })
        {
            var t = TReal.CreateChecked(parameter);
            // Evenly spaced controls make both curves linear, and nothing clamps: the line continues outside [0, 1].
            Assert.Equal(axis * (two * t), q.Evaluate(t));
            Assert.Equal(axis * two, q.Derivative(t));
            Assert.Equal(axis * (three * t), c.Evaluate(t));
            Assert.Equal(axis * three, c.Derivative(t));
        }
    }

    [Fact]
    public void TransformPreservesTheCurveAndAppliesNoTranslationToItsDerivative()
    {
        var curve = new CubicBezier<Vector3f, float>(new(0, 1, -2), new(1, 2, 3), new(3, -1, -4), new(4, 2, 1));
        var frame = new Frame3D(new(5, 7, 11), new(2, 0, 0), new(.5f, 3, 0), new(0, 0, -4));
        var transformed = curve.Ops.Transform(frame);
        foreach (var t in new[] { 0f, .25f, .5f, .75f, 1f })
        {
            Assert.Equal(frame.TransformPoint(curve.Evaluate(t)), transformed.Evaluate(t));
            Assert.Equal(frame.TransformDirection(curve.Derivative(t)), transformed.Derivative(t));
        }
        var derivative = (transformed.Evaluate(.501f) - transformed.Evaluate(.499f)) / .002f;
        Assert.InRange((derivative - transformed.Derivative(.5f)).Length(), 0, .002f);
    }

    [Fact]
    public void DoubleTransformationDoesNotNarrowLargeCoordinates()
    {
        var curve = new QuadraticBezier<Vector3d, double>(new(16_777_217, 0, 1), new(16_777_218, 1, 2), new(16_777_219, 0, 3));
        var transformed = curve.Ops.Transform(Frame3D.Identity.WithOrigin(Vector3f.UnitX));
        Assert.Equal(16_777_218, transformed.Evaluate(0).X);
        Assert.Equal(curve.Derivative(.5), transformed.Derivative(.5));
    }

    [Fact]
    public void Transformed2DCurveMovesPointsButNotTangentsByTranslation()
    {
        var line = LineSegmentCurve2D.Create(new(1, 0), new(3, 0));
        var turned = line.Ops.Transform(Frame2D.FromRotation(MathF.PI / 2, new(10, 0)));
        var moved = line.Ops.Transform(new RigidTransform2D(new(10, 0)));

        Assert.Equal(10f, turned.Evaluate(0).X, 5);
        Assert.Equal(1f, turned.Evaluate(0).Y, 5);
        Assert.Equal(0f, turned.Derivative(.5f).X, 5);
        Assert.Equal(2f, turned.Derivative(.5f).Y, 5);
        Assert.Equal(new Vector2f(11, 0), moved.Evaluate(0));
        Assert.Equal(new Vector2f(2, 0), moved.Derivative(0));
    }

    [Fact]
    public void ScalarAttributeEvaluationUsesTheSameBernsteinWeightsAsSpace()
    {
        var curve = new CubicBezier<Vector3f, float>(new(0, 0, -3), new(1, 2, 8), new(3, 4, -2), new(5, 6, 9));
        for (var i = 0; i <= 16; i++)
            Assert.Equal(curve.Evaluate(i / 16f).Z, BezierMath.Cubic<float, float>(-3, 8, -2, 9, i / 16f));
    }
}
