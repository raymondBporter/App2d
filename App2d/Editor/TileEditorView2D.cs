using App2d.Rendering;
using App2d.Tiles;
using SkiaSharp;
using System.Numerics;

namespace App2d.Editor;

/// <summary>Draws the tile cursor and editor status while editor mode is active.</summary>
internal static class TileEditorView2D
{
    private static readonly SKColor CursorColor = new(255, 214, 64);

    public static void Draw(Renderer2D renderer, TileEditor2D editor, Vector2 origin, float tileSize)
    {
        if (!editor.IsActive)
            return;

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
