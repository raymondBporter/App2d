using App2d.Core.Validation;

namespace App2d.Core.Mathematics.Progress;

/// <summary>Reusable phase maps, easing transformations, and composition using the chain rule.</summary>
public static class ProgressMaps
{
    public static ProgressMap Loop => Easing.Linear;

    /// <summary>Run from the end to the beginning.</summary>
    public static ProgressMap Reverse { get; } = new(phase => 1d - phase, _ => -1d, ProgressMapKind.Reverse, EasingTravel.Backward, true);

    /// <summary>0 to 1 and back to 0 in one cycle.</summary>
    public static ProgressMap PingPong { get; } = new(
        phase => phase <= 0.5d ? 2d * phase : 2d * (1d - phase),
        phase => phase < 0.5d ? 2d : -2d, ProgressMapKind.PingPong, EasingTravel.ForwardAndBackward, true);

    /// <summary>1 to 0 and back to 1. The derivative at the midpoint follows the return leg.</summary>
    public static ProgressMap ReversePingPong { get; } = new(
        phase => phase <= 0.5d ? 1d - 2d * phase : 2d * phase - 1d,
        phase => phase < 0.5d ? -2d : 2d, ProgressMapKind.ReversePingPong, EasingTravel.BackwardAndForward, true);

    /// <summary>Holds zero before phase 1, then jumps to one. The derivative is NaN at that discontinuity.</summary>
    public static ProgressMap Step { get; } = new(
        phase => double.IsNaN(phase) ? double.NaN : phase < 1d ? 0d : 1d,
        phase => double.IsNaN(phase) || phase == 1d ? double.NaN : 0d,
        ProgressMapKind.Step, EasingTravel.Forward, false);

    public static ProgressMap FromKind(ProgressMapKind kind) => kind switch
    {
        ProgressMapKind.Linear => Easing.Linear,
        ProgressMapKind.Reverse => Reverse,
        ProgressMapKind.PingPong => PingPong,
        ProgressMapKind.ReversePingPong => ReversePingPong,
        ProgressMapKind.Step => Step,
        ProgressMapKind.Smooth => Easing.Smooth,
        ProgressMapKind.Smoother => Easing.Smoother,
        ProgressMapKind.Smoothest => Easing.Smoothest,
        ProgressMapKind.EaseInQuad => Easing.EaseInQuad,
        ProgressMapKind.EaseOutQuad => Easing.EaseOutQuad,
        ProgressMapKind.EaseInOutQuad => Easing.EaseInOutQuad,
        ProgressMapKind.EaseInCubic => Easing.EaseInCubic,
        ProgressMapKind.EaseOutCubic => Easing.EaseOutCubic,
        ProgressMapKind.EaseInOutCubic => Easing.EaseInOutCubic,
        ProgressMapKind.EaseInQuart => Easing.EaseInQuart,
        ProgressMapKind.EaseOutQuart => Easing.EaseOutQuart,
        ProgressMapKind.EaseInOutQuart => Easing.EaseInOutQuart,
        ProgressMapKind.EaseInQuint => Easing.EaseInQuint,
        ProgressMapKind.EaseOutQuint => Easing.EaseOutQuint,
        ProgressMapKind.EaseInOutQuint => Easing.EaseInOutQuint,
        ProgressMapKind.EaseInSine => Easing.EaseInSine,
        ProgressMapKind.EaseOutSine => Easing.EaseOutSine,
        ProgressMapKind.EaseInOutSine => Easing.EaseInOutSine,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown progress map.")
    };

    /// <summary>Named maps with the requested travel, optionally restricted to continuous maps.</summary>
    public static IReadOnlyList<ProgressMapKind> ForTravel(EasingTravel travel, bool continuousOnly = false)
    {
        ArgGuard.ThrowIf(!Enum.IsDefined(travel), "Unknown easing travel.", nameof(travel));
        return Array.AsReadOnly(Enum.GetValues<ProgressMapKind>()
            .Where(kind => FromKind(kind).Travel == travel && (!continuousOnly || FromKind(kind).IsContinuous == true)).ToArray());
    }

    /// <summary>Apply named maps left to right, including their analytic derivatives.</summary>
    public static IProgressMap Compose(IEnumerable<ProgressMapKind> kinds)
    {
        ArgGuard.ThrowIfNull(kinds);
        IProgressMap? result = null;
        foreach (var kind in kinds)
            result = result is null ? FromKind(kind) : Compose(result, FromKind(kind));
        return result ?? throw new ArgumentException("At least one progress map is required.", nameof(kinds));
    }

    /// <summary>Reflects an ease-in map in both axes: 1 - easeIn(1 - phase).</summary>
    public static ProgressMap EaseOut(IProgressMap easeIn)
    {
        ArgGuard.ThrowIfNull(easeIn);
        return new(
            phase => 1d - easeIn.Sample(1d - phase),
            phase => easeIn.Derivative(1d - phase),
            travel: easeIn is ProgressMap { Travel: EasingTravel.Forward } ? EasingTravel.Forward : null,
            isContinuous: (easeIn as ProgressMap)?.IsContinuous);
    }

    /// <summary>Fits ease-in into the first half and its reflection into the second half.</summary>
    public static ProgressMap EaseInOut(IProgressMap easeIn)
    {
        ArgGuard.ThrowIfNull(easeIn);
        return new(
            phase => phase < 0.5d ? easeIn.Sample(2d * phase) / 2d : 1d - easeIn.Sample(2d * (1d - phase)) / 2d,
            phase => easeIn.Derivative(phase < 0.5d ? 2d * phase : 2d * (1d - phase)),
            travel: easeIn is ProgressMap { Travel: EasingTravel.Forward } ? EasingTravel.Forward : null,
            isContinuous: easeIn is ProgressMap { Travel: EasingTravel.Forward } forward ? forward.IsContinuous : null);
    }

    /// <summary>Apply <paramref name="first"/> and then <paramref name="second"/>.</summary>
    public static ProgressMap Compose(IProgressMap first, IProgressMap second)
    {
        ArgGuard.ThrowIfNull(first);
        ArgGuard.ThrowIfNull(second);
        return new ProgressMap(
            phase => second.Sample(first.Sample(phase)),
            phase => second.Derivative(first.Sample(phase)) * first.Derivative(phase),
            isContinuous: first is ProgressMap { IsContinuous: true, Travel: not null } && second is ProgressMap { IsContinuous: true } ? true : null);
    }
}
