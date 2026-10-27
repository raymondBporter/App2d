using App2d.Core.Characters.Authored;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Grids;
using App2d.Core.Mathematics;
using App2d.Core.Objects;
using App2d.Core.Shapes;
using App2d.Presentation;
using App2d.Rendering;
using App2d.Things;
using System.Numerics;

namespace App2d.Editor;

/// <summary>Draws the tile grid, cursor and editor status while editor mode is active.</summary>
internal static class TileEditorView2D
{
    public static void DrawWorldDebug(Renderer2D renderer, TileEditor2D editor)
    {
        if (!editor.IsActive)
        {
            return;
        }

        DrawGrid(renderer, editor.VisibleWorldBounds, editor.MapBounds, editor.GridGeometry);

        if (editor.Mode == LevelEditorMode2D.Things)
        {
            DrawThings(renderer, editor);
            return;
        }

        var hasTile = editor.TryGetHoveredTile(out var tileX, out var tileY);
        if (hasTile)
        {
            var bounds = editor.GridGeometry.GetCellBounds(new Vector2i(tileX, tileY));
            renderer.DrawShape(Rectangle2D.Builder.Create(bounds.Min, bounds.Max),
                outlineColor: GameUiTheme.EditorSelection, screenStrokeWidth: 2f);
        }
    }

    private static void DrawThings(Renderer2D renderer, TileEditor2D editor)
    {
        var selectedColor = GameUiTheme.EditorSelection;
        var disabledColor = GameUiTheme.EditorDisabled;

        foreach (var thing in editor.PositionThings)
        {
            var descriptor = ThingTypeRegistry2D.Require(thing.TypeKey);
            var markerPosition = new Vector2(thing.X, thing.Y);
            var isSelected = editor.SelectedThingId == thing.ThingId;
            var color = isSelected ? selectedColor : thing.Enabled ? descriptor.EditorColor : disabledColor;
            if (descriptor.IsObject && editor.TryGetObjectAsset(thing.Asset, out var asset))
            {
                DrawObject(renderer, editor, asset, markerPosition, color, isSelected);
                continue;
            }

            var radius = editor.PixelsToWorldUnits(isSelected ? 11f : 8f);
            renderer.DrawWorldCircle(markerPosition, radius, color, isSelected ? 4f : 3f);
        }

        if (editor.TryGetPositionPlacementPreview(out var positionDefinition, out var positionPreview))
        {
            var descriptor = ThingTypeRegistry2D.Require(positionDefinition.TypeKey);
            if (descriptor.IsObject && editor.TryGetObjectAsset(positionDefinition.Asset, out var previewAsset))
            {
                DrawObject(renderer, editor, previewAsset, positionPreview, GameUiTheme.EditorPlacementPreview, isSelected: false);
            }
            else
            {
                renderer.DrawWorldCircle(positionPreview, editor.PixelsToWorldUnits(11f), descriptor.EditorColor, 3f);
            }
        }
    }

    /// <summary>An object's collider (or drawn shape) outline at its placement, and its path when it moves.</summary>
    private static void DrawObject(Renderer2D renderer, TileEditor2D editor, Part asset, Vector2 position, Color4b color, bool isSelected)
    {
        renderer.DrawShape((asset.Physics is not null ? App2d.Core.Characters.Geometry.PartShapes2D.Collision(asset) : new App2d.Core.Rendering.Characters.PartShader2D(asset).BoundsShape()), Matrix3x2.CreateTranslation(position), outlineColor: color, screenStrokeWidth: isSelected ? 3f : 2f);
        var path = ObjectPathPreview2D.Sample(asset, position);
        if (path.Length == 0)
        {
            return;
        }

        renderer.DrawWorldPolyline(path, color, isSelected ? 3f : 2f);
        if (isSelected)
        {
            var radius = editor.PixelsToWorldUnits(9f);
            renderer.DrawWorldCircle(path[0], radius, color, 3f);
            renderer.DrawWorldCircle(path[^1], radius, color, 3f);
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
        {
            return;
        }

        var cells = grid.GetCellRange(clipped);
        for (var x = cells.Minimum.X; x <= cells.Maximum.X; x++)
        {
            var worldX = grid.GetCellBounds(new Vector2i(x, 0)).Left;
            renderer.DrawWorldSegment(new(worldX, clipped.Bottom), new(worldX, clipped.Top), GameUiTheme.EditorGrid, 1f);
        }

        for (var y = cells.Minimum.Y; y <= cells.Maximum.Y; y++)
        {
            var worldY = grid.GetCellBounds(new Vector2i(0, y)).Bottom;
            renderer.DrawWorldSegment(new(clipped.Left, worldY), new(clipped.Right, worldY), GameUiTheme.EditorGrid, 1f);
        }
    }

}
