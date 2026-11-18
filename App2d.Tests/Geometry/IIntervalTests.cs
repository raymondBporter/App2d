using App2d.Core.Characters;
using App2d.Core.Geometry.Interval;
using App2d.Core.Geometry.Primitives;
using System.Numerics;
using System.Reflection;

namespace App2d.Tests.Geometry;

public sealed class IIntervalTests
{
    [Fact]
    public void EndpointsAloneSupplyAllSharedQueries()
    {
        AssertSharedQueries<float>();
        AssertSharedQueries<double>();
        AssertSharedQueries<int>();
        AssertSharedQueries<decimal>();

        var reference = new ReferenceInterval<double>(2, 5);
        Assert.Equal(3d, IInterval<double>.Length(reference));
        Assert.True(reference.Contains(3d));
        Assert.True(reference.TryIntersect(new Interval<double>(4, 8), out var shared));
        Assert.Equal(new Interval<double>(4, 5), shared);
    }

    [Fact]
    public void SharedBoundsQueriesHandleUnboundedAndSingletonImplementations()
    {
        var entire = new EndpointsOnly<double>(double.NegativeInfinity, double.PositiveInfinity);
        Assert.True(IInterval<double>.IsEntire(entire));
        Assert.False(IInterval<double>.IsBoundedBelow(entire));
        Assert.False(IInterval<double>.IsBoundedAbove(entire));
        Assert.False(IInterval<double>.IsSingleton(entire));
        Assert.Equal(double.PositiveInfinity, IInterval<double>.Length(entire));
        Assert.True(double.IsNaN(entire.Clamp(double.NaN)));
        Assert.False(entire.Contains(double.NaN));

        var lower = new EndpointsOnly<double>(2, double.PositiveInfinity);
        Assert.True(IInterval<double>.IsBoundedBelow(lower));
        Assert.False(IInterval<double>.IsBoundedAbove(lower));
        Assert.False(IInterval<double>.IsEntire(lower));
        Assert.Equal(2d, lower.Clamp(-1d));

        var singleton = default(EndpointsOnly<int>);
        Assert.True(IInterval<int>.IsSingleton(singleton));
        Assert.Equal(0, IInterval<int>.Length(singleton));
        Assert.Equal(0, singleton.Clamp(42));
    }

    [Fact]
    public void InterfaceQueriesAreOrdinaryStaticMethods()
    {
        var queries = typeof(IInterval<float>).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        Assert.NotEmpty(queries);
        Assert.All(queries, query =>
        {
            Assert.False(query.IsAbstract);
            Assert.False(query.IsVirtual);
        });

        var interval = new EndpointsOnly<float>(2, 5);
        Assert.True(IInterval<float>.Contains(interval, 3f));
        Assert.Equal(3f, IInterval<float>.Length(interval));
        Assert.False(IInterval<float>.Contains(interval, 6f));
    }

    [Fact]
    public void SharedQueriesRetainDecimalAndBigIntegerPrecision()
    {
        var precise = new EndpointsOnly<decimal>(1m, 1.0000000000000000000000000001m);
        Assert.Equal(0.0000000000000000000000000001m, IInterval<decimal>.Length(precise));
        Assert.Equal(precise.Max, precise.Clamp(2m));

        var start = BigInteger.One << 200;
        var wide = new EndpointsOnly<BigInteger>(start, start + 5);
        Assert.Equal(new BigInteger(5), IInterval<BigInteger>.Length(wide));
        Assert.True(wide.Contains(start + 1));
        Assert.Equal(start + 5, wide.Clamp(start + 100));
        Assert.True(wide.TryIntersect(new Interval<BigInteger>(start + 3, start + 8), out var shared));
        Assert.Equal(new Interval<BigInteger>(start + 3, start + 5), shared);
    }

    [Fact]
    public void EndpointsOnlySharedQueriesDoNotAllocateAfterWarmup()
    {
        AssertSharedQueriesDoNotAllocate<float>();
        AssertSharedQueriesDoNotAllocate<double>();
        AssertSharedQueriesDoNotAllocate<int>();
        AssertSharedQueriesDoNotAllocate<decimal>();
    }

