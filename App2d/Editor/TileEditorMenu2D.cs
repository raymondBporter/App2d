using App2d.Rendering;
using App2d.Rendering.Textures;
using App2d.Tiles;
using SkiaSharp;
using System.Numerics;

namespace App2d.Editor;

/// <summary>Shared screen-space layout, hit testing, and drawing for the tile palette.</summary>
internal static class TileEditorMenu2D
{
    private const float PanelWidth = 360f;
    private const float Padding = 18f;
    private const float ButtonHeight = 52f;
    private const float TileTypeButtonHeight = 110f;
    private const float ButtonGap = 10f;
    private const float SectionGap = 38f;

    private static readonly (string Label, TileKind2D Kind)[] TileTypes =
    [
        ("Filled", TileKind2D.Solid),
        ("Grippable", TileKind2D.Solid | TileKind2D.Grippable),
        ("One-way", TileKind2D.OneWay),
        ("Spikes", TileKind2D.Spikes)
    ];

    private static readonly SKColor PanelColor = new(17, 24, 37, 242);
    private static readonly SKColor DividerColor = new(62, 77, 99, 255);
    private static readonly SKColor ButtonColor = new(35, 47, 66, 255);
    private static readonly SKColor HoverColor = new(50, 65, 88, 255);
    private static readonly SKColor SelectedColor = new(255, 214, 64, 255);
    private static readonly SKColor TextColor = new(244, 247, 252, 255);
    private static readonly SKColor MutedTextColor = new(160, 174, 196, 255);
    private static readonly SKColor SelectedTextColor = new(22, 29, 41, 255);

    public static bool Contains(Vector2 viewportSize, Vector2 point) =>
        GetPanelBounds(viewportSize).Contains(point.X, point.Y);

    public static void TrySelect(TileEditor2D editor, Vector2 viewportSize, Vector2 point)
    {
        if (GetThingsButton(viewportSize).Contains(point.X, point.Y))
        {
            editor.SelectMode(LevelEditorMode2D.Things);
            return;
        }

        for (var index = 0; index < editor.TilesetIds.Count; index++)
        {
            if (GetTilesetButton(viewportSize, index).Contains(point.X, point.Y))
            {
                editor.SelectTileset(index);
                return;
            }
        }

        for (var index = 0; index < TileTypes.Length; index++)
        {
            if (GetTileTypeButton(viewportSize, editor.TilesetIds.Count, index).Contains(point.X, point.Y))
            {
                editor.SelectKind(TileTypes[index].Kind);
                return;
            }
        }
    }

    public static void Draw(Renderer2D renderer, TileEditor2D editor, TextureCache2D textures)
    {
        var viewport = editor.VisibleDeviceSize;
        var panel = GetPanelBounds(viewport);
        renderer.DrawScreenRoundedRectangle(panel, 0f, PanelColor);
        renderer.DrawScreenRoundedRectangle(
            new SKRect(panel.Left, panel.Top, panel.Left + 2f, panel.Bottom),
            0f,
            DividerColor);

        var left = panel.Left + Padding;
        renderer.DrawScreenText("TILE EDITOR", new Vector2(left, 42f), TextColor);
        renderer.DrawScreenText("TILESET", new Vector2(left, 80f), MutedTextColor);

        for (var index = 0; index < editor.TilesetIds.Count; index++)
        {
            DrawButton(
                renderer,
                GetTilesetButton(viewport, index),
                editor.TilesetIds[index],
                editor.SelectedTilesetIndex == index,
                editor.MouseDevicePosition);
        }

        var typeLabelY = GetTileTypeStartY(editor.TilesetIds.Count) - 14f;
        renderer.DrawScreenText("TILE TYPE", new Vector2(left, typeLabelY), MutedTextColor);
        for (var index = 0; index < TileTypes.Length; index++)
        {
            DrawTileTypeButton(
                renderer,
                textures,
                GetTileTypeButton(viewport, editor.TilesetIds.Count, index),
                TileTypes[index].Label,
                TileTypes[index].Kind,
                editor.SelectedTilesetId,
                editor.SelectedKind == TileTypes[index].Kind,
                editor.MouseDevicePosition);
        }

        DrawButton(
            renderer,
            GetThingsButton(viewport),
            "Things",
            isSelected: false,
            editor.MouseDevicePosition);

        var position = editor.TryGetHoveredTile(out var tileX, out var tileY)
            ? $"Tile  {tileX}, {tileY}"
            : "Tile  —";
        renderer.DrawScreenText(position, new Vector2(left, panel.Bottom - 64f), MutedTextColor);
        renderer.DrawScreenText("Ctrl+Z  Undo     F1  Play", new Vector2(left, panel.Bottom - 24f), TextColor);
    }

