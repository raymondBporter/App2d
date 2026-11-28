using App2d.Core.Mathematics.Progress;
using System.Numerics;

namespace App2d.Core.Mathematics;

internal sealed class BezierInterpolation<TReal>(TReal firstTime, TReal firstValue, TReal secondTime, TReal secondValue)
    where TReal : IFloatingPointIeee754<TReal>
{
    internal TReal FirstTime { get; } = firstTime;
    internal TReal FirstValue { get; } = firstValue;
    internal TReal SecondTime { get; } = secondTime;
    internal TReal SecondValue { get; } = secondValue;

    internal TReal Evaluate(TReal start, TReal end, TReal time) =>
        Easing<TReal>.TimedBezierValue(start, FirstTime, FirstValue, SecondTime, SecondValue, end, time);
}

internal interface IPolarInterpolation<TReal> where TReal : IFloatingPointIeee754<TReal>
{
    Interpolator<TReal, TReal> Radius { get; }
    Interpolator<TReal, TReal> Angle { get; }
}

internal sealed class PolarInterpolation<TVector, TReal>(Interpolator<TReal, TReal> radius, Interpolator<TReal, TReal> angle)
    : IPolarInterpolation<TReal>
    where TVector : unmanaged, IVector2<TVector, TReal>
    where TReal : unmanaged, IFloatingPointIeee754<TReal>
{
    public Interpolator<TReal, TReal> Radius { get; } = radius;
    public Interpolator<TReal, TReal> Angle { get; } = angle;

    internal TVector Evaluate(TVector start, TVector end, TReal time)
    {
        var a = TVector.ToPolar(start);
        var b = TVector.ToPolar(end);
        var firstAngle = a.Radius == TReal.Zero ? b.AngleRadians : a.AngleRadians;
        var secondAngle = b.Radius == TReal.Zero ? firstAngle : b.AngleRadians;
        return TVector.Direction(Angle.Evaluate(firstAngle, secondAngle, time)) * Radius.Evaluate(a.Radius, b.Radius, time);
    }
}

internal interface IChannelInterpolation<TValue, TReal> where TReal : IFloatingPointIeee754<TReal>
{
    Interpolator<TValue, TReal> Source { get; }
    object Accessor { get; }
}

internal sealed class ChannelInterpolation<TValue, TChannel, TReal>(Interpolator<TValue, TReal> source,
    InterpolationChannel<TValue, TChannel> channel, Interpolator<TChannel, TReal> child) : IChannelInterpolation<TValue, TReal>
    where TReal : IFloatingPointIeee754<TReal>
{
    public Interpolator<TValue, TReal> Source { get; } = source;
    internal InterpolationChannel<TValue, TChannel> Channel { get; } = channel;
    internal Interpolator<TChannel, TReal> Child { get; } = child;
    object IChannelInterpolation<TValue, TReal>.Accessor => Channel;

    internal TValue Evaluate(TValue start, TValue end, TReal time) =>
        Channel.Replace(Source.Evaluate(start, end, time), Child.Evaluate(Channel.Read(start), Channel.Read(end), time));
}

internal interface ICoordinateInterpolation
{
    Delegate To { get; }
    Delegate From { get; }
}

internal sealed class CoordinateInterpolation<TValue, TCoordinates, TReal>(Interpolator<TCoordinates, TReal> source,
    Func<TValue, TCoordinates> to, Func<TCoordinates, TValue> from) : ICoordinateInterpolation
    where TReal : IFloatingPointIeee754<TReal>
{
    internal Interpolator<TCoordinates, TReal> Source { get; } = source;

    Delegate ICoordinateInterpolation.To => to;
    Delegate ICoordinateInterpolation.From => from;

    internal TValue Evaluate(TValue start, TValue end, TReal time) =>
        from(Source.Evaluate(to(start), to(end), time));
}
