using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using ButtonState = Microsoft.Xna.Framework.Input.ButtonState;
using Keys = Microsoft.Xna.Framework.Input.Keys;

namespace App2d.EditorUi;

/// <summary>Input adapter for applications which already use MonoGame's Game loop.</summary>
public sealed class MonoGameImGuiPlatform : IImGuiPlatform
{
    private static readonly Keys[] KeysToPoll = Enum.GetValues<Keys>();
    private readonly Game _game;
    private readonly Queue<char> _characters = [];
    private int _wheel;

    public MonoGameImGuiPlatform(Game game)
    {
        _game = game;
        _game.Window.TextInput += OnTextInput;
    }

    public float DpiScale => Control.FromHandle(_game.Window.Handle)?.DeviceDpi / 96f ?? 1;

    // Buffer event input instead of writing into whichever ImGui context happens to be current.
    private void OnTextInput(object? sender, TextInputEventArgs args)
    {
        if (!char.IsControl(args.Character))
        {
            _characters.Enqueue(args.Character);
        }
    }

    public void UpdateInput(ImGuiIOPtr io)
    {
        var mouse = Mouse.GetState();
        var keys = Keyboard.GetState();
        var active = _game.IsActive;
        io.AddFocusEvent(active);
        io.AddMousePosEvent(active ? mouse.X : -float.MaxValue, active ? mouse.Y : -float.MaxValue);
        io.AddMouseButtonEvent(0, active && mouse.LeftButton == ButtonState.Pressed);
        io.AddMouseButtonEvent(1, active && mouse.RightButton == ButtonState.Pressed);
        io.AddMouseButtonEvent(2, active && mouse.MiddleButton == ButtonState.Pressed);
        if (active)
        {
            io.AddMouseWheelEvent(0, (mouse.ScrollWheelValue - _wheel) / 120f);
        }

        _wheel = mouse.ScrollWheelValue;
        foreach (var key in KeysToPoll)
        {
            if (ImGuiKeyMap.FromMonoGame(key) is { } mapped)
            {
                io.AddKeyEvent(mapped, active && keys.IsKeyDown(key));
            }
        }

        io.AddKeyEvent(ImGuiKey.ModCtrl, active && (keys.IsKeyDown(Keys.LeftControl) || keys.IsKeyDown(Keys.RightControl)));
        io.AddKeyEvent(ImGuiKey.ModShift, active && (keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift)));
        io.AddKeyEvent(ImGuiKey.ModAlt, active && (keys.IsKeyDown(Keys.LeftAlt) || keys.IsKeyDown(Keys.RightAlt)));
        io.AddKeyEvent(ImGuiKey.ModSuper, active && (keys.IsKeyDown(Keys.LeftWindows) || keys.IsKeyDown(Keys.RightWindows)));
        while (_characters.TryDequeue(out var character))
        {
            if (active)
            {
                io.AddInputCharacterUTF16(character);
            }
        }
    }

    public void Dispose()
    {
        if (_game.Window is { } window) window.TextInput -= OnTextInput;
        _characters.Clear();
    }
}
