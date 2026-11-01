using App2d.Core.Geometry.Meshes;
using App2d.Core.Rendering;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class IndexedMesh3DTests
{
    [Fact]
    public void BoxIsAClosedOutwardSolid()
    {
        var box = MeshGenerators3D.Box(new(1, 2, 3));
        box.Validate();
        Assert.Equal(12, box.FaceCount);
        var analysis = new TriangleMeshAnalysis3D(box.Vertices, box.Triangles);
        for (var face = 0; face < box.FaceCount; face++)
        {
            Assert.True(analysis.TryGetFaceNormal(face, out var normal));
            var center = (box.Vertices[box.Triangles[face * 3]] + box.Vertices[box.Triangles[face * 3 + 1]] + box.Vertices[box.Triangles[face * 3 + 2]]) / 3;
            Assert.True(Vector3.Dot(normal, center) > 0, $"face {face} points inward");
        }
    }

    [Fact]
    public void ExtrusionOfAClockwiseOutlineMatchesItsCounterClockwiseTwin()
    {
        Vector3[] square = [new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0)];
        var counterClockwise = MeshGenerators3D.Extrude(square, .5f);
        var clockwise = MeshGenerators3D.Extrude([.. square.Reverse()], .5f);
        counterClockwise.Validate();
        Assert.Equal(8, counterClockwise.Vertices.Count);
        Assert.Equal(12, counterClockwise.FaceCount);
        Assert.Equal(-.25f, counterClockwise.Vertices[0].Z);
        Assert.Equal(.25f, counterClockwise.Vertices[4].Z);
        Assert.Equal(clockwise.Triangles, counterClockwise.Triangles);
    }

    [Fact]
    public void InvalidMeshesAreRejectedByName()
    {
        Vector3[] points = [Vector3.Zero, Vector3.UnitX, Vector3.UnitY];
        Assert.Contains("indexed triangles", Assert.Throws<InvalidDataException>(() => new IndexedMesh3D(points, [0, 1]).Validate()).Message);
        Assert.Contains("outside vertices", Assert.Throws<InvalidDataException>(() => new IndexedMesh3D(points, [0, 1, 3]).Validate()).Message);
        Assert.Contains("degenerate", Assert.Throws<InvalidDataException>(() => new IndexedMesh3D(points, [0, 1, 1]).Validate()).Message);
    }

    [Fact]
    public void LightShadesFromAmbientToFullAlongTheLightDirection()
    {
        var light = new DirectionalLight3D(new(0, 0, -2), .7f, .3f);
        Assert.Equal(1f, light.Shade(-Vector3.UnitZ), 5);
        Assert.Equal(.7f, light.Shade(Vector3.UnitZ), 5);
        Assert.Equal(.7f, light.Shade(Vector3.UnitX), 5);
    }
}
