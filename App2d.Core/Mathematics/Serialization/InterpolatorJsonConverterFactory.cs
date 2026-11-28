using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics.Progress;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Mathematics.Vectors;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.Core.Mathematics.Serialization;

/// <summary>Core interpolation recipes, with optional named bindings for application-defined types and functions.</summary>
public sealed class InterpolatorJsonConverterFactory : JsonConverterFactory
{
    private readonly Dictionary<Type, object> _types = [];
    private readonly Lock _gate = new();
    private bool _started;
    internal bool Started => Volatile.Read(ref _started);

    public InterpolationJsonType<TValue, TReal> For<TValue, TReal>() where TReal : IFloatingPointIeee754<TReal>
    {
        lock (_gate)
        {
            if (_started) throw new InvalidOperationException("Configure interpolation bindings before using the serializer.");
            return GetType<TValue, TReal>();
        }
    }

    private InterpolationJsonType<TValue, TReal> GetType<TValue, TReal>() where TReal : IFloatingPointIeee754<TReal>
    {
        var key = typeof(Interpolator<TValue, TReal>);
        if (!_types.TryGetValue(key, out var binding))
        {
            binding = typeof(TValue) == typeof(TReal) ? Scalar<TReal>() : BuiltIn(typeof(TValue), typeof(TReal))
                ?? new InterpolationJsonType<TValue, TReal>();
            _types.Add(key, binding);
        }
        var result = (InterpolationJsonType<TValue, TReal>)binding;
        result.Owner = this;
        return result;
    }

    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsGenericType &&
        typeToConvert.GetGenericTypeDefinition() == typeof(Interpolator<,>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        lock (_gate)
        {
            _started = true;
            return (JsonConverter)Activator.CreateInstance(typeof(Converter<,>).MakeGenericType(typeToConvert.GetGenericArguments()), this)!;
        }
    }

    private sealed class Converter<TValue, TReal> : JsonConverter<Interpolator<TValue, TReal>> where TReal : IFloatingPointIeee754<TReal>
    {
        private readonly InterpolationJsonType<TValue, TReal> _type;

        public Converter(InterpolatorJsonConverterFactory factory)
        {
            lock (factory._gate)
            {
                _type = factory.GetType<TValue, TReal>();
                _type.Freeze();
            }
        }

        public override Interpolator<TValue, TReal> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            try { return InterpolatorJson<TValue, TReal>.Read(document.RootElement, options, _type); }
            catch (ArgumentException exception) { throw new JsonException("Invalid interpolation recipe.", exception); }
        }

