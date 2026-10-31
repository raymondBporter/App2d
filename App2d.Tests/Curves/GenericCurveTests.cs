using App2d.Core.Geometry.Curves;
using App2d.Core.Mathematics;
using App2d.Core.Mathematics.Transforms;
using System.Numerics;

namespace App2d.Tests.Curves;

public sealed class GenericCurveTests
{
    [Fact]
    public void BeziersSupportAllSixVectorTypesAndNormalizedDerivatives()
    {
        Check<Vector2f, float>(Vector2f.UnitY);
        Check<Vector3f, float>(Vector3f.UnitZ);
        Check<Vector4f, float>(Vector4f.UnitW);
        Check<Vector2d, double>(Vector2d.UnitY);
        Check<Vector3d, double>(Vector3d.UnitZ);
        Check<Vector4d, double>(Vector4d.UnitW);
    }

    private static void Check<TVector, TReal>(TVector axis)
        where TVector : unmanaged, IVector<TVector, TReal>
        where TReal : unmanaged, IFloatingPointIeee754<TReal>
    {
        var two = TReal.CreateChecked(2);
        var three = TReal.CreateChecked(3);
        var q = new QuadraticBezier<TVector, TReal>(TVector.Zero, axis, axis * two);
        var c = new CubicBezier<TVector, TReal>(TVector.Zero, axis, axis * two, axis * three);
        foreach (var parameter in new[] { -.5, 0, .25, .5, .75, 1, 1.5 })
        {
            var t = TReal.CreateChecked(parameter);
            var progress = TReal.Clamp(t, TReal.Zero, TReal.One);
            Assert.Equal(axis * (two * progress), q.Evaluate(t));
            Assert.Equal(axis * two, q.EvaluateDerivative(t));
            Assert.Equal(axis * (three * progress), c.Evaluate(t));
            Assert.Equal(axis * three, c.EvaluateDerivative(t));
        }
    }

    [Fact]
    public void TransformPreservesTheCurveAndAppliesNoTranslationToItsDerivative()
    {
        var curve = new CubicBezier<Vector3f, float>(new(0, 1, -2), new(1, 2, 3), new(3, -1, -4), new(4, 2, 1));
        var frame = new Frame3D(new(5, 7, 11), new(2, 0, 0), new(.5f, 3, 0), new(0, 0, -4));
        var transformed = curve.TransformedBy(frame);
        foreach (var t in new[] { 0f, .25f, .5f, .75f, 1f })
        {
            Assert.Equal(new Vector3f(frame.TransformPoint(curve.Evaluate(t).ToVector3())), transformed.Evaluate(t));
            Assert.Equal(new Vector3f(frame.TransformDirection(curve.EvaluateDerivative(t).ToVector3())), transformed.EvaluateDerivative(t));
        }
        var derivative = (transformed.Evaluate(.501f) - transformed.Evaluate(.499f)) / .002f;
        Assert.InRange((derivative - transformed.EvaluateDerivative(.5f)).Length(), 0, .002f);
    }

    [Fact]
    public void DoubleTransformationDoesNotNarrowLargeCoordinates()
    {
        var curve = new QuadraticBezier<Vector3d, double>(new(16_777_217, 0, 1), new(16_777_218, 1, 2), new(16_777_219, 0, 3));
        var transformed = curve.TransformedBy(Frame3D.Identity.WithOrigin(Vector3.UnitX));
        Assert.Equal(16_777_218, transformed.Evaluate(0).X);
        Assert.Equal(curve.EvaluateDerivative(.5), transformed.EvaluateDerivative(.5));
    }

    [Fact]
    public void ScalarAttributeEvaluationUsesTheSameBernsteinWeightsAsSpace()
    {
        var curve = new CubicBezier<Vector3f, float>(new(0, 0, -3), new(1, 2, 8), new(3, 4, -2), new(5, 6, 9));
        for (var i = 0; i <= 16; i++)
            Assert.Equal(curve.Evaluate(i / 16f).Z, BezierMath.Cubic<float, float>(-3, 8, -2, 9, i / 16f));
    }
}
