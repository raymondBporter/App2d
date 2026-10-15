using App2d.Core.Validation;

namespace App2d.Core.Geometry.Meshes;

/// <summary>Allocation-free views of triangle indices and ordered perimeter edges, in any dimension.</summary>
public static class MeshTopology
{
    /// <summary>Reads complete index triples in buffer order, preserving winding and repeated indices.</summary>
    /// <remarks>The span is borrowed, not copied. Vertex-index bounds are the caller's responsibility.</remarks>
    public static TriangleEnumerable Triangles(ReadOnlySpan<int> indices)
    {
        ArgGuard.ThrowIf(indices.Length % 3 != 0, "Triangle indices must form complete triples.", nameof(indices));
        return new(indices, indices.Length / 3, fan: false);
    }

    /// <summary>Returns (0, 1, 2), (0, 2, 3), and so on; fewer than three vertices produce no triangles.</summary>
    /// <remarks>This describes a fan, not general polygon triangulation. The perimeter must be visible from vertex zero.</remarks>
    public static TriangleEnumerable TriangleFan(int vertexCount)
    {
        ArgGuard.ThrowIfNegative(vertexCount);
        return new(default, Math.Max(0, vertexCount - 2), fan: true);
    }

    /// <summary>Returns consecutive directed edges, optionally including the last-to-first closing edge.</summary>
    /// <remarks>An empty sequence has no edges; a closed one-vertex sequence has the self-edge (0, 0).</remarks>
    public static EdgeEnumerable Edges(int vertexCount, bool closed = true)
    {
        ArgGuard.ThrowIfNegative(vertexCount);
        return new(vertexCount, closed);
    }

    /// <summary>Reads vertex triples through an index span without copying either buffer.</summary>
    /// <remarks>Invalid vertex indices throw when the affected triangle is read. Do not mutate the buffers during enumeration.</remarks>
    public static IndexedTriangleEnumerable<T> IndexedTriangles<T>(ReadOnlySpan<T> vertices, ReadOnlySpan<int> indices) => new(vertices, Triangles(indices));

    /// <summary>A borrowed index buffer or an implicit triangle fan, usable directly in foreach.</summary>
    public readonly ref struct TriangleEnumerable
    {
        private readonly ReadOnlySpan<int> _indices;
        private readonly bool _fan;

        internal TriangleEnumerable(ReadOnlySpan<int> indices, int count, bool fan)
        {
            _indices = indices;
            Count = count;
            _fan = fan;
        }

        public int Count
        {
            get;
        }
        public Enumerator GetEnumerator() => new(_indices, Count, _fan);

        public ref struct Enumerator
        {
            private readonly ReadOnlySpan<int> _indices;
            private readonly int _count;
            private readonly bool _fan;
            private int _next;

            internal Enumerator(ReadOnlySpan<int> indices, int count, bool fan)
            {
                _indices = indices;
                _count = count;
                _fan = fan;
            }

            public (int A, int B, int C) Current
            {
                get; private set;
            }

            public bool MoveNext()
            {
                if (_next >= _count)
                {
                    return false;
                }

                var triangle = _next++;
                if (_fan)
                {
                    Current = (0, triangle + 1, triangle + 2);
                }
                else
                {
                    var offset = triangle * 3;
                    Current = (_indices[offset], _indices[offset + 1], _indices[offset + 2]);
                }
                return true;
            }
        }
    }

    /// <summary>An implicit open or closed sequence of directed edge indices.</summary>
    public readonly record struct EdgeEnumerable
    {
        private readonly int _vertexCount;

        internal EdgeEnumerable(int vertexCount, bool closed)
        {
            _vertexCount = vertexCount;
            Count = closed ? vertexCount : Math.Max(0, vertexCount - 1);
        }

        public int Count
        {
            get;
        }
        public Enumerator GetEnumerator() => new(_vertexCount, Count);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1815",
            Justification = "A mutable foreach cursor has no useful value-equality contract.")]
        public struct Enumerator
        {
            private readonly int _vertexCount;
            private readonly int _count;
            private int _next;

            internal Enumerator(int vertexCount, int count)
            {
                _vertexCount = vertexCount;
                _count = count;
            }

            public (int A, int B) Current
            {
                get; private set;
            }

            public bool MoveNext()
            {
                if (_next >= _count)
                {
                    return false;
                }

                var start = _next++;
                Current = (start, start + 1 == _vertexCount ? 0 : start + 1);
                return true;
            }
        }
    }
}
