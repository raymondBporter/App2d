using App2d.Core.Mathematics.Progress;
using System.Numerics;
using System.Text.Json;

namespace App2d.Core.Mathematics.Serialization;

// The recipe describes the function before its outer progress map. WithProgress keeps that map separately,
// so ordinary named interpolation remains a value wrapper without allocating a recipe on every sample.
internal abstract record InterpolatorRecipe<TValue, TReal> where TReal : IFloatingPointIeee754<TReal>
{
    internal static readonly Named Default = new("default");
    internal static readonly Named Linear = new("linear");
    internal static readonly Named Hold = new("hold");
    internal static readonly Named ShortestAngle = new("shortest-angle");

    internal virtual InterpolatorRecipe<TValue, TReal> Collect(List<IOperation> operations, InterpolationJsonType<TValue, TReal> type) => this;
    internal abstract void WriteBase(Utf8JsonWriter writer, JsonSerializerOptions options, InterpolationJsonType<TValue, TReal> type);

    internal interface IOperation
    {
        void Write(Utf8JsonWriter writer, JsonSerializerOptions options, InterpolationJsonType<TValue, TReal> type);
    }

    internal sealed record Named(string Kind) : InterpolatorRecipe<TValue, TReal>
    {
        internal override void WriteBase(Utf8JsonWriter writer, JsonSerializerOptions options, InterpolationJsonType<TValue, TReal> type)
        {
            if (Kind == "default" && type.DefaultRule is null) throw new JsonException($"No default interpolator is registered for {typeof(TValue).Name}.");
            writer.WriteString("kind", Kind);
        }
    }

    internal sealed record Registered(string Name) : InterpolatorRecipe<TValue, TReal>
    {
        internal override void WriteBase(Utf8JsonWriter writer, JsonSerializerOptions options, InterpolationJsonType<TValue, TReal> type)
        {
            writer.WriteString("kind", "registered");
            writer.WriteString("name", Name);
        }
    }

    internal sealed record Bezier(TReal FirstTime, TReal FirstValue, TReal SecondTime, TReal SecondValue) : InterpolatorRecipe<TValue, TReal>
    {
        internal override void WriteBase(Utf8JsonWriter writer, JsonSerializerOptions options, InterpolationJsonType<TValue, TReal> type)
        {
            writer.WriteString("kind", "bezier");
            writer.WritePropertyName("x1"); JsonSerializer.Serialize(writer, FirstTime, options);
            writer.WritePropertyName("y1"); JsonSerializer.Serialize(writer, FirstValue, options);
            writer.WritePropertyName("x2"); JsonSerializer.Serialize(writer, SecondTime, options);
            writer.WritePropertyName("y2"); JsonSerializer.Serialize(writer, SecondValue, options);
            writer.WriteBoolean("absolute", true);
        }
    }

    internal sealed record Polar(Interpolator<TReal, TReal> Radius, Interpolator<TReal, TReal> Angle) : InterpolatorRecipe<TValue, TReal>
    {
        internal override void WriteBase(Utf8JsonWriter writer, JsonSerializerOptions options, InterpolationJsonType<TValue, TReal> type)
        {
            writer.WriteString("kind", "polar");
            writer.WritePropertyName("radius"); JsonSerializer.Serialize(writer, Radius, options);
            writer.WritePropertyName("angle"); JsonSerializer.Serialize(writer, Angle, options);
        }
    }

    internal sealed record Channel<TChannel>(Interpolator<TValue, TReal> Source, InterpolationChannel<TValue, TChannel> Accessor,
        Interpolator<TChannel, TReal> Child) : InterpolatorRecipe<TValue, TReal>, IOperation
    {
        internal override InterpolatorRecipe<TValue, TReal> Collect(List<IOperation> operations, InterpolationJsonType<TValue, TReal> type)
        {
            var basis = InterpolatorJson<TValue, TReal>.Collect(Source, operations, type);
            operations.Add(this);
            return basis;
        }

        internal override void WriteBase(Utf8JsonWriter writer, JsonSerializerOptions options, InterpolationJsonType<TValue, TReal> type) =>
            throw new InvalidOperationException("A channel is an operation, not a base interpolator.");

        public void Write(Utf8JsonWriter writer, JsonSerializerOptions options, InterpolationJsonType<TValue, TReal> type)
        {
            writer.WriteStartObject();
            writer.WriteString("kind", "channel");
            writer.WriteString("channel", type.ChannelName(Accessor));
            writer.WritePropertyName("interpolator"); JsonSerializer.Serialize(writer, Child, options);
            writer.WriteEndObject();
        }
    }

    internal sealed record Progress(ProgressMap<TReal> Map) : IOperation
    {
        public void Write(Utf8JsonWriter writer, JsonSerializerOptions options, InterpolationJsonType<TValue, TReal> type)
        {
            writer.WriteStartObject();
            writer.WriteString("kind", "progress");
            writer.WritePropertyName("map"); JsonSerializer.Serialize(writer, Map, options);
            writer.WriteEndObject();
        }
    }

    internal sealed record Coordinates<TCoordinates>(Interpolator<TCoordinates, TReal> Source,
        Func<TValue, TCoordinates> To, Func<TCoordinates, TValue> From) : InterpolatorRecipe<TValue, TReal>
    {
        internal override void WriteBase(Utf8JsonWriter writer, JsonSerializerOptions options, InterpolationJsonType<TValue, TReal> type)
        {
            writer.WriteString("kind", "coordinates");
            writer.WriteString("system", type.CoordinateName(To, From));
            writer.WritePropertyName("interpolator"); JsonSerializer.Serialize(writer, Source, options);
        }
    }
}
