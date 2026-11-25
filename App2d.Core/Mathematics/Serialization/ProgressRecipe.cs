using App2d.Core.Mathematics.Progress;
using System.Numerics;

namespace App2d.Core.Mathematics.Serialization;

internal enum ProgressOperation
{
    Linear, Reverse, PingPong, ReversePingPong, Sine, Step, SmoothStep, Power,
    Constant, Affine, Normalize, FromRange, ToRange, Then, Direction, Bezier, TimedBezier, Gaussian
}

// Only factories create these immutable argument lists. They describe construction, not sampled output.
internal sealed record ProgressRecipe<TReal>(ProgressOperation Kind, TReal[] Parameters,
    int Integer = 0, ProgressMap<TReal>? Source = null, ProgressMap<TReal>? Next = null,
    EasingDirection Direction = EasingDirection.In) where TReal : IFloatingPointIeee754<TReal>;
