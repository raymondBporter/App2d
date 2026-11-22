using App2d.Core.Mathematics.Progress;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.Core.Mathematics.Serialization;

/// <summary>Named progress functions, their parameters, and ordered composition. Anonymous functions need an explicit registration.</summary>
public sealed class ProgressMapJsonConverterFactory : JsonConverterFactory
{
    private readonly Dictionary<Type, object> _names = [];
    private readonly Lock _gate = new();
    private bool _started;

    public ProgressMapJsonConverterFactory Register<TReal>(string name, ProgressMap<TReal> map) where TReal : IFloatingPointIeee754<TReal>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(map);
        lock (_gate)
        {
            if (_started) throw new InvalidOperationException("Register progress functions before using the serializer.");
            Names<TReal>().Add(name, map);
        }
        return this;
    }

    private Dictionary<string, ProgressMap<TReal>> Names<TReal>() where TReal : IFloatingPointIeee754<TReal>
    {
        if (!_names.TryGetValue(typeof(TReal), out var names))
        {
            names = new Dictionary<string, ProgressMap<TReal>>(StringComparer.Ordinal);
            _names.Add(typeof(TReal), names);
        }
        return (Dictionary<string, ProgressMap<TReal>>)names;
    }

    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(ProgressMap<>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        lock (_gate)
        {
            _started = true;
            return (JsonConverter)Activator.CreateInstance(typeof(Converter<>).MakeGenericType(typeToConvert.GetGenericArguments()), this)!;
        }
    }

    private sealed class Converter<TReal> : JsonConverter<ProgressMap<TReal>> where TReal : IFloatingPointIeee754<TReal>
    {
        private readonly Dictionary<string, ProgressMap<TReal>> _names;

        public Converter(ProgressMapJsonConverterFactory factory)
        {
            lock (factory._gate) _names = factory.Names<TReal>();
        }

        public override ProgressMap<TReal> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            try { return ReadMap(document.RootElement, options); }
            catch (ArgumentException exception) { throw new JsonException("Invalid progress recipe.", exception); }
        }

        private ProgressMap<TReal> ReadMap(JsonElement element, JsonSerializerOptions options)
        {
            if (element.ValueKind == JsonValueKind.String) return Primitive(element.GetString()!);
            var fields = new RecipeObject(element, options);
            var kind = fields.Text("kind");
            var result = kind switch
            {
                "linear" or "reverse" or "ping-pong" or "reverse-ping-pong" or "smooth" or "smoother" or "smoothest" or "sine" or "step"
                    or "quad" or "cubic" or "quart" or "quint" => Primitive(kind),
                "power" => Easing<TReal>.Power(fields.Required<int>("degree")),
                "smoothstep" => Easing<TReal>.SmoothStep(fields.Required<int>("order")),
                "constant" => ProgressMap<TReal>.Constant(fields.Required<TReal>("value"),
                    fields.Optional("inputBegin", TReal.Zero), fields.Optional("inputEnd", TReal.One)),
                "affine" => ProgressMap<TReal>.Affine(fields.Required<TReal>("scale"), fields.Required<TReal>("offset")),
                "normalize" => ProgressMap<TReal>.Normalize(fields.Required<TReal>("begin"), fields.Required<TReal>("end")),
                "from-range" => fields.Required<ProgressMap<TReal>>("source").FromRange(fields.Required<TReal>("begin"), fields.Required<TReal>("end")),
                "to-range" => fields.Required<ProgressMap<TReal>>("source").ToRange(fields.Required<TReal>("begin"), fields.Required<TReal>("end")),
                "then" => fields.Required<ProgressMap<TReal>>("first").Then(fields.Required<ProgressMap<TReal>>("next")),
                "direction" => fields.Required<ProgressMap<TReal>>("source").WithDirection(fields.Required<EasingDirection>("direction")),
                "bezier" => Easing<TReal>.Bezier(fields.Required<TReal>("begin"), fields.Required<TReal>("y1"), fields.Required<TReal>("y2"), fields.Required<TReal>("end")),
                "timed-bezier" => Easing<TReal>.TimedBezier(fields.Required<TReal>("begin"), fields.Required<TReal>("x1"), fields.Required<TReal>("y1"),
                    fields.Required<TReal>("x2"), fields.Required<TReal>("y2"), fields.Required<TReal>("end")),
                "registered" => _names.TryGetValue(fields.Text("name"), out var named) ? named : throw new JsonException("Unknown registered progress function."),
                _ => throw new JsonException($"Unknown progress kind '{kind}'.")
            };
            fields.Complete();
            return result;
        }

        private static ProgressMap<TReal> Primitive(string kind) => kind switch
        {
            "linear" => ProgressMap<TReal>.Forward,
            "reverse" => ProgressMap<TReal>.Reverse,
            "ping-pong" => ProgressMap<TReal>.PingPong,
            "reverse-ping-pong" => ProgressMap<TReal>.ReversePingPong,
            "smooth" => Easing<TReal>.Smooth,
            "smoother" => Easing<TReal>.Smoother,
            "smoothest" => Easing<TReal>.Smoothest,
            "sine" => Easing<TReal>.Sine,
            "step" => Easing<TReal>.Step,
            "quad" => Easing<TReal>.Power(2),
            "cubic" => Easing<TReal>.Power(3),
            "quart" => Easing<TReal>.Power(4),
            "quint" => Easing<TReal>.Power(5),
            _ => throw new JsonException($"Unknown progress kind '{kind}'.")
        };

        public override void Write(Utf8JsonWriter writer, ProgressMap<TReal> value, JsonSerializerOptions options)
        {
            if (value.Recipe is not { } recipe)
            {
                var name = _names.FirstOrDefault(pair => ReferenceEquals(pair.Value, value)).Key
                    ?? throw new JsonException("This progress map contains an unnamed function. Register a name before saving it.");
                writer.WriteStartObject(); writer.WriteString("kind", "registered"); writer.WriteString("name", name); writer.WriteEndObject();
                return;
            }
            var primitive = recipe.Kind switch
            {
                ProgressOperation.Linear => "linear",
                ProgressOperation.Reverse => "reverse",
                ProgressOperation.PingPong => "ping-pong",
                ProgressOperation.ReversePingPong => "reverse-ping-pong",
                ProgressOperation.Sine => "sine",
                ProgressOperation.Step => "step",
                ProgressOperation.SmoothStep => recipe.Integer switch { 1 => "smooth", 2 => "smoother", 3 => "smoothest", _ => null },
                _ => null
            };
            if (primitive is not null) { writer.WriteStringValue(primitive); return; }

            writer.WriteStartObject();
            void Kind(string kind) => writer.WriteString("kind", kind);
            void Number(string name, int index) { writer.WritePropertyName(name); JsonSerializer.Serialize(writer, recipe.Parameters[index], options); }
            void Source(string name, ProgressMap<TReal>? source) { writer.WritePropertyName(name); JsonSerializer.Serialize(writer, source, options); }
            switch (recipe.Kind)
            {
                case ProgressOperation.Power: Kind("power"); writer.WriteNumber("degree", recipe.Integer); break;
                case ProgressOperation.SmoothStep: Kind("smoothstep"); writer.WriteNumber("order", recipe.Integer); break;
                case ProgressOperation.Constant:
                    Kind("constant"); Number("value", 0);
                    if (recipe.Parameters[1] != TReal.Zero || recipe.Parameters[2] != TReal.One) { Number("inputBegin", 1); Number("inputEnd", 2); }
                    break;
                case ProgressOperation.Affine: Kind("affine"); Number("scale", 0); Number("offset", 1); break;
                case ProgressOperation.Normalize: Kind("normalize"); Number("begin", 0); Number("end", 1); break;
                case ProgressOperation.FromRange: Kind("from-range"); Source("source", recipe.Source); Number("begin", 0); Number("end", 1); break;
                case ProgressOperation.ToRange: Kind("to-range"); Source("source", recipe.Source); Number("begin", 0); Number("end", 1); break;
                case ProgressOperation.Then: Kind("then"); Source("first", recipe.Source); Source("next", recipe.Next); break;
                case ProgressOperation.Direction:
                    Kind("direction"); Source("source", recipe.Source); writer.WritePropertyName("direction"); JsonSerializer.Serialize(writer, recipe.Direction, options); break;
                case ProgressOperation.Bezier: Kind("bezier"); Number("begin", 0); Number("y1", 1); Number("y2", 2); Number("end", 3); break;
                case ProgressOperation.TimedBezier:
                    Kind("timed-bezier"); Number("begin", 0); Number("x1", 1); Number("y1", 2); Number("x2", 3); Number("y2", 4); Number("end", 5); break;
                default: throw new JsonException("Unsupported progress recipe.");
            }
            writer.WriteEndObject();
        }
    }
}
