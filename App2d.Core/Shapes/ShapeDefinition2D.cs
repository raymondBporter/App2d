using App2d.Core.Geometry;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.Core.Shapes;

/// <summary>
/// Editable, tagged data for the built-in shapes, mirroring <see cref="Curves.CurveDefinition2D"/>. Runtime shapes stay
/// independent of JSON; <see cref="Build"/> validates through the shape constructors. Definitions are records, so editors
/// change them with <c>with</c> expressions. Custom <see cref="IShape2D"/> implementations need their own definition.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(CircleShapeDefinition2D), ShapeKinds2D.Circle)]
[JsonDerivedType(typeof(EllipseShapeDefinition2D), ShapeKinds2D.Ellipse)]
[JsonDerivedType(typeof(CapsuleShapeDefinition2D), ShapeKinds2D.Capsule)]
[JsonDerivedType(typeof(RectangleShapeDefinition2D), ShapeKinds2D.Rectangle)]
[JsonDerivedType(typeof(TriangleShapeDefinition2D), ShapeKinds2D.Triangle)]
[JsonDerivedType(typeof(ConvexPolygonShapeDefinition2D), ShapeKinds2D.ConvexPolygon)]
[JsonDerivedType(typeof(SimplePolygonShapeDefinition2D), ShapeKinds2D.SimplePolygon)]
[JsonDerivedType(typeof(HalfSpaceShapeDefinition2D), ShapeKinds2D.HalfSpace)]
[JsonDerivedType(typeof(CompositeShapeDefinition2D), ShapeKinds2D.Composite)]
public abstract record ShapeDefinition2D : GeometryDefinition2D
{
    /// <summary>Web-style camelCase JSON with an indented layout, strict members and a leading or trailing kind tag.</summary>
    public new static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowOutOfOrderMetadataProperties = true
    };

    /// <summary>The kind tag this definition serializes under; one of <see cref="ShapeKinds2D"/>.</summary>
    [JsonIgnore] public abstract string Kind { get; }

    /// <summary>Creates the runtime shape, validating its geometry.</summary>
    /// <returns>A new immutable shape.</returns>
    public abstract IShape2D Build();

    /// <summary>Serializes this definition with its kind tag.</summary>
    /// <returns>The JSON text.</returns>
    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>Reads a tagged definition.</summary>
    /// <param name="json">JSON produced by <see cref="ToJson"/> or written by hand with a kind tag.</param>
    /// <returns>The definition.</returns>
    /// <exception cref="JsonException">The kind is unknown, a required member is missing or an unknown member is present.</exception>
    public new static ShapeDefinition2D FromJson(string json) => JsonSerializer.Deserialize<ShapeDefinition2D>(json, JsonOptions) ?? throw new JsonException("Empty shape definition.");

    /// <summary>Copies a built-in runtime shape into editable data. Arbitrary implementations have no known schema.</summary>
    /// <param name="shape">A built-in shape.</param>
    /// <returns>A definition that builds an equivalent shape.</returns>
    /// <exception cref="NotSupportedException">The shape is a custom implementation.</exception>
    public new static ShapeDefinition2D FromShape(IShape2D shape) => shape switch
    {
        Circle2D circle => new CircleShapeDefinition2D { Center = Point2D.From(circle.Center), Radius = circle.Radius },
        Ellipse2D ellipse => new EllipseShapeDefinition2D { Center = Point2D.From(ellipse.Center), Radii = Point2D.From(ellipse.Radii) },
        Capsule2D capsule => new CapsuleShapeDefinition2D { Start = Point2D.From(capsule.Start), End = Point2D.From(capsule.End), Radius = capsule.Radius },
        AxisAlignedRectangle2D rectangle => new RectangleShapeDefinition2D { Min = Point2D.From(rectangle.Min), Max = Point2D.From(rectangle.Max), AxisAligned = true },
        Rectangle2D rectangle => new RectangleShapeDefinition2D { Min = Point2D.From(rectangle.Min), Max = Point2D.From(rectangle.Max) },
        Triangle2D triangle => new TriangleShapeDefinition2D { A = Point2D.From(triangle.A), B = Point2D.From(triangle.B), C = Point2D.From(triangle.C) },
        ConvexPolygon2D polygon => new ConvexPolygonShapeDefinition2D { Vertices = [.. polygon.Vertices.ToArray().Select(Point2D.From)] },
        SimplePolygon2D polygon => new SimplePolygonShapeDefinition2D { Vertices = [.. polygon.Vertices.ToArray().Select(Point2D.From)] },
        HalfSpace2D halfSpace => new HalfSpaceShapeDefinition2D { Normal = Point2D.From(halfSpace.Normal), Offset = halfSpace.Offset },
        CompositeShape2D composite => new CompositeShapeDefinition2D { Parts = [.. composite.Parts.ToArray().Select(FromShape)] },
        null => throw new ArgumentNullException(nameof(shape)),
        _ => throw new NotSupportedException($"No shape definition exists for {shape.GetType().Name}.")
    };

    /// <summary>
    /// Re-authors this definition as another kind fitted to the same bounds, for editors that switch kinds: a circle
    /// takes the smaller extent, a capsule runs along the longer one, and triangles and polygons take the box corners.
    /// </summary>
    /// <param name="kind">One of <see cref="ShapeKinds2D"/>.</param>
    /// <returns>This definition when the kind already matches, otherwise a new definition of that kind.</returns>
    /// <exception cref="ArgumentException">The kind is unknown.</exception>
    /// <exception cref="InvalidOperationException">This definition has no finite bounds to fit.</exception>
    public ShapeDefinition2D WithKind(string kind)
    {
        if (kind == Kind) return this;
        var bounds = ShapeBounds2D.Calculate(Build());
        if (!bounds.IsFinite) throw new InvalidOperationException($"A {Kind} has no finite bounds to re-author from.");
        var center = bounds.Center;
        var size = bounds.Size;
        var reach = MathF.Abs(size.X - size.Y) / 2f;
        return kind switch
        {
            ShapeKinds2D.Rectangle => RectangleShapeDefinition2D.FromSize(size, center),
            ShapeKinds2D.Circle => new CircleShapeDefinition2D { Center = Point2D.From(center), Radius = MathF.Min(size.X, size.Y) / 2f },
            ShapeKinds2D.Ellipse => new EllipseShapeDefinition2D { Center = Point2D.From(center), Radii = Point2D.From(size / 2f) },
            ShapeKinds2D.Capsule => size.X >= size.Y
                ? new CapsuleShapeDefinition2D { Start = Point2D.From(center - new Vector2(reach, 0f)), End = Point2D.From(center + new Vector2(reach, 0f)), Radius = size.Y / 2f }
                : new CapsuleShapeDefinition2D { Start = Point2D.From(center - new Vector2(0f, reach)), End = Point2D.From(center + new Vector2(0f, reach)), Radius = size.X / 2f },
            ShapeKinds2D.Triangle => new TriangleShapeDefinition2D { A = Point2D.From(bounds.BottomLeft), B = Point2D.From(bounds.BottomRight), C = Point2D.From(bounds.TopCenter) },
            ShapeKinds2D.ConvexPolygon => new ConvexPolygonShapeDefinition2D { Vertices = [Point2D.From(bounds.BottomLeft), Point2D.From(bounds.BottomRight), Point2D.From(bounds.TopRight), Point2D.From(bounds.TopLeft)] },
            ShapeKinds2D.SimplePolygon => new SimplePolygonShapeDefinition2D { Vertices = [Point2D.From(bounds.BottomLeft), Point2D.From(bounds.BottomRight), Point2D.From(bounds.TopRight), Point2D.From(bounds.TopLeft)] },
            ShapeKinds2D.HalfSpace => new HalfSpaceShapeDefinition2D { Normal = new(0f, 1f), Offset = bounds.Top },
            ShapeKinds2D.Composite => new CompositeShapeDefinition2D { Parts = [this] },
            _ => throw new ArgumentException($"Unknown shape kind '{kind}'.", nameof(kind))
        };
    }
}

