using App2d.Core.Input;

namespace App2d.Gameplay.Player;

internal readonly record struct InputButtonState(bool Held, bool Pressed, bool Released);

/// <summary>Combines physical bindings into one logical button, including brief taps.</summary>
internal sealed class ButtonBinding(Keys[] keys, GamepadButton2D controller = GamepadButton2D.None,
    MouseButtons mouse = MouseButtons.None)
{
    public InputButtonState Read(InputState input, InputButtonState previous = default)
    {
        var held = input.Gamepad.IsDown(controller) || input.Mouse.IsDown(mouse);
        var pressed = input.Gamepad.WasPressed(controller) || input.Mouse.WasPressed(mouse);
        var released = input.Gamepad.WasReleased(controller) || input.Mouse.WasReleased(mouse);
        foreach (var key in keys)
        {
            held |= input.Keyboard.IsDown(key);
            pressed |= input.Keyboard.WasPressed(key);
            released |= input.Keyboard.WasReleased(key);
        }
        return new(held, pressed && !previous.Held, !held && (previous.Held || released));
    }
}
