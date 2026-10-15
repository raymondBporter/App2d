namespace App2d.Core.Geometry.Meshes;

/// <summary>A borrowed vertex/index view with allocation-free foreach iteration over vertex triples.</summary>
/// <remarks>Each triple contains vertex values; indexed vertices need not be contiguous in memory.</remarks>
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
    public Enumerator GetEnumerator() => new(_vertices, _triangles.GetEnumerator());

    public ref struct Enumerator
    {
        private readonly ReadOnlySpan<T> _vertices;
        private MeshTopology.TriangleEnumerable.Enumerator _triangles;

        internal Enumerator(ReadOnlySpan<T> vertices, MeshTopology.TriangleEnumerable.Enumerator triangles)
        {
            _vertices = vertices;
            _triangles = triangles;
        }

        public (T A, T B, T C) Current
        {
            get
            {
                var (a, b, c) = _triangles.Current;
                return (_vertices[a], _vertices[b], _vertices[c]);
            }
        }

        public bool MoveNext() => _triangles.MoveNext();
    }
}