        public override void Write(Utf8JsonWriter writer, Interpolator<TValue, TReal> value, JsonSerializerOptions options) =>
            InterpolatorJson<TValue, TReal>.Write(writer, value, options, _type);
    }

    private static InterpolationJsonType<TReal, TReal> Scalar<TReal>() where TReal : IFloatingPointIeee754<TReal> =>
        new(Interpolation.Linear<TReal>())
        {
            Linear = Interpolation.Linear<TReal>,
            ShortestAngle = Interpolation.ShortestAngle<TReal>,
            Bezier = Interpolation.Bezier
        };

    private static InterpolationJsonType<TVector, TReal> Vector<TVector, TReal>()
        where TVector : unmanaged, IVector<TVector, TReal>
        where TReal : unmanaged, IFloatingPointIeee754<TReal>
    {
        var result = new InterpolationJsonType<TVector, TReal>(Interpolation.Default<TVector, TReal>());
        string[] names = ["x", "y", "z", "w"];
        for (var axis = 0; axis < TVector.Dimension; axis++) result.Channel(names[axis], VectorChannels<TVector, TReal>.Instance[axis]);
        return result;
    }

    private static InterpolationJsonType<TVector, TReal> Vector2<TVector, TReal>()
        where TVector : unmanaged, IVector2<TVector, TReal>
        where TReal : unmanaged, IFloatingPointIeee754<TReal>
    {
        var result = Vector<TVector, TReal>();
        result.Polar = static (radius, angle) => Interpolation.Polar<TVector, TReal>(radius, angle);
        return result;
    }

    private static InterpolationJsonType<Polar<TReal>, TReal> PolarValue<TReal>() where TReal : unmanaged, IFloatingPointIeee754<TReal> =>
        new InterpolationJsonType<Polar<TReal>, TReal>(Interpolation.Default<Polar<TReal>, TReal>())
            .Channel("radius", Polar<TReal>.Channels.Radius).Channel("angle", Polar<TReal>.Channels.Angle);

    private static object? BuiltIn(Type value, Type real)
    {
        if (real == typeof(float))
        {
            if (value == typeof(Vector2f)) return Vector2<Vector2f, float>();
            if (value == typeof(Vector3f)) return Vector<Vector3f, float>();
            if (value == typeof(Vector4f))
            {
                return Vector<Vector4f, float>()
                .Channel("r", Vector4f.Channels.R).Channel("g", Vector4f.Channels.G)
                .Channel("b", Vector4f.Channels.B).Channel("a", Vector4f.Channels.A).Channel("rgb", Vector4f.Channels.RGB);
            }

            if (value == typeof(Polar<float>)) return PolarValue<float>();
            if (value == typeof(RigidTransform2D))
            {
                return new InterpolationJsonType<RigidTransform2D, float>(Interpolation.Default<RigidTransform2D, float>())
                .Channel("translation", RigidTransform2D.Channels.Translation).Channel("rotation", RigidTransform2D.Channels.Rotation);
            }

            if (value == typeof(Ellipse2D))
            {
                return new InterpolationJsonType<Ellipse2D, float>(Interpolation.Default<Ellipse2D, float>())
                .Channel("center", Ellipse2D.Channels.Center).Channel("radii", Ellipse2D.Channels.Radii)
                .Channel("radiusX", Ellipse2D.Channels.RadiusX).Channel("radiusY", Ellipse2D.Channels.RadiusY).Channel("angle", Ellipse2D.Channels.Angle);
            }

            if (value == typeof(QuadraticBezier2D))
            {
                return new InterpolationJsonType<QuadraticBezier2D, float>(Interpolation.Default<QuadraticBezier2D, float>())
                .Channel("start", QuadraticBezier2D.Channels.Start).Channel("control", QuadraticBezier2D.Channels.Control).Channel("end", QuadraticBezier2D.Channels.End);
            }

            if (value == typeof(LineSegmentCurve2D))
            {
                return new InterpolationJsonType<LineSegmentCurve2D, float>(Interpolation.Default<LineSegmentCurve2D, float>())
                .Channel("start", LineSegmentCurve2D.Channels.Start).Channel("end", LineSegmentCurve2D.Channels.End);
            }

            if (value == typeof(SimplePolygon2D)) return new InterpolationJsonType<SimplePolygon2D, float>(Interpolation.Default<SimplePolygon2D, float>());
        }
        if (real == typeof(double))
        {
            if (value == typeof(Vector2d)) return Vector2<Vector2d, double>();
            if (value == typeof(Vector3d)) return Vector<Vector3d, double>();
            if (value == typeof(Vector4d))
            {
                return Vector<Vector4d, double>()
                .Channel("r", Vector4d.Channels.R).Channel("g", Vector4d.Channels.G)
                .Channel("b", Vector4d.Channels.B).Channel("a", Vector4d.Channels.A).Channel("rgb", Vector4d.Channels.RGB);
            }

            if (value == typeof(Polar<double>)) return PolarValue<double>();
        }
        return null;
    }
}

internal static class InterpolatorJson<TValue, TReal> where TReal : IFloatingPointIeee754<TReal>
{
    private static (string Kind, object? State) Collect(Interpolator<TValue, TReal> value,
        List<object> operations, InterpolationJsonType<TValue, TReal> type)
    {
        if (!value.IsInitialized) throw new JsonException("An uninitialized interpolator cannot be saved.");
        if (type.FindName(value) is { } name) return ("registered", name);

        if (value.Kind == InterpolationKind.Default)
        {
            if (value == type.DefaultRule) return ("default", null);
            if (type.DefaultRule is not { } declared || value.FunctionOnly != declared)
                throw new JsonException("The registered default differs from this constructed default. Register a distinct name for the rule.");
        }

        (string Kind, object? State) basis;
        switch (value.FunctionState)
        {
            case IChannelInterpolation<TValue, TReal> channel:
                basis = Collect(channel.Source, operations, type);
                operations.Add(channel);
                break;
            case BezierInterpolation<TReal> bezier:
                basis = ("bezier", bezier);
                break;
            case IPolarInterpolation<TReal> polar:
                basis = ("polar", polar);
                break;
            case ICoordinateInterpolation coordinates:
                basis = ("coordinates", coordinates);
                break;
            default:
                // A registered default can already include the outer progress map.
                if (value.Kind == InterpolationKind.Custom && value == type.DefaultRule) return ("default", null);
                basis = value.Kind switch
                {
                    InterpolationKind.Default => ("default", null),
                    InterpolationKind.Linear => ("linear", null),
                    InterpolationKind.Hold => ("hold", null),
                    InterpolationKind.ShortestAngle => ("shortest-angle", null),
                    _ => DescribeCustom()
                };
                break;
        }
        if (value.Progress is { } progress) operations.Add(progress);
        return basis;

        (string, object?) DescribeCustom()
        {
            var functionOnly = value.FunctionOnly;
            if (functionOnly == type.DefaultRule) return ("default", null);
            if (type.FindName(functionOnly) is { } registered) return ("registered", registered);
            throw new JsonException("This interpolator contains an unnamed function. Register a default or named rule before saving it.");
        }
    }