    [Fact]
    public void AuthoredAndPhysicsLimitsShareClosedRangeQueries()
    {
        var authored = new Limit(-1, 5).Soft(0, 1);
        var physics = new Interval<float>(1, 7);

        Assert.Equal(6f, IInterval<float>.Length(authored));
        Assert.True(authored.Contains(-1f));
        Assert.True(authored.Contains(5f));
        Assert.False(authored.Contains(5.01f));
        Assert.False(authored.Contains(float.NaN));
        Assert.True(physics.Contains(7f));
        Assert.True(IInterval<float>.Overlaps(authored, physics));
        Assert.True(IInterval<float>.ContainsInterval(Interval.Entire<float>(), authored));
        Assert.False(IInterval<float>.ContainsInterval(authored, physics));
        Assert.True(IInterval<float>.TryIntersect(authored, physics, out var shared));
        Assert.Equal(new Interval<float>(1, 5), shared);
        Assert.Equal(new Interval<float>(-1, 7), IInterval<float>.Hull(authored, physics));

        Assert.Equal(0f, authored.SoftMin);
        Assert.Equal(1f, authored.SoftMax);
    }

    [Fact]
    public void ExtensionQueriesDistinguishTouchingFromDisjointIntervals()
    {
        var limits = new Interval<float>(2, 5);

        Assert.True(limits.ContainsInterval(new Interval<float>(3, 4)));
        Assert.True(limits.Overlaps(new Interval<float>(5, 8)));
        Assert.True(limits.TryIntersect(new Interval<float>(5, 8), out var touching));
        Assert.Equal(new Interval<float>(5, 5), touching);
        Assert.False(limits.TryIntersect(new Interval<float>(6, 8), out var disjoint));
        Assert.Equal(default, disjoint);
        Assert.Equal(new Interval<float>(2, 8), limits.Hull(new Interval<float>(6, 8)));
    }

    [Fact]
    public void SharedArithmeticRetainsIntegerAndDoubleEndpointTypes()
    {
        var cells = new Interval<int>(2, 5);
        Assert.Equal(3, cells.Length);
        Assert.Equal(5, cells.Clamp(10));

        var next = double.BitIncrement(1d);
        var precise = new Interval<double>(1d, next);
        Assert.Equal(next - 1d, precise.Length);
        Assert.True(precise.Length > 0d);
        Assert.Equal(next, precise.Clamp(2d));
        Assert.True(precise.TryIntersect(new Interval<double>(next, 2d), out var shared));
        Assert.Equal(new Interval<double>(next, next), shared);
        Assert.False(precise.Overlaps(new Interval<double>(double.BitIncrement(next), 2d)));
    }

