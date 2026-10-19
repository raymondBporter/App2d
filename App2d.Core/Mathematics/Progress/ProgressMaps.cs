using App2d.Core.Validation;

namespace App2d.Core.Mathematics.Progress;

/// <summary>Reusable phase maps, easing transformations, and composition using the chain rule.</summary>
public static class ProgressMaps
{
    public static ProgressMap Loop => Easing.Linear;

    /// <summary>Run from the end to the beginning.</summary>
    public static ProgressMap Reverse { get; } = new(phase => 1d - phase, _ => -1d, ProgressMapKind.Reverse);

    /// <summary>0 to 1 and back to 0 in one cycle.</summary>
    public static ProgressMap PingPong { get; } = new(
        phase => phase <= 0.5d ? 2d * phase : 2d * (1d - phase),
        phase => phase < 0.5d ? 2d : -2d, ProgressMapKind.PingPong);

    public static ProgressMap FromKind(ProgressMapKind kind) => kind switch
    {
        ProgressMapKind.Linear => Easing.Linear,
        ProgressMapKind.Reverse => Reverse,
        ProgressMapKind.PingPong => PingPong,
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
            phase => easeIn.Derivative(1d - phase));
    }

    /// <summary>Fits ease-in into the first half and its reflection into the second half.</summary>
    public static ProgressMap EaseInOut(IProgressMap easeIn)
    {
        ArgGuard.ThrowIfNull(easeIn);
        return new(
            phase => phase < 0.5d ? easeIn.Sample(2d * phase) / 2d : 1d - easeIn.Sample(2d * (1d - phase)) / 2d,
            phase => easeIn.Derivative(phase < 0.5d ? 2d * phase : 2d * (1d - phase)));
    }

    /// <summary>Apply <paramref name="first"/> and then <paramref name="second"/>.</summary>
    public static ProgressMap Compose(IProgressMap first, IProgressMap second)
    {
        ArgGuard.ThrowIfNull(first);
        ArgGuard.ThrowIfNull(second);
        return new ProgressMap(
            phase => second.Sample(first.Sample(phase)),
            phase => second.Derivative(first.Sample(phase)) * first.Derivative(phase));
    }
}
