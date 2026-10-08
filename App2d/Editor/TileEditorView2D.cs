using App2d.Core.Geometry;
using App2d.Core.Grids;
using App2d.Core.Rendering.Textures;
using App2d.Core.Shapes;
using App2d.Rendering;
using App2d.Things;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Editor;

/// <summary>Draws the tile grid, cursor and editor status while editor mode is active.</summary>
internal static class TileEditorView2D
{
    private static readonly XnaColor CursorColor = new(255, 214, 64);

    // Low alpha and a hairline stroke: the grid must read as a faint reference, never
    // compete visually with the cursor outline or the painted tiles themselves.
    private static readonly XnaColor GridColor = new(255, 255, 255, 28);

    public static void DrawWorldDebug(Renderer2D renderer, TileEditor2D editor)
    {
        if (!editor.IsActive)
            return;

        DrawGrid(renderer, editor.VisibleWorldBounds, editor.MapBounds, editor.GridGeometry);

        if (editor.Mode == LevelEditorMode2D.Things)
        {
            DrawThings(renderer, editor);
            return;
        }

        var hasTile = editor.TryGetHoveredTile(out var tileX, out var tileY);
        if (hasTile)
        {
            var bounds = editor.GridGeometry.GetCellBounds(new GridCell2D(tileX, tileY));
            renderer.DrawShape(new Rectangle2D(bounds.Min, bounds.Max),
                outlineColor: CursorColor, screenStrokeWidth: 2f);
        }
    }

    public static void DrawUI(Renderer2D renderer, TileEditor2D editor, TextureCache2D textures)
    {
        if (editor.IsActive && editor.Mode == LevelEditorMode2D.Tiles)
            TileEditorMenu2D.Draw(renderer, editor, textures);
    }

    private static void DrawThings(Renderer2D renderer, TileEditor2D editor)
    {
        var pathColor = new XnaColor(130, 180, 210, 170);
        var selectedColor = new XnaColor(255, 214, 64);
        var disabledColor = new XnaColor(130, 130, 140, 180);

        foreach (var thing in editor.PositionThings)
        {
            var descriptor = ThingTypeRegistry2D.Require(thing.TypeKey);
            var markerPosition = new Vector2(thing.X, thing.Y);
            var isSelected = editor.SelectedThingId == thing.ThingId;
            var color = isSelected ? selectedColor : thing.Enabled ? descriptor.EditorColor : disabledColor;
            var radius = editor.PixelsToWorldUnits(isSelected ? 11f : 8f);
            renderer.DrawWorldCircle(markerPosition, radius, color, isSelected ? 4f : 3f);
        }

        foreach (var thing in editor.MovingPlatformThings)
        {
            var start = new Vector2(thing.X, thing.Y);
            var end = start + new Vector2(thing.TravelX, thing.TravelY);
            var isSelected = editor.SelectedThingId == thing.ThingId;
            var color = isSelected ? selectedColor : thing.Enabled ? pathColor : disabledColor;
            renderer.DrawWorldSegment(start, end, color, isSelected ? 3f : 2f);
            renderer.DrawShape(Rectangle2D.FromSize(new Vector2(thing.Width, thing.Height), start),
                outlineColor: color, screenStrokeWidth: isSelected ? 3f : 2f);
            if (isSelected)
            {
                var radius = editor.PixelsToWorldUnits(9f);
                renderer.DrawWorldCircle(start, radius, selectedColor, 3f);
                renderer.DrawWorldCircle(end, radius, selectedColor, 3f);
            }
        }

        if (editor.TryGetPlacementPreview(out var definition, out var position))
        {
            renderer.DrawShape(Rectangle2D.FromSize(new Vector2(definition.Width, definition.Height), position),
                outlineColor: new XnaColor(105, 245, 180, 220), screenStrokeWidth: 3f);
            renderer.DrawWorldSegment(position, position + new Vector2(editor.TileSize * 3f, 0f),
                new XnaColor(105, 245, 180, 180), 2f);
        }

        if (editor.TryGetPositionPlacementPreview(out var positionDefinition, out var positionPreview))
        {
            var descriptor = ThingTypeRegistry2D.Require(positionDefinition.TypeKey);
            renderer.DrawWorldCircle(
                positionPreview,
                editor.PixelsToWorldUnits(11f),
                descriptor.EditorColor,
                3f);
        }
    }

    /// <summary>
    /// Draws grid lines over the tiles currently on screen, in world space so the grid
    /// tracks pan and zoom like the cursor outline. Bounded to the visible region rather
    /// than the whole map (a zoomed-out view could otherwise ask for hundreds of lines)
    /// and clamped to the map bounds so nothing is drawn outside the paintable area.
    /// </summary>
    private static void DrawGrid(Renderer2D renderer, Rect2D visible, Rect2D mapBounds, GridGeometry2D grid)
    {
        if (!visible.TryIntersect(mapBounds, out var clipped) ||
            !clipped.TryGetPositiveSize(out _))
            return;

        var cells = grid.GetCellRange(clipped);
        for (var x = cells.Minimum.X; x <= cells.Maximum.X; x++)
        {
            var worldX = grid.GetCellBounds(new GridCell2D(x, 0)).Left;
            renderer.DrawWorldSegment(new(worldX, clipped.Bottom), new(worldX, clipped.Top), GridColor, 1f);
        }

        for (var y = cells.Minimum.Y; y <= cells.Maximum.Y; y++)
        {
            var worldY = grid.GetCellBounds(new GridCell2D(0, y)).Bottom;
            renderer.DrawWorldSegment(new(clipped.Left, worldY), new(clipped.Right, worldY), GridColor, 1f);
        }
    }

}