/// <summary>A circle: <see cref="Circle2D"/>.</summary>
public sealed record CircleShapeDefinition2D : ShapeDefinition2D
{
    public required Point2D Center { get; init; }
    public required float Radius { get; init; }
    [JsonIgnore] public override string Kind => ShapeKinds2D.Circle;
    public override IShape2D Build() => new Circle2D(Radius, Center.Vector);
}

/// <summary>An axis-aligned ellipse: <see cref="Ellipse2D"/>.</summary>
public sealed record EllipseShapeDefinition2D : ShapeDefinition2D
{
    public required Point2D Center { get; init; }
    public required Point2D Radii { get; init; }
    [JsonIgnore] public override string Kind => ShapeKinds2D.Ellipse;
    public override IShape2D Build() => new Ellipse2D(Radii.Vector, Center.Vector);
}

/// <summary>A capsule: <see cref="Capsule2D"/>.</summary>
public sealed record CapsuleShapeDefinition2D : ShapeDefinition2D
{
    public required Point2D Start { get; init; }
    public required Point2D End { get; init; }
    public required float Radius { get; init; }
    [JsonIgnore] public override string Kind => ShapeKinds2D.Capsule;
    public override IShape2D Build() => new Capsule2D(Start.Vector, End.Vector, Radius);
}