    [Fact]
    public void UnboundedIntervalsShareArithmeticAndClampBehavior()
    {
        Assert.Equal(float.PositiveInfinity, IInterval<float>.Length(Interval.Entire<float>()));
        Assert.Equal(42f, Interval.Entire<float>().Clamp(42f));
        Assert.Equal(2f, Interval.AtLeast(2f).Clamp(-5));
        Assert.Equal(0f, IInterval<float>.Length(Interval<float>.Singleton(4)));
        Assert.True(float.IsNaN(Interval.Entire<float>().Clamp(float.NaN)));
        Assert.Equal(float.PositiveInfinity, Interval.Entire<float>().Clamp(float.PositiveInfinity));
        Assert.True(float.IsNaN(IInterval<float>.Clamp(new Limit(0, 1), float.NaN)));
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
    public void MixedStructQueriesDoNotAllocateAfterWarmup()
    {
        var authored = new Limit(-1, 5);
        var physics = new Interval<float>(1, 7);
        RunQueries(authored, physics, 256);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var matches = RunQueries(authored, physics, 4096);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(4096, matches);
        Assert.Equal(0L, allocated);
    }

    private static int RunQueries(Limit authored, Interval<float> physics, int count)
    {
        var matches = 0;
        for (var i = 0; i < count; i++)
        {
            if (authored.Contains(3f) && IInterval<float>.Overlaps(authored, physics) &&
                IInterval<float>.TryIntersect(authored, physics, out var shared) && shared.Length == 4f)
            {
                matches++;
            }
        }
        return matches;
    }

    private static void AssertSharedQueries<T>() where T : INumber<T>
    {
        var limits = new EndpointsOnly<T>(T.CreateChecked(2), T.CreateChecked(5));
        var touching = new Interval<T>(T.CreateChecked(5), T.CreateChecked(8));
        var disjoint = new Interval<T>(T.CreateChecked(6), T.CreateChecked(8));

        Assert.Equal(T.CreateChecked(3), IInterval<T>.Length(limits));
        Assert.True(IInterval<T>.IsBoundedBelow(limits));
        Assert.True(IInterval<T>.IsBoundedAbove(limits));
        Assert.False(IInterval<T>.IsEntire(limits));
        Assert.False(IInterval<T>.IsSingleton(limits));
        Assert.True(limits.Contains(T.CreateChecked(2)));
        Assert.True(limits.Contains(T.CreateChecked(5)));
        Assert.False(limits.Contains(T.CreateChecked(6)));
        Assert.Equal(T.CreateChecked(2), limits.Clamp(T.Zero));
        Assert.Equal(T.CreateChecked(5), limits.Clamp(T.CreateChecked(10)));
        Assert.True(limits.ContainsInterval(new Interval<T>(T.CreateChecked(3), T.CreateChecked(4))));
        Assert.False(limits.ContainsInterval(touching));
        Assert.True(limits.Overlaps(touching));
        Assert.False(limits.Overlaps(disjoint));
        Assert.True(limits.TryIntersect(touching, out var shared));
        Assert.Equal(Interval<T>.Singleton(T.CreateChecked(5)), shared);
        Assert.False(limits.TryIntersect(disjoint, out var empty));
        Assert.Equal(default, empty);
        Assert.Equal(new Interval<T>(T.CreateChecked(2), T.CreateChecked(8)), limits.Hull(disjoint));
    }

    private static void AssertSharedQueriesDoNotAllocate<T>() where T : INumber<T>
    {
        var limits = new EndpointsOnly<T>(T.CreateChecked(-1), T.CreateChecked(5));
        var other = new Interval<T>(T.One, T.CreateChecked(7));
        RunSharedQueries(limits, other, 256);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var matches = RunSharedQueries(limits, other, 4096);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(4096, matches);
        Assert.Equal(0L, allocated);
    }

    private static int RunSharedQueries<T>(EndpointsOnly<T> limits, Interval<T> other, int count)
        where T : INumber<T>
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
            if (IInterval<T>.IsBoundedBelow(limits) && IInterval<T>.IsBoundedAbove(limits) &&
                !IInterval<T>.IsEntire(limits) && !IInterval<T>.IsSingleton(limits) &&
                IInterval<T>.Length(limits) == six && limits.Contains(three) &&
                limits.Clamp(ten) == limits.Max && limits.Overlaps(other) &&
                !limits.ContainsInterval(other) && limits.TryIntersect(other, out var shared) &&
                shared.Min == other.Min && shared.Max == limits.Max && shared.Length == four &&
                limits.Hull(other) == new Interval<T>(limits.Min, other.Max) &&
                limits.Distance(other) == T.Zero && limits.MaxDistance(other) == eight &&
                limits.SignedDistance(other) == -four && limits.Lerp(two) == eleven &&
                limits.LerpClamped(two) == limits.Max && limits.InverseLerp(three) == four / six &&
                limits.InverseLerpClamped(ten) == T.One &&
                limits.Translate(three) == new Interval<T>(two, eight) &&
                IInterval<T>.Reflect(limits) == new Interval<T>(-limits.Max, -limits.Min) &&
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
