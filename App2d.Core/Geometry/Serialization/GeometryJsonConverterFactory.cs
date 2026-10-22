using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Operations.Area;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Shapes;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.Core.Geometry.Serialization;

/// <summary>Kind dispatch and source fields for geometry values. Deserialization never calls a validating factory.</summary>
public sealed class GeometryJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeof(IGeometryValue2D).IsAssignableFrom(typeToConvert);
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(typeof(Converter<>).MakeGenericType(typeToConvert))!;

    private sealed class Converter<T> : JsonConverter<T> where T : IGeometryValue2D
    {
        public Converter() { }
        public override bool HandleNull => true;
        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null) return default;
            using var document = JsonDocument.ParseValue(ref reader);
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException("Expected a geometry object.");
            var comparer = options.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            var properties = new Dictionary<string, JsonElement>(comparer);
            foreach (var property in document.RootElement.EnumerateObject()) properties[property.Name] = property.Value;
            var read = new HashSet<string>(comparer);
            string Name(string name) => options.PropertyNamingPolicy?.ConvertName(name) ?? name;
            V R<V>(string name)
            {
                name = Name(name);
                read.Add(name);
                if (!properties.TryGetValue(name, out var value)) throw new JsonException($"Missing geometry member '{name}'.");
                return value.Deserialize<V>(options)!;
            }
            V Optional<V>(string name, V fallback)
            {
                var key = Name(name);
                read.Add(key);
                return properties.ContainsKey(key) ? R<V>(name) : fallback;
            }
            read.Add("kind");
            var kind = properties.TryGetValue("kind", out var tag) ? tag.GetString() : KindOf(typeof(T));
            // Raw construction is intentional here: callers validate after reading the whole document.
#pragma warning disable APP2D001
            IGeometryValue2D geometry = kind switch
            {
                ShapeKinds2D.Circle => new Circle2D(R<float>("Radius"), R<Vector2>("Center")),
                ShapeKinds2D.Ellipse => new Ellipse2D(R<Vector2>("Radii"), R<Vector2>("Center")),
                ShapeKinds2D.Capsule => new Capsule2D(R<Vector2>("Start"), R<Vector2>("End"), R<float>("Radius")),
                ShapeKinds2D.Rectangle => new Rectangle2D(R<Vector2>("Center"), R<Vector2>("Size") / 2f, Primitives.OrientedFrame2D.AxisFromAngle(Optional("AngleRadians", 0f))),
                ShapeKinds2D.AxisAlignedRectangle => new AxisAlignedRectangle2D(R<Vector2>("Min"), R<Vector2>("Max")),
                ShapeKinds2D.RoundedRectangle => new RoundedRectangle2D(R<Vector2>("Min"), R<Vector2>("Max"), R<float>("Radius")),
                ShapeKinds2D.Triangle => new Triangle2D(R<Vector2>("A"), R<Vector2>("B"), R<Vector2>("C")),
                ShapeKinds2D.ConvexPolygon => new ConvexPolygon2D(R<Vector2[]>("Vertices")),
                ShapeKinds2D.SimplePolygon => new SimplePolygon2D(R<Vector2[]>("Vertices")),
                ShapeKinds2D.HeightField => new HeightFieldShape2D(R<Vector2[]>("Surface"), R<float>("YMin")),
                ShapeKinds2D.HalfSpace => new HalfSpace2D(R<Vector2>("Normal"), R<float>("Offset")),
                ShapeKinds2D.Composite => new CompositeShape2D(R<IShape2D[]>("Parts"), Optional("IncludeOverlap", true), Optional("AreaOutlineSegments", ShapeArea2D.DefaultOutlineSegments)),
                CurveKinds2D.Line => new LineSegmentCurve2D(R<Vector2>("Start"), R<Vector2>("End")),
                CurveKinds2D.Polyline => new PolylineCurve2D(R<Vector2[]>("Points")),
                CurveKinds2D.Arc => new Arc2D(R<Vector2>("Center"), R<float>("Radius"), R<float>("StartAngleRadians"), R<float>("SweepAngleRadians")),
                CurveKinds2D.QuadraticBezier => new QuadraticBezier2D(R<Vector2>("Start"), R<Vector2>("Control"), R<Vector2>("End")),
                CurveKinds2D.CubicBezier => new CubicBezier2D(R<Vector2>("Start"), R<Vector2>("Control1"), R<Vector2>("Control2"), R<Vector2>("End")),
                CurveKinds2D.BSpline => new BSpline2D(R<Vector2[]>("ControlPoints"), Optional("Degree", 3)),
                _ => throw new JsonException($"Unknown geometry kind '{kind}'.")
            };
