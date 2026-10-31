using App2d.Core.Geometry;
using App2d.Core.Geometry.Operations.Bounds;
using App2d.Core.Geometry.Operations.Distance;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Json;
using App2d.Core.Shapes;
using System.Numerics;
using System.Text.Json;

namespace App2d.Tests.Geometry;

public sealed class ShapeSerialization2DTests
{
    public static TheoryData<string, string> BuiltInShapes => new()
    {
        { nameof(Circle2D), ShapeKinds2D.Circle },
        { nameof(Ellipse2D), ShapeKinds2D.Ellipse },
        { nameof(Capsule2D), ShapeKinds2D.Capsule },
        { nameof(Rectangle2D), ShapeKinds2D.Rectangle },
        { nameof(RoundedRectangle2D), ShapeKinds2D.RoundedRectangle },
        { nameof(AxisAlignedRectangle2D), ShapeKinds2D.AxisAlignedRectangle },
        { nameof(Triangle2D), ShapeKinds2D.Triangle },
        { nameof(ConvexPolygon2D), ShapeKinds2D.ConvexPolygon },
        { nameof(SimplePolygon2D), ShapeKinds2D.SimplePolygon },
        { nameof(HalfSpace2D), ShapeKinds2D.HalfSpace },
        { nameof(CompositeShape2D), ShapeKinds2D.Composite }
    };

    private static IShape2D CreateShape(string shapeName) => shapeName switch
    {
        nameof(Circle2D) => Circle2D.Builder.Create(1.5f, new(1, -2)),
        nameof(Ellipse2D) => Ellipse2D.Builder.Create(new(2, 1), new(-1, .5f)),
        nameof(Capsule2D) => Capsule2D.Builder.Create(new(-1, 0), new(1, .5f), .3f),
        nameof(Rectangle2D) => Rectangle2D.Builder.Create(new(-1, -2), new(3, 1)),
        nameof(RoundedRectangle2D) => RoundedRectangle2D.Builder.Create(new(-2, -1), new(2, 1), .4f),
        nameof(AxisAlignedRectangle2D) => AxisAlignedRectangle2D.Builder.Create(new(-1, -2), new(3, 1)),
        nameof(Triangle2D) => Triangle2D.Builder.Create(new(0, 0), new(2, 0), new(1, 3)),
        nameof(ConvexPolygon2D) => ConvexPolygon2D.Builder.Create([new(0, 0), new(2, 0), new(3, 1), new(1, 2)]),
        nameof(SimplePolygon2D) => SimplePolygon2D.Builder.Create([new(0, 0), new(3, 0), new(1, 1), new(0, 3)]),
        nameof(HalfSpace2D) => HalfSpace2D.Builder.Create(new(1, 1), 2),
        nameof(CompositeShape2D) => CompositeShape2D.Builder.Create([Circle2D.Builder.Create(1, new(-3, 0)), Rectangle2D.Builder.Create(new(1, -1), new(4, 1))]),
        _ => throw new ArgumentOutOfRangeException(nameof(shapeName), shapeName, null)
    };

    [Theory]
    [MemberData(nameof(BuiltInShapes))]
    public void RuntimeShapesAreReadonlyValuesAndBuildersKeepTheirRuntimeType(string shapeName, string kind)
    {
        var shape = CreateShape(shapeName);
        var type = shape.GetType();
        Assert.True(type.IsValueType);
        Assert.True(type.IsDefined(typeof(System.Runtime.CompilerServices.IsReadOnlyAttribute), inherit: false));
        Assert.Equal(kind, shape.Kind);
        Assert.Equal(type, shape.Validated().GetType());
    }

    [Fact]
    public void ScalarShapesHaveValueEqualityAndTypedBuildersMatchPolymorphicBuilders()
    {
        var builder = new Circle2D { Center = new(1, 2), Radius = 3 }.Validated();
        Circle2D circle = builder.Validated();
        IShape2D shapeBuilder = builder;
        IConvexShape2D convexBuilder = builder;
        Assert.Equal(circle, Circle2D.Builder.Create(3, new(1, 2)));
        Assert.True(circle == Assert.IsType<Circle2D>(shapeBuilder.Validated()));
        Assert.True(circle == Assert.IsType<Circle2D>(convexBuilder.Validated()));
        Assert.NotEqual(circle, Circle2D.Builder.Create(4, new(1, 2)));
    }

    [Theory]
    [MemberData(nameof(BuiltInShapes))]
    public void TaggedValuesRoundTripIntoEquivalentRuntimeShapes(string shapeName, string kind)
    {
        var original = CreateShape(shapeName);
        var json = original.ToJson();
        using var document = JsonDocument.Parse(json);
        Assert.Equal(kind, original.Kind);
        Assert.Equal(kind, document.RootElement.GetProperty("kind").GetString());
        var restored = GeometryJson2D.ShapeFromJson(json).Validated();
        Assert.Equal(original.GetType(), restored.GetType());
        Assert.Equal(original.Area, restored.Area);
        Assert.Equal(ShapeBounds2D.Calculate(original), ShapeBounds2D.Calculate(restored));
        foreach (var point in new Vector2[] { new(0, 0), new(1.3f, -.7f), new(-2, 2), new(5, 5), new(.5f, .5f) })
        {
            Assert.Equal(original.ContainsPoint(point), restored.ContainsPoint(point));
            Assert.Equal(ShapeDistance2D.Distance(point, original), ShapeDistance2D.Distance(point, restored), 5);
        }
    }

