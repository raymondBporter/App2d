namespace App2d.Core.Input;

/// <summary>Physical keyboard state accumulated until the next simulation tick.</summary>
public sealed class KeyboardState2D
{
    private readonly ButtonState<Keys> _buttons = new();

    public bool IsDown(Keys key) => _buttons.IsDown(key);
    public bool WasPressed(Keys key) => _buttons.WasPressed(key);
    public bool WasReleased(Keys key) => _buttons.WasReleased(key);
    public bool IsControlDown => IsDown(Keys.ControlKey) || IsDown(Keys.LControlKey) || IsDown(Keys.RControlKey);
    public bool IsShiftDown => IsDown(Keys.ShiftKey) || IsDown(Keys.LShiftKey) || IsDown(Keys.RShiftKey);

    internal void SetKey(Keys key, bool isDown, bool suppressed) => _buttons.SetButton(key, isDown, suppressed);

    internal void CancelHeld() => _buttons.CancelHeld();
    internal void EndFrame() => _buttons.EndFrame();
}
