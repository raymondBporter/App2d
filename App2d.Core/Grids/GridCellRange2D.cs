using App2d.Core.Validation;
namespace App2d.Core.Grids;

/// <summary>An inclusive rectangle of integer cells. Default is empty. Foreach visits X first without allocating.</summary>
public readonly record struct GridCellRange2D
{
    private readonly bool _hasCells;

    public GridCellRange2D(GridCell2D minimum, GridCell2D maximum)
    {
        ArgGuard.ThrowIfLessThan(maximum.X, minimum.X, nameof(maximum));
        ArgGuard.ThrowIfLessThan(maximum.Y, minimum.Y, nameof(maximum));
        var width = (long)maximum.X - minimum.X + 1;
        var height = (long)maximum.Y - minimum.Y + 1;
        if (width > long.MaxValue / height)
            ArgGuard.ThrowOutOfRange(maximum, "The cell count must fit a signed 64-bit integer.");
        Minimum = minimum;
        Maximum = maximum;
        _hasCells = true;
    }

    public GridCell2D Minimum { get; }
    public GridCell2D Maximum { get; }
    public bool IsEmpty => !_hasCells;
    public long Width => IsEmpty ? 0 : (long)Maximum.X - Minimum.X + 1;
    public long Height => IsEmpty ? 0 : (long)Maximum.Y - Minimum.Y + 1;
    public long CellCount => Width * Height;

    public bool Contains(GridCell2D cell) => !IsEmpty &&
        cell.X >= Minimum.X && cell.X <= Maximum.X && cell.Y >= Minimum.Y && cell.Y <= Maximum.Y;

    public GridCellRange2D Intersect(GridCellRange2D other)
    {
        if (IsEmpty || other.IsEmpty) return default;
        var min = new GridCell2D(Math.Max(Minimum.X, other.Minimum.X), Math.Max(Minimum.Y, other.Minimum.Y));
        var max = new GridCell2D(Math.Min(Maximum.X, other.Maximum.X), Math.Min(Maximum.Y, other.Maximum.Y));
        return min.X > max.X || min.Y > max.Y ? default : new(min, max);
    }

    public Enumerator GetEnumerator() => new(this);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1815",
        Justification = "A mutable foreach cursor has no useful value-equality contract.")]
    public struct Enumerator
    {
        private readonly GridCellRange2D _range;
        private bool _started;
        private bool _finished;

        internal Enumerator(GridCellRange2D range) => _range = range;
        public GridCell2D Current { get; private set; }

        public bool MoveNext()
        {
            if (_finished || _range.IsEmpty) return false;
            if (!_started)
            {
                Current = _range.Minimum;
                _started = true;
                return true;
            }
            if (Current.X < _range.Maximum.X)
            {
                Current = new(Current.X + 1, Current.Y);
                return true;
            }
            if (Current.Y < _range.Maximum.Y)
            {
                Current = new(_range.Minimum.X, Current.Y + 1);
                return true;
            }
            _finished = true;
            return false;
        }
    }
}
