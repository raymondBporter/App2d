using App2d.Core.Validation;

namespace App2d.Core.Mathematics;

/// <summary>Reads a part of a value and returns a value with that part replaced. Channels do not choose interpolation rules.</summary>
public sealed class InterpolationChannel<TValue, TChannel>
{
    private readonly Func<TValue, TChannel> _read;
    private readonly Func<TValue, TChannel, TValue> _replace;

    public InterpolationChannel(Func<TValue, TChannel> read, Func<TValue, TChannel, TValue> replace)
    {
        Ensure.ThrowIfNull(read);
        Ensure.ThrowIfNull(replace);
        _read = read;
        _replace = replace;
    }

    public TChannel Read(TValue value) => _read(value);
    public TValue Replace(TValue value, TChannel channelValue) => _replace(value, channelValue);

    public static InterpolationChannel<TValue, TValue> Whole { get; } = new(static value => value, static (_, value) => value);
}
