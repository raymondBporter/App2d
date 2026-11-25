using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics.Progress;
using App2d.Core.Mathematics.Transforms;
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
    internal static InterpolatorRecipe<TValue, TReal> Collect(Interpolator<TValue, TReal> value,
        List<InterpolatorRecipe<TValue, TReal>.IOperation> operations, InterpolationJsonType<TValue, TReal> type)
    {
        if (!value.IsInitialized) throw new JsonException("An uninitialized interpolator cannot be saved.");
        if (type.DescribeNamed(value) is { } named) return named;
        if (value.Recipe is null && type.DescribeKnown(value) is { } knownWhole) return knownWhole;
        if (value.Recipe is InterpolatorRecipe<TValue, TReal>.Named { Kind: "default" })
        {
            if (type.DescribeKnown(value) is InterpolatorRecipe<TValue, TReal>.Named { Kind: "default" })
                return InterpolatorRecipe<TValue, TReal>.Default;
            if (type.DefaultRule is not { } declared || value.FunctionOnly != declared)
                throw new JsonException("The registered default differs from this constructed default. Register a distinct name for the rule.");
        }
        var recipe = value.Recipe ?? type.DescribeKnown(value.FunctionOnly)
            ?? throw new JsonException("This interpolator contains an unnamed function. Register a default or named rule before saving it.");
        var basis = recipe.Collect(operations, type);
        if (value.Progress is { } progress) operations.Add(new InterpolatorRecipe<TValue, TReal>.Progress(progress));
        return basis;
    }

    internal static void Write(Utf8JsonWriter writer, Interpolator<TValue, TReal> value, JsonSerializerOptions options,
        InterpolationJsonType<TValue, TReal> type)
    {
        List<InterpolatorRecipe<TValue, TReal>.IOperation> operations = [];
        var basis = Collect(value, operations, type);
        if (operations.Count == 0 && basis is InterpolatorRecipe<TValue, TReal>.Named named && named.Kind != "default")
        {
            writer.WriteStringValue(named.Kind);
            return;
        }
        writer.WriteStartObject();
        basis.WriteBase(writer, options, type);
        if (operations.Count == 1 && operations[0] is InterpolatorRecipe<TValue, TReal>.Progress progress)
        {
            writer.WritePropertyName("progress"); JsonSerializer.Serialize(writer, progress.Map, options);
        }
        else if (operations.Count > 0)
        {
            writer.WriteStartArray("operations");
            foreach (var operation in operations) operation.Write(writer, options, type);
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
