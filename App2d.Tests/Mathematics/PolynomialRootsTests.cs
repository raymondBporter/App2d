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

    private static void AssertRoots(double[] coefficients, double minimum, double maximum, double[] expected, double tolerance = 1e-12)
    {
        Span<double> roots = stackalloc double[4];
        var count = PolynomialRoots.FindRealRoots(coefficients, minimum, maximum, roots);
        Assert.Equal(expected.Length, count);
        for (var i = 0; i < count; i++) Assert.InRange(Math.Abs(roots[i] - expected[i]), 0, tolerance);
    }
}
