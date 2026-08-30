using App2d.Core;

namespace App2d.Tiles;

/// <summary>
/// The editable core of the tile painter: strokes, undo, and which chunks a stroke's data
/// touched. Knows nothing about input, cameras, rendering or storage.
/// </summary>
public sealed class TileEditSession2D(EditableTileMap2D map)
{
    private readonly EditableTileMap2D _map = ArgGuard.RequireNotNull(map);
    private readonly List<TileEdit2D> _currentStroke = [];
    private readonly List<TileEdit2D[]> _undoStack = [];

    public bool IsStrokeActive { get; private set; }
    public int UndoCount => _undoStack.Count;

    public void BeginStroke()
    {
        _currentStroke.Clear();
        IsStrokeActive = true;
    }

    /// <summary>
    /// Paints one tile. Out-of-bounds coordinates and writes that would not change the
    /// tile are ignored, so a drag over unchanged tiles records nothing to undo.
    /// </summary>
    public void Paint(int x, int y, TileKind2D kind)
    {
        StateGuard.ThrowIf(!IsStrokeActive, "Begin a stroke before painting.");

        if (x < 0 || x >= _map.Width || y < 0 || y >= _map.Height)
            return;

        var before = _map.GetTileKind(x, y);
        if (before == kind)
            return;

        _map.SetTileKind(x, y, kind);
        _currentStroke.Add(new TileEdit2D(x, y, before, kind));
    }

    /// <summary>
    /// Paints every tile on the line between two tile coordinates, so a fast mouse drag
    /// leaves no gaps between sampled positions.
    /// </summary>
    public void PaintLine(int fromX, int fromY, int toX, int toY, TileKind2D kind)
    {
        StateGuard.ThrowIf(!IsStrokeActive, "Begin a stroke before painting.");

        var deltaX = Math.Abs(toX - fromX);
        var deltaY = Math.Abs(toY - fromY);
        var stepX = fromX < toX ? 1 : -1;
        var stepY = fromY < toY ? 1 : -1;
        var error = deltaX - deltaY;

        var x = fromX;
        var y = fromY;
        while (true)
        {
            Paint(x, y, kind);
            if (x == toX && y == toY)
                return;

            var doubledError = error * 2;
            if (doubledError > -deltaY)
            {
                error -= deltaY;
                x += stepX;
            }

            if (doubledError < deltaX)
            {
                error += deltaX;
                y += stepY;
            }
        }
    }

    /// <summary>
    /// Ends the stroke and returns the chunks whose tile DATA changed — the chunks that
    /// must be persisted. This is deliberately narrower than the chunks whose appearance
    /// changed: painting near a chunk border dirties neighbours visually, but their stored
    /// tiles are untouched.
    /// </summary>
    public IReadOnlyCollection<TileChunk2D> EndStroke()
    {
        StateGuard.ThrowIf(!IsStrokeActive, "No stroke is in progress.");
        IsStrokeActive = false;

        if (_currentStroke.Count == 0)
            return [];

        _undoStack.Add([.. _currentStroke]);
        var chunks = CollectChunks(_currentStroke);
        _currentStroke.Clear();
        return chunks;
    }

    /// <summary>
    /// Reverts the most recent stroke by replaying its previous values through the same
    /// paint path, so invalidation and persistence behave identically to painting.
    /// </summary>
    public IReadOnlyCollection<TileChunk2D> Undo()
    {
        StateGuard.ThrowIf(IsStrokeActive, "Finish the stroke before undoing.");
        if (_undoStack.Count == 0)
            return [];

        var stroke = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);

        BeginStroke();
        for (var index = stroke.Length - 1; index >= 0; index--)
        {
            var edit = stroke[index];
            Paint(edit.X, edit.Y, edit.Before);
        }

        IsStrokeActive = false;
        var chunks = CollectChunks(_currentStroke);
        _currentStroke.Clear();
        return chunks;
    }

    private HashSet<TileChunk2D> CollectChunks(List<TileEdit2D> edits)
    {
        var chunks = new HashSet<TileChunk2D>();
        foreach (var edit in edits)
            chunks.Add(_map.TileToChunk(edit.X, edit.Y));
        return chunks;
    }

    private readonly record struct TileEdit2D(int X, int Y, TileKind2D Before, TileKind2D After);
}
