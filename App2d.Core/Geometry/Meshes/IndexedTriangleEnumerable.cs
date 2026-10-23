using App2d.Core.Geometry.Primitives;

namespace App2d.Core.Geometry.Meshes;

/// <summary>A borrowed vertex/index view with allocation-free foreach iteration over vertex triples.</summary>
/// <remarks>Each triangle contains vertex values; indexed vertices need not be contiguous in memory.</remarks>
public readonly ref struct IndexedTriangleEnumerable<T>
{
    private readonly ReadOnlySpan<T> _vertices;
    private readonly MeshTopology.TriangleEnumerable _triangles;

    internal IndexedTriangleEnumerable(ReadOnlySpan<T> vertices, MeshTopology.TriangleEnumerable triangles)
    {
        _vertices = vertices;
        _triangles = triangles;
    }

    public int Count => _triangles.Count;
    public TriangleVertexEnumerable<Triangle<T>, T>.Enumerator GetEnumerator() => As<Triangle<T>>().GetEnumerator();

    /// <summary>Uses a caller-selected triangle type and its construction/validation policy.</summary>
    public TriangleVertexEnumerable<TTriangle, T> As<TTriangle>() where TTriangle : ITriangle<TTriangle, T> => new(_vertices, _triangles);
}
