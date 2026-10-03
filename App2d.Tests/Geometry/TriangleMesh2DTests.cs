using App2d.Core;
using App2d.Core.Collision.Contacts;
using App2d.Core.Geometry;
using App2d.Core.Mathematics;
using App2d.Core.Meshes;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class TriangleMesh2DTests
{
    private static readonly Vector2[] Notched =
        [new(0, 0), new(2, 0), new(2, 1), new(1, 1), new(1, 2), new(0, 2)];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EarClippingPreservesAConcaveOutlineInEitherWinding(bool reverse)
    {
        var points = reverse ? [.. Notched.Reverse()] : Notched;
        var mesh = TriangleMesh2D.TriangulateSimplePolygon(points);
        Assert.Equal(4, mesh.TriangleCount);
        Assert.Equal(3f, mesh.Area, 5);
        Assert.Equal(new Vector2(0, 0), mesh.Bounds.Min);
        Assert.Equal(new Vector2(2, 2), mesh.Bounds.Max);
        Assert.True(mesh.ContainsPoint(new(.5f, 1.5f)));
        Assert.True(mesh.ContainsPoint(new(1, 1)));
        Assert.False(mesh.ContainsPoint(new(1.5f, 1.5f)));
        for (var i = 0; i < mesh.TriangleCount; i++)
        {
            var (a, b, c) = mesh.TriangleAt(i);
            Assert.Equal(reverse ? -1 : 1, Math.Sign(CrossProduct2D.Orientation(a, b, c)));
        }
    }

    [Fact]
    public void MeshRejectsBadIndicesAndDegenerateTriangles()
    {
        Assert.Throws<ArgumentException>(() => new TriangleMesh2D(Notched, [0, 1]));
        Assert.Throws<ArgumentException>(() => new TriangleMesh2D(Notched, [0, 1, 9]));
        Assert.Throws<ArgumentException>(() => new TriangleMesh2D(Notched, [0, 0, 1]));
        Assert.Throws<ArgumentException>(() => TriangleMesh2D.TriangulateSimplePolygon(
            [new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 1), new Vector2(1, 0)]));
    }

    [Fact]
    public void TinyValidPolygonsDoNotDependOnAWorldScaleEpsilon()
    {
        var mesh = TriangleMesh2D.TriangulateSimplePolygon(
            [new Vector2(0, 0), new Vector2(1e-5f, 0), new Vector2(1e-5f, 1e-5f), new Vector2(0, 1e-5f)]);
        Assert.InRange(mesh.Area, .9e-10f, 1.1e-10f);
        Assert.True(mesh.ContainsPoint(new(5e-6f, 5e-6f)));
        Assert.False(mesh.ContainsPoint(new(1.1e-5f, 5e-6f)));
    }

    [Fact]
    public void EdgeToleranceCanIncludeAPointJustOutsideTheMeshBounds()
    {
        var mesh = TriangleMesh2D.TriangulateSimplePolygon(
            [new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1)]);
        Assert.False(mesh.ContainsPoint(new(1.0001f, 0)));
        Assert.True(mesh.ContainsPoint(new(1.0001f, 0), .001));
    }

    [Fact]
    public void CrossProductsShareOneDoublePrecisionOrientation()
    {
        var a = new Vector2(1, 2); var b = new Vector2(4, 2); var c = new Vector2(1, 5);
        Assert.Equal(9d, CrossProduct2D.Orientation(a, b, c));
        Assert.Equal(9d, CrossProduct2D.Of(b - a, c - a));
        Assert.Equal(9f, (b - a).Cross(c - a));
        Assert.Equal(-9d, CrossProduct2D.Orientation(a, c, b));
    }

    [Fact]
    public void ConcaveMeshTrianglesWorkAsACompositeCollisionShape()
    {
        var mesh = TriangleMesh2D.TriangulateSimplePolygon(Notched);
        var shape = mesh.ToCompositeShape();
        var world = new SpatialObject2D(shape);
        var solidPoint = new SpatialObject2D(new Circle2D(.1f, new(.5f, 1.5f)));
        var notch = new SpatialObject2D(new Circle2D(.1f, new(1.5f, 1.5f)));

        Assert.Equal(mesh.Area, shape.Area, 5);
        Assert.True(ShapeCollision2D.TryGetContact(world, solidPoint, out _));
        Assert.False(ShapeCollision2D.TryGetContact(world, notch, out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConvexClippingHandlesEitherWindingAndInterpolatesDepth(bool reverse)
    {
        Vector2[] clip = [new(0, 0), new(1, 0), new(1, 1), new(0, 1)];
        if (reverse) Array.Reverse(clip);
        Vector3[] subject = [new(-1, .5f, 0), new(2, .5f, 3), new(2, 1.5f, 3), new(-1, 1.5f, 0)];
        var clipped = PolygonClipping2D.ClipConvexXY(subject, clip.Select(p => new Vector3(p, 0)).ToArray());
        Assert.NotEmpty(clipped);
        Assert.All(clipped, point =>
        {
            Assert.InRange(point.X, -1e-5f, 1 + 1e-5f);
            Assert.InRange(point.Y, .5f - 1e-5f, 1 + 1e-5f);
            Assert.Equal(point.X + 1, point.Z, 5);
        });
        Assert.Contains(clipped, point => MathF.Abs(point.X) < 1e-5f && MathF.Abs(point.Y - .5f) < 1e-5f);
        Assert.Contains(clipped, point => MathF.Abs(point.X - 1) < 1e-5f && MathF.Abs(point.Y - .5f) < 1e-5f);
        Assert.Equal(4, PolygonClipping2D.ClipConvex(
            [new(-1, -1), new(2, -1), new(2, 2), new(-1, 2)], clip).Count);
    }
}
