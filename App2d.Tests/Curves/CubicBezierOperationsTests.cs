using App2d.Core.Geometry.Curves;
using System.Numerics;

namespace App2d.Tests.Curves;

public sealed class CubicBezierOperationsTests
{
    private static CubicBezier2D Example => new(new(1, 2), new(-3, 8), new(7, -5), new(9, 3));

    [Theory]
    [InlineData(0f)]
    [InlineData(.23f)]
    [InlineData(.8f)]
    [InlineData(1f)]
    public void SplitPreservesBothHalvesAndRescalesTheirDerivatives(float at)
    {
        var curve = Example;
        var (left, right) = curve.Split(at);
        Assert.Equal(curve.Start, left.Start);
        Assert.Equal(curve.End, right.End);
        Assert.Equal(left.End, right.Start);
        for (var i = 0; i <= 20; i++)
        {
            var u = i / 20f;
            Near(curve.Evaluate(at * u), left.Evaluate(u));
            Near(curve.Evaluate(at + (1 - at) * u), right.Evaluate(u));
            Near(curve.EvaluateDerivative(at * u) * at, left.EvaluateDerivative(u));
            Near(curve.EvaluateDerivative(at + (1 - at) * u) * (1 - at), right.EvaluateDerivative(u));
        }
    }

    [Theory]
    [InlineData(0f, 1f)]
    [InlineData(0f, .7f)]
    [InlineData(.2f, 1f)]
    [InlineData(.2f, .7f)]
    [InlineData(0f, 0f)]
    [InlineData(.4f, .4f)]
    [InlineData(1f, 1f)]
    public void SlicePreservesTheParameterIntervalIncludingCollapsedIntervals(float from, float to)
    {
        var curve = Example;
        var slice = curve.Slice(from, to);
        for (var i = 0; i <= 20; i++)
        {
            var u = i / 20f;
            Near(curve.Evaluate(from + (to - from) * u), slice.Evaluate(u));
        }
        if (from == 0 && to == 1)
        {
            Assert.Equal(curve, slice);
        }
    }

    [Fact]
    public void XLookupReturnsAllThreeCrossingsInOrderWithoutTouchingUnusedStorage()
    {
        // X(u) = 3 * (u - 1/4) * (u - 1/2) * (u - 3/4).
        var curve = new CubicBezier2D(new(-.28125f, 0), new(.40625f, 1), new(-.40625f, 2), new(.28125f, 3));
        Span<double> parameters = stackalloc double[] { 99, 99, 99, 99 };
        Assert.Equal(3, curve.FindParametersAtX(0, parameters));
        Assert.Equal(.25, parameters[0], 12);
        Assert.Equal(.5, parameters[1], 12);
        Assert.Equal(.75, parameters[2], 12);
        Assert.Equal(99d, parameters[3]);
        Assert.Throws<InvalidOperationException>(() => curve.ParameterAtX(0));
    }

    [Fact]
    public void XLookupIncludesTangenciesEndpointsAndFlatTimeTangents()
    {
        // X(u) = 3 * (u - 1/2)^2 * (u - 1).
        var tangent = new CubicBezier2D(new(-.75f, 0), new(.5f, 1), new(-.25f, 2), new(0, 3));
        Span<double> parameters = stackalloc double[3];
        Assert.Equal(2, tangent.FindParametersAtX(0, parameters));
        Assert.Equal(.5, parameters[0], 12);
        Assert.Equal(1d, parameters[1]);
        var flat = new CubicBezier2D(Vector2.Zero, new(1, 1), new(0, 2), new(1, 3));
        Assert.Equal(.5f, flat.ParameterAtX(.5f));
        Assert.Equal(0f, flat.ParameterAtX(0));
        Assert.Equal(1f, flat.ParameterAtX(1));
    }

    [Fact]
    public void XLookupHandlesDecreasingLinearAndConstantCoordinates()
    {
        var decreasing = new CubicBezier2D(new(3, 0), new(2, 1), new(1, 2), new(0, 3));
        Assert.Equal(.25f, decreasing.ParameterAtX(2.25f));
        Span<double> parameters = stackalloc double[3];
        Assert.Equal(0, decreasing.FindParametersAtX(4, parameters));
        Assert.Throws<InvalidOperationException>(() => decreasing.ParameterAtX(4));
        var vertical = new CubicBezier2D(new(2, 0), new(2, 1), new(2, 2), new(2, 3));
        Assert.Equal(0, vertical.FindParametersAtX(3, parameters));
        Assert.Throws<InvalidOperationException>(() => vertical.FindParametersAtX(2, new double[3]));
    }

    [Fact]
    public void UniqueXLookupAgreesWithAllRootsForBoundedAndCrossedHandles()
    {
        var random = new Random(712);
        Span<double> parameters = stackalloc double[3];
        for (var i = 0; i < 100; i++)
        {
            var curve = new CubicBezier2D(Vector2.Zero, new(random.NextSingle(), 1), new(random.NextSingle(), -1), Vector2.One);
            for (var j = 0; j <= 20; j++)
            {
                var x = j / 20f;
                Assert.Equal(1, curve.FindParametersAtX(x, parameters));
                Assert.InRange(Math.Abs(curve.ParameterAtX(x) - parameters[0]), 0, 1e-7);
            }
        }
        var veryFlat = new CubicBezier2D(Vector2.Zero, Vector2.Zero, Vector2.Zero, new(float.MaxValue, 1));
        Assert.Equal(1, veryFlat.FindParametersAtX(float.Epsilon, parameters));
        Assert.InRange(Math.Abs(veryFlat.ParameterAtX(float.Epsilon) / parameters[0] - 1), 0, 1e-6);
    }

    [Fact]
    public void InvalidParametersAndShortBuffersAreRejectedBeforeWriting()
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => Example.Split(-.1f));
        Assert.Equal("amount", error.ParamName);
        Assert.Equal(-.1f, error.ActualValue);
        Assert.Throws<ArgumentOutOfRangeException>(() => Example.Split(float.NaN));
        Assert.Throws<ArgumentException>(() => Example.Slice(.7f, .2f));
        Assert.Throws<ArgumentException>(() => Example.Slice(0, 1.1f));
        Assert.Throws<ArgumentException>(() => Example.Slice(float.NaN, 1));
        var buffer = new double[] { 99, 99 };
        Assert.Throws<ArgumentOutOfRangeException>(() => Example.FindParametersAtX(0, buffer));
        Assert.Equal(new double[] { 99, 99 }, buffer);
        Assert.Throws<ArgumentOutOfRangeException>(() => Example.ParameterAtX(float.PositiveInfinity));
    }

    [Fact]
    public void SplittingSlicingAndXLookupDoNotAllocate()
    {
        var curve = new CubicBezier2D(Vector2.Zero, new(.15f, 2), new(.8f, -1), Vector2.One);
        for (var i = 0; i < 100; i++)
        {
            curve.Split(.3f);
            curve.Slice(.2f, .8f);
            curve.ParameterAtX(.4f);
        }
        var before = GC.GetAllocatedBytesForCurrentThread();
        var left = curve.Split(.3f).Left;
        var slice = curve.Slice(.2f, .8f);
        var parameter = curve.ParameterAtX(.4f);
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
        Near(curve.Evaluate(.3f), left.End);
        Near(curve.Evaluate(.2f), slice.Start);
        Assert.InRange(MathF.Abs(curve.Evaluate(parameter).X - .4f), 0, 1e-6f);
    }

    private static void Near(Vector2 expected, Vector2 actual) =>
        Assert.True(Vector2.Distance(expected, actual) < 2e-5f, $"Expected {expected}, got {actual}.");
}
