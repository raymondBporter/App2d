using App2d.Core.Rendering.Textures;
using App2d.Core.Tiles;
using App2d.EditorUi;
using App2d.Presentation.Assets;
using ImGuiNET;
using System.Numerics;

namespace App2d.Editor;

/// <summary>The map tile palette, composed from shared editor panels and ImGui image buttons.</summary>
internal static class TileEditorMenu2D
{
    private static readonly (string Label, TileKind2D Kind)[] TileTypes =
    [
        ("Filled", TileKind2D.Solid), ("Grippable", TileKind2D.Solid | TileKind2D.Grippable),
        ("One-way", TileKind2D.OneWay), ("Spikes", TileKind2D.Spikes), ("Ladder", TileKind2D.Ladder)
    ];

    public static void Draw(TileEditor2D editor, TextureCache2D textures, Func<string, nint> textureId)
    {
        Ui.Section("Tileset", () =>
        {
            if (Ui.Combo("Terrain", editor.SelectedTilesetId, editor.TilesetIds) is { } selected)
            {
                editor.SelectTileset(editor.TilesetIds.ToList().IndexOf(selected));
            }
        });
        Ui.Section("Tile type", () =>
        {
            if (ImGui.BeginTable("tile-types", 3))
            {
                foreach (var (label, kind) in TileTypes)
                {
                    ImGui.TableNextColumn();
                    ImGui.PushID(label);
                    var path = PreviewPath(textures, editor.SelectedTilesetId, kind);
                    var selected = editor.SelectedKind == kind;
                    if (selected)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetStyle().Colors[(int)ImGuiCol.HeaderActive]);
                    }

                    if (ImGui.ImageButton("preview", textureId(path), new Vector2(58, 58) * Ui.Scale))
                    {
                        editor.SelectKind(kind);
                    }

                    if (selected)
                    {
                        ImGui.PopStyleColor();
                    }

                    if (ImGui.Selectable(label, selected))
                    {
                        editor.SelectKind(kind);
                    }

                    ImGui.PopID();
                }
                ImGui.EndTable();
            }
        });
        Ui.Section("Painting", () =>
        {
            Ui.Button("Undo tiles [Ctrl+Z]", editor.UndoTiles);
            Ui.Help("Left drag paints. Right drag erases. Middle drag pans. Scroll zooms.");
            if (editor.TryGetHoveredTile(out var x, out var y))
            {
                ImGui.TextUnformatted($"Tile {x}, {y}");
            }
        });
    }

    private static string PreviewPath(TextureCache2D textures, string tileset, TileKind2D kind)
    {
        var root = Path.Combine("environments", "tilesets", tileset);
        if (kind.IsLadder())
        {
            return LadderAssets2D.ResolvePath(textures, tileset, isTop: true);
        }

        var path = kind.IsGrippable() ? Path.Combine(root, "grippable.png") :
            kind.IsSpikes() ? Path.Combine(root, "hazards", "spikes", "standalone.png") :
            kind.IsOneWay() ? Path.Combine(root, "one-way", "standalone.png") : Path.Combine(root, "fill.png");
        return File.Exists(Path.Combine(textures.ContentRoot, path)) ? path : Path.Combine(root, kind.IsSpikes() ? "one-way/standalone.png" : "fill.png");
    }
}
