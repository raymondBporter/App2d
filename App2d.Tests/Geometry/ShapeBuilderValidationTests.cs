using App2d.Core.Geometry.Builders;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class ShapeBuilderValidationTests
{
    public static TheoryData<ShapeBuilder2D> InvalidBuilders =>
    [
        new Circle2D.Builder { Center = Vector2.Zero, Radius = 0 },
        new Ellipse2D.Builder { Center = Vector2.Zero, Radii = new(0, 1) },
        new Capsule2D.Builder { Start = Vector2.Zero, End = Vector2.One, Radius = -1 },
        new Rectangle2D.Builder { Min = Vector2.One, Max = Vector2.Zero },
        new RoundedRectangle2D.Builder { Min = Vector2.Zero, Max = Vector2.One, Radius = 1 },
        new Triangle2D.Builder { A = Vector2.Zero, B = Vector2.One, C = new(2, 2) },
        new ConvexPolygon2D.Builder { Vertices = [Vector2.Zero, Vector2.One] },
        new SimplePolygon2D.Builder { Vertices = [Vector2.Zero, Vector2.One, new(2, 2)] },
        new HalfSpace2D.Builder { Normal = Vector2.Zero, Offset = 0 },
        new CompositeShape2D.Builder { Parts = [] },
    ];

    [Theory]
    [MemberData(nameof(InvalidBuilders))]
    public void ValidationAndBuildBothRejectInvalidEditableGeometry(ShapeBuilder2D builder)
    {
        Assert.ThrowsAny<ArgumentException>(() => builder.Validate());
        Assert.ThrowsAny<ArgumentException>(() => builder.Build());
    }

    [Fact]
    public void BuiltPolygonAndCompositeSnapshotsSurviveBuilderEdits()
    {
        var polygonBuilder = new ConvexPolygon2D.Builder { Vertices = [new(0, 0), new(3, 0), new(0, 3)] };
        var polygon = polygonBuilder.Build();
        polygonBuilder.Vertices[1] = new(300, 0);
        Assert.Equal(new Vector2(3, 0), polygon.Vertices[1]);
        Assert.Equal(4.5f, polygon.Area);
        var compositeBuilder = new CompositeShape2D.Builder { Parts = [new Circle2D.Builder { Center = Vector2.Zero, Radius = 1 }] };
        var composite = compositeBuilder.Build();
        compositeBuilder.Parts.Add(new Circle2D.Builder { Center = new(5, 0), Radius = 2 });
        Assert.Equal(1, composite.Parts.Length);
        Assert.Equal(MathF.PI, composite.Area);
        Assert.False(composite.ContainsPoint(new(5, 0)));
    }

    [Fact]
    public void CircumcircleFactoryPassesThroughAllThreePointsInEitherOrder()
    {
        var a = new Vector2(0, 0);
        var b = new Vector2(2, 0);
        var c = new Vector2(0, 2);
        var circle = Circle2D.Builder.CreateFromPoints(a, b, c);
        var reversed = Circle2D.Builder.FromPoints(a, c, b).Build();
        Assert.Equal(Vector2.One, circle.Center);
        Assert.Equal(circle.Center, reversed.Center);
        foreach (var point in new[] { a, b, c })
        {
            Assert.InRange(MathF.Abs(Vector2.Distance(point, circle.Center) - circle.Radius), 0, .000001f);
        }

        Assert.Throws<ArgumentException>(() => Circle2D.Builder.CreateFromPoints(Vector2.Zero, Vector2.One, new(2, 2)));
    }
}