    private static void DrawButton(
        Renderer2D renderer,
        SKRect bounds,
        string label,
        bool isSelected,
        Vector2 pointer)
    {
        var isHovered = bounds.Contains(pointer.X, pointer.Y);
        var background = isSelected ? SelectedColor : isHovered ? HoverColor : ButtonColor;
        renderer.DrawScreenRoundedRectangle(bounds, 8f, background);
        renderer.DrawScreenText(
            label,
            new Vector2(bounds.Left + 12f, bounds.Top + 35f),
            isSelected ? SelectedTextColor : TextColor);
    }

    private static void DrawTileTypeButton(
        Renderer2D renderer,
        TextureCache2D textures,
        SKRect bounds,
        string label,
        TileKind2D kind,
        string tilesetId,
        bool isSelected,
        Vector2 pointer)
    {
        var isHovered = bounds.Contains(pointer.X, pointer.Y);
        var background = isSelected ? SelectedColor : isHovered ? HoverColor : ButtonColor;
        renderer.DrawScreenRoundedRectangle(bounds, 8f, background);

        const float previewSize = 62f;
        var previewLeft = bounds.MidX - previewSize / 2f;
        var preview = new SKRect(
            previewLeft,
            bounds.Top + 8f,
            previewLeft + previewSize,
            bounds.Top + 8f + previewSize);
        DrawTilePreview(renderer, textures, preview, tilesetId, kind);
        renderer.DrawScreenText(
            label,
            new Vector2(bounds.Left + 10f, bounds.Bottom - 9f),
            isSelected ? SelectedTextColor : TextColor);
    }

    private static void DrawTilePreview(
        Renderer2D renderer,
        TextureCache2D textures,
        SKRect bounds,
        string tilesetId,
        TileKind2D kind)
    {
        var root = Path.Combine("environments", "tilesets", tilesetId);
        if (kind.IsGrippable())
        {
            var grippablePath = Path.Combine(root, "grippable.png");
            if (!File.Exists(Path.Combine(textures.ContentRoot, grippablePath)))
                grippablePath = Path.Combine(root, "fill.png");
            renderer.DrawScreenTexture(textures.Load(grippablePath), bounds);
            return;
        }

        if (kind.IsOneWay())
        {
            renderer.DrawScreenTexture(
                textures.Load(Path.Combine(root, "one-way", "standalone.png")),
                bounds);
            return;
        }

        if (kind.IsSpikes())
        {
            var spikePath = Path.Combine(root, "hazards", "spikes", "standalone.png");
            if (!File.Exists(Path.Combine(textures.ContentRoot, spikePath)))
                spikePath = Path.Combine(root, "one-way", "standalone.png");
            renderer.DrawScreenTexture(textures.Load(spikePath), bounds);
            return;
        }

        renderer.DrawScreenTexture(textures.Load(Path.Combine(root, "fill.png")), bounds);
        var topHeight = MathF.Max(8f, bounds.Height * 0.22f);
        renderer.DrawScreenTexture(
            textures.Load(Path.Combine(root, "surfaces", "top.png")),
            new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Top + topHeight));
    }

    private static SKRect GetPanelBounds(Vector2 viewportSize) =>
        new(MathF.Max(0f, viewportSize.X - PanelWidth), 0f, viewportSize.X, viewportSize.Y);

    private static SKRect GetTilesetButton(Vector2 viewportSize, int index)
    {
        var panel = GetPanelBounds(viewportSize);
        var top = 94f + index * (ButtonHeight + ButtonGap);
        return new SKRect(panel.Left + Padding, top, panel.Right - Padding, top + ButtonHeight);
    }

    private static float GetTileTypeStartY(int tilesetCount) =>
        94f + tilesetCount * (ButtonHeight + ButtonGap) + SectionGap;

    private static SKRect GetTileTypeButton(Vector2 viewportSize, int tilesetCount, int index) =>
        GetGridButton(viewportSize, GetTileTypeStartY(tilesetCount), index);

    private static SKRect GetGridButton(Vector2 viewportSize, float startY, int index)
    {
        var panel = GetPanelBounds(viewportSize);
        var availableWidth = panel.Width - Padding * 2f - ButtonGap;
        var width = availableWidth / 2f;
        var column = index % 2;
        var row = index / 2;
        var left = panel.Left + Padding + column * (width + ButtonGap);
        var top = startY + row * (TileTypeButtonHeight + ButtonGap);
        return new SKRect(left, top, left + width, top + TileTypeButtonHeight);
    }

    private static SKRect GetThingsButton(Vector2 viewportSize)
    {
        var panel = GetPanelBounds(viewportSize);
        return new SKRect(
            panel.Left + Padding,
            panel.Bottom - 142f,
            panel.Right - Padding,
            panel.Bottom - 90f);
    }
}
