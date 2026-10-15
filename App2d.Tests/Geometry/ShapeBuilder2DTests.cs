using App2d.Core.Geometry.Builders;
using App2d.Core.Geometry.Authoring;
using App2d.Core.Characters;
using App2d.Core.Shapes;
using System.Numerics;
using System.Text.Json;

namespace App2d.Tests.Geometry;

public sealed class ShapeBuilder2DTests
{
    public static TheoryData<string, string> BuiltInShapes => new()
    {
        { nameof(Circle2D), ShapeKinds2D.Circle },
        { nameof(Ellipse2D), ShapeKinds2D.Ellipse },
        { nameof(Capsule2D), ShapeKinds2D.Capsule },
        { nameof(Rectangle2D), ShapeKinds2D.Rectangle },
        { nameof(RoundedRectangle2D), ShapeKinds2D.RoundedRectangle },
        { nameof(AxisAlignedRectangle2D), ShapeKinds2D.Rectangle },
        { nameof(Triangle2D), ShapeKinds2D.Triangle },
        { nameof(ConvexPolygon2D), ShapeKinds2D.ConvexPolygon },
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
        nameof(HalfSpace2D) => HalfSpace2D.Builder.Create(new(1, 1), 2),
        nameof(CompositeShape2D) => CompositeShape2D.Builder.Create([Circle2D.Builder.Create(1, new(-3, 0)), Rectangle2D.Builder.Create(new(1, -1), new(4, 1))]),
        _ => throw new ArgumentOutOfRangeException(nameof(shapeName), shapeName, null)
    };

    [Theory]
    [MemberData(nameof(BuiltInShapes))]
    public void TaggedDefinitionsRoundTripIntoEquivalentRuntimeShapes(string shapeName, string kind)
    {
        var original = CreateShape(shapeName);
        var json = ShapeBuilder2D.FromShape(original).ToJson();
        using var document = JsonDocument.Parse(json);
        Assert.Equal(kind, original.Kind);
        Assert.Equal(kind, document.RootElement.GetProperty("kind").GetString());
        var restored = ShapeBuilder2D.FromJson(json).Build();
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
        var rectangle = Assert.IsType<Rectangle2D.Builder>(ShapeBuilder2D.FromJson(ShapeBuilder2D.FromShape(AxisAlignedRectangle2D.Builder.Create(new(0, 0), new(1, 1))).ToJson()));
        Assert.True(rectangle.AxisAligned);
        Assert.IsType<AxisAlignedRectangle2D>(rectangle.Build());
        Assert.IsType<Rectangle2D>(ShapeBuilder2D.FromJson(ShapeBuilder2D.FromShape(Rectangle2D.Builder.Create(new(0, 0), new(1, 1))).ToJson()).Build());

        var composite = Assert.IsType<CompositeShape2D.Builder>(ShapeBuilder2D.FromJson(ShapeBuilder2D.FromShape(CompositeShape2D.Builder.Create([Circle2D.Builder.Create(1), Triangle2D.Builder.Create(new(0, 0), new(1, 0), new(0, 1))])).ToJson()));
        Assert.Collection(composite.Parts, part => Assert.IsType<Circle2D.Builder>(part), part => Assert.IsType<Triangle2D.Builder>(part));
    }

    private sealed class ShapeEnvelope
    {
        public required ShapeBuilder2D Hit { get; init; }
    }

    [Fact]
    public void DefinitionWorksInsideExistingAuthoredJsonOptions()
    {
        var envelope = new ShapeEnvelope { Hit = new Capsule2D.Builder { Start = new(0, 0), End = new(2, 0), Radius = .5f } };
        var json = JsonSerializer.Serialize(envelope, AuthoredJson.Options);
        Assert.Contains("\"kind\": \"capsule\"", json);
        Assert.Contains("\"radius\": 0.5", json);
        var restored = JsonSerializer.Deserialize<ShapeEnvelope>(json, AuthoredJson.Options)!;
        Assert.Equal(.5f, Assert.IsType<Capsule2D>(restored.Hit.Build()).Radius);
    }

    [Fact]
    public void HandWrittenJsonUsesCamelCaseAndTheKindTagInAnyPosition()
    {
        var circle = Assert.IsType<Circle2D>(ShapeBuilder2D.FromJson("{\"center\":{\"x\":1,\"y\":2},\"radius\":3,\"kind\":\"circle\"}").Build());
        Assert.Equal(new Vector2(1, 2), circle.Center);
        Assert.Equal(3f, circle.Radius);
        Assert.Equal(10, ShapeKinds2D.All.Count);
    }

    [Fact]
    public void UnknownKindsMissingFieldsAndMisspelledFieldsAreRejected()
    {
        Assert.Throws<JsonException>(() => ShapeBuilder2D.FromJson("{\"kind\":\"blob\"}"));
        Assert.Throws<JsonException>(() => ShapeBuilder2D.FromJson("{\"kind\":\"circle\",\"center\":{\"x\":0,\"y\":0}}"));
        Assert.Throws<JsonException>(() => ShapeBuilder2D.FromJson("{\"kind\":\"circle\",\"center\":{\"x\":0,\"y\":0},\"radius\":1,\"radiuss\":2}"));
    }

    [Fact]
    public void InvalidGeometryFailsWhenBuiltNotWhenRead()
    {
        var zeroRadius = ShapeBuilder2D.FromJson("{\"kind\":\"circle\",\"center\":{\"x\":0,\"y\":0},\"radius\":0}");
        Assert.Throws<ArgumentOutOfRangeException>(zeroRadius.Build);
        var concave = ShapeBuilder2D.FromJson("{\"kind\":\"convex-polygon\",\"vertices\":[{\"x\":0,\"y\":0},{\"x\":2,\"y\":0},{\"x\":1,\"y\":0.5},{\"x\":2,\"y\":2},{\"x\":0,\"y\":2}]}");
        Assert.Throws<ArgumentException>(concave.Build);
        var backwards = ShapeBuilder2D.FromJson("{\"kind\":\"rectangle\",\"min\":{\"x\":1,\"y\":1},\"max\":{\"x\":0,\"y\":0}}");
        Assert.ThrowsAny<ArgumentException>(backwards.Build);
        var halfSpacePart = ShapeBuilder2D.FromJson("{\"kind\":\"composite\",\"parts\":[{\"kind\":\"half-space\",\"normal\":{\"x\":0,\"y\":1},\"offset\":0}]}");
        Assert.Throws<InvalidOperationException>(halfSpacePart.Build);
    }

    [Fact]
    public void CustomShapesNeedAnExplicitDefinition()
    {
        Assert.Throws<NotSupportedException>(() => ShapeBuilder2D.FromShape(new CustomShape()));
        Assert.Throws<ArgumentNullException>(() => ShapeBuilder2D.FromShape(null!));
    }

    private sealed class CustomShape : IShape2D
    {
        public string Kind => "custom";
        public float Area => 1;
        public bool ContainsPoint(Vector2 point) => false;
    }
}
