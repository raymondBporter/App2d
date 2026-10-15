using App2d.Core.Geometry.Authoring;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Shapes;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.Core.Geometry.Builders;

/// <summary>
/// Editable, serializable shape builders. Build validates once before creating immutable runtime geometry.
/// Records retain editor with-expressions and the existing kind-tagged JSON fields.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(Circle2D.Builder), ShapeKinds2D.Circle)]
[JsonDerivedType(typeof(Ellipse2D.Builder), ShapeKinds2D.Ellipse)]
[JsonDerivedType(typeof(Capsule2D.Builder), ShapeKinds2D.Capsule)]
[JsonDerivedType(typeof(Rectangle2D.Builder), ShapeKinds2D.Rectangle)]
[JsonDerivedType(typeof(RoundedRectangle2D.Builder), ShapeKinds2D.RoundedRectangle)]
[JsonDerivedType(typeof(Triangle2D.Builder), ShapeKinds2D.Triangle)]
[JsonDerivedType(typeof(ConvexPolygon2D.Builder), ShapeKinds2D.ConvexPolygon)]
[JsonDerivedType(typeof(SimplePolygon2D.Builder), ShapeKinds2D.SimplePolygon)]
[JsonDerivedType(typeof(HalfSpace2D.Builder), ShapeKinds2D.HalfSpace)]
[JsonDerivedType(typeof(CompositeShape2D.Builder), ShapeKinds2D.Composite)]
public abstract record ShapeBuilder2D : GeometryDefinition2D
{
    /// <summary>The kind tag this definition serializes under; one of <see cref="ShapeKinds2D"/>.</summary>
    [JsonIgnore]
    public abstract override string Kind
    {
        get;
    }

    /// <summary>Creates the runtime shape, validating its geometry.</summary>
    /// <returns>A new immutable shape.</returns>
    public abstract IShape2D Build();

    /// <summary>Validates this editable builder without creating a runtime shape.</summary>
    public abstract void Validate();

    /// <summary>Serializes this definition with its kind tag.</summary>
    /// <returns>The JSON text.</returns>
    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>Reads a tagged definition.</summary>
    /// <param name="json">JSON produced by <see cref="ToJson"/> or written by hand with a kind tag.</param>
    /// <returns>The definition.</returns>
    /// <exception cref="JsonException">The kind is unknown, a required member is missing or an unknown member is present.</exception>
    public new static ShapeBuilder2D FromJson(string json) => JsonSerializer.Deserialize<ShapeBuilder2D>(json, JsonOptions) ?? throw new JsonException("Empty shape definition.");

    /// <summary>Copies a built-in runtime shape into editable data. Arbitrary implementations have no known schema.</summary>
    /// <param name="shape">A built-in shape.</param>
    /// <returns>A definition that builds an equivalent shape.</returns>
    /// <exception cref="NotSupportedException">The shape is a custom implementation.</exception>
    public new static ShapeBuilder2D FromShape(IShape2D shape) => shape switch
    {
        Circle2D circle => new Circle2D.Builder { Center = circle.Center, Radius = circle.Radius },
        Ellipse2D ellipse => new Ellipse2D.Builder { Center = ellipse.Center, Radii = ellipse.Radii },
        Capsule2D capsule => new Capsule2D.Builder { Start = capsule.Start, End = capsule.End, Radius = capsule.Radius },
        AxisAlignedRectangle2D rectangle => new Rectangle2D.Builder { Min = rectangle.Min, Max = rectangle.Max, AxisAligned = true },
        Rectangle2D rectangle => new Rectangle2D.Builder { Min = rectangle.Min, Max = rectangle.Max },
        RoundedRectangle2D rectangle => new RoundedRectangle2D.Builder { Min = rectangle.OuterRect.Min, Max = rectangle.OuterRect.Max, Radius = rectangle.Radius },
        Triangle2D triangle => new Triangle2D.Builder { A = triangle.A, B = triangle.B, C = triangle.C },
        ConvexPolygon2D polygon => new ConvexPolygon2D.Builder { Vertices = [.. polygon.Vertices] },
        SimplePolygon2D polygon => new SimplePolygon2D.Builder { Vertices = [.. polygon.Vertices] },
        HalfSpace2D halfSpace => new HalfSpace2D.Builder { Normal = halfSpace.Normal, Offset = halfSpace.Offset },
        CompositeShape2D composite => new CompositeShape2D.Builder
        {
            Parts = [.. composite.Parts.ToArray().Select(FromShape)],
            IncludeOverlap = composite.IncludeOverlap,
            AreaOutlineSegments = composite.AreaOutlineSegments
        },
        null => throw new ArgumentNullException(nameof(shape)),
        _ => throw new NotSupportedException($"No shape definition exists for {shape.GetType().Name}.")
    };

}