/// <summary>A rectangle: <see cref="Rectangle2D"/>, or <see cref="AxisAlignedRectangle2D"/> when <see cref="AxisAligned"/> is set.</summary>
public sealed record RectangleShapeDefinition2D : ShapeDefinition2D
{
    public required Point2D Min { get; init; }
    public required Point2D Max { get; init; }
    /// <summary>Marks a rectangle its owner keeps axis-aligned in world space.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool AxisAligned { get; init; }
    [JsonIgnore] public override string Kind => ShapeKinds2D.Rectangle;
    public override IShape2D Build() => AxisAligned ? new AxisAlignedRectangle2D(Min.Vector, Max.Vector) : new Rectangle2D(Min.Vector, Max.Vector);

    /// <summary>A rectangle of a given size around a center.</summary>
    /// <param name="size">The width and height.</param>
    /// <param name="center">The center point.</param>
    /// <returns>The definition spanning half the size on each side of the center.</returns>
    public static RectangleShapeDefinition2D FromSize(Vector2 size, Vector2 center = default) => new() { Min = Point2D.From(center - size / 2f), Max = Point2D.From(center + size / 2f) };
}

/// <summary>A triangle: <see cref="Triangle2D"/>.</summary>
public sealed record TriangleShapeDefinition2D : ShapeDefinition2D
{
    public required Point2D A { get; init; }
    public required Point2D B { get; init; }
    public required Point2D C { get; init; }
    [JsonIgnore] public override string Kind => ShapeKinds2D.Triangle;
    public override IShape2D Build() => new Triangle2D(A.Vector, B.Vector, C.Vector);
}

/// <summary>A convex polygon in perimeter order: <see cref="ConvexPolygon2D"/>.</summary>
public sealed record ConvexPolygonShapeDefinition2D : ShapeDefinition2D
{
    public required List<Point2D> Vertices { get; init; }
    [JsonIgnore] public override string Kind => ShapeKinds2D.ConvexPolygon;
    public override IShape2D Build() => new ConvexPolygon2D(Vertices.Select(point => point.Vector));
}

/// <summary>A simple polygon that may be concave.</summary>
public sealed record SimplePolygonShapeDefinition2D : ShapeDefinition2D
{
    public required List<Point2D> Vertices { get; init; }
    [JsonIgnore] public override string Kind => ShapeKinds2D.SimplePolygon;
    public override IShape2D Build() => new SimplePolygon2D(Vertices.Select(point => point.Vector));
}

/// <summary>A half-space whose solid side is dot(point, Normal) &lt;= Offset: <see cref="HalfSpace2D"/>.</summary>
public sealed record HalfSpaceShapeDefinition2D : ShapeDefinition2D
{
    public required Point2D Normal { get; init; }
    public required float Offset { get; init; }
    [JsonIgnore] public override string Kind => ShapeKinds2D.HalfSpace;
    public override IShape2D Build() => new HalfSpace2D(Normal.Vector, Offset);
}

/// <summary>A union of convex parts: <see cref="CompositeShape2D"/>.</summary>
public sealed record CompositeShapeDefinition2D : ShapeDefinition2D
{
    public required List<ShapeDefinition2D> Parts { get; init; }
    [JsonIgnore] public override string Kind => ShapeKinds2D.Composite;
    public override IShape2D Build() => new CompositeShape2D(Parts.Select(part => part.Build() as IConvexShape2D ?? throw new InvalidOperationException($"Composite parts must be convex; a {part.Kind} is not.")));
}
