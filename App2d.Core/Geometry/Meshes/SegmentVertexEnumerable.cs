using App2d.Core.Geometry.Primitives;

namespace App2d.Core.Geometry.Meshes;

/// <summary>Borrowed open/closed perimeter or indexed boundary segments. Neither buffer is copied.</summary>
public readonly ref struct SegmentVertexEnumerable<TSegment, TPoint>
    where TSegment : ISegment<TSegment, TPoint>
{
    private readonly ReadOnlySpan<TPoint> _vertices;
    private readonly ReadOnlySpan<Segment<int>> _indices;
    private readonly bool _indexed;

    internal SegmentVertexEnumerable(ReadOnlySpan<TPoint> vertices, bool closed)
    {
        _vertices = vertices;
        _indices = default;
        _indexed = false;
        Count = closed ? vertices.Length : Math.Max(0, vertices.Length - 1);
    }

    internal SegmentVertexEnumerable(ReadOnlySpan<TPoint> vertices, ReadOnlySpan<Segment<int>> indices)
    {
        _vertices = vertices;
        _indices = indices;
        _indexed = true;
        Count = indices.Length;
    }

    public int Count { get; }
    public Enumerator GetEnumerator() => new(_vertices, _indices, _indexed, Count);

    public ref struct Enumerator
    {
        private readonly ReadOnlySpan<TPoint> _vertices;
        private readonly ReadOnlySpan<Segment<int>> _indices;
        private readonly bool _indexed;
        private readonly int _count;
        private int _current;

        internal Enumerator(ReadOnlySpan<TPoint> vertices, ReadOnlySpan<Segment<int>> indices, bool indexed, int count)
        {
            _vertices = vertices;
            _indices = indices;
            _indexed = indexed;
            _count = count;
            _current = -1;
        }

        public readonly TSegment Current
        {
            get
            {
                var (start, end) = _indexed ? _indices[_current]
                    : new Segment<int>(_current, _current + 1 == _vertices.Length ? 0 : _current + 1);
                return TSegment.Create(_vertices[start], _vertices[end]);
            }
        }

        public bool MoveNext()
        {
            if (_current + 1 >= _count) return false;
            _current++;
            return true;
        }
    }
}
