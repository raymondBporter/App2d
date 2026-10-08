using App2d.Core.Geometry;
using App2d.Core.IO;
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
[JsonDerivedType(typeof(RoundedRectangleShapeDefinition2D), ShapeKinds2D.RoundedRectangle)]
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
        AllowOutOfOrderMetadataProperties = true,
        Converters = { new Vector2JsonConverter() }
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
        Circle2D circle => new CircleShapeDefinition2D { Center = circle.Center, Radius = circle.Radius },
        Ellipse2D ellipse => new EllipseShapeDefinition2D { Center = ellipse.Center, Radii = ellipse.Radii },
        Capsule2D capsule => new CapsuleShapeDefinition2D { Start = capsule.Start, End = capsule.End, Radius = capsule.Radius },
        AxisAlignedRectangle2D rectangle => new RectangleShapeDefinition2D { Min = rectangle.Min, Max = rectangle.Max, AxisAligned = true },
        Rectangle2D rectangle => new RectangleShapeDefinition2D { Min = rectangle.Min, Max = rectangle.Max },
        RoundedRectangle2D rectangle => new RoundedRectangleShapeDefinition2D { Min = rectangle.Min, Max = rectangle.Max, Radius = rectangle.Radius },
        Triangle2D triangle => new TriangleShapeDefinition2D { A = triangle.A, B = triangle.B, C = triangle.C },
        ConvexPolygon2D polygon => new ConvexPolygonShapeDefinition2D { Vertices = [.. polygon.Vertices] },
        SimplePolygon2D polygon => new SimplePolygonShapeDefinition2D { Vertices = [.. polygon.Vertices] },
        HalfSpace2D halfSpace => new HalfSpaceShapeDefinition2D { Normal = halfSpace.Normal, Offset = halfSpace.Offset },
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
            ShapeKinds2D.RoundedRectangle => RoundedRectangleShapeDefinition2D.FromSize(size, MathF.Min(size.X, size.Y) * .1f, center),
            ShapeKinds2D.Circle => new CircleShapeDefinition2D { Center = center, Radius = MathF.Min(size.X, size.Y) / 2f },
            ShapeKinds2D.Ellipse => new EllipseShapeDefinition2D { Center = center, Radii = size / 2f },
            ShapeKinds2D.Capsule => size.X >= size.Y
                ? new CapsuleShapeDefinition2D { Start = center - new Vector2(reach, 0f), End = center + new Vector2(reach, 0f), Radius = size.Y / 2f }
                : new CapsuleShapeDefinition2D { Start = center - new Vector2(0f, reach), End = center + new Vector2(0f, reach), Radius = size.X / 2f },
            ShapeKinds2D.Triangle => new TriangleShapeDefinition2D { A = bounds.BottomLeft, B = bounds.BottomRight, C = bounds.TopCenter },
            ShapeKinds2D.ConvexPolygon => new ConvexPolygonShapeDefinition2D { Vertices = [bounds.BottomLeft, bounds.BottomRight, bounds.TopRight, bounds.TopLeft] },
            ShapeKinds2D.SimplePolygon => new SimplePolygonShapeDefinition2D { Vertices = [bounds.BottomLeft, bounds.BottomRight, bounds.TopRight, bounds.TopLeft] },
            ShapeKinds2D.HalfSpace => new HalfSpaceShapeDefinition2D { Normal = new(0f, 1f), Offset = bounds.Top },
            ShapeKinds2D.Composite => new CompositeShapeDefinition2D { Parts = [this] },
            _ => throw new ArgumentException($"Unknown shape kind '{kind}'.", nameof(kind))
        };
    }
}

/// <summary>A circle: <see cref="Circle2D"/>.</summary>
public sealed record CircleShapeDefinition2D : ShapeDefinition2D
{
    public required Vector2 Center { get; init; }
    public required float Radius { get; init; }
    [JsonIgnore] public override string Kind => ShapeKinds2D.Circle;
    public override IShape2D Build() => new Circle2D(Radius, Center);
}

/// <summary>An axis-aligned ellipse: <see cref="Ellipse2D"/>.</summary>
public sealed record EllipseShapeDefinition2D : ShapeDefinition2D
{
    public required Vector2 Center { get; init; }
    public required Vector2 Radii { get; init; }
    [JsonIgnore] public override string Kind => ShapeKinds2D.Ellipse;
    public override IShape2D Build() => new Ellipse2D(Radii, Center);
}

