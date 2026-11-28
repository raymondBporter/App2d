using System.Numerics;
using System.Text.Json;

namespace App2d.Core.Mathematics.Serialization;

/// <summary>Named JSON bindings for one value/scalar pair. Configure these before using the converter factory.</summary>
public sealed class InterpolationJsonType<TValue, TReal> where TReal : IFloatingPointIeee754<TReal>
{
    private readonly Dictionary<string, IChannel> _channels = [with(StringComparer.Ordinal)];
    private readonly Dictionary<object, string> _channelNames = [with(ReferenceEqualityComparer.Instance)];
    private readonly Dictionary<string, ICoordinates> _coordinates = [with(StringComparer.Ordinal)];
    private readonly Dictionary<(Delegate To, Delegate From), string> _coordinateNames = [];
    private readonly Dictionary<string, Interpolator<TValue, TReal>> _named = [with(StringComparer.Ordinal)];
    private bool _frozen;
    internal InterpolatorJsonConverterFactory? Owner { get; set; }

    internal InterpolationJsonType(Interpolator<TValue, TReal>? defaultRule = null)
    {
        DefaultRule = defaultRule;
        Channel("v", InterpolationChannel<TValue, TValue>.Whole);
    }

    internal Interpolator<TValue, TReal>? DefaultRule { get; private set; }
    internal Func<Interpolator<TValue, TReal>>? Linear { get; set; }
    internal Func<Interpolator<TValue, TReal>>? ShortestAngle { get; set; }
    internal Func<TReal, TReal, TReal, TReal, bool, Interpolator<TValue, TReal>>? Bezier { get; set; }
    internal Func<Interpolator<TReal, TReal>, Interpolator<TReal, TReal>, Interpolator<TValue, TReal>>? Polar { get; set; }

    public InterpolationJsonType<TValue, TReal> Default(Interpolator<TValue, TReal> rule)
    {
        Editable();
        if (!rule.IsInitialized) throw new ArgumentException("The default interpolator needs a function.", nameof(rule));
        DefaultRule = rule;
        return this;
    }

    public InterpolationJsonType<TValue, TReal> Channel<TChannel>(string name, InterpolationChannel<TValue, TChannel> channel)
    {
        Editable();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(channel);
        _channels.Add(name, new ChannelBinding<TChannel>(channel));
        _channelNames.TryAdd(channel, name);
        return this;
    }

    public InterpolationJsonType<TValue, TReal> Coordinates<TCoordinates>(string name, Func<TValue, TCoordinates> to,
        Func<TCoordinates, TValue> from)
    {
        Editable();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(to);
        ArgumentNullException.ThrowIfNull(from);
        _coordinates.Add(name, new CoordinateBinding<TCoordinates>(to, from));
        _coordinateNames.Add((to, from), name);
        return this;
    }

    /// <summary>Gives a fixed code-defined rule a stable name. Its captured state remains supplied by that registration.</summary>
    public InterpolationJsonType<TValue, TReal> Named(string name, Interpolator<TValue, TReal> rule)
    {
        Editable();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!rule.IsInitialized) throw new ArgumentException("A named interpolator needs a function.", nameof(rule));
        _named.Add(name, rule);
        return this;
    }

    private void Editable()
    {
        if (_frozen || Owner?.Started == true) throw new InvalidOperationException("Configure interpolation bindings before using the serializer.");
    }

    internal void Freeze() => _frozen = true;
    private string ChannelName(object channel) => _channelNames.TryGetValue(channel, out var name) ? name
        : throw new JsonException($"This {typeof(TValue).Name} channel has no registered JSON name.");
    private string CoordinateName(Delegate to, Delegate from) => _coordinateNames.TryGetValue((to, from), out var name) ? name
        : throw new JsonException($"This coordinate conversion for {typeof(TValue).Name} has no registered JSON name.");
    internal Interpolator<TValue, TReal> ReadNamed(string name) => _named.TryGetValue(name, out var rule) ? rule
        : throw new JsonException($"Unknown registered interpolator '{name}'.");

    internal string? FindName(Interpolator<TValue, TReal> rule)
    {
        foreach (var (name, known) in _named)
            if (rule == known) return name;
        return null;
    }

    internal void WriteChannel(Utf8JsonWriter writer, IChannelInterpolation<TValue, TReal> channel, JsonSerializerOptions options)
    {
        var name = ChannelName(channel.Accessor);
        writer.WriteString("channel", name);
        writer.WritePropertyName("interpolator");
        _channels[name].WriteChild(writer, channel, options);
    }

    internal void WriteCoordinates(Utf8JsonWriter writer, ICoordinateInterpolation coordinates, JsonSerializerOptions options)
    {
        var name = CoordinateName(coordinates.To, coordinates.From);
        writer.WriteString("system", name);
        writer.WritePropertyName("interpolator");
        _coordinates[name].WriteSource(writer, coordinates, options);
    }

    internal Interpolator<TValue, TReal> ApplyChannel(Interpolator<TValue, TReal> source, string name, JsonElement child, JsonSerializerOptions options) =>
        _channels.TryGetValue(name, out var channel) ? channel.Apply(source, child, options)
            : throw new JsonException($"Unknown {typeof(TValue).Name} interpolation channel '{name}'.");

    internal Interpolator<TValue, TReal> ReadCoordinates(string name, JsonElement source, JsonSerializerOptions options) =>
        _coordinates.TryGetValue(name, out var coordinates) ? coordinates.Read(source, options)
            : throw new JsonException($"Unknown {typeof(TValue).Name} coordinate system '{name}'.");

    private interface IChannel
    {
        Interpolator<TValue, TReal> Apply(Interpolator<TValue, TReal> source, JsonElement child, JsonSerializerOptions options);
        void WriteChild(Utf8JsonWriter writer, IChannelInterpolation<TValue, TReal> channel, JsonSerializerOptions options);
    }

    private sealed record ChannelBinding<TChannel>(InterpolationChannel<TValue, TChannel> Accessor) : IChannel
    {
        public Interpolator<TValue, TReal> Apply(Interpolator<TValue, TReal> source, JsonElement child, JsonSerializerOptions options) =>
            source.With(Accessor, child.Deserialize<Interpolator<TChannel, TReal>>(options));

        public void WriteChild(Utf8JsonWriter writer, IChannelInterpolation<TValue, TReal> channel, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, ((ChannelInterpolation<TValue, TChannel, TReal>)channel).Child, options);
    }

    private interface ICoordinates
    {
        Interpolator<TValue, TReal> Read(JsonElement source, JsonSerializerOptions options);
        void WriteSource(Utf8JsonWriter writer, ICoordinateInterpolation coordinates, JsonSerializerOptions options);
    }

    private sealed record CoordinateBinding<TCoordinates>(Func<TValue, TCoordinates> To, Func<TCoordinates, TValue> From) : ICoordinates
    {
        public Interpolator<TValue, TReal> Read(JsonElement source, JsonSerializerOptions options) =>
            source.Deserialize<Interpolator<TCoordinates, TReal>>(options).InCoordinates(To, From);

        public void WriteSource(Utf8JsonWriter writer, ICoordinateInterpolation coordinates, JsonSerializerOptions options)
        {
            if (coordinates is not CoordinateInterpolation<TValue, TCoordinates, TReal> typed)
                throw new JsonException("The coordinate binding uses a different value type.");
            JsonSerializer.Serialize(writer, typed.Source, options);
        }
    }
}
