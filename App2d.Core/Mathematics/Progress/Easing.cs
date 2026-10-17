using App2d.Core.Validation;

namespace App2d.Core.Mathematics.Progress;

/// <summary>
/// Easing maps with analytic derivatives. Inputs are not clamped; back and elastic maps intentionally overshoot.
/// Circular tangents have infinite slopes. Expo/elastic endpoint jumps have NaN derivatives;
/// bounce corners use the slope of the selected branch.
/// </summary>
public static class Easing
{
    private const double BackOvershoot = 1.70158d;
    private const double BounceScale = 7.5625d;
    private const double BounceDivisor = 2.75d;
    private static readonly double Log2 = Math.Log(2d);

    public static readonly ProgressMap Linear = new(x => x, _ => 1d);
    public static readonly ProgressMap Smooth = Progress.SmoothStepMap;
    public static readonly ProgressMap Smoother = Progress.SmootherStepMap;
    public static readonly ProgressMap Smoothest = Progress.SmoothestStepMap;

    public static readonly ProgressMap EaseInQuad = EaseInPower(2);
    public static readonly ProgressMap EaseOutQuad = ProgressMaps.EaseOut(EaseInQuad);
    public static readonly ProgressMap EaseInOutQuad = ProgressMaps.EaseInOut(EaseInQuad);
    public static readonly ProgressMap EaseInCubic = EaseInPower(3);
    public static readonly ProgressMap EaseOutCubic = ProgressMaps.EaseOut(EaseInCubic);
    public static readonly ProgressMap EaseInOutCubic = ProgressMaps.EaseInOut(EaseInCubic);
    public static readonly ProgressMap EaseInQuart = EaseInPower(4);
    public static readonly ProgressMap EaseOutQuart = ProgressMaps.EaseOut(EaseInQuart);
    public static readonly ProgressMap EaseInOutQuart = ProgressMaps.EaseInOut(EaseInQuart);
    public static readonly ProgressMap EaseInQuint = EaseInPower(5);
    public static readonly ProgressMap EaseOutQuint = ProgressMaps.EaseOut(EaseInQuint);
    public static readonly ProgressMap EaseInOutQuint = ProgressMaps.EaseInOut(EaseInQuint);

    public static readonly ProgressMap EaseInSine = new(
        x => 1d - Math.Cos(x * Math.PI / 2d),
        x => Math.PI / 2d * Math.Sin(x * Math.PI / 2d));
    public static readonly ProgressMap EaseOutSine = ProgressMaps.EaseOut(EaseInSine);
    public static readonly ProgressMap EaseInOutSine = ProgressMaps.EaseInOut(EaseInSine);

    public static readonly ProgressMap EaseInExpo = new(
        x => x == 0d ? 0d : Math.Pow(2d, 10d * x - 10d),
        x => x == 0d ? double.NaN : 10d * Log2 * Math.Pow(2d, 10d * x - 10d));
    public static readonly ProgressMap EaseOutExpo = ProgressMaps.EaseOut(EaseInExpo);
    public static readonly ProgressMap EaseInOutExpo = ProgressMaps.EaseInOut(EaseInExpo);

    public static readonly ProgressMap EaseInCirc = new(
        x => 1d - Math.Sqrt(1d - x * x),
        x => x / Math.Sqrt(1d - x * x));
    public static readonly ProgressMap EaseOutCirc = ProgressMaps.EaseOut(EaseInCirc);
    public static readonly ProgressMap EaseInOutCirc = ProgressMaps.EaseInOut(EaseInCirc);

    public static readonly ProgressMap EaseInBack = BackIn(BackOvershoot);
    public static readonly ProgressMap EaseOutBack = ProgressMaps.EaseOut(EaseInBack);
    public static readonly ProgressMap EaseInOutBack = ProgressMaps.EaseInOut(BackIn(BackOvershoot * 1.525d));

    public static readonly ProgressMap EaseInElastic = ElasticIn(2d * Math.PI / 3d);
    public static readonly ProgressMap EaseOutElastic = ProgressMaps.EaseOut(EaseInElastic);
    public static readonly ProgressMap EaseInOutElastic = ProgressMaps.EaseInOut(ElasticIn(2d * Math.PI / 4.5d));

    public static readonly ProgressMap EaseOutBounce = new(BounceOut, BounceOutDerivative);
    public static readonly ProgressMap EaseInBounce = ProgressMaps.EaseOut(EaseOutBounce);
    public static readonly ProgressMap EaseInOutBounce = ProgressMaps.EaseInOut(EaseInBounce);

    /// <summary>x raised to a positive integer power, paired with n * x^(n-1). Power 1 is linear.</summary>
    public static ProgressMap EaseInPower(int power)
    {
        ArgGuard.Positive(power);
        return power == 1 ? Linear : new(
            x => Math.Pow(x, power),
            x => power * Math.Pow(x, power - 1));
    }

    public static ProgressMap EaseOutPower(int power) => power == 1 ? Linear : ProgressMaps.EaseOut(EaseInPower(power));
    public static ProgressMap EaseInOutPower(int power) => power == 1 ? Linear : ProgressMaps.EaseInOut(EaseInPower(power));

    private static ProgressMap BackIn(double overshoot) => new(
        x => (overshoot + 1d) * x * x * x - overshoot * x * x,
        x => 3d * (overshoot + 1d) * x * x - 2d * overshoot * x);

    private static ProgressMap ElasticIn(double frequency) => new(
        x => x is 0d or 1d ? x : Math.Pow(2d, 10d * x - 10d) * Math.Cos(10d * (x - 1d) * frequency),
        x => x == 0d ? double.NaN : 10d * Math.Pow(2d, 10d * x - 10d) *
            (Log2 * Math.Cos(10d * (x - 1d) * frequency) - frequency * Math.Sin(10d * (x - 1d) * frequency)));

    private static (double Offset, double Height) BounceSegment(double x) => x < 1d / BounceDivisor ? (0d, 0d)
        : x < 2d / BounceDivisor ? (1.5d / BounceDivisor, 0.75d)
        : x < 2.5d / BounceDivisor ? (2.25d / BounceDivisor, 0.9375d)
        : (2.625d / BounceDivisor, 0.984375d);

    private static double BounceOut(double x)
    {
        var (offset, height) = BounceSegment(x);
        x -= offset;
        return BounceScale * x * x + height;
    }

    private static double BounceOutDerivative(double x) => 2d * BounceScale * (x - BounceSegment(x).Offset);
}
