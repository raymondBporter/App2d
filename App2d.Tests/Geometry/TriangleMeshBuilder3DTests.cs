using App2d.Core.Meshes;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class TriangleMeshBuilder3DTests
{
    [Fact]
    public void RibbonSharesCrossSectionsKeepsDepthAndCapsOnlyTheEnds()
    {
        var builder = new TriangleMeshBuilder3D<Vector3, byte>((point, _, _) => point);
        builder.Ribbon([new(0, 0, 0), new(1, 0, 1), new(2, 0, 2)], 0, .2f);
        Assert.Equal(28, builder.TriangleCount);
        Assert.Equal(-.1f, builder.Min.Y, 5); Assert.Equal(.1f, builder.Max.Y, 5);
        Assert.Equal(0, builder.Min.Z); Assert.Equal(2, builder.Max.Z);
        Assert.All(builder.Vertices.ToArray(), p => Assert.True(float.IsFinite(p.X + p.Y + p.Z)));
        builder.Clear(); builder.Ribbon([Vector3.Zero, Vector3.Zero, Vector3.Zero], 0, .2f);
        Assert.All(builder.Vertices.ToArray(), p => Assert.True(float.IsFinite(p.X + p.Y + p.Z)));
    }

    [Fact]
    public void IndexedTwoDimensionalMeshCanBePlacedAndColoredInThreeDimensions()
    {
        var source = TriangleMesh2D.TriangulateSimplePolygon(
            [new(0f, 0f), new(2f, 0f), new(2f, 2f), new(1f, 1f), new(0f, 2f)]);
        var builder = new TriangleMeshBuilder3D<(Vector3 Position, int Material), int>(
            (point, material, _) => (point, material), initialCapacity: 3);

        builder.Add(source, point => new Vector3(point, 4f), 7);

        Assert.Equal(source.TriangleCount, builder.TriangleCount);
        Assert.Equal(source.TriangleCount * 3, builder.Count);
        Assert.All(builder.Vertices.ToArray(), vertex =>
        {
            Assert.Equal(4f, vertex.Position.Z);
            Assert.Equal(7, vertex.Material);
        });
        Assert.Equal(new Vector3(0f, 0f, 4f), builder.Min);
        Assert.Equal(new Vector3(2f, 2f, 4f), builder.Max);
    }

    [Fact]
    public void EllipseCanFollowANonflatSurface()
    {
        var builder = new TriangleMeshBuilder3D<Vector3, byte>((point, _, _) => point, 3);
        builder.Ellipse(new Vector2(1f, 2f), new Vector2(2f, 1f),
            point => new Vector3(point, point.X - point.Y), 0);

        Assert.Equal(24, builder.TriangleCount);
        Assert.All(builder.Vertices.ToArray(), point => Assert.Equal(point.X - point.Y, point.Z, 5));
        Assert.Equal(-1f, builder.Min.X, 5);
        Assert.Equal(3f, builder.Max.X, 5);
    }
}
