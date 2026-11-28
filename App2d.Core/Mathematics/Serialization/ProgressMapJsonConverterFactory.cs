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
                "power" => Easing<TReal>.Power(fields.Required<int>("degree")),
                "smoothstep" => Easing<TReal>.SmoothStep(fields.Required<int>("order")),
                "constant" => ProgressMap<TReal>.Constant(fields.Required<TReal>("value"),
                    fields.Optional("inputBegin", TReal.Zero), fields.Optional("inputEnd", TReal.One)),
                "affine" => ProgressMap<TReal>.Affine(fields.Required<TReal>("scale"), fields.Required<TReal>("offset")),
                "gaussian" => Easing<TReal>.Gaussian(fields.Required<TReal>("center"), fields.Required<TReal>("sigma")),
                "normalize" => ProgressMap<TReal>.Normalize(fields.Required<TReal>("begin"), fields.Required<TReal>("end")),
                "from-range" => fields.Required<ProgressMap<TReal>>("source").FromRange(fields.Required<TReal>("begin"), fields.Required<TReal>("end")),
                "to-range" => fields.Required<ProgressMap<TReal>>("source").ToRange(fields.Required<TReal>("begin"), fields.Required<TReal>("end")),
                "then" => fields.Required<ProgressMap<TReal>>("first").Then(fields.Required<ProgressMap<TReal>>("next")),
                "direction" => fields.Required<ProgressMap<TReal>>("source").WithDirection(fields.Required<EasingDirection>("direction")),
                "bezier" => Easing<TReal>.Bezier(fields.Required<TReal>("begin"), fields.Required<TReal>("y1"), fields.Required<TReal>("y2"), fields.Required<TReal>("end")),
                "timed-bezier" => Easing<TReal>.TimedBezier(fields.Required<TReal>("begin"), fields.Required<TReal>("x1"), fields.Required<TReal>("y1"),
                    fields.Required<TReal>("x2"), fields.Required<TReal>("y2"), fields.Required<TReal>("end")),
                "registered" => _names.TryGetValue(fields.Text("name"), out var named) ? named : throw new JsonException("Unknown registered progress function."),
                _ => Primitive(kind)
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
            if (PresetName(value) is { } preset)
            {
                writer.WriteStringValue(preset);
                return;
            }

            writer.WriteStartObject();
            void Kind(string kind) => writer.WriteString("kind", kind);
            void Field<T>(string name, T field)
            {
                writer.WritePropertyName(name);
                JsonSerializer.Serialize(writer, field, options);
            }
            void Range(TReal begin, TReal end)
            {
                Field("begin", begin);
                Field("end", end);
            }
            void Direction(ProgressMap<TReal> source, EasingDirection direction)
            {
                Kind("direction");
                Field("source", source);
                Field("direction", direction);
            }

            switch (value.FunctionState)
            {
                case ProgressMap<TReal>.ConstantFunction constant:
                    Kind("constant");
                    Field("value", constant.Value);
                    if (value.InputBegin != TReal.Zero || value.InputEnd != TReal.One)
                    {
                        Field("inputBegin", value.InputBegin);
                        Field("inputEnd", value.InputEnd);
                    }
                    break;
                case ProgressMap<TReal>.AffineFunction affine:
                    Kind("affine");
                    Field("scale", affine.Scale);
                    Field("offset", affine.Offset);
                    break;
                case ProgressMap<TReal>.NormalizeFunction normalize:
                    Kind("normalize");
                    Range(normalize.Begin, normalize.End);
                    break;
                case ProgressMap<TReal>.InputRangeFunction input:
                    Kind("from-range");
                    Field("source", input.Source);
                    Range(input.Begin, input.End);
                    break;
                case ProgressMap<TReal>.OutputRangeFunction output:
                    Kind("to-range");
                    Field("source", output.Source);
                    Range(output.Begin, output.End);
                    break;
                case ProgressMap<TReal>.ComposedFunction composition:
                    Kind("then");
                    Field("first", composition.First);
                    Field("next", composition.Next);
                    break;
                case ProgressMap<TReal>.EaseOutFunction easeOut:
                    Direction(easeOut.Source, EasingDirection.Out);
                    break;
                case ProgressMap<TReal>.EaseInOutFunction easeInOut:
                    Direction(easeInOut.Source, EasingDirection.InOut);
                    break;
                case Easing<TReal>.PowerFunction power:
                    Kind("power");
                    writer.WriteNumber("degree", power.Degree);
                    break;
                case Easing<TReal>.GaussianFunction gaussian:
                    Kind("gaussian");
                    Field("center", gaussian.Center);
                    Field("sigma", gaussian.Sigma);
                    break;
                case Easing<TReal>.BezierFunction bezier:
                    Kind("bezier");
                    var curve = bezier.Curve;
                    Field("begin", curve.Start);
                    Field("y1", curve.Control1);
                    Field("y2", curve.Control2);
                    Field("end", curve.End);
                    break;
                case Easing<TReal>.TimedBezierFunction timed:
                    Kind("timed-bezier");
                    Field("begin", timed.Begin);
                    Field("x1", timed.FirstTime);
                    Field("y1", timed.FirstValue);
                    Field("x2", timed.SecondTime);
                    Field("y2", timed.SecondValue);
                    Field("end", timed.End);
                    break;
                default:
                    var name = _names.FirstOrDefault(pair => ReferenceEquals(pair.Value, value)).Key
                        ?? throw new JsonException("This progress map contains an unnamed function. Register a name before saving it.");
                    Kind("registered");
                    writer.WriteString("name", name);
                    break;
            }
            writer.WriteEndObject();
        }

        private static string? PresetName(ProgressMap<TReal> value)
        {
            if (value == ProgressMap<TReal>.Forward) return "linear";
            if (value == ProgressMap<TReal>.Reverse) return "reverse";
            if (value == ProgressMap<TReal>.PingPong) return "ping-pong";
            if (value == ProgressMap<TReal>.ReversePingPong) return "reverse-ping-pong";
            if (value == Easing<TReal>.Smooth) return "smooth";
            if (value == Easing<TReal>.Smoother) return "smoother";
            if (value == Easing<TReal>.Smoothest) return "smoothest";
            if (value == Easing<TReal>.Sine) return "sine";
            if (value == Easing<TReal>.Step) return "step";
            return null;
        }
    }
}
