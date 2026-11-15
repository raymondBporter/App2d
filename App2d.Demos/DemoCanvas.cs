using App2d.EditorUi;
using ImGuiNET;
using System.Numerics;

namespace App2d.Demos;

internal readonly record struct DemoCanvas(Vector2 Min, Vector2 Size, Vector2f Center, float Scale,
    bool Activated, bool Active, ImDrawListPtr Draw)
{
    internal Vector2 Max => Min + Size;
    internal Vector2f WorldMin => WorldAt(new(Min.X, Max.Y));
    internal Vector2f WorldMax => WorldAt(new(Max.X, Min.Y));

    internal static void Layout(Action canvas, Action controls)
    {
        var room = ImGui.GetContentRegionAvail();
        var sidebar = MathF.Min(280f * Ui.Scale, room.X * .4f);
        var canvasWidth = MathF.Max(1f, room.X - sidebar - ImGui.GetStyle().ItemSpacing.X);
        Ui.Row(
            () => Ui.Panel("canvas", canvas, size: new(canvasWidth, room.Y), border: false, flags: ImGuiWindowFlags.NoScrollbar),
            () => Ui.Panel("controls", controls, size: new(sidebar, room.Y)));
    }

    internal static DemoCanvas Begin(string id, Vector2f center, Vector2f minimumWorldSize)
    {
        var min = ImGui.GetCursorScreenPos();
        var size = Vector2.Max(Vector2.One, ImGui.GetContentRegionAvail());
        ImGui.InvisibleButton(id, size, ImGuiButtonFlags.MouseButtonLeft);
        var scale = MathF.Min(size.X / minimumWorldSize.X, size.Y / minimumWorldSize.Y);
        return new(min, size, center, scale, ImGui.IsItemActivated(), ImGui.IsItemActive(), ImGui.GetWindowDrawList());
    }

    internal Vector2 ScreenAt(Vector2f point)
    {
        var delta = point - Center;
        return Min + Size / 2f + new Vector2(delta.X, -delta.Y) * Scale;
    }

    internal Vector2f WorldAt(Vector2 screen)
    {
        var delta = (screen - Min - Size / 2f) / Scale;
        return Center + new Vector2f(delta.X, -delta.Y);
    }

    internal Vector2f Pointer() => WorldAt(Vector2.Clamp(ImGui.GetMousePos(), Min, Max));

    internal void DrawGrid()
    {
        Draw.AddRectFilled(Min, Max, Ui.Color(14, 19, 28));
        var minimum = WorldMin;
        var maximum = WorldMax;
        var spacing = MathF.Max(1f, MathF.Ceiling(22f * Ui.Scale / Scale));
        var grid = Ui.Color(36, 46, 61);
        for (var x = MathF.Ceiling(minimum.X / spacing) * spacing; x < maximum.X; x += spacing)
        {
            Draw.AddLine(ScreenAt(new(x, minimum.Y)), ScreenAt(new(x, maximum.Y)), grid);
        }
        for (var y = MathF.Ceiling(minimum.Y / spacing) * spacing; y < maximum.Y; y += spacing)
        {
            Draw.AddLine(ScreenAt(new(minimum.X, y)), ScreenAt(new(maximum.X, y)), grid);
        }
    }
}
