using App2d.Core.Validation;
using App2d.Core.Geometry.Primitives;

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
        ArgGuard.NonNegative(vertexCount);
        return new(default, Math.Max(0, vertexCount - 2), fan: true);
    }

    /// <summary>Returns consecutive directed edges, optionally including the last-to-first closing edge.</summary>
    /// <remarks>An empty sequence has no edges; a closed one-vertex sequence has the self-edge (0, 0).</remarks>
    public static EdgeEnumerable Edges(int vertexCount, bool closed = true)
    {
        ArgGuard.NonNegative(vertexCount);
        return new(vertexCount, closed);
    }

    /// <summary>Reads vertex triples through an index span without copying either buffer.</summary>
    /// <remarks>Invalid vertex indices throw when the affected triangle is read. Do not mutate the buffers during enumeration.</remarks>
    public static IndexedTriangleEnumerable<T> IndexedTriangles<T>(ReadOnlySpan<T> vertices, ReadOnlySpan<int> indices) => new(vertices, Triangles(indices));

    /// <summary>Triangle values from a perimeter visible from vertex zero. Concave polygons need triangulation first.</summary>
    public static IndexedTriangleEnumerable<T> TriangleFan<T>(ReadOnlySpan<T> vertices) => new(vertices, TriangleFan(vertices.Length));

    /// <summary>A packed triangle buffer: every three consecutive vertices form one triangle.</summary>
    public static IndexedTriangleEnumerable<T> TriangleList<T>(ReadOnlySpan<T> vertices)
    {
        ArgGuard.ThrowIf(vertices.Length % 3 != 0, "Triangle vertices must form complete triples.", nameof(vertices));
        return new(vertices, new(default, vertices.Length / 3, fan: false, list: true));
    }

    public static SegmentVertexEnumerable<Segment<T>, T> Segments<T>(ReadOnlySpan<T> vertices, bool closed = true) => new(vertices, closed);

    public static SegmentVertexEnumerable<TSegment, T> Segments<TSegment, T>(ReadOnlySpan<T> vertices, bool closed = true)
        where TSegment : ISegment<TSegment, T> => new(vertices, closed);

    public static SegmentVertexEnumerable<Segment<T>, T> IndexedSegments<T>(ReadOnlySpan<T> vertices, ReadOnlySpan<Segment<int>> indices) => new(vertices, indices);

    /// <summary>Finds edges used exactly once, preserving their first directed winding. Shared/internal edges are omitted.</summary>
    /// <remarks>Allocates topology storage; immutable meshes cache the result. Coincident positions with different indices are not welded.</remarks>
    public static Segment<int>[] BoundaryEdges(ReadOnlySpan<int> indices)
    {
        var edges = new Dictionary<(int, int), (Segment<int> Edge, int Count)>();
        foreach (var (a, b, c) in Triangles(indices))
        {
            CountEdge(a, b);
            CountEdge(b, c);
            CountEdge(c, a);
        }
        return [.. edges.Values.Where(entry => entry.Count == 1).Select(entry => entry.Edge)];

        void CountEdge(int a, int b)
        {
            var key = a < b ? (a, b) : (b, a);
            edges.TryGetValue(key, out var entry);
            edges[key] = entry.Count == 0 ? (new(a, b), 1) : (entry.Edge, entry.Count + 1);
        }
    }

    /// <summary>A borrowed index buffer or an implicit triangle fan, usable directly in foreach.</summary>
    public readonly ref struct TriangleEnumerable
    {
        private readonly ReadOnlySpan<int> _indices;
        private readonly bool _fan;
        private readonly bool _list;

        internal TriangleEnumerable(ReadOnlySpan<int> indices, int count, bool fan, bool list = false)
        {
            _indices = indices;
            Count = count;
            _fan = fan;
            _list = list;
        }

        public int Count { get; }
        public Enumerator GetEnumerator() => new(_indices, Count, _fan, _list);

        public ref struct Enumerator
        {
            private readonly ReadOnlySpan<int> _indices;
            private readonly int _count;
            private readonly bool _fan;
            private readonly bool _list;
            private int _next;

            internal Enumerator(ReadOnlySpan<int> indices, int count, bool fan, bool list)
            {
                _indices = indices;
                _count = count;
                _fan = fan;
                _list = list;
            }

            public (int A, int B, int C) Current { get; private set; }

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
                else if (_list)
                {
                    var offset = triangle * 3;
                    Current = (offset, offset + 1, offset + 2);
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

        public int Count { get; }
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

            public (int A, int B) Current { get; private set; }

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
