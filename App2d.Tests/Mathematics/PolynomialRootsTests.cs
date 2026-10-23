using App2d.Core.Mathematics;

namespace App2d.Tests.Mathematics;

public sealed class PolynomialRootsTests
{
    [Fact]
    public void LinearThroughQuarticRootsAreSortedAndFilteredToTheInterval()
    {
        AssertRoots([-2, 1], -10, 10, [2]);
        AssertRoots([6, -5, 1], -10, 10, [2, 3]);
        AssertRoots([6, -5, 1], 2, 2.5, [2]);
        AssertRoots([6, -5, 1], 2.5, 3, [3]);
        AssertRoots([6, -5, 1], -1, 1, []);
        AssertRoots([6, -5, -2, 1], -10, 10, [-2, 1, 3]);
        AssertRoots([24, -14, -13, 2, 1], -10, 10, [-4, -2, 1, 3]);
        AssertRoots([24, -14, -13, 2, 1], -2, 1, [-2, 1]);
    }

    [Fact]
    public void RepeatedRootsAreReportedOnceEvenWithoutASignChange()
    {
        AssertRoots([1, -2, 1], -2, 2, [1]);
        AssertRoots([0, 0, 0, 1], -1, 1, [0]);
        AssertRoots([.0625, 0, -.5, 0, 1], -1, 1, [-.5, .5]);
        AssertRoots([.0625, -.5, 1.5, -2, 1], -1, 1, [.5]);
        AssertRoots([0, 0, 1], 0, 0, [0]);
    }

    [Fact]
    public void ComplexRootsAndTrailingZeroCoefficientsAreHandled()
    {
        AssertRoots([1, 0, 1], -10, 10, []);
        AssertRoots([-1, 0, 0, 0, 1], -10, 10, [-1, 1]);
        AssertRoots([-1, 1, 0, 0, 0], -10, 10, [1]);
        AssertRoots([5, 0, 0], -10, 10, []);
    }

    [Theory]
    [InlineData(1e-280)]
    [InlineData(-1e280)]
    public void MultiplyingCoefficientsByAScaleDoesNotChangeTheRoots(double scale)
    {
        AssertRoots([24 * scale, -14 * scale, -13 * scale, 2 * scale, scale], -10, 10, [-4, -2, 1, 3]);
    }

    [Fact]
    public void CloseRootsAndLargeIntervalsRemainResolvable()
    {
        AssertRoots([-1e-20, 0, 1], -1, 1, [-1e-10, 1e-10], 1e-20);
        AssertRoots([.25 - 1e-12, -1, 1], 0, 1, [.5 - 1e-6, .5 + 1e-6], 2e-11);
        AssertRoots([-1, 0, 1], -double.MaxValue, double.MaxValue, [-1, 1]);
    }

    [Fact]
    public void QuadraticsKeepSmallRootsWhenTheOtherRootWouldCauseCancellation()
    {
        foreach (var scale in new[] { 1d, 1e-280, -1e280 })
        {
            AssertRelativeRoots([scale, -1e16 * scale, scale], [1e-16, 1e16]);
            AssertRelativeRoots([scale, 1e16 * scale, scale], [-1e16, -1e-16]);
        }
        AssertRelativeRoots([1, -1e200, 1], [1e-200, 1e200]);
    }

    [Fact]
    public void ZeroConstantQuadraticsKeepDistinctTinyRootsWithoutSquaringThem()
    {
        AssertRelativeRoots([0, -1e-200, 1], [0, 1e-200]);
        AssertRelativeRoots([0, 1e-200, 1], [-1e-200, 0]);
        AssertRoots([0, 0, 1], -1, 1, [0]);
        AssertRoots([1, -1, double.Epsilon], -double.MaxValue, double.MaxValue, [1]);
    }

    [Fact]
    public void QuadraticTangenciesRetainTheNumericalSolversEvaluationTolerance()
    {
        AssertRoots([1, -2, 1], 1, 1, [1]);
        AssertRoots([Math.BitIncrement(1d), -2, 1], 0, 2, [1]);
        AssertRoots([Math.BitDecrement(1d), -2, 1], 0, 2, [1]);
        AssertRoots([1 + 1e-10, -2, 1], 0, 2, []);
        AssertRoots([1 - 1e-10, -2, 1], 0, 2, [1 - 1e-5, 1 + 1e-5], 1e-11);
    }