/// <summary>A capsule: <see cref="Capsule2D"/>.</summary>
public sealed record CapsuleShapeDefinition2D : ShapeDefinition2D
{
    public required Vector2 Start { get; init; }
    public required Vector2 End { get; init; }
    public required float Radius { get; init; }
    [JsonIgnore] public override string Kind => ShapeKinds2D.Capsule;
    public override IShape2D Build() => new Capsule2D(Start, End, Radius);
}

/// <summary>A rectangle: <see cref="Rectangle2D"/>, or <see cref="AxisAlignedRectangle2D"/> when <see cref="AxisAligned"/> is set.</summary>
public sealed record RectangleShapeDefinition2D : ShapeDefinition2D
{
    public required Vector2 Min { get; init; }
    public required Vector2 Max { get; init; }
    /// <summary>Marks a rectangle its owner keeps axis-aligned in world space.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool AxisAligned { get; init; }
    [JsonIgnore] public override string Kind => ShapeKinds2D.Rectangle;
    public override IShape2D Build() => AxisAligned ? new AxisAlignedRectangle2D(Min, Max) : new Rectangle2D(Min, Max);

    /// <summary>A rectangle of a given size around a center.</summary>
    /// <param name="size">The width and height.</param>
    /// <param name="center">The center point.</param>
    /// <returns>The definition spanning half the size on each side of the center.</returns>
    public static RectangleShapeDefinition2D FromSize(Vector2 size, Vector2 center = default) => new() { Min = center - size / 2f, Max = center + size / 2f };
}

/// <summary>A rectangle with circular corners: <see cref="RoundedRectangle2D"/>.</summary>
public sealed record RoundedRectangleShapeDefinition2D : ShapeDefinition2D
{
    public required Vector2 Min { get; init; }
    public required Vector2 Max { get; init; }
    public required float Radius { get; init; }
    [JsonIgnore] public override string Kind => ShapeKinds2D.RoundedRectangle;
    public override IShape2D Build() => new RoundedRectangle2D(Min, Max, Radius);

    public static RoundedRectangleShapeDefinition2D FromSize(Vector2 size, float radius, Vector2 center = default) =>
        new() { Min = center - size / 2f, Max = center + size / 2f, Radius = radius };
}

/// <summary>A triangle: <see cref="Triangle2D"/>.</summary>
public sealed record TriangleShapeDefinition2D : ShapeDefinition2D
{
    public required Vector2 A { get; init; }
    public required Vector2 B { get; init; }
    public required Vector2 C { get; init; }
    [JsonIgnore] public override string Kind => ShapeKinds2D.Triangle;
    public override IShape2D Build() => new Triangle2D(A, B, C);
}

/// <summary>A convex polygon in perimeter order: <see cref="ConvexPolygon2D"/>.</summary>
public sealed record ConvexPolygonShapeDefinition2D : ShapeDefinition2D
{
    public required List<Vector2> Vertices { get; init; }
    [JsonIgnore] public override string Kind => ShapeKinds2D.ConvexPolygon;
    public override IShape2D Build() => new ConvexPolygon2D(Vertices);
}

/// <summary>A simple polygon that may be concave.</summary>
public sealed record SimplePolygonShapeDefinition2D : ShapeDefinition2D
{
    public required List<Vector2> Vertices { get; init; }
    [JsonIgnore] public override string Kind => ShapeKinds2D.SimplePolygon;
    public override IShape2D Build() => new SimplePolygon2D(Vertices);
}

/// <summary>A half-space whose solid side is dot(point, Normal) &lt;= Offset: <see cref="HalfSpace2D"/>.</summary>
public sealed record HalfSpaceShapeDefinition2D : ShapeDefinition2D
{
    public required Vector2 Normal { get; init; }
    public required float Offset { get; init; }
    [JsonIgnore] public override string Kind => ShapeKinds2D.HalfSpace;
    public override IShape2D Build() => new HalfSpace2D(Normal, Offset);
}

/// <summary>A union of convex parts: <see cref="CompositeShape2D"/>.</summary>
public sealed record CompositeShapeDefinition2D : ShapeDefinition2D
{
    public required List<ShapeDefinition2D> Parts { get; init; }
    [JsonIgnore] public override string Kind => ShapeKinds2D.Composite;
    public override IShape2D Build() => new CompositeShape2D(Parts.Select(part => part.Build() as IConvexShape2D ?? throw new InvalidOperationException($"Composite parts must be convex; a {part.Kind} is not.")));
}
