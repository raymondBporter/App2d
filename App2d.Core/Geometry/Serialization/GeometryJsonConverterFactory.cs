using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Measurements;
using App2d.Core.Geometry.Shapes;
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
        // Untagged concrete JSON already identifies its kind through the converter's target type.
        private static readonly GeometryKind? ConcreteKind = typeof(T) switch
        {
            var type when type == typeof(Circle2D) => GeometryKind.Circle,
            var type when type == typeof(Ellipse2D) => GeometryKind.Ellipse,
            var type when type == typeof(Capsule2) => GeometryKind.Capsule,
            var type when type == typeof(Rectangle2D) => GeometryKind.Rectangle,
            var type when type == typeof(AxisAlignedRectangle2D) => GeometryKind.AxisAlignedRectangle,
            var type when type == typeof(RoundedRectangle2D) => GeometryKind.RoundedRectangle,
            var type when type == typeof(Triangle2D) => GeometryKind.Triangle,
            var type when type == typeof(ConvexPolygon2D) => GeometryKind.ConvexPolygon,
            var type when type == typeof(SimplePolygon2D) => GeometryKind.SimplePolygon,
            var type when type == typeof(HalfSpace2D) => GeometryKind.HalfSpace,
            var type when type == typeof(CompositeShape2D) => GeometryKind.Composite,
            var type when type == typeof(LineSegmentCurve2D) => GeometryKind.LineSegment,
            var type when type == typeof(PolylineCurve2D) => GeometryKind.Polyline,
            var type when type == typeof(RoundedPolylineCurve2D) => GeometryKind.RoundedPolyline,
            var type when type == typeof(Arc2D) => GeometryKind.Arc,
            var type when type == typeof(QuadraticBezier2D) => GeometryKind.QuadraticBezier,
            var type when type == typeof(CubicBezier2D) => GeometryKind.CubicBezier,
            var type when type == typeof(BSpline2D) => GeometryKind.BSpline,
            _ => null
        };

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
            var kind = properties.TryGetValue("kind", out var tag) ? GeometryJson2D.ReadKind(tag) : ConcreteKind;
            // Raw construction is intentional here: callers validate after reading the whole document.
#pragma warning disable APP2D001
            // A rectangle is spelled with min and max (the axis-aligned form) or with center and size, never both.
            (Vector2f Center, Vector2f HalfSize) RectangleExtents()
            {
                var cornered = properties.ContainsKey(Name("Min")) || properties.ContainsKey(Name("Max"));
                var centered = properties.ContainsKey(Name("Center")) || properties.ContainsKey(Name("Size"));
                if (cornered && centered) throw new JsonException("A rectangle is spelled with min and max or with center and size, not both.");
                if (!cornered) return (R<Vector2f>("Center"), R<Vector2f>("Size") / 2f);
                var min = R<Vector2f>("Min");
                var max = R<Vector2f>("Max");
                return ((min + max) / 2f, (max - min) / 2f);
            }
            Rectangle2D ReadRectangle()
            {
                var (center, halfSize) = RectangleExtents();
                return new(center, halfSize, Primitives.OrientedFrame2D.AxisFromAngle(Optional("AngleRadians", 0f)));
            }
            RoundedRectangle2D ReadRoundedRectangle()
            {
                var (center, halfSize) = RectangleExtents();
                return new(center, halfSize, R<float>("Radius"), Primitives.OrientedFrame2D.AxisFromAngle(Optional("AngleRadians", 0f)));
            }
            IGeometryValue2D geometry = kind switch
            {
                GeometryKind.Circle => new Circle2D(R<float>("Radius"), R<Vector2f>("Center")),
                GeometryKind.Ellipse => new Ellipse2D(R<Vector2f>("Radii"), R<Vector2f>("Center"), Primitives.OrientedFrame2D.AxisFromAngle(Optional("AngleRadians", 0f))),
                GeometryKind.Capsule => new Capsule2(R<Vector2f>("Start"), R<Vector2f>("End"), R<float>("Radius")),
                GeometryKind.Rectangle => ReadRectangle(),
                GeometryKind.AxisAlignedRectangle => new AxisAlignedRectangle2D(R<Vector2f>("Min"), R<Vector2f>("Max")),
                GeometryKind.RoundedRectangle => ReadRoundedRectangle(),
                GeometryKind.Triangle => new Triangle2D(R<Vector2f>("A"), R<Vector2f>("B"), R<Vector2f>("C")),
                GeometryKind.ConvexPolygon => new ConvexPolygon2D(R<Vector2f[]>("Vertices")),
                GeometryKind.SimplePolygon => new SimplePolygon2D(R<Vector2f[]>("Vertices")),
                GeometryKind.HalfSpace => new HalfSpace2D(R<Vector2f>("Normal"), R<float>("Offset")),
                GeometryKind.Composite => new CompositeShape2D(R<IShape2D[]>("Parts"), Optional("IncludeOverlap", true), Optional("AreaOutlineSegments", ShapeArea2D.DefaultOutlineSegments)),
                GeometryKind.LineSegment => new LineSegmentCurve2D(R<Vector2f>("Start"), R<Vector2f>("End")),
                GeometryKind.Polyline => new PolylineCurve2D(R<Vector2f[]>("Points")),
                GeometryKind.RoundedPolyline => new RoundedPolylineCurve2D(R<Vector2f[]>("Points"), Optional("Softness", 1f)),
                GeometryKind.Arc => new Arc2D(R<Vector2f>("Center"), R<float>("Radius"), R<float>("StartAngleRadians"), R<float>("SweepAngleRadians")),
                GeometryKind.QuadraticBezier => new QuadraticBezier2D(R<Vector2f>("Start"), R<Vector2f>("Control"), R<Vector2f>("End")),
                GeometryKind.CubicBezier => new CubicBezier2D(R<Vector2f>("Start"), R<Vector2f>("Control1"), R<Vector2f>("Control2"), R<Vector2f>("End")),
                GeometryKind.BSpline => new BSpline2D(R<Vector2f[]>("ControlPoints"), Optional("Degree", 3)),
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
            if (typeof(T).IsInterface)
            {
                writer.WritePropertyName("kind");
                JsonSerializer.Serialize(writer, value.Kind);
            }
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
                    W("AngleRadians", geometry.AngleRadians);
                    break;
                case Capsule2 geometry:
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
                    W("Center", geometry.Center);
                    W("Size", geometry.Size);
                    W("Radius", geometry.Radius);
                    W("AngleRadians", geometry.AngleRadians);
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
                case RoundedPolylineCurve2D geometry:
                    W("Points", geometry.Points.ToArray());
                    W("Softness", geometry.Softness);
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
                default:
                    throw new NotSupportedException($"No geometry schema exists for {value.GetType().Name}.");
            }
            writer.WriteEndObject();
        }
    }
}
