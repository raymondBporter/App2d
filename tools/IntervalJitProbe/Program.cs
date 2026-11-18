using App2d.Core.Characters;
using App2d.Core.Geometry.Primitives;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace IntervalJitProbe;

// Intentionally implements no queries: only the two endpoint properties.
public readonly record struct EndpointsOnly<T>(T Min, T Max) : IInterval<T> where T : INumber<T>;

// Keep each entry point visible to the JIT disassembler; everything inside it may inline normally.
public static class Probes
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static float FloatLength(EndpointsOnly<float> interval) => IInterval<float>.Length(interval);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static double DoubleLength(EndpointsOnly<double> interval) => IInterval<double>.Length(interval);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int IntLength(EndpointsOnly<int> interval) => IInterval<int>.Length(interval);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static decimal DecimalLength(EndpointsOnly<decimal> interval) => IInterval<decimal>.Length(interval);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool FloatContains(EndpointsOnly<float> interval, float value) => interval.Contains(value);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool DoubleContains(EndpointsOnly<double> interval, double value) => interval.Contains(value);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool IntContains(EndpointsOnly<int> interval, int value) => interval.Contains(value);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool FloatContainsDirect(EndpointsOnly<float> interval, float value) =>
        IInterval<float>.Contains(interval, value);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static float FloatClamp(EndpointsOnly<float> interval, float value) => interval.Clamp(value);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static double DoubleClamp(EndpointsOnly<double> interval, double value) => interval.Clamp(value);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int IntClamp(EndpointsOnly<int> interval, int value) => interval.Clamp(value);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool FloatOverlaps(EndpointsOnly<float> first, Limit second) => IInterval<float>.Overlaps(first, second);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool DoubleOverlaps(EndpointsOnly<double> first, Interval<double> second) => first.Overlaps(second);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool FloatContainsInterval(EndpointsOnly<float> outer, Interval<float> inner) => outer.ContainsInterval(inner);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static float FloatDistance(EndpointsOnly<float> first, Interval<float> second) => first.Distance(second);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static double DoubleDistance(EndpointsOnly<double> first, Interval<double> second) => first.Distance(second);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static float FloatMaxDistance(EndpointsOnly<float> first, Interval<float> second) => first.MaxDistance(second);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static float FloatSignedDistance(EndpointsOnly<float> first, Interval<float> second) => first.SignedDistance(second);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool FloatIntersect(EndpointsOnly<float> first, Interval<float> second, out Interval<float> result) =>
        first.TryIntersect(second, out result);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool DoubleIntersect(EndpointsOnly<double> first, Interval<double> second, out Interval<double> result) =>
        first.TryIntersect(second, out result);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Interval<float> FloatHull(EndpointsOnly<float> first, Interval<float> second) => first.Hull(second);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Interval<double> DoubleHull(EndpointsOnly<double> first, Interval<double> second) => first.Hull(second);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int FloatBounds(EndpointsOnly<float> interval) =>
        (IInterval<float>.IsBoundedBelow(interval) ? 1 : 0) |
        (IInterval<float>.IsBoundedAbove(interval) ? 2 : 0) |
        (IInterval<float>.IsEntire(interval) ? 4 : 0) |
        (IInterval<float>.IsSingleton(interval) ? 8 : 0);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool ConcreteContains(Interval<float> interval, float value) => interval.Contains(value);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static double ConcreteLength(Interval<double> interval) => interval.Length;
}

internal static class Program
{
    public static void Main()
    {
        var singles = new EndpointsOnly<float>(-1, 5);
        var doubles = new EndpointsOnly<double>(-1, 5);
        var integers = new EndpointsOnly<int>(-1, 5);
        var otherSingle = new Interval<float>(1, 7);
        var otherDouble = new Interval<double>(1, 7);

        Check(Probes.FloatLength(singles) == 6f, nameof(Probes.FloatLength));
        Check(Probes.DoubleLength(doubles) == 6d, nameof(Probes.DoubleLength));
        Check(Probes.IntLength(integers) == 6, nameof(Probes.IntLength));
        Check(Probes.DecimalLength(new(-1m, 5m)) == 6m, nameof(Probes.DecimalLength));
        Check(Probes.FloatContains(singles, 3f), nameof(Probes.FloatContains));
        Check(Probes.DoubleContains(doubles, 3d), nameof(Probes.DoubleContains));
        Check(Probes.IntContains(integers, 3), nameof(Probes.IntContains));
        Check(Probes.FloatContainsDirect(singles, 3f), nameof(Probes.FloatContainsDirect));
        Check(Probes.FloatClamp(singles, 10f) == 5f, nameof(Probes.FloatClamp));
        Check(Probes.DoubleClamp(doubles, 10d) == 5d, nameof(Probes.DoubleClamp));
        Check(Probes.IntClamp(integers, 10) == 5, nameof(Probes.IntClamp));
        Check(Probes.FloatOverlaps(singles, new Limit(1, 7)), nameof(Probes.FloatOverlaps));
        Check(Probes.DoubleOverlaps(doubles, otherDouble), nameof(Probes.DoubleOverlaps));
        Check(Probes.FloatContainsInterval(singles, new(2, 4)), nameof(Probes.FloatContainsInterval));
        Check(Probes.FloatDistance(singles, new(8, 10)) == 3f, nameof(Probes.FloatDistance));
        Check(Probes.DoubleDistance(doubles, otherDouble) == 0d, nameof(Probes.DoubleDistance));
        Check(Probes.FloatMaxDistance(singles, otherSingle) == 8f, nameof(Probes.FloatMaxDistance));
        Check(Probes.FloatSignedDistance(singles, otherSingle) == -4f, nameof(Probes.FloatSignedDistance));
        Check(Probes.FloatIntersect(singles, otherSingle, out var sharedSingle) && sharedSingle == new Interval<float>(1, 5), nameof(Probes.FloatIntersect));
        Check(Probes.DoubleIntersect(doubles, otherDouble, out var sharedDouble) && sharedDouble == new Interval<double>(1, 5), nameof(Probes.DoubleIntersect));
        Check(Probes.FloatHull(singles, otherSingle) == new Interval<float>(-1, 7), nameof(Probes.FloatHull));
        Check(Probes.DoubleHull(doubles, otherDouble) == new Interval<double>(-1, 7), nameof(Probes.DoubleHull));
        Check(Probes.FloatBounds(singles) == 3, nameof(Probes.FloatBounds));
        Check(Probes.FloatBounds(new(float.NegativeInfinity, float.PositiveInfinity)) == 4, nameof(Probes.FloatBounds));
        Check(Probes.FloatBounds(new(2, 2)) == 11, nameof(Probes.FloatBounds));
        Check(Probes.ConcreteContains(otherSingle, 3f), nameof(Probes.ConcreteContains));
        Check(Probes.ConcreteLength(otherDouble) == 6d, nameof(Probes.ConcreteLength));
        Console.WriteLine($"Interval JIT probe passed: {RuntimeInformation.FrameworkDescription}, {RuntimeInformation.ProcessArchitecture}.");
    }

    private static void Check(bool condition, string operation)
    {
        if (!condition) throw new InvalidOperationException($"Unexpected result for {operation}.");
    }
}