    internal static void Write(Utf8JsonWriter writer, Interpolator<TValue, TReal> value, JsonSerializerOptions options,
        InterpolationJsonType<TValue, TReal> type)
    {
        List<object> operations = [];
        var (kind, state) = Collect(value, operations, type);
        if (operations.Count == 0 && kind is "linear" or "hold" or "shortest-angle")
        {
            writer.WriteStringValue(kind);
            return;
        }

        void Field<T>(string name, T field)
        {
            writer.WritePropertyName(name);
            JsonSerializer.Serialize(writer, field, options);
        }

        writer.WriteStartObject();
        writer.WriteString("kind", kind);
        switch (state)
        {
            case string registered:
                writer.WriteString("name", registered);
                break;
            case BezierInterpolation<TReal> bezier:
                Field("x1", bezier.FirstTime);
                Field("y1", bezier.FirstValue);
                Field("x2", bezier.SecondTime);
                Field("y2", bezier.SecondValue);
                writer.WriteBoolean("absolute", true);
                break;
            case IPolarInterpolation<TReal> polar:
                Field("radius", polar.Radius);
                Field("angle", polar.Angle);
                break;
            case ICoordinateInterpolation coordinates:
                type.WriteCoordinates(writer, coordinates, options);
                break;
        }
        if (operations is [ProgressMap<TReal> progress])
        {
            Field("progress", progress);
        }
        else if (operations.Count > 0)
        {
            writer.WriteStartArray("operations");
            foreach (var operation in operations)
            {
                writer.WriteStartObject();
                if (operation is ProgressMap<TReal> map)
                {
                    writer.WriteString("kind", "progress");
                    Field("map", map);
                }
                else
                {
                    writer.WriteString("kind", "channel");
                    type.WriteChannel(writer, (IChannelInterpolation<TValue, TReal>)operation, options);
                }
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }
        writer.WriteEndObject();
    }

    internal static Interpolator<TValue, TReal> Read(JsonElement element, JsonSerializerOptions options, InterpolationJsonType<TValue, TReal> type)
    {
        if (element.ValueKind == JsonValueKind.String) return ReadSimple(element.GetString()!, type);
        var fields = new RecipeObject(element, options);
        var kind = fields.Text("kind");
        var result = kind switch
        {
            "default" or "linear" or "hold" or "shortest-angle" => ReadSimple(kind, type),
            "bezier" => type.Bezier is { } bezier
                ? bezier(fields.Required<TReal>("x1"), fields.Required<TReal>("y1"), fields.Required<TReal>("x2"), fields.Required<TReal>("y2"), fields.Optional("absolute", false))
                : throw new JsonException("Bezier interpolation requires a scalar value."),
            "polar" => type.Polar is { } polar
                ? polar(fields.Optional("radius", Interpolation.Linear<TReal>()), fields.Optional("angle", Interpolation.ShortestAngle<TReal>()))
                : throw new JsonException("Polar interpolation requires a Cartesian 2D vector."),
            "coordinates" => type.ReadCoordinates(fields.Text("system"), fields.Element("interpolator"), options),
            "registered" => type.ReadNamed(fields.Text("name")),
            _ => throw new JsonException($"Unknown interpolator kind '{kind}'.")
        };
        if (fields.Has("progress") && fields.Has("operations")) throw new JsonException("Use progress or an ordered operations list, not both.");
        if (fields.Has("progress")) result = result.WithProgress(fields.Required<ProgressMap<TReal>>("progress"));
        if (fields.Has("operations"))
        {
            var list = fields.Element("operations");
            if (list.ValueKind != JsonValueKind.Array) throw new JsonException("Interpolator operations must be an array.");
            foreach (var elementOperation in list.EnumerateArray())
            {
                var operation = new RecipeObject(elementOperation, options);
                var operationKind = operation.Text("kind");
                result = operationKind switch
                {
                    "progress" => result.WithProgress(operation.Required<ProgressMap<TReal>>("map")),
                    "channel" => type.ApplyChannel(result, operation.Text("channel"), operation.Element("interpolator"), options),
                    _ => throw new JsonException($"Unknown interpolation operation '{operationKind}'.")
                };
                operation.Complete();
            }
        }
        fields.Complete();
        return result;
    }

    private static Interpolator<TValue, TReal> ReadSimple(string kind, InterpolationJsonType<TValue, TReal> type) => kind switch
    {
        "default" => type.DefaultRule ?? throw new JsonException($"No default interpolator is registered for {typeof(TValue).Name}."),
        "linear" => type.Linear?.Invoke() ?? throw new JsonException("Linear interpolation requires a scalar value; use default for other value types."),
        "hold" => Interpolation.Hold<TValue, TReal>(),
        "shortest-angle" => type.ShortestAngle?.Invoke() ?? throw new JsonException("Shortest-angle interpolation requires a scalar value."),
        _ => throw new JsonException($"Unknown interpolator kind '{kind}'.")
    };
}
