using App2d.Core.Geometry.Primitives;

namespace App2d.Core.Geometry.Meshes;

/// <summary>A borrowed vertex/index view that constructs concrete triangle values through their static factory.</summary>
public readonly ref struct TriangleVertexEnumerable<TTriangle, TPoint>
    where TTriangle : ITriangle<TTriangle, TPoint>
{
    private readonly ReadOnlySpan<TPoint> _vertices;
    private readonly MeshTopology.TriangleEnumerable _triangles;

    internal TriangleVertexEnumerable(ReadOnlySpan<TPoint> vertices, MeshTopology.TriangleEnumerable triangles)
    {
        _vertices = vertices;
        _triangles = triangles;
    }

    public int Count => _triangles.Count;
    public Enumerator GetEnumerator() => new(_vertices, _triangles.GetEnumerator());

    public ref struct Enumerator
    {
        private readonly ReadOnlySpan<TPoint> _vertices;
        private MeshTopology.TriangleEnumerable.Enumerator _triangles;

        internal Enumerator(ReadOnlySpan<TPoint> vertices, MeshTopology.TriangleEnumerable.Enumerator triangles)
        {
            _vertices = vertices;
            _triangles = triangles;
        }

        public readonly TTriangle Current
        {
            get
            {
                var (a, b, c) = _triangles.Current;
                return TTriangle.Create(_vertices[a], _vertices[b], _vertices[c]);
            }
        }

        public bool MoveNext() => _triangles.MoveNext();
    }
}
