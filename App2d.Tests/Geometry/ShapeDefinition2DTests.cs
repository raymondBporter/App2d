using App2d.Core.Characters;
using App2d.Core.Shapes;
using System.Numerics;
using System.Text.Json;

namespace App2d.Tests.Geometry;

public sealed class ShapeDefinition2DTests
{
    public static TheoryData<IShape2D, string> BuiltInShapes => new()
    {
        { new Circle2D(1.5f, new(1, -2)), ShapeKinds2D.Circle },
        { new Ellipse2D(new(2, 1), new(-1, .5f)), ShapeKinds2D.Ellipse },
        { new Capsule2D(new(-1, 0), new(1, .5f), .3f), ShapeKinds2D.Capsule },
        { new Rectangle2D(new(-1, -2), new(3, 1)), ShapeKinds2D.Rectangle },
        { new RoundedRectangle2D(new(-2, -1), new(2, 1), .4f), ShapeKinds2D.RoundedRectangle },
        { new AxisAlignedRectangle2D(new(-1, -2), new(3, 1)), ShapeKinds2D.Rectangle },
        { new Triangle2D(new(0, 0), new(2, 0), new(1, 3)), ShapeKinds2D.Triangle },
        { new ConvexPolygon2D([new(0, 0), new(2, 0), new(3, 1), new(1, 2)]), ShapeKinds2D.ConvexPolygon },
        { new HalfSpace2D(new(1, 1), 2), ShapeKinds2D.HalfSpace },
        { new CompositeShape2D([new Circle2D(1, new(-3, 0)), new Rectangle2D(new(1, -1), new(4, 1))]), ShapeKinds2D.Composite }
    };

    [Theory]
    [MemberData(nameof(BuiltInShapes))]
    public void TaggedDefinitionsRoundTripIntoEquivalentRuntimeShapes(IShape2D original, string kind)
    {
        var json = ShapeDefinition2D.FromShape(original).ToJson();
        using var document = JsonDocument.Parse(json);
        Assert.Equal(kind, document.RootElement.GetProperty("kind").GetString());
        var restored = ShapeDefinition2D.FromJson(json).Build();
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
        var rectangle = Assert.IsType<RectangleShapeDefinition2D>(ShapeDefinition2D.FromJson(ShapeDefinition2D.FromShape(new AxisAlignedRectangle2D(new(0, 0), new(1, 1))).ToJson()));
        Assert.True(rectangle.AxisAligned);
        Assert.IsType<AxisAlignedRectangle2D>(rectangle.Build());
        Assert.IsType<Rectangle2D>(ShapeDefinition2D.FromJson(ShapeDefinition2D.FromShape(new Rectangle2D(new(0, 0), new(1, 1))).ToJson()).Build());

        var composite = Assert.IsType<CompositeShapeDefinition2D>(ShapeDefinition2D.FromJson(ShapeDefinition2D.FromShape(new CompositeShape2D([new Circle2D(1), new Triangle2D(new(0, 0), new(1, 0), new(0, 1))])).ToJson()));
        Assert.Collection(composite.Parts, part => Assert.IsType<CircleShapeDefinition2D>(part), part => Assert.IsType<TriangleShapeDefinition2D>(part));
    }

    private sealed class ShapeEnvelope
    {
        public required ShapeDefinition2D Hit { get; init; }
    }

    [Fact]
    public void DefinitionWorksInsideExistingAuthoredJsonOptions()
    {
        var envelope = new ShapeEnvelope { Hit = new CapsuleShapeDefinition2D { Start = new(0, 0), End = new(2, 0), Radius = .5f } };
        var json = JsonSerializer.Serialize(envelope, AuthoredJson.Options);
        Assert.Contains("\"kind\": \"capsule\"", json);
        Assert.Contains("\"radius\": 0.5", json);
        var restored = JsonSerializer.Deserialize<ShapeEnvelope>(json, AuthoredJson.Options)!;
        Assert.Equal(.5f, Assert.IsType<Capsule2D>(restored.Hit.Build()).Radius);
    }

    [Fact]
    public void HandWrittenJsonUsesCamelCaseAndTheKindTagInAnyPosition()
    {
        var circle = Assert.IsType<Circle2D>(ShapeDefinition2D.FromJson("{\"center\":{\"x\":1,\"y\":2},\"radius\":3,\"kind\":\"circle\"}").Build());
        Assert.Equal(new Vector2(1, 2), circle.Center);
        Assert.Equal(3f, circle.Radius);
        Assert.Equal(10, ShapeKinds2D.All.Count);
    }

    [Fact]
    public void UnknownKindsMissingFieldsAndMisspelledFieldsAreRejected()
    {
        Assert.Throws<JsonException>(() => ShapeDefinition2D.FromJson("{\"kind\":\"blob\"}"));
        Assert.Throws<JsonException>(() => ShapeDefinition2D.FromJson("{\"kind\":\"circle\",\"center\":{\"x\":0,\"y\":0}}"));
        Assert.Throws<JsonException>(() => ShapeDefinition2D.FromJson("{\"kind\":\"circle\",\"center\":{\"x\":0,\"y\":0},\"radius\":1,\"radiuss\":2}"));
    }

    [Fact]
    public void InvalidGeometryFailsWhenBuiltNotWhenRead()
    {
        var zeroRadius = ShapeDefinition2D.FromJson("{\"kind\":\"circle\",\"center\":{\"x\":0,\"y\":0},\"radius\":0}");
        Assert.Throws<ArgumentOutOfRangeException>(zeroRadius.Build);
        var concave = ShapeDefinition2D.FromJson("{\"kind\":\"convex-polygon\",\"vertices\":[{\"x\":0,\"y\":0},{\"x\":2,\"y\":0},{\"x\":1,\"y\":0.5},{\"x\":2,\"y\":2},{\"x\":0,\"y\":2}]}");
        Assert.Throws<ArgumentException>(concave.Build);
        var backwards = ShapeDefinition2D.FromJson("{\"kind\":\"rectangle\",\"min\":{\"x\":1,\"y\":1},\"max\":{\"x\":0,\"y\":0}}");
        Assert.ThrowsAny<ArgumentException>(backwards.Build);
        var halfSpacePart = ShapeDefinition2D.FromJson("{\"kind\":\"composite\",\"parts\":[{\"kind\":\"half-space\",\"normal\":{\"x\":0,\"y\":1},\"offset\":0}]}");
        Assert.Throws<InvalidOperationException>(halfSpacePart.Build);
    }

    [Fact]
    public void CustomShapesNeedAnExplicitDefinition()
    {
        Assert.Throws<NotSupportedException>(() => ShapeDefinition2D.FromShape(new CustomShape()));
        Assert.Throws<ArgumentNullException>(() => ShapeDefinition2D.FromShape(null!));
    }

    private sealed class CustomShape : IShape2D
    {
        public float Area => 1;
        public bool ContainsPoint(Vector2 point) => false;
    }
}
