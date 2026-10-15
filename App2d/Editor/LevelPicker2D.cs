using App2d.Core.Input;
using App2d.Core.Rendering;
using App2d.Levels;
using App2d.Rendering;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Editor;

/// <summary>Offline playground navigation, independent of future in-world travel rules.</summary>
internal sealed class LevelPicker2D(IReadOnlyList<WorldMapAsset2D> maps)
{
    private int _selected;
    private Vector2 _mouse;
    public bool IsOpen
    {
        get; private set;
    }
    private const int PageSize = 5;
    private int First => _selected / PageSize * PageSize;
    private static ScreenRectangle2D Row(int row) => new(40, 140 + row * 48, 480, 182 + row * 48);

    public int? Update(InputState input)
    {
        if (input.IsSuppressed)
        {
            return null;
        }

        if (input.Keyboard.WasPressed(Keys.F2))
        {
            IsOpen = !IsOpen;
        }

        if (!IsOpen)
        {
            return null;
        }

        _mouse = input.Mouse.PositionDevice;
        if (input.Keyboard.WasPressed(Keys.Down))
        {
            _selected = (_selected + 1) % maps.Count;
        }

        if (input.Keyboard.WasPressed(Keys.Up))
        {
            _selected = (_selected + maps.Count - 1) % maps.Count;
        }

        var chosen = input.Keyboard.WasPressed(Keys.Enter);
        for (var row = 0; row < Math.Min(PageSize, maps.Count - First); row++)
        {
            if (input.Mouse.WasPressed(MouseButtons.Left) && Row(row).Contains(_mouse.X, _mouse.Y))
            {
                _selected = First + row;
                chosen = true;
                break;
            }
        }

        if (!chosen)
        {
            return null;
        }

        IsOpen = false;
        return _selected;
    }

    public void Draw(Renderer2D renderer)
    {
        renderer.DrawScreenText("F2  Levels   |   F1  Edit", new(32, 54), XnaColor.White);
        if (!IsOpen)
        {
            return;
        }

        renderer.DrawScreenRoundedRectangle(new(24, 82, 500, 440), 12, new(20, 28, 43, 245));
        renderer.DrawScreenText("LEVEL PLAYGROUND", new(40, 104), new(255, 214, 100));
        for (var row = 0; row < Math.Min(PageSize, maps.Count - First); row++)
        {
            var index = First + row;
            var bounds = Row(row);
            var selected = index == _selected || bounds.Contains(_mouse.X, _mouse.Y);
            renderer.DrawScreenRoundedRectangle(bounds, 6, selected ? new(62, 85, 115) : new(34, 46, 64));
            renderer.DrawScreenText(maps[index].Name, new(54, bounds.Top + 12), XnaColor.White);
        }
        renderer.DrawScreenText("Up/Down + Enter, or click to travel", new(40, 394), XnaColor.White);
        renderer.DrawScreenText("F2 closes   -   game paused", new(40, 416), new(165, 182, 201));
    }
}
