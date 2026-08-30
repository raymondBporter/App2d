using App2d.Core.Geometry;
using App2d.Rendering;
using App2d.Rendering.Textures;
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

    public static void Draw(
        Renderer2D renderer,
        TileEditor2D editor,
        Bounds2D mapBounds,
        float tileSize,
        TextureCache2D textures)
    {
        if (!editor.IsActive)
            return;

        var origin = mapBounds.Min;
        DrawGrid(renderer, editor.VisibleWorldBounds, mapBounds, tileSize);

        if (editor.Mode == LevelEditorMode2D.Things)
        {
            DrawThings(renderer, editor, tileSize);
            return;
        }

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

        TileEditorMenu2D.Draw(renderer, editor, textures);
    }

    private static void DrawThings(Renderer2D renderer, TileEditor2D editor, float tileSize)
    {
        var pathColor = new SKColor(130, 180, 210, 170);
        var selectedColor = new SKColor(255, 214, 64);
        var disabledColor = new SKColor(130, 130, 140, 180);
        foreach (var thing in editor.MovingPlatformThings)
        {
            var start = new Vector2(thing.X, thing.Y);
            var end = start + new Vector2(thing.TravelX, thing.TravelY);
            var isSelected = editor.SelectedThingId == thing.ThingId;
            var color = isSelected ? selectedColor : thing.Enabled ? pathColor : disabledColor;
            Span<Vector2> path = [start, end];
            renderer.DrawWorldPolyline(path, color, isSelected ? 3f : 2f);
            DrawRectangleOutline(renderer, start, new Vector2(thing.Width, thing.Height), color, isSelected ? 3f : 2f);
            if (isSelected)
            {
                var radius = 9f / editor.Zoom;
                renderer.DrawWorldCircle(start, radius, selectedColor, 3f);
                renderer.DrawWorldCircle(end, radius, selectedColor, 3f);
            }
        }

        if (editor.TryGetPlacementPreview(out var definition, out var position))
        {
            DrawRectangleOutline(
                renderer,
                position,
                new Vector2(definition.Width, definition.Height),
                new SKColor(105, 245, 180, 220),
                3f);
            Span<Vector2> previewPath = [position, position + new Vector2(tileSize * 3f, 0f)];
            renderer.DrawWorldPolyline(previewPath, new SKColor(105, 245, 180, 180), 2f);
        }
    }

    private static void DrawRectangleOutline(
        Renderer2D renderer,
        Vector2 center,
        Vector2 size,
        SKColor color,
        float strokeWidth)
    {
        var half = size / 2f;
        Span<Vector2> outline =
        [
            center + new Vector2(-half.X, -half.Y),
            center + new Vector2(half.X, -half.Y),
            center + new Vector2(half.X, half.Y),
            center + new Vector2(-half.X, half.Y),
            center + new Vector2(-half.X, -half.Y)
        ];
        renderer.DrawWorldPolyline(outline, color, strokeWidth);
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

}
