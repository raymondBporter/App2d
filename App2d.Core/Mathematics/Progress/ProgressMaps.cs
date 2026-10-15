using App2d.Core.Validation;

namespace App2d.Core.Mathematics.Progress;

/// <summary>Reusable phase maps, easing transformations, and composition using the chain rule.</summary>
public static class ProgressMaps
{
    public static ProgressMap Loop => Easing.Linear;

    /// <summary>0 to 1 and back to 0 in one cycle.</summary>
    public static ProgressMap PingPong { get; } = new(
        phase => phase <= 0.5d ? 2d * phase : 2d * (1d - phase),
        phase => phase < 0.5d ? 2d : -2d);

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
