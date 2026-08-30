using App2d.Core.Geometry;
using App2d.Rendering;
using App2d.Tiles;
using SkiaSharp;
using System.Numerics;

namespace App2d.Editor;

/// <summary>Draws the tile grid, cursor and editor status while editor mode is active.</summary>
internal static class TileEditorView2D
{
    private static readonly SKColor CursorColor = new(255, 214, 64);

    // Low alpha and a hairline stroke: the grid must read as a faint reference, never
    // compete visually with the cursor outline or the painted tiles themselves.
    private static readonly SKColor GridColor = new(255, 255, 255, 28);

    public static void Draw(Renderer2D renderer, TileEditor2D editor, Bounds2D mapBounds, float tileSize)
    {
        if (!editor.IsActive)
            return;

        var origin = mapBounds.Min;
        DrawGrid(renderer, editor.VisibleWorldBounds, mapBounds, tileSize);

        var hasTile = editor.TryGetHoveredTile(out var tileX, out var tileY);
        if (hasTile)
        {
            var min = origin + new Vector2(tileX, tileY) * tileSize;
            var max = min + new Vector2(tileSize);
            Span<Vector2> outline =
            [
                new(min.X, min.Y),
                new(max.X, min.Y),
                new(max.X, max.Y),
                new(min.X, max.Y),
                new(min.X, min.Y)
            ];
            renderer.DrawWorldPolyline(outline, CursorColor, strokeWidth: 2f);
        }

        var position = hasTile ? $"{tileX}, {tileY}" : "outside";
        renderer.DrawScreenLabel(
            $"EDIT  kind: {Describe(editor.SelectedKind)}  tile: {position}  [F1] play  [1-5] kind  [RMB] erase  [Ctrl+Z] undo",
            new Vector2(16f, 16f));
    }

    /// <summary>
    /// Draws grid lines over the tiles currently on screen, in world space so the grid
    /// tracks pan and zoom like the cursor outline. Bounded to the visible region rather
    /// than the whole map (a zoomed-out view could otherwise ask for hundreds of lines)
    /// and clamped to the map bounds so nothing is drawn outside the paintable area.
    /// </summary>
    private static void DrawGrid(Renderer2D renderer, Bounds2D visible, Bounds2D mapBounds, float tileSize)
    {
        var minX = MathF.Max(visible.Left, mapBounds.Left);
        var maxX = MathF.Min(visible.Right, mapBounds.Right);
        var minY = MathF.Max(visible.Bottom, mapBounds.Bottom);
        var maxY = MathF.Min(visible.Top, mapBounds.Top);
        if (minX >= maxX || minY >= maxY)
            return;

        var firstX = mapBounds.Left + MathF.Floor((minX - mapBounds.Left) / tileSize) * tileSize;
        for (var x = firstX; x <= maxX; x += tileSize)
        {
            Span<Vector2> line = [new(x, minY), new(x, maxY)];
            renderer.DrawWorldPolyline(line, GridColor, strokeWidth: 1f);
        }

        var firstY = mapBounds.Bottom + MathF.Floor((minY - mapBounds.Bottom) / tileSize) * tileSize;
        for (var y = firstY; y <= maxY; y += tileSize)
        {
            Span<Vector2> line = [new(minX, y), new(maxX, y)];
            renderer.DrawWorldPolyline(line, GridColor, strokeWidth: 1f);
        }
    }

    private static string Describe(TileKind2D kind) => kind switch
    {
        TileKind2D.Empty => "empty",
        TileKind2D.Solid => "solid",
        TileKind2D.OneWay => "one-way",
        TileKind2D.Spikes => "spikes",
        _ when kind.IsGrippable() => "grippable",
        _ => kind.ToString()
    };
}
