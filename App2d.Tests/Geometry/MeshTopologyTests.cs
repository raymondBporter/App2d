using App2d.Core.Geometry.Meshes;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class MeshTopologyTests
{
    [Fact]
    public void IndexTriplesPreserveBufferOrderWindingAndRepeatedIndices()
    {
        var triangles = MeshTopology.Triangles([4, 1, 3, 3, 1, 1]);
        var actual = new List<(int, int, int)>();
        foreach (var triangle in triangles)
        {
            actual.Add(triangle);
        }

        Assert.Equal(2, triangles.Count);
        Assert.Equal([(4, 1, 3), (3, 1, 1)], actual);
    }

    [Fact]
    public void IndexViewBorrowsStackMemoryAndCanBeEnumeratedAgain()
    {
        Span<int> indices = [0, 1, 2];
        var triangles = MeshTopology.Triangles(indices);
        indices[0] = 7;
        for (var pass = 0; pass < 2; pass++)
        {
            var enumerator = triangles.GetEnumerator();
            Assert.True(enumerator.MoveNext());
            Assert.Equal((7, 1, 2), enumerator.Current);
            Assert.False(enumerator.MoveNext());
            Assert.False(enumerator.MoveNext());
        }
    }

    [Fact]
    public void FanAnchorsAtZeroAndHasExactlyNMinusTwoFaces()
    {
        var fan = MeshTopology.TriangleFan(5);
        var actual = new List<(int, int, int)>();
        foreach (var triangle in fan)
        {
            actual.Add(triangle);
        }

        Assert.Equal(3, fan.Count);
        Assert.Equal([(0, 1, 2), (0, 2, 3), (0, 3, 4)], actual);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void SmallFansAreEmpty(int vertexCount)
    {
        var fan = MeshTopology.TriangleFan(vertexCount);
        Assert.Equal(0, fan.Count);
        Assert.False(fan.GetEnumerator().MoveNext());
    }

    [Fact]
    public void ClosedEdgesWrapAndOpenEdgesDoNot()
    {
        Assert.Equal([(0, 1), (1, 2), (2, 0)], Edges(3, closed: true));
        Assert.Equal([(0, 1), (1, 2)], Edges(3, closed: false));
        Assert.Equal([(0, 1), (1, 0)], Edges(2, closed: true));
        Assert.Equal([(0, 0)], Edges(1, closed: true));
        Assert.Empty(Edges(1, closed: false));
        Assert.Empty(Edges(0, closed: true));
        Assert.Empty(Edges(0, closed: false));
    }

    [Fact]
    public void VertexTriplesFollowIndicesWithoutCopyingTheBuffers()
    {
        Span<Vector2> vertices = [Vector2.Zero, Vector2.UnitX, Vector2.UnitY, Vector2.One];
        Span<int> indices = [3, 0, 2];
        var triangles = MeshTopology.IndexedTriangles<Vector2>(vertices, indices);
        vertices[3] = new(7, 8);
        indices[1] = 1;

        var enumerator = triangles.GetEnumerator();
        Assert.Equal(1, triangles.Count);
        Assert.True(enumerator.MoveNext());
        Assert.Equal((new Vector2(7, 8), Vector2.UnitX, Vector2.UnitY), enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void DefaultsAndEmptyBuffersEnumerateNothing()
    {
        Assert.False(default(MeshTopology.TriangleEnumerable).GetEnumerator().MoveNext());
        Assert.False(default(MeshTopology.EdgeEnumerable).GetEnumerator().MoveNext());
        Assert.False(default(IndexedTriangleEnumerable<Vector2>).GetEnumerator().MoveNext());
        Assert.Equal(0, MeshTopology.Triangles([]).Count);
        Assert.False(MeshTopology.IndexedTriangles<Vector2>([], []).GetEnumerator().MoveNext());
    }

    [Fact]
    public void InvalidTopologyIsRejectedWithoutTruncatingATrailingFace()
    {
        Assert.Throws<ArgumentException>(() => MeshTopology.Triangles([0, 1]));
        Assert.Throws<ArgumentException>(() => MeshTopology.Triangles([0, 1, 2, 3]));
        Assert.Throws<ArgumentOutOfRangeException>(() => MeshTopology.TriangleFan(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => MeshTopology.Edges(-1));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void InvalidVertexIndicesFailWhenRead(int index)
    {
        Assert.Throws<IndexOutOfRangeException>(() =>
        {
            var triangles = MeshTopology.IndexedTriangles<Vector2>([Vector2.Zero, Vector2.UnitX, Vector2.UnitY], [0, 1, index]);
            var enumerator = triangles.GetEnumerator();
            enumerator.MoveNext();
            _ = enumerator.Current;
        });
    }

    [Fact]
    public void MeshViewMatchesRandomAccessTriangles()
    {
        var mesh = new TriangleMesh2D([Vector2.Zero, Vector2.UnitX, Vector2.UnitY, Vector2.One], [3, 1, 0, 0, 2, 3]);
        var index = 0;
        foreach (var triangle in mesh.Triangles)
        {
            Assert.Equal(mesh.TriangleAt(index++), triangle);
        }

        Assert.Equal(mesh.TriangleCount, index);
    }

    [Fact]
    public void RepeatedTopologyAndVertexIterationDoesNotAllocate()
    {
        var mesh = new TriangleMesh2D([Vector2.Zero, Vector2.UnitX, Vector2.UnitY, Vector2.One], [0, 1, 2, 1, 3, 2]);
        _ = Sum(mesh);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var sum = 0f;
        for (var iteration = 0; iteration < 1_000; iteration++)
        {
            sum += Sum(mesh);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(sum > 0);
        Assert.Equal(0, allocated);

        static float Sum(TriangleMesh2D mesh)
        {
            var sum = 0f;
            foreach (var (a, b, c) in mesh.Triangles)
            {
                sum += a.X + b.Y + c.X;
            }
            foreach (var (a, b, c) in MeshTopology.Triangles(mesh.Indices))
            {
                sum += a + b + c;
            }
            foreach (var (a, b, c) in MeshTopology.TriangleFan(mesh.Vertices.Length))
            {
                sum += a + b + c;
            }
            foreach (var (a, b) in MeshTopology.Edges(mesh.Vertices.Length))
            {
                sum += a + b;
            }
            return sum;
        }
    }

    private static List<(int, int)> Edges(int vertexCount, bool closed)
    {
        var edges = MeshTopology.Edges(vertexCount, closed);
        var result = new List<(int, int)>();
        foreach (var edge in edges)
        {
            result.Add(edge);
        }

        Assert.Equal(edges.Count, result.Count);
        return result;
    }
}
