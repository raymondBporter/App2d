using App2d.Core.Validation;

namespace App2d.Core.Mathematics.Progress;

/// <summary>Unclamped polynomial and sine easing maps with analytic derivatives.</summary>
public static class Easing
{
    public static readonly ProgressMap Linear = new(x => x, _ => 1d, ProgressMapKind.Linear);
    public static readonly ProgressMap Smooth = Progress.SmoothStepMap;
    public static readonly ProgressMap Smoother = Progress.SmootherStepMap;
    public static readonly ProgressMap Smoothest = Progress.SmoothestStepMap;

    public static readonly ProgressMap EaseInQuad = EaseInPower(2).Named(ProgressMapKind.EaseInQuad);
    public static readonly ProgressMap EaseOutQuad = ProgressMaps.EaseOut(EaseInQuad).Named(ProgressMapKind.EaseOutQuad);
    public static readonly ProgressMap EaseInOutQuad = ProgressMaps.EaseInOut(EaseInQuad).Named(ProgressMapKind.EaseInOutQuad);
    public static readonly ProgressMap EaseInCubic = EaseInPower(3).Named(ProgressMapKind.EaseInCubic);
    public static readonly ProgressMap EaseOutCubic = ProgressMaps.EaseOut(EaseInCubic).Named(ProgressMapKind.EaseOutCubic);
    public static readonly ProgressMap EaseInOutCubic = ProgressMaps.EaseInOut(EaseInCubic).Named(ProgressMapKind.EaseInOutCubic);
    public static readonly ProgressMap EaseInQuart = EaseInPower(4).Named(ProgressMapKind.EaseInQuart);
    public static readonly ProgressMap EaseOutQuart = ProgressMaps.EaseOut(EaseInQuart).Named(ProgressMapKind.EaseOutQuart);
    public static readonly ProgressMap EaseInOutQuart = ProgressMaps.EaseInOut(EaseInQuart).Named(ProgressMapKind.EaseInOutQuart);
    public static readonly ProgressMap EaseInQuint = EaseInPower(5).Named(ProgressMapKind.EaseInQuint);
    public static readonly ProgressMap EaseOutQuint = ProgressMaps.EaseOut(EaseInQuint).Named(ProgressMapKind.EaseOutQuint);
    public static readonly ProgressMap EaseInOutQuint = ProgressMaps.EaseInOut(EaseInQuint).Named(ProgressMapKind.EaseInOutQuint);

    public static readonly ProgressMap EaseInSine = new(
        x => 1d - Math.Cos(x * Math.PI / 2d),
        x => Math.PI / 2d * Math.Sin(x * Math.PI / 2d), ProgressMapKind.EaseInSine);
    public static readonly ProgressMap EaseOutSine = ProgressMaps.EaseOut(EaseInSine).Named(ProgressMapKind.EaseOutSine);
    public static readonly ProgressMap EaseInOutSine = ProgressMaps.EaseInOut(EaseInSine).Named(ProgressMapKind.EaseInOutSine);

    /// <summary>x raised to a positive integer power, paired with n * x^(n-1).</summary>
    public static ProgressMap EaseInPower(int power)
    {
        ArgGuard.Positive(power);
        return power == 1 ? Linear : new(x => Math.Pow(x, power), x => power * Math.Pow(x, power - 1));
    }

    public static ProgressMap EaseOutPower(int power) => power == 1 ? Linear : ProgressMaps.EaseOut(EaseInPower(power));
    public static ProgressMap EaseInOutPower(int power) => power == 1 ? Linear : ProgressMaps.EaseInOut(EaseInPower(power));
}
