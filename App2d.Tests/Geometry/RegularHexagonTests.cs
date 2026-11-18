using App2d.Core.Collision.Contacts;
using App2d.Core.Geometry.Operations.Distance;
using App2d.Core.Geometry.Operations.Vertices;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Shapes;

namespace App2d.Tests.Geometry;

public sealed class RegularHexagonTests
{
    [Fact]
    public void RotatingAndTranslatingKeepsAllSixSidesAndRadiiEqual()
    {
        const float radius = 2;
        const float angle = .37f;
        var center = new Vector2f(3, -4);
        var hexagon = ConvexPolygon2D.Builder.CreateRegularHexagon(radius, center, angle);

        Assert.Equal(6, hexagon.Vertices.Length);
        Assert.InRange(Vector2f.Distance(center + Vector2f.Direction(angle) * radius, hexagon.Vertices[0]), 0, 1e-6f);
        for (var i = 0; i < hexagon.Vertices.Length; i++)
        {
            Assert.Equal(radius, Vector2f.Distance(center, hexagon.Vertices[i]), 5);
            Assert.Equal(radius, Vector2f.Distance(hexagon.Vertices[i], hexagon.Vertices[(i + 1) % 6]), 5);
        }
        Assert.Equal(6f * MathF.Sqrt(3), hexagon.Area, 5);
    }

    [Fact]
    public void HexagonUsesPolygonQueriesAndSerialization()
    {
        var hexagon = ConvexPolygon2D.Builder.CreateRegularHexagon(2);
        Assert.True(hexagon.ContainsPoint(Vector2f.Zero));
        Assert.False(hexagon.ContainsPoint(new(3, 0)));
        Assert.Equal(1f, ShapeDistance2D.SignedDistance(new Vector2f(3, 0), hexagon), 5);
        Assert.Equal(6, hexagon.GetOutlineVertCount());
        var circle = Circle2D.Builder.Create(.5f, new(0, 2));
        Assert.True(ShapeCollision2D.TryGetContact(hexagon, Similarity2D.Identity, circle, Similarity2D.Identity, out _));

        var restored = Assert.IsType<ConvexPolygon2D>(GeometryJson2D.ShapeFromJson(hexagon.ToJson<IShape2D>()));
        Assert.Equal(hexagon.Vertices.ToArray(), restored.Vertices.ToArray());
    }

    [Fact]
    public void CornerWriterKeepsTrailingBufferEntries()
    {
        Span<Vector2f> buffer = stackalloc Vector2f[7];
        buffer[6] = new(42);

        Assert.Equal(6, VertexGenerator2D.WriteRegularHexagon(buffer, Vector2f.Zero, 2));
        Assert.Equal(new Vector2f(2, 0), buffer[0]);
        Assert.Equal(new Vector2f(1, MathF.Sqrt(3)), buffer[1]);
        Assert.Equal(new Vector2f(-1, MathF.Sqrt(3)), buffer[2]);
        Assert.Equal(new Vector2f(42), buffer[6]);
        Assert.Throws<ArgumentOutOfRangeException>(() => ConvexPolygon2D.Builder.CreateRegularHexagon(0));
    }
}
