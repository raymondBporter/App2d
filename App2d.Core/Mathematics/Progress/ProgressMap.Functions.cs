using System.Numerics;

namespace App2d.Core.Mathematics.Progress;

public sealed partial class ProgressMap<TReal> where TReal : IFloatingPointIeee754<TReal>
{
    // The bound methods share a single object for the parameters of each function.
    internal sealed class ConstantFunction(TReal value)
    {
        internal TReal Value { get; } = value;
        internal TReal Evaluate(TReal _) => Value;
    }

    internal sealed class AffineFunction(TReal scale, TReal offset)
    {
        internal TReal Scale { get; } = scale;
        internal TReal Offset { get; } = offset;

        internal TReal Evaluate(TReal x) => Scale * x + Offset;
        internal TReal Derivative(TReal _) => Scale;
        internal TReal Invert(TReal y) => (y - Offset) / Scale;
    }

    internal sealed class NormalizeFunction(TReal begin, TReal end)
    {
        private readonly TReal _slope = DivideByRange(begin, end, TReal.One);
        internal TReal Begin { get; } = begin;
        internal TReal End { get; } = end;

        internal TReal Evaluate(TReal x) => Interval.InverseLerp(Begin, End, x);
        internal TReal Derivative(TReal _) => _slope;
        internal TReal Invert(TReal y) => Interval.Lerp(Begin, End, y);
    }

    internal sealed class InputRangeFunction(ProgressMap<TReal> source, TReal begin, TReal end)
    {
        internal ProgressMap<TReal> Source { get; } = source;
        internal TReal Begin { get; } = begin;
        internal TReal End { get; } = end;

        internal TReal Evaluate(TReal x) => Source.Evaluate(Interval.InverseLerp(Begin, End, x));
        internal TReal Derivative(TReal x) => DivideByRange(Begin, End, Source.Derivative(Interval.InverseLerp(Begin, End, x)));
        internal TReal Invert(TReal y) => Interval.Lerp(Begin, End, Source.Inverse!(y));
    }

    internal sealed class OutputRangeFunction(ProgressMap<TReal> source, TReal begin, TReal end)
    {
        internal ProgressMap<TReal> Source { get; } = source;
        internal TReal Begin { get; } = begin;
        internal TReal End { get; } = end;

        internal TReal Evaluate(TReal x) => Interval.Lerp(Begin, End, Source.Evaluate(x));
        internal TReal Derivative(TReal x) => ScaleByRange(Begin, End, Source.Derivative(x));
        internal TReal Invert(TReal y) => Source.Inverse!(Interval.InverseLerp(Begin, End, y));
    }

    internal sealed class ComposedFunction(ProgressMap<TReal> first, ProgressMap<TReal> next)
    {
        internal ProgressMap<TReal> First { get; } = first;
        internal ProgressMap<TReal> Next { get; } = next;

        internal TReal Evaluate(TReal phase) => Next.Evaluate(First.Evaluate(phase));
        internal TReal Derivative(TReal phase) => First._isConstant || Next._isConstant
            ? TReal.Zero : Next.Derivative(First.Evaluate(phase)) * First.Derivative(phase);
        internal TReal Invert(TReal y) => First.Inverse!(Next.Inverse!(y));
    }

    internal sealed class EaseOutFunction(ProgressMap<TReal> source)
    {
        internal ProgressMap<TReal> Source { get; } = source;

        internal TReal Evaluate(TReal phase) => TReal.One - Source.Evaluate(TReal.One - phase);
        internal TReal Derivative(TReal phase) => Source.Derivative(TReal.One - phase);
        internal TReal Invert(TReal y) => TReal.One - Source.Inverse!(TReal.One - y);
    }

    internal sealed class EaseInOutFunction(ProgressMap<TReal> source, bool joinsContinuously)
    {
        internal ProgressMap<TReal> Source { get; } = source;

        internal TReal Evaluate(TReal phase) => phase < Half
            ? Source.Evaluate(Two * phase) / Two : TReal.One - Source.Evaluate(Two * (TReal.One - phase)) / Two;
        internal TReal Derivative(TReal phase) => phase == Half && !joinsContinuously ? TReal.NaN
            : Source.Derivative(phase < Half ? Two * phase : Two * (TReal.One - phase));
        internal TReal Invert(TReal y) => y < Half
            ? Source.Inverse!(Two * y) / Two : TReal.One - Source.Inverse!(Two * (TReal.One - y)) / Two;
    }
}
