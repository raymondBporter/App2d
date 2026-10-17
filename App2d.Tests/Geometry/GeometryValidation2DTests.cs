using App2d.Core.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Shapes;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

namespace App2d.Tests.Geometry;

#pragma warning disable APP2D001 // These tests intentionally exercise unchecked geometry values.
public sealed class GeometryValidation2DTests
{
    public static TheoryData<IShape2D> InvalidGeometry =>
    [
        new Circle2D(0),
        new Ellipse2D(new(0, 1)),
        new Capsule2D(Vector2.Zero, Vector2.One, -1),
        new Rectangle2D(Vector2.One, Vector2.Zero),
        new AxisAlignedRectangle2D(Vector2.One, Vector2.Zero),
        new RoundedRectangle2D(Vector2.Zero, Vector2.One, 1),
        new Triangle2D(Vector2.Zero, Vector2.One, new(2, 2)),
        new ConvexPolygon2D([Vector2.Zero, Vector2.One]),
        new SimplePolygon2D([Vector2.Zero, Vector2.One, new(2, 2)]),
        new HalfSpace2D(Vector2.Zero, 0),
        new CompositeShape2D([]),
    ];

    [Theory]
    [MemberData(nameof(InvalidGeometry))]
    public void InvalidValuesCanBeConstructedAndSerializedBeforeValidation(IShape2D geometry)
    {
        var json = geometry.ToJson();
        var restored = GeometryJson2D.ShapeFromJson(json);
        Assert.Equal(geometry.GetType(), restored.GetType());
        Assert.ThrowsAny<ArgumentException>(() => geometry.Validate());
        Assert.ThrowsAny<ArgumentException>(() => restored.Validate());
    }

    [Fact]
    public void EveryBuilderIsAStatelessFactory()
    {
        Type[] types = [typeof(Circle2D), typeof(Ellipse2D), typeof(Capsule2D), typeof(Rectangle2D),
            typeof(AxisAlignedRectangle2D), typeof(RoundedRectangle2D), typeof(Triangle2D),
            typeof(ConvexPolygon2D), typeof(SimplePolygon2D), typeof(HalfSpace2D), typeof(CompositeShape2D),
            typeof(LineSegmentCurve2D), typeof(PolylineCurve2D), typeof(Arc2D), typeof(QuadraticBezier2D),
            typeof(CubicBezier2D), typeof(BSpline2D)];
        foreach (var type in types)
        {
            var factory = type.GetNestedType("Builder")!;
            Assert.True(factory.IsAbstract && factory.IsSealed, type.Name);
            Assert.Empty(factory.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
        }
    }

    [Fact]
    public void FactoriesRejectInvalidDataAndNormalizeHalfSpaces()
    {
        Assert.ThrowsAny<ArgumentException>(() => Circle2D.Builder.Create(0));
        Assert.ThrowsAny<ArgumentException>(() => AxisAlignedRectangle2D.Builder.Create(Vector2.One, Vector2.Zero));
        Assert.ThrowsAny<ArgumentException>(() => SimplePolygon2D.Builder.Create([Vector2.Zero, Vector2.One, new(2, 2)]));
        Assert.ThrowsAny<ArgumentException>(() => BSpline2D.Builder.Create([Vector2.Zero, Vector2.One], 3));
        var halfSpace = HalfSpace2D.Builder.Create(new(2, 0), 4);
        Assert.Equal(Vector2.UnitX, halfSpace.Normal);
        Assert.Equal(2, halfSpace.Offset);
        halfSpace.Validate();
    }

    [Fact]
    public void CollectionValuesSnapshotInputAndWithEditsReplaceTheSnapshot()
    {
        Vector2[] vertices = [new(0, 0), new(3, 0), new(0, 3)];
        var polygon = ConvexPolygon2D.Builder.Create(vertices);
        vertices[1] = new(300, 0);
        Assert.Equal(new Vector2(3, 0), polygon.Vertices[1]);
        var edited = polygon with { Vertices = [new(0, 0), new(6, 0), new(0, 3)] };
        Assert.Equal(4.5f, polygon.Area);
        Assert.Equal(9f, edited.Area);

        IConvexShape2D[] parts = [Circle2D.Builder.Create(1)];
        var composite = CompositeShape2D.Builder.Create(parts);
        parts[0] = Circle2D.Builder.Create(10);
        Assert.Equal(MathF.PI, composite.Area);
        Assert.Single(composite.Parts.ToArray());
    }

    [Fact]
    public void DerivedGeometryFollowsWithEditsWithoutStaleConstructorCaches()
    {
        var triangle = Triangle2D.Builder.Create(Vector2.Zero, new(2, 0), new(0, 2));
        Assert.Equal(2, triangle.Area);
        Assert.Equal(4, (triangle with { C = new(0, 4) }).Area);
        var polygon = SimplePolygon2D.Builder.Create([Vector2.Zero, new(2, 0), new(0, 2)]);
        Assert.Equal(2, polygon.Area);
        Assert.Equal(4, (polygon with { Vertices = [Vector2.Zero, new(4, 0), new(0, 2)] }).Area);
        var composite = CompositeShape2D.Builder.Create([Circle2D.Builder.Create(1), Circle2D.Builder.Create(1)]);
        Assert.Equal(2 * MathF.PI, composite.Area);
        Assert.Equal(MathF.PI, (composite with { IncludeOverlap = false }).Area);
        Assert.Equal(2 * MathF.PI, composite.Area);
    }

    [Fact]
    public void ConcreteJsonReadsTheSameValueAndDefersValidation()
    {
        var circle = JsonSerializer.Deserialize<Circle2D>("{\"kind\":\"circle\",\"center\":{\"x\":2,\"y\":3},\"radius\":-1}", GeometryJson2D.JsonOptions);
        Assert.Equal(new Vector2(2, 3), circle.Center);
        Assert.Equal(-1, circle.Radius);
        Assert.ThrowsAny<ArgumentException>(circle.Validate);
        Assert.DoesNotContain("area", circle.ToJson());
        Assert.DoesNotContain("diameter", circle.ToJson());
    }

    [Fact]
    public void CircumcircleFactoryPassesThroughAllThreePointsInEitherOrder()
    {
        var a = new Vector2(0, 0);
        var b = new Vector2(2, 0);
        var c = new Vector2(0, 2);
        var circle = Circle2D.Builder.CreateFromPoints(a, b, c);
        var reversed = Circle2D.Builder.FromPoints(a, c, b);
        Assert.Equal(Vector2.One, circle.Center);
        Assert.Equal(circle.Center, reversed.Center);
        foreach (var point in new[] { a, b, c })
            Assert.InRange(MathF.Abs(Vector2.Distance(point, circle.Center) - circle.Radius), 0, .000001f);
        Assert.Throws<ArgumentException>(() => Circle2D.Builder.CreateFromPoints(Vector2.Zero, Vector2.One, new(2, 2)));
    }
}
