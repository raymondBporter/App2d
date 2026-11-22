using App2d.Core.Geometry.Meshes;

namespace App2d.Tests.Geometry;

public sealed class TriangleMeshAnalysis3DTests
{
    [Fact]
    public void PlanarTriangulationHasNoInkOnItsInternalEdge()
    {
        Vector3f[] vertices = [new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0)];
        var mesh = new TriangleMeshAnalysis3D(vertices, [0, 1, 2, 0, 2, 3]);
        Assert.Equal(2, mesh.FaceCount);
        Assert.True(mesh.TryGetFaceNormal(0, out var normal));
        Assert.Equal(Vector3f.UnitZ, normal);
        Assert.Equal(4, mesh.FeatureEdges(Vector3f.UnitZ, .7f).Count);
        Assert.DoesNotContain(new IndexedEdge3D(0, 2), mesh.FeatureEdges(Vector3f.UnitZ, .7f));
        Assert.Empty(mesh.FeatureEdges(-Vector3f.UnitZ, .7f));

        var reflected = new TriangleMeshAnalysis3D(vertices, [0, 1, 2, 0, 2, 3], handedness: -1);
        Assert.True(reflected.TryGetFaceNormal(0, out normal));
        Assert.Equal(-Vector3f.UnitZ, normal);
        Assert.Equal(4, reflected.FeatureEdges(-Vector3f.UnitZ, .7f).Count);
    }

    [Fact]
    public void FeatureEdgesNormalizeExtremeViewDirections()
    {
        Vector3f[] vertices = [Vector3f.Zero, Vector3f.UnitX, Vector3f.UnitY];
        var mesh = new TriangleMeshAnalysis3D(vertices, [0, 1, 2]);
        var expected = mesh.FeatureEdges(Vector3f.UnitZ, .7f).ToArray();
        Assert.Equal(expected, mesh.FeatureEdges(new(0, 0, float.Epsilon), .7f));
        Assert.Equal(expected, mesh.FeatureEdges(new(0, 0, float.MaxValue), .7f));
        Assert.Empty(mesh.FeatureEdges(new(0, 0, -float.MaxValue), .7f));
    }

    [Fact]
    public void SharedEdgeBecomesACreaseOrSilhouetteWhenAppropriate()
    {
        Vector3f[] vertices = [new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1)];
        var mesh = new TriangleMeshAnalysis3D(vertices, [0, 1, 2, 0, 3, 1]);
        var shared = new IndexedEdge3D(0, 1);
        Assert.Contains(shared, mesh.FeatureEdges(new(0, 1, 1), .7f));
        Assert.DoesNotContain(shared, mesh.FeatureEdges(new(0, 1, 1), -1f));
        Assert.Contains(shared, mesh.FeatureEdges(Vector3f.UnitZ, -1f));
    }

    [Fact]
    public void DegenerateFacesAreSkippedWithoutPoisoningNormalsOrEdges()
    {
        Vector3f[] vertices = [Vector3f.Zero, Vector3f.UnitX, Vector3f.UnitY];
        var mesh = new TriangleMeshAnalysis3D(vertices, [0, 1, 2, 0, 1, 1]);
        Assert.False(mesh.TryGetFaceNormal(1, out var normal));
        Assert.Equal(Vector3f.Zero, normal);
        Assert.Equal(3, mesh.FeatureEdges(Vector3f.UnitZ, .7f).Count);
    }

    [Fact]
    public void TopologyIsWorkedOutOncePerMeshAndFollowsTheVerticesItIsGiven()
    {
        var box = MeshGenerators3D.Box(new(1, 2, 3));
        var topology = IndexedMeshTopology3D.Of(box);
        Assert.Same(topology, IndexedMeshTopology3D.Of(box));
        Assert.Equal(12, topology.FaceCount);

        // Seen from in front, a box shows the four edges round its front face and not the diagonal across it.
        Vector3f[] vertices = [.. box.Vertices];
        var (normals, valid, edges) = (new Vector3f[12], new bool[12], new List<IndexedEdge3D>());
        topology.FaceNormals(vertices, 1, normals, valid);
        topology.FeatureEdges(normals, valid, Vector3f.UnitZ, .7f, 0, edges);
        Assert.All(valid, Assert.True);
        Assert.Equal([new(4, 5), new(4, 7), new(5, 6), new(6, 7)], edges.OrderBy(edge => edge.A).ThenBy(edge => edge.B));

        // Pressed flat, its sides have no area: in the same buffers they lose their normals and count as absent.
        for (var i = 0; i < vertices.Length; i++) vertices[i] = vertices[i] with { Z = 0 };
        topology.FaceNormals(vertices, 1, normals, valid);
        Assert.Equal([true, true, true, true, false, false, false, false, false, false, false, false], valid);
        Assert.All(normals[4..], normal => Assert.Equal(Vector3f.Zero, normal));
        edges.Clear();
        topology.FeatureEdges(normals, valid, Vector3f.UnitZ, .7f, 0, edges);
        Assert.Equal(4, edges.Count);
    }

    [Fact]
    public void IndexedEdgesHaveStableUndirectedIdentity()
    {
        Assert.Equal(new IndexedEdge3D(2, 5), new IndexedEdge3D(5, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new IndexedEdge3D(2, 2));
    }
}