#pragma warning restore APP2D001
            if (options.UnmappedMemberHandling == JsonUnmappedMemberHandling.Disallow)
            {
                foreach (var name in properties.Keys)
                    if (!read.Contains(name)) throw new JsonException($"Unknown geometry member '{name}'.");
            }

            if (geometry is not T typed)
                throw new JsonException($"Geometry kind '{kind}' is not a {typeof(T).Name}.");
            return typed;
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            if (value is null) { writer.WriteNullValue(); return; }
            writer.WriteStartObject();
            var kind = KindOf(value.GetType()) ?? throw new NotSupportedException($"No geometry schema exists for {value.GetType().Name}.");
            writer.WriteString("kind", kind);
            void W<V>(string name, V item)
            {
                writer.WritePropertyName(options.PropertyNamingPolicy?.ConvertName(name) ?? name);
                JsonSerializer.Serialize(writer, item, options);
            }
            switch (value)
            {
                case Circle2D geometry:
                    W("Center", geometry.Center);
                    W("Radius", geometry.Radius);
                    break;
                case Ellipse2D geometry:
                    W("Center", geometry.Center);
                    W("Radii", geometry.Radii);
                    break;
                case Capsule2D geometry:
                    W("Start", geometry.Start);
                    W("End", geometry.End);
                    W("Radius", geometry.Radius);
                    break;
                case Rectangle2D geometry:
                    W("Center", geometry.Center);
                    W("Size", geometry.Size);
                    W("AngleRadians", geometry.AngleRadians);
                    break;
                case AxisAlignedRectangle2D geometry:
                    W("Min", geometry.Min);
                    W("Max", geometry.Max);
                    break;
                case RoundedRectangle2D geometry:
                    W("Min", geometry.Min);
                    W("Max", geometry.Max);
                    W("Radius", geometry.Radius);
                    break;
                case Triangle2D geometry:
                    W("A", geometry.A);
                    W("B", geometry.B);
                    W("C", geometry.C);
                    break;
                case ConvexPolygon2D geometry:
                    W("Vertices", geometry.Vertices.ToArray());
                    break;
                case SimplePolygon2D geometry:
                    W("Vertices", geometry.Vertices.ToArray());
                    break;
                case HeightFieldShape2D geometry:
                    W("Surface", geometry.Surface.ToArray());
                    W("YMin", geometry.YMin);
                    break;
                case HalfSpace2D geometry:
                    W("Normal", geometry.Normal);
                    W("Offset", geometry.Offset);
                    break;
                case CompositeShape2D geometry:
                    W("Parts", geometry.Parts.ToArray());
                    W("IncludeOverlap", geometry.IncludeOverlap);
                    W("AreaOutlineSegments", geometry.AreaOutlineSegments);
                    break;
                case LineSegmentCurve2D geometry:
                    W("Start", geometry.Start);
                    W("End", geometry.End);
                    break;
                case PolylineCurve2D geometry:
                    W("Points", geometry.Points.ToArray());
                    break;
                case Arc2D geometry:
                    W("Center", geometry.Center);
                    W("Radius", geometry.Radius);
                    W("StartAngleRadians", geometry.StartAngleRadians);
                    W("SweepAngleRadians", geometry.SweepAngleRadians);
                    break;
                case QuadraticBezier2D geometry:
                    W("Start", geometry.Start);
                    W("Control", geometry.Control);
                    W("End", geometry.End);
                    break;
                case CubicBezier2D geometry:
                    W("Start", geometry.Start);
                    W("Control1", geometry.Control1);
                    W("Control2", geometry.Control2);
                    W("End", geometry.End);
                    break;
                case BSpline2D geometry:
                    W("ControlPoints", geometry.ControlPoints.ToArray());
                    W("Degree", geometry.Degree);
                    break;
            }
            writer.WriteEndObject();
        }
    }

    private static string? KindOf(Type type) => type.Name switch
    {
        nameof(Circle2D) when type == typeof(Circle2D) => ShapeKinds2D.Circle,
        nameof(Ellipse2D) when type == typeof(Ellipse2D) => ShapeKinds2D.Ellipse,
        nameof(Capsule2D) when type == typeof(Capsule2D) => ShapeKinds2D.Capsule,
        nameof(Rectangle2D) when type == typeof(Rectangle2D) => ShapeKinds2D.Rectangle,
        nameof(AxisAlignedRectangle2D) when type == typeof(AxisAlignedRectangle2D) => ShapeKinds2D.AxisAlignedRectangle,
        nameof(RoundedRectangle2D) when type == typeof(RoundedRectangle2D) => ShapeKinds2D.RoundedRectangle,
        nameof(Triangle2D) when type == typeof(Triangle2D) => ShapeKinds2D.Triangle,
        nameof(ConvexPolygon2D) when type == typeof(ConvexPolygon2D) => ShapeKinds2D.ConvexPolygon,
        nameof(SimplePolygon2D) when type == typeof(SimplePolygon2D) => ShapeKinds2D.SimplePolygon,
        nameof(HeightFieldShape2D) when type == typeof(HeightFieldShape2D) => ShapeKinds2D.HeightField,
        nameof(HalfSpace2D) when type == typeof(HalfSpace2D) => ShapeKinds2D.HalfSpace,
        nameof(CompositeShape2D) when type == typeof(CompositeShape2D) => ShapeKinds2D.Composite,
        nameof(LineSegmentCurve2D) when type == typeof(LineSegmentCurve2D) => CurveKinds2D.Line,
        nameof(PolylineCurve2D) when type == typeof(PolylineCurve2D) => CurveKinds2D.Polyline,
        nameof(Arc2D) when type == typeof(Arc2D) => CurveKinds2D.Arc,
        nameof(QuadraticBezier2D) when type == typeof(QuadraticBezier2D) => CurveKinds2D.QuadraticBezier,
        nameof(CubicBezier2D) when type == typeof(CubicBezier2D) => CurveKinds2D.CubicBezier,
        nameof(BSpline2D) when type == typeof(BSpline2D) => CurveKinds2D.BSpline,
        _ => null
    };
}
