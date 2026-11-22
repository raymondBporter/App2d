using App2d.Core.Geometry.Primitives;
using System.Numerics;
using System.Reflection;

namespace App2d.Tests.Mathematics;

/// <summary>
/// IInterval is only Min and Max. A type that implements it reaches every query through Interval&lt;T&gt;.From.
/// </summary>
public sealed class IIntervalTests
{
    [Fact]
    public void TheInterfaceIsOnlyTheTwoEndpoints()
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        Assert.Equal(["Max", "Min"], typeof(IInterval<float>).GetProperties(all).Select(property => property.Name).Order());
        Assert.DoesNotContain(typeof(IInterval<float>).GetMethods(all), method => !method.IsSpecialName);
    }

    [Fact]
    public void EndpointsAloneReachEveryQueryThroughFrom()
    {
        AssertQueriesThroughFrom<float>();
        AssertQueriesThroughFrom<double>();
        AssertQueriesThroughFrom<int>();
        AssertQueriesThroughFrom<decimal>();

        var reference = Interval<double>.From(new ReferenceInterval<double>(2, 5));
        Assert.Equal(3d, reference.Length);
        Assert.True(reference.Contains(3d));
        Assert.True(reference.TryIntersect(new(4, 8), out var shared));
        Assert.Equal(new Interval<double>(4, 5), shared);
    }

    [Theory]
    [InlineData(float.NaN, 1f)]
    [InlineData(0f, float.NaN)]
    [InlineData(2f, 1f)]
    [InlineData(float.PositiveInfinity, 1f)]
    [InlineData(0f, float.NegativeInfinity)]
    public void FromValidatesTheEndpointsItCopies(float min, float max)
    {
        Assert.ThrowsAny<ArgumentException>(() => Interval<float>.From(new EndpointsOnly<float>(min, max)));
        Assert.False(Interval<float>.IsValid(min, max));
    }

    [Fact]
    public void FromKeepsUnboundedAndSingletonEndpoints()
    {
        var entire = Interval<double>.From(new EndpointsOnly<double>(double.NegativeInfinity, double.PositiveInfinity));
        Assert.True(entire.IsEntire);
        Assert.False(entire.IsBoundedBelow);
        Assert.False(entire.IsBoundedAbove);
        Assert.False(entire.IsSingleton);
        Assert.Equal(double.PositiveInfinity, entire.Length);
        Assert.True(double.IsNaN(entire.Clamp(double.NaN)));
        Assert.False(entire.Contains(double.NaN));

        var lower = Interval<double>.From(new EndpointsOnly<double>(2, double.PositiveInfinity));
        Assert.True(lower.IsBoundedBelow);
        Assert.False(lower.IsBoundedAbove);
        Assert.False(lower.IsEntire);
        Assert.Equal(2d, lower.Clamp(-1d));

        var singleton = Interval<int>.From(default(EndpointsOnly<int>));
        Assert.True(singleton.IsSingleton);
        Assert.Equal(0, singleton.Length);
        Assert.Equal(0, singleton.Clamp(42));
    }

    [Fact]
    public void QueriesRetainDecimalAndBigIntegerPrecision()
    {
        var precise = new Interval<decimal>(1m, 1.0000000000000000000000000001m);
        Assert.Equal(0.0000000000000000000000000001m, precise.Length);
        Assert.Equal(precise.Max, precise.Clamp(2m));

        var start = BigInteger.One << 200;
        var wide = Interval<BigInteger>.From(new EndpointsOnly<BigInteger>(start, start + 5));
        Assert.Equal(new BigInteger(5), wide.Length);
        Assert.True(wide.Contains(start + 1));
        Assert.Equal(start + 5, wide.Clamp(start + 100));
        Assert.True(wide.TryIntersect(new(start + 3, start + 8), out var shared));
        Assert.Equal(new Interval<BigInteger>(start + 3, start + 5), shared);
    }

    [Fact]
    public void QueriesDistinguishTouchingFromDisjointIntervals()
    {
        var limits = new Interval<float>(2, 5);

        Assert.True(limits.ContainsInterval(new(3, 4)));
        Assert.True(limits.Overlaps(new(5, 8)));
        Assert.True(limits.TryIntersect(new(5, 8), out var touching));
        Assert.Equal(new Interval<float>(5, 5), touching);
        Assert.False(limits.TryIntersect(new(6, 8), out var disjoint));
        Assert.Equal(default, disjoint);
        Assert.Equal(new Interval<float>(2, 8), limits.Hull(new(6, 8)));
    }

    [Fact]
    public void ArithmeticRetainsIntegerAndDoubleEndpointTypes()
    {
        var cells = new Interval<int>(2, 5);
        Assert.Equal(3, cells.Length);
        Assert.Equal(5, cells.Clamp(10));

        var next = double.BitIncrement(1d);
        var precise = new Interval<double>(1d, next);
        Assert.Equal(next - 1d, precise.Length);
        Assert.True(precise.Length > 0d);
        Assert.Equal(next, precise.Clamp(2d));
        Assert.True(precise.TryIntersect(new(next, 2d), out var shared));
        Assert.Equal(new Interval<double>(next, next), shared);
        Assert.False(precise.Overlaps(new(double.BitIncrement(next), 2d)));
    }

    [Fact]
    public void UnboundedIntervalsShareArithmeticAndClampBehavior()
    {
        Assert.Equal(float.PositiveInfinity, Interval.Entire<float>().Length);
        Assert.Equal(42f, Interval.Entire<float>().Clamp(42f));
        Assert.Equal(2f, Interval.AtLeast(2f).Clamp(-5));
        Assert.Equal(0f, Interval<float>.Singleton(4).Length);
        Assert.True(float.IsNaN(Interval.Entire<float>().Clamp(float.NaN)));
        Assert.Equal(float.PositiveInfinity, Interval.Entire<float>().Clamp(float.PositiveInfinity));
    }

    [Fact]
    public void RectangleAxesExposeIntervalOperations()
    {
        var rectangle = new Rect2f(new Vector2f(2, -3), new Vector2f(8, 5));

        Assert.Equal(new Interval<float>(2, 8), rectangle.XInterval);
        Assert.Equal(new Interval<float>(-3, 5), rectangle.YInterval);
        Assert.Equal(rectangle.Width, rectangle.XInterval.Length);
        Assert.Equal(rectangle.Height, rectangle.YInterval.Length);
        Assert.Equal(8f, rectangle.XInterval.Clamp(10f));
        Assert.True(rectangle.YInterval.Contains(0));
    }

    [Fact]
    public void ConvertingAStructAndQueryingItDoesNotAllocateAfterWarmup()
    {
        AssertQueriesDoNotAllocate<float>();
        AssertQueriesDoNotAllocate<double>();
        AssertQueriesDoNotAllocate<int>();
        AssertQueriesDoNotAllocate<decimal>();
    }

    private static void AssertQueriesThroughFrom<T>() where T : INumber<T>
    {
        var limits = Interval<T>.From(new EndpointsOnly<T>(T.CreateChecked(2), T.CreateChecked(5)));
        var touching = new Interval<T>(T.CreateChecked(5), T.CreateChecked(8));
        var disjoint = new Interval<T>(T.CreateChecked(6), T.CreateChecked(8));

        Assert.Equal(new Interval<T>(T.CreateChecked(2), T.CreateChecked(5)), limits);
        Assert.Equal(T.CreateChecked(3), limits.Length);
        Assert.True(limits.IsBoundedBelow);
        Assert.True(limits.IsBoundedAbove);
        Assert.False(limits.IsEntire);
        Assert.False(limits.IsSingleton);
        Assert.True(limits.Contains(T.CreateChecked(2)));
        Assert.True(limits.Contains(T.CreateChecked(5)));
        Assert.False(limits.Contains(T.CreateChecked(6)));
        Assert.Equal(T.CreateChecked(2), limits.Clamp(T.Zero));
        Assert.Equal(T.CreateChecked(5), limits.Clamp(T.CreateChecked(10)));
        Assert.True(limits.ContainsInterval(new(T.CreateChecked(3), T.CreateChecked(4))));
        Assert.False(limits.ContainsInterval(touching));
        Assert.True(limits.Overlaps(touching));
        Assert.False(limits.Overlaps(disjoint));
        Assert.True(limits.TryIntersect(touching, out var shared));
        Assert.Equal(Interval<T>.Singleton(T.CreateChecked(5)), shared);
        Assert.False(limits.TryIntersect(disjoint, out var empty));
        Assert.Equal(default, empty);
        Assert.Equal(new Interval<T>(T.CreateChecked(2), T.CreateChecked(8)), limits.Hull(disjoint));
    }

    private static void AssertQueriesDoNotAllocate<T>() where T : INumber<T>
    {
        var limits = new EndpointsOnly<T>(T.CreateChecked(-1), T.CreateChecked(5));
        var other = new Interval<T>(T.One, T.CreateChecked(7));
        RunQueries(limits, other, 256);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var matches = RunQueries(limits, other, 4096);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(4096, matches);
        Assert.Equal(0L, allocated);
    }

    private static int RunQueries<T>(EndpointsOnly<T> endpoints, Interval<T> other, int count) where T : INumber<T>
    {
        var three = T.CreateChecked(3);
        var four = T.CreateChecked(4);
        var six = T.CreateChecked(6);
        var ten = T.CreateChecked(10);
        var two = T.CreateChecked(2);
        var eight = T.CreateChecked(8);
        var eleven = T.CreateChecked(11);
        var matches = 0;
        for (var i = 0; i < count; i++)
        {
            var limits = Interval<T>.From(endpoints);
            if (limits.IsBoundedBelow && limits.IsBoundedAbove && !limits.IsEntire && !limits.IsSingleton &&
                limits.Length == six && limits.Contains(three) &&
                limits.Clamp(ten) == limits.Max && limits.Overlaps(other) &&
                !limits.ContainsInterval(other) && limits.TryIntersect(other, out var shared) &&
                shared.Min == other.Min && shared.Max == limits.Max && shared.Length == four &&
                limits.Hull(other) == new Interval<T>(limits.Min, other.Max) &&
                limits.Distance(other) == T.Zero && limits.MaxDistance(other) == eight &&
                limits.SignedDistance(other) == -four && limits.Lerp(two) == eleven &&
                limits.LerpClamped(two) == limits.Max && limits.InverseLerp(three) == four / six &&
                limits.InverseLerpClamped(ten) == T.One &&
                limits.Translate(three) == new Interval<T>(two, eight) &&
                limits.Reflect() == new Interval<T>(-limits.Max, -limits.Min) &&
                limits.Reflect(three) == other && limits.Scale(two) == new Interval<T>(-two, ten) &&
                limits.Scale(T.Zero) == default && limits.Scale(-T.One, three) == other &&
                limits.Include(ten) == new Interval<T>(limits.Min, ten))
            {
                matches++;
            }
        }
        return matches;
    }

    private readonly record struct EndpointsOnly<T>(T Min, T Max) : IInterval<T> where T : INumber<T>;

    private sealed record ReferenceInterval<T>(T Min, T Max) : IInterval<T> where T : INumber<T>;
}
