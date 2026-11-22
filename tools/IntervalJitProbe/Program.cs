using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace IntervalJitProbe;

// Only the two endpoint properties. It reaches the queries by converting with Interval<T>.From.
public readonly record struct EndpointsOnly<T>(T Min, T Max) : IInterval<T> where T : INumber<T>;

// Keep each entry point visible to the JIT disassembler; everything inside it may inline normally.
public static class Probes
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static float FloatLength(Interval<float> interval) => interval.Length;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static double DoubleLength(Interval<double> interval) => interval.Length;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int IntLength(Interval<int> interval) => interval.Length;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static decimal DecimalLength(Interval<decimal> interval) => interval.Length;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool FloatContains(Interval<float> interval, float value) => interval.Contains(value);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool DoubleContains(Interval<double> interval, double value) => interval.Contains(value);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool IntContains(Interval<int> interval, int value) => interval.Contains(value);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool FloatContainsFromEndpoints(EndpointsOnly<float> endpoints, float value) =>
        Interval<float>.From(endpoints).Contains(value);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static float FloatClamp(Interval<float> interval, float value) => interval.Clamp(value);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static double DoubleClamp(Interval<double> interval, double value) => interval.Clamp(value);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int IntClamp(Interval<int> interval, int value) => interval.Clamp(value);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool FloatOverlapsFromLimit(EndpointsOnly<float> limits, Interval<float> other) =>
        Interval<float>.From(limits).Overlaps(other);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool DoubleOverlaps(Interval<double> first, Interval<double> second) => first.Overlaps(second);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool FloatContainsInterval(Interval<float> outer, Interval<float> inner) => outer.ContainsInterval(inner);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static float FloatDistance(Interval<float> first, Interval<float> second) => first.Distance(second);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static double DoubleDistance(Interval<double> first, Interval<double> second) => first.Distance(second);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static float FloatMaxDistance(Interval<float> first, Interval<float> second) => first.MaxDistance(second);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static float FloatSignedDistance(Interval<float> first, Interval<float> second) => first.SignedDistance(second);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool FloatIntersect(Interval<float> first, Interval<float> second, out Interval<float> result) =>
        first.TryIntersect(second, out result);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool DoubleIntersect(Interval<double> first, Interval<double> second, out Interval<double> result) =>
        first.TryIntersect(second, out result);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Interval<float> FloatHull(Interval<float> first, Interval<float> second) => first.Hull(second);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Interval<double> DoubleHull(Interval<double> first, Interval<double> second) => first.Hull(second);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int FloatBounds(Interval<float> interval) =>
        (interval.IsBoundedBelow ? 1 : 0) |
        (interval.IsBoundedAbove ? 2 : 0) |
        (interval.IsEntire ? 4 : 0) |
        (interval.IsSingleton ? 8 : 0);
}

internal static class Program
{
    public static void Main()
    {
        var singles = Interval<float>.Builder.Create(-1, 5);
        var doubles = Interval<double>.Builder.Create(-1, 5);
        var integers = Interval<int>.Builder.Create(-1, 5);
        var otherSingle = Interval<float>.Builder.Create(1, 7);
        var otherDouble = Interval<double>.Builder.Create(1, 7);

        Check(Probes.FloatLength(singles) == 6f, nameof(Probes.FloatLength));
        Check(Probes.DoubleLength(doubles) == 6d, nameof(Probes.DoubleLength));
        Check(Probes.IntLength(integers) == 6, nameof(Probes.IntLength));
        Check(Probes.DecimalLength(Interval<decimal>.Builder.Create(-1m, 5m)) == 6m, nameof(Probes.DecimalLength));
        Check(Probes.FloatContains(singles, 3f), nameof(Probes.FloatContains));
        Check(Probes.DoubleContains(doubles, 3d), nameof(Probes.DoubleContains));
        Check(Probes.IntContains(integers, 3), nameof(Probes.IntContains));
        Check(Probes.FloatContainsFromEndpoints(new(-1, 5), 3f), nameof(Probes.FloatContainsFromEndpoints));
        Check(Probes.FloatClamp(singles, 10f) == 5f, nameof(Probes.FloatClamp));
        Check(Probes.DoubleClamp(doubles, 10d) == 5d, nameof(Probes.DoubleClamp));
        Check(Probes.IntClamp(integers, 10) == 5, nameof(Probes.IntClamp));
        Check(Probes.FloatOverlapsFromLimit(new(-1, 5), otherSingle), nameof(Probes.FloatOverlapsFromLimit));
        Check(Probes.DoubleOverlaps(doubles, otherDouble), nameof(Probes.DoubleOverlaps));
        Check(Probes.FloatContainsInterval(singles, Interval<float>.Builder.Create(2, 4)), nameof(Probes.FloatContainsInterval));
        Check(Probes.FloatDistance(singles, Interval<float>.Builder.Create(8, 10)) == 3f, nameof(Probes.FloatDistance));
        Check(Probes.DoubleDistance(doubles, otherDouble) == 0d, nameof(Probes.DoubleDistance));
        Check(Probes.FloatMaxDistance(singles, otherSingle) == 8f, nameof(Probes.FloatMaxDistance));
        Check(Probes.FloatSignedDistance(singles, otherSingle) == -4f, nameof(Probes.FloatSignedDistance));
        Check(Probes.FloatIntersect(singles, otherSingle, out var sharedSingle) && sharedSingle == Interval<float>.Builder.Create(1, 5), nameof(Probes.FloatIntersect));
        Check(Probes.DoubleIntersect(doubles, otherDouble, out var sharedDouble) && sharedDouble == Interval<double>.Builder.Create(1, 5), nameof(Probes.DoubleIntersect));
        Check(Probes.FloatHull(singles, otherSingle) == Interval<float>.Builder.Create(-1, 7), nameof(Probes.FloatHull));
        Check(Probes.DoubleHull(doubles, otherDouble) == Interval<double>.Builder.Create(-1, 7), nameof(Probes.DoubleHull));
        Check(Probes.FloatBounds(singles) == 3, nameof(Probes.FloatBounds));
        Check(Probes.FloatBounds(Interval.Entire<float>()) == 4, nameof(Probes.FloatBounds));
        Check(Probes.FloatBounds(Interval<float>.Singleton(2)) == 11, nameof(Probes.FloatBounds));
        Console.WriteLine($"Interval JIT probe passed: {RuntimeInformation.FrameworkDescription}, {RuntimeInformation.ProcessArchitecture}.");
    }

    private static void Check(bool condition, string operation)
    {
        if (!condition) throw new InvalidOperationException($"Unexpected result for {operation}.");
    }
}