    [Fact]
    public void AxisAlignmentAndCompositePartsSurviveTheRoundTrip()
    {
        var rectangle = Assert.IsType<AxisAlignedRectangle2D>(GeometryJson2D.ShapeFromJson(AxisAlignedRectangle2D.Builder.Create(new(0, 0), new(1, 1)).ToJson()));
        Assert.Equal(ShapeKinds2D.AxisAlignedRectangle, rectangle.Kind);
        Assert.IsType<AxisAlignedRectangle2D>(rectangle.Validated());
        Assert.IsType<Rectangle2D>(GeometryJson2D.ShapeFromJson(Rectangle2D.Builder.Create(new(0, 0), new(1, 1)).ToJson()).Validated());

        var composite = Assert.IsType<CompositeShape2D>(GeometryJson2D.ShapeFromJson(CompositeShape2D.Builder.Create([Circle2D.Builder.Create(1), Triangle2D.Builder.Create(new(0, 0), new(1, 0), new(0, 1)), AxisAlignedRectangle2D.Builder.Create(Vector2.Zero, Vector2.One)]).ToJson()));
        Assert.Collection(composite.Parts.ToArray(), part => Assert.IsType<Circle2D>(part), part => Assert.IsType<Triangle2D>(part), part => Assert.IsType<AxisAlignedRectangle2D>(part));
        Assert.IsType<AxisAlignedRectangle2D>(composite.Validated().Parts[2]);
    }

    private sealed class ShapeEnvelope
    {
        public required IShape2D Hit { get; init; }
    }

    [Fact]
    public void GeometryWorksInsideExistingAuthoredJsonOptions()
    {
        var envelope = new ShapeEnvelope { Hit = new Capsule2D { Start = new(0, 0), End = new(2, 0), Radius = .5f }.Validated() };
        var json = JsonSerializer.Serialize(envelope, AuthoredJson.Options);
        Assert.Contains("\"kind\": \"capsule\"", json);
        Assert.Contains("\"radius\": 0.5", json);
        var restored = JsonSerializer.Deserialize<ShapeEnvelope>(json, AuthoredJson.Options)!;
        Assert.Equal(.5f, Assert.IsType<Capsule2D>(restored.Hit.Validated()).Radius);
    }

    [Fact]
    public void HandWrittenJsonUsesCamelCaseAndTheKindTagInAnyPosition()
    {
        var circle = Assert.IsType<Circle2D>(GeometryJson2D.ShapeFromJson("{\"center\":{\"x\":1,\"y\":2},\"radius\":3,\"kind\":\"circle\"}").Validated());
        Assert.Equal(new Vector2(1, 2), circle.Center);
        Assert.Equal(3f, circle.Radius);
        Assert.Equal(12, ShapeKinds2D.All.Count);
    }

    [Fact]
    public void UnknownKindsMissingFieldsAndMisspelledFieldsAreRejected()
    {
        Assert.Throws<JsonException>(() => GeometryJson2D.ShapeFromJson("{\"kind\":\"blob\"}"));
        Assert.Throws<JsonException>(() => GeometryJson2D.ShapeFromJson("{\"kind\":\"circle\",\"center\":{\"x\":0,\"y\":0}}"));
        Assert.Throws<JsonException>(() => GeometryJson2D.ShapeFromJson("{\"kind\":\"circle\",\"center\":{\"x\":0,\"y\":0},\"radius\":1,\"radiuss\":2}"));
    }

    [Fact]
    public void InvalidGeometryFailsWhenValidatedNotWhenRead()
    {
        var zeroRadius = GeometryJson2D.ShapeFromJson("{\"kind\":\"circle\",\"center\":{\"x\":0,\"y\":0},\"radius\":0}");
        Assert.Throws<ArgumentOutOfRangeException>(zeroRadius.Validate);
        var concave = GeometryJson2D.ShapeFromJson("{\"kind\":\"convex-polygon\",\"vertices\":[{\"x\":0,\"y\":0},{\"x\":2,\"y\":0},{\"x\":1,\"y\":0.5},{\"x\":2,\"y\":2},{\"x\":0,\"y\":2}]}");
        Assert.Throws<ArgumentException>(concave.Validate);
        var backwards = GeometryJson2D.ShapeFromJson("{\"kind\":\"rectangle\",\"min\":{\"x\":1,\"y\":1},\"max\":{\"x\":0,\"y\":0}}");
        Assert.ThrowsAny<ArgumentException>(backwards.Validate);
        var halfSpacePart = GeometryJson2D.ShapeFromJson("{\"kind\":\"composite\",\"parts\":[{\"kind\":\"half-space\",\"normal\":{\"x\":0,\"y\":1},\"offset\":0}]}");
        Assert.Throws<InvalidOperationException>(halfSpacePart.Validate);
    }

    [Fact]
    public void CustomShapesNeedAnExplicitDefinition()
    {
        Assert.Throws<NotSupportedException>(() => new CustomShape().ToJson());
        Assert.Throws<ArgumentNullException>(() => GeometryJson2D.ToJson(null!));
    }

    private sealed class CustomShape : IShape2D
    {
        public string Kind => "custom";
        public float Area => 1;
        public bool ContainsPoint(Vector2 point) => false;
    }
}