    [Fact]
    public void QuadraticFormulaRootsRetainInclusiveBoundariesAndLeaveUnusedStorageAlone()
    {
        AssertRoots([6, -5, 1], 2, 2, [2]);
        AssertRoots([6, -5, 1], 3, 3, [3]);
        AssertRoots([6, -5, 1], 2 + 1e-8, 3 - 1e-8, []);
        AssertRoots([6, -5, 1], 2, 3 - 1e-8, [2]);
        AssertRoots([6, -5, 1], 2 + 1e-8, 3, [3]);
        Span<double> roots = [99, 99, 99];
        Assert.Equal(1, PolynomialRoots.FindRealRoots([1, -2, 1], 0, 2, roots));
        Assert.Equal(1d, roots[0]);
        Assert.Equal(99d, roots[1]);
        Assert.Equal(99d, roots[2]);
    }

    [Fact]
    public void CubicsAndQuarticsStillResolveWhenTheirDerivativeCoefficientsHaveATinyScale()
    {
        AssertRelativeRoots([-1, 0, 0, 1e-240], [1e80]);
        AssertRelativeRoots([-1, 0, 2e-200, 0, 1e-200], [-1e50, 1e50]);
    }

    [Fact]
    public void QuarticsWithFourKnownRootsKeepAllOfThem()
    {
        var random = new Random(71599);
        for (var sample = 0; sample < 64; sample++)
        {
            var expected = new double[4];
            double[] coefficients = [1];
            for (var i = 0; i < expected.Length; i++)
            {
                expected[i] = -.9d + .5d * i + .2d * random.NextDouble();
                var next = new double[coefficients.Length + 1];
                for (var j = 0; j < coefficients.Length; j++)
                {
                    next[j] -= coefficients[j] * expected[i];
                    next[j + 1] += coefficients[j];
                }
                coefficients = next;
            }
            AssertRoots(coefficients, -1, 1, expected);
            AssertRoots(coefficients, 0, 1, expected[2..]);
        }
    }

    [Fact]
    public void InvalidInputsAndInsufficientBuffersAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PolynomialRoots.FindRealRoots([], -1, 1, new double[4]));
        Assert.Throws<ArgumentException>(() => PolynomialRoots.FindRealRoots([0, 0], -1, 1, new double[4]));
        Assert.Throws<ArgumentException>(() => PolynomialRoots.FindRealRoots([1, 1, 1, 1, 1, 1], -1, 1, new double[5]));
        Assert.Throws<ArgumentException>(() => PolynomialRoots.FindRealRoots([1, 0, 1], -1, 1, new double[1]));
        Assert.Throws<ArgumentOutOfRangeException>(() => PolynomialRoots.FindRealRoots([double.NaN, 1], -1, 1, new double[4]));
        Assert.Throws<ArgumentOutOfRangeException>(() => PolynomialRoots.FindRealRoots([1, double.PositiveInfinity], -1, 1, new double[4]));
        Assert.Throws<ArgumentOutOfRangeException>(() => PolynomialRoots.FindRealRoots([1, 1], double.NegativeInfinity, 1, new double[4]));
        Assert.Throws<ArgumentException>(() => PolynomialRoots.FindRealRoots([1, 1], 1, -1, new double[4]));
    }

    private static void AssertRelativeRoots(double[] coefficients, double[] expected)
    {
        Span<double> roots = stackalloc double[4];
        var count = PolynomialRoots.FindRealRoots(coefficients, -double.MaxValue, double.MaxValue, roots);
        Assert.Equal(expected.Length, count);
        for (var i = 0; i < count; i++)
        {
            if (expected[i] == 0d)
            {
                Assert.Equal(0d, roots[i]);
            }
            else
            {
                Assert.InRange(Math.Abs(roots[i] / expected[i] - 1d), 0, 2e-14);
            }
        }
    }

    private static void AssertRoots(double[] coefficients, double minimum, double maximum, double[] expected, double tolerance = 1e-12)
    {
        Span<double> roots = stackalloc double[4];
        var count = PolynomialRoots.FindRealRoots(coefficients, minimum, maximum, roots);
        Assert.Equal(expected.Length, count);
        for (var i = 0; i < count; i++)
        {
            Assert.InRange(Math.Abs(roots[i] - expected[i]), 0, tolerance);
        }
    }
}
