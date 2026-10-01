using App2d.Core.Meshes;
using App2d.Core.Geometry;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class TriangleMeshAnalysis3DTests
{
    [Fact]
    public void PlanarTriangulationHasNoInkOnItsInternalEdge()
    {
        Vector3[] vertices = [new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0)];
        var mesh = new TriangleMeshAnalysis3D(vertices, [0, 1, 2, 0, 2, 3]);
        Assert.Equal(2, mesh.FaceCount);
        Assert.True(mesh.TryGetFaceNormal(0, out var normal));
        Assert.Equal(Vector3.UnitZ, normal);
        Assert.Equal(4, mesh.FeatureEdges(Vector3.UnitZ, .7f).Count());
        Assert.DoesNotContain(new IndexedEdge3D(0, 2), mesh.FeatureEdges(Vector3.UnitZ, .7f));
        Assert.Empty(mesh.FeatureEdges(-Vector3.UnitZ, .7f));

        var reflected = new TriangleMeshAnalysis3D(vertices, [0, 1, 2, 0, 2, 3], handedness: -1);
        Assert.True(reflected.TryGetFaceNormal(0, out normal));
        Assert.Equal(-Vector3.UnitZ, normal);
        Assert.Equal(4, reflected.FeatureEdges(-Vector3.UnitZ, .7f).Count());
    }

    [Fact]
    public void SharedEdgeBecomesACreaseOrSilhouetteWhenAppropriate()
    {
        Vector3[] vertices = [new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1)];
        var mesh = new TriangleMeshAnalysis3D(vertices, [0, 1, 2, 0, 3, 1]);
        var shared = new IndexedEdge3D(0, 1);
        Assert.Contains(shared, mesh.FeatureEdges(new(0, 1, 1), .7f));
        Assert.DoesNotContain(shared, mesh.FeatureEdges(new(0, 1, 1), -1f));
        Assert.Contains(shared, mesh.FeatureEdges(Vector3.UnitZ, -1f));
    }

    [Fact]
    public void DegenerateFacesAreSkippedWithoutPoisoningNormalsOrEdges()
    {
        Vector3[] vertices = [Vector3.Zero, Vector3.UnitX, Vector3.UnitY];
        var mesh = new TriangleMeshAnalysis3D(vertices, [0, 1, 2, 0, 1, 1]);
        Assert.False(mesh.TryGetFaceNormal(1, out var normal));
        Assert.Equal(Vector3.Zero, normal);
        Assert.Equal(3, mesh.FeatureEdges(Vector3.UnitZ, .7f).Count());
    }

    [Fact]
    public void IndexedEdgesHaveStableUndirectedIdentity()
    {
        Assert.Equal(new IndexedEdge3D(2, 5), new IndexedEdge3D(5, 2));
        Assert.Throws<ArgumentException>(() => new IndexedEdge3D(2, 2));
    }
}
