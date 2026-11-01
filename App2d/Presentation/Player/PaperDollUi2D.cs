using App2d.Core;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Drawing;
using App2d.Core.Rendering;
using App2d.Core.Rendering.Characters;
using App2d.EditorUi;
using App2d.Presentation.Persons;
using ImGuiNET;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace App2d.Presentation.Player;

/// <summary>The in-game paper doll. Its preview and item icons render the actual authored parts.</summary>
internal sealed class PaperDollUi2D(PlayerWardrobe2D wardrobe)
{
    private readonly record struct Cell(int Index, WardrobeSlot2D? Slot = null);
    private readonly Dictionary<string, (RenderTarget2D Target, nint Id)> _icons = [];
    private readonly PuppetDrawing _drawing = new(), _iconDrawing = new();
    private PointCharacterRenderer? _renderer;
    private RenderTarget2D? _preview;
    private nint _previewId;
    private Cell? _pressed, _hovered;
    private bool _dragging;
    private double _clock;
    private Vector2 _origin;
    private float _scale;
    private string _message = "Drag to equip. Double-click an item to equip it, or right-click a slot to remove it.";

    private static readonly Color4b Panel = new(18, 24, 35), Stage = new(24, 33, 48), CellFill = new(29, 41, 58);
    private static readonly Color4b Border = new(59, 78, 98), Accent = new(126, 221, 197), Muted = new(147, 166, 185);
    private static readonly Color4b Gold = new(239, 204, 137), WrongSlot = new(230, 126, 114);

    public bool IsOpen { get; private set; }
    public void Open()
    {
        IsOpen = true;
        _pressed = null;
        _dragging = false;
    }

