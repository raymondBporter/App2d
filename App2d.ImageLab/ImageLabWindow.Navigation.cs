using App2d.EditorUi;
using ImGuiNET;
using System.Numerics;

namespace App2d.ImageLab;

internal sealed partial class ImageLabWindow
{
    private void HandleArrowPan()
    {
        var io = ImGui.GetIO();
        if (_gui!.WantTextInput || ImGui.IsAnyItemActive() || io.NavVisible || io.KeyCtrl || io.KeyAlt
            || ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopup)
            || (!ImGui.IsWindowHovered() && !ImGui.IsWindowFocused()))
        {
            return;
        }
        // Route these keys to the viewport so ImGui does not also move focus among its controls.
        var direction = Vector2.Zero;
        if (PanKey(ImGuiKey.LeftArrow))
        {
            direction.X--;
        }
        if (PanKey(ImGuiKey.RightArrow))
        {
            direction.X++;
        }
        if (PanKey(ImGuiKey.UpArrow))
        {
            direction.Y--;
        }
        if (PanKey(ImGuiKey.DownArrow))
        {
            direction.Y++;
        }
        if (direction != Vector2.Zero)
        {
            _pan += direction * (32 * Ui.Scale * (io.KeyShift ? 3 : 1));
            _fit = false;
        }
    }

    private static bool PanKey(ImGuiKey key)
    {
        var flags = ImGuiInputFlags.RouteGlobal | ImGuiInputFlags.Repeat;
        var normal = ImGui.Shortcut(key, flags);
        var fast = ImGui.Shortcut(key | ImGuiKey.ModShift, flags);
        return normal || fast;
    }

    internal void ArrowPanStudy(Action draw)
    {
        _fit = false;
        _zoom = 1;
        _pan = Vector2.Zero;
        draw();
        draw();
        var io = ImGui.GetIO();
        io.ConfigInputTrickleEventQueue = false;
        var frame = _history.Current!;
        var steps = _history.Position;
        var center = new Vector2(frame.Width / 2f, frame.Height / 2f);
        PointAtPixelForStudy(center);
        draw();
        draw();
        var step = 32 * Ui.Scale;
        Tap(ImGuiKey.RightArrow);
        Check(new(step, 0));
        Tap(ImGuiKey.LeftArrow);
        Check(Vector2.Zero);
        Tap(ImGuiKey.DownArrow);
        Check(new(0, step));
        Tap(ImGuiKey.UpArrow);
        Check(Vector2.Zero);
        _zoom = 8;
        draw();
        PointAtPixelForStudy(center);
        draw();
        draw();
        Tap(ImGuiKey.RightArrow);
        Check(new(step, 0)); // Same screen movement at a different zoom.
        io.AddKeyEvent(ImGuiKey.ModShift, true);
        draw();
        Tap(ImGuiKey.DownArrow);
        Check(new(step, step * 3));
        io.AddKeyEvent(ImGuiKey.ModShift, false);
        draw();
        io.AddKeyEvent(ImGuiKey.RightArrow, true);
        for (var i = 0; i < 30; i++)
        {
            draw();
        }
        io.AddKeyEvent(ImGuiKey.RightArrow, false);
        draw();
        if (_pan.X <= step * 2 || _zoom != 8 || _history.Position != steps)
        {
            throw new InvalidOperationException("Held arrows must repeat without changing zoom or history.");
        }

        void Tap(ImGuiKey key)
        {
            io.AddKeyEvent(key, true);
            draw();
            draw();
            io.AddKeyEvent(key, false);
            draw();
            draw();
        }
        void Check(Vector2 expected)
        {
            if (Vector2.Distance(_pan, expected) > .001f)
            {
                throw new InvalidOperationException($"Arrow pan was {_pan}; expected {expected}.");
            }
        }
    }
}
