namespace App2d.Input;

internal readonly record struct InputButtonState(bool Held, bool Pressed, bool Released);

/// <summary>Combines physical bindings into one logical button, including brief taps.</summary>
internal sealed class ButtonBinding(Keys[] keys, XboxButtons controller = XboxButtons.None,
    MouseButtons mouse = MouseButtons.None)
{
    public InputButtonState Read(InputState input, XboxControllerState2D pad, InputButtonState previous = default)
    {
        var held = (pad.Down & controller) != 0 || input.IsMouseDown(mouse);
        var pressed = (pad.Pressed & controller) != 0 || input.WasMousePressed(mouse);
        var released = (pad.Released & controller) != 0 || input.WasMouseReleased(mouse);
        foreach (var key in keys)
        {
            held |= input.IsKeyDown(key);
            pressed |= input.WasKeyPressed(key);
            released |= input.WasKeyReleased(key);
        }
        return new(held, pressed && !previous.Held, !held && (previous.Held || released));
    }
}