    /// <returns>Whether a shortcut was consumed, including Escape when closing.</returns>
    public bool Draw(GraphicsDevice device, ImGuiHost gui, FrameTime time, bool canOpen)
    {
        var shortcut = false;
        if ((canOpen && ImGui.IsKeyPressed(ImGuiKey.F7, false)) || (IsOpen && ImGui.IsKeyPressed(ImGuiKey.Escape, false)))
        {
            IsOpen = !IsOpen;
            _pressed = null;
            _dragging = false;
            shortcut = true;
        }
        if (!IsOpen) return shortcut;

        var io = ImGui.GetIO();
        _scale = MathF.Min(Ui.Scale, MathF.Min((io.DisplaySize.X - 32) / 1080, (io.DisplaySize.Y - 32) / 700));
        var size = new Vector2(1080, 700) * _scale;
        _origin = (io.DisplaySize - size) / 2;
        _clock += time.DeltaSeconds;
        PrepareImages(device, gui);

        ImGui.GetBackgroundDrawList().AddRectFilled(Vector2.Zero, io.DisplaySize, new Color4b(4, 8, 15, 195).PackedValue);
        ImGui.SetNextWindowPos(_origin, ImGuiCond.Always);
        ImGui.SetNextWindowSize(size, ImGuiCond.Always);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 14 * _scale);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Panel.PackedValue);
        var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove |
            ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        ImGui.Begin("Equipment###PaperDoll", flags);
        var draw = ImGui.GetWindowDrawList();
        Text(draw, At(28, 19), "Equipment", 30, Color4b.White);
        Text(draw, At(29, 57), "One character. A different look.", 14, Muted);
        ImGui.SetCursorScreenPos(At(937, 27));
        if (ImGui.Button("Close [F7]", new Vector2(112, 32) * _scale))
        {
            IsOpen = false;
            _pressed = null;
            _dragging = false;
            shortcut = true;
        }
        draw.AddLine(At(24, 83), At(1056, 83), Border.PackedValue);
        draw.AddRectFilled(At(24, 99), At(528, 613), Stage.PackedValue, 10 * _scale);
        Text(draw, At(46, 118), "PAPER DOLL", 13, Accent);
        Text(draw, At(381, 118), "Paired slots", 13, Muted);
        draw.AddImage(_previewId, At(142, 151), At(410, 551));
        draw.AddLine(At(170, 548), At(383, 548), Border.PackedValue, _scale);
        // The links point toward the hands and feet, while the slots remain outside the figure.
        draw.AddLine(At(127, 304), At(204, 337), Border.PackedValue, _scale);
        draw.AddLine(At(432, 481), At(345, 520), Border.PackedValue, _scale);

        _hovered = null;
        DrawCell(draw, new(0, WardrobeSlot2D.Gloves), At(48, 264), 78, "GLOVES");
        DrawCell(draw, new(0, WardrobeSlot2D.Shoes), At(432, 442), 78, "SHOES");
        DrawCell(draw, new(0, WardrobeSlot2D.Shins), At(48, 442), 78, "SHINS");
        draw.AddLine(At(127, 481), At(228, 454), Border.PackedValue, _scale);
        Text(draw, At(46, 550), "Gloves / " + (wardrobe.Equipped(WardrobeSlot2D.Gloves)?.Name ?? "Bare hands"), 14, Muted);
        Text(draw, At(46, 573), "Shoes  / " + (wardrobe.Equipped(WardrobeSlot2D.Shoes)?.Name ?? "Bare feet"), 14, Muted);
        Text(draw, At(46, 596), "Shins  / " + (wardrobe.Equipped(WardrobeSlot2D.Shins)?.Name ?? "Bare shins"), 14, Muted);

        Text(draw, At(570, 111), "Inventory", 23, Color4b.White);
        Text(draw, At(570, 143), "Drop a pair onto its matching slot.", 14, Muted);
        for (var i = 0; i < wardrobe.Inventory.Count; i++)
            DrawCell(draw, new(i), At(570 + i % PlayerWardrobe2D.Columns * 74, 176 + i / PlayerWardrobe2D.Columns * 74), 66);

        FinishDrag();
        draw.AddLine(At(24, 634), At(1056, 634), Border.PackedValue);
        Text(draw, At(28, 649), _message, 14, Gold);
        Text(draw, At(28, 674), "Game paused  /  F7 or Esc to return", 12, Muted);
        ImGui.End();
        ImGui.PopStyleColor();
        ImGui.PopStyleVar(2);
        return shortcut;
    }

    private Vector2 At(float x, float y) => _origin + new Vector2(x, y) * _scale;
    private void Text(ImDrawListPtr draw, Vector2 position, string text, float size, Color4b color) =>
        draw.AddText(ImGui.GetFont(), size * _scale, position, color.PackedValue, text);
    private WardrobeItem2D? Item(Cell cell) => cell.Slot is { } slot ? wardrobe.Equipped(slot) : wardrobe.Inventory[cell.Index];

    private void DrawCell(ImDrawListPtr draw, Cell cell, Vector2 min, float width, string? label = null)
    {
        var size = new Vector2(width) * _scale;
        var max = min + size;
        var item = Item(cell);
        var mouse = ImGui.GetIO().MousePos;
        var hovered = mouse.X >= min.X && mouse.X < max.X && mouse.Y >= min.Y && mouse.Y < max.Y;
        if (hovered) _hovered = cell;
        var edge = hovered ? Accent : Border;
        if (_dragging && _pressed is { } source && Item(source) is { } dragged && cell.Slot is { } targetSlot)
            edge = dragged.Slot == targetSlot ? Accent : WrongSlot;
        draw.AddRectFilled(min, max, CellFill.PackedValue, 7 * _scale);
        draw.AddRect(min, max, edge.PackedValue, 7 * _scale, ImDrawFlags.None, (hovered || _dragging ? 2 : 1) * _scale);
        if (item is not null)
        {
            var pad = new Vector2(7) * _scale;
            draw.AddImage(_icons[item.Id].Id, min + pad, max - pad, Vector2.Zero, Vector2.One,
                new Color4b(255, 255, 255, _dragging && _pressed == cell ? 85 : 255).PackedValue);
        }
        else if (cell.Slot is not null)
        {
            Text(draw, min + new Vector2(18, 29) * _scale, "Empty", 14, Muted);
        }
        if (label is not null) Text(draw, min - new Vector2(0, 23) * _scale, label, 13, Gold);

        ImGui.SetCursorScreenPos(min);
        ImGui.InvisibleButton(cell.Slot is { } slot ? $"equipped-{slot}" : $"inventory-{cell.Index}", size);
        if (hovered && item is not null && ImGui.IsMouseClicked(ImGuiMouseButton.Left)) _pressed = cell;
        if (hovered && item is not null && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
        {
            if (cell.Slot is { } equipped) wardrobe.Unequip(equipped);
            else wardrobe.Equip(cell.Index, item.Slot);
            _pressed = null;
        }
        if (hovered && cell.Slot is { } worn && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            if (wardrobe.Unequip(worn)) _message = "Returned to inventory.";
        }
        if (hovered && !_dragging && item is not null)
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted(item.Name);
            ImGui.TextDisabled(item.Slot switch { WardrobeSlot2D.Shoes => "Shoes / both feet", WardrobeSlot2D.Gloves => "Gloves / both hands", _ => "Shins / both legs" });
            ImGui.TextDisabled(item.Settings.Replace ? "Replaces base artwork while equipped" : "Worn over base artwork");
            ImGui.EndTooltip();
        }
    }

    private void FinishDrag()
    {
        if (_pressed is { } pressed && ImGui.IsMouseDragging(ImGuiMouseButton.Left, 4)) _dragging = true;
        if (_dragging && _pressed is { } source && Item(source) is { } item)
        {
            var mouse = ImGui.GetIO().MousePos;
            var size = new Vector2(64) * _scale;
            ImGui.GetForegroundDrawList().AddImage(_icons[item.Id].Id, mouse - size / 2, mouse + size / 2);
            if (ImGui.IsMouseReleased(ImGuiMouseButton.Left) && _hovered is { } target && target != source)
            {
                var changed = target.Slot is { } slot
                    ? source.Slot is null && wardrobe.Equip(source.Index, slot)
                    : source.Slot is { } worn ? wardrobe.Unequip(worn, target.Index) : wardrobe.Move(source.Index, target.Index);
                _message = changed ? "Loadout updated." : "That item belongs in a different slot.";
            }
        }
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            _pressed = null;
            _dragging = false;
        }
    }

    private void PrepareImages(GraphicsDevice device, ImGuiHost gui)
    {
        _renderer ??= new(device);
        foreach (var item in wardrobe.Items)
        {
            if (_icons.ContainsKey(item.Id)) continue;
            var target = RenderCapture2D.CreateTarget(device, 112, 112);
            _iconDrawing.Build(item.Artwork);
            RenderDrawing(device, target, _iconDrawing, .76f);
            _icons.Add(item.Id, (target, gui.Register(target)));
        }
        var width = Math.Max(1, (int)(268 * _scale));
        var height = Math.Max(1, (int)(400 * _scale));
        if (_preview is null || _preview.Width != width || _preview.Height != height)
        {
            if (_preview is not null)
            {
                gui.Unregister(_previewId);
                _preview.Dispose();
            }
            _preview = RenderCapture2D.CreateTarget(device, width, height);
            _previewId = gui.Register(_preview);
        }
        var moves = wardrobe.Moves;
        var idle = moves[PersonMoves.Idle];
        var pose = PoseEvaluator.Sample(moves.Model, idle, _clock, repeat: true, new("relaxed") { InPlace = true, Clock = _clock });
        _drawing.Build(moves.Model, pose, wardrobe.Artwork);
        var actor = new ActorPose(pose, Vector2.Zero, 1);
        foreach (var (prop, socket) in wardrobe.Dress(idle, (float)(_clock % idle.Duration), PersonGear.None))
            _drawing.AddAttachment(prop, actor, socket);
        foreach (var attachment in wardrobe.Attachments) _drawing.AddAttachment(attachment, actor);
        RenderDrawing(device, _preview, _drawing, .91f);
    }

    private void RenderDrawing(GraphicsDevice device, RenderTarget2D target, PuppetDrawing drawing, float fit)
    {
        var min = drawing.Mesh.Min;
        var max = drawing.Mesh.Max;
        var ppu = MathF.Min(target.Width / MathF.Max(.01f, max.X - min.X), target.Height / MathF.Max(.01f, max.Y - min.Y)) * fit;
        var anchor = new Vector2(target.Width / 2f - (min.X + max.X) / 2 * ppu, target.Height / 2f + (min.Y + max.Y) / 2 * ppu);
        using var bound = device.UseRenderTarget(target, Color4b.Transparent);
        _renderer!.DrawActor(drawing, PointCharacterRenderer.Projection(target.Width, target.Height, anchor, ppu), Matrix.Identity);
    }

    public void ReleaseDevice(ImGuiHost? gui)
    {
        if (_preview is not null) gui?.Unregister(_previewId);
        _preview?.Dispose();
        _preview = null;
        foreach (var (target, id) in _icons.Values)
        {
            gui?.Unregister(id);
            target.Dispose();
        }
        _icons.Clear();
        _renderer?.Dispose();
        _renderer = null;
    }
}
