using System.Numerics;

namespace App2d.Core.Input;

/// <summary>Raw device state for one simulation tick. Game and editor bindings read this state separately.</summary>
public sealed class InputState
{
    private readonly XboxControllerInput2D _gamepadInput = new();
    private bool _isSuppressed;
    private bool _isWindowActive = true;

    public KeyboardState2D Keyboard { get; } = new();
    public MouseState2D Mouse { get; } = new();
    public GamepadState2D Gamepad { get; private set; }
    public bool IsSuppressed => _isSuppressed || !_isWindowActive;

    internal void Attach(Form window, Control surface)
    {
        window.KeyPreview = true;
        window.KeyDown += (_, e) => SetKey(e.KeyCode, true);
        window.KeyUp += (_, e) => SetKey(e.KeyCode, false);
        window.Deactivate += (_, _) => SetWindowActive(false);
        window.Activated += (_, _) => SetWindowActive(true);

        surface.PreviewKeyDown += (_, e) =>
        {
            if (e.KeyCode is Keys.Left or Keys.Right or Keys.Up or Keys.Down)
                e.IsInputKey = true;
        };
        surface.MouseMove += (_, e) => Mouse.SetClientPosition(new Vector2(e.X, e.Y));
        surface.MouseDown += (_, e) =>
        {
            surface.Focus();
            Mouse.SetClientPosition(new Vector2(e.X, e.Y));
            SetMouseButton(e.Button, true);
        };
        surface.MouseUp += (_, e) =>
        {
            Mouse.SetClientPosition(new Vector2(e.X, e.Y));
            SetMouseButton(e.Button, false);
        };
        surface.MouseWheel += (_, e) => { if (!IsSuppressed) Mouse.AddWheelDelta(e.Delta); };
    }

    internal void SetDeviceMapping(Size clientSize, int deviceWidth, int deviceHeight) =>
        Mouse.SetDeviceMapping(clientSize, deviceWidth, deviceHeight);

    /// <summary>Sample XInput once before the game and editor read this simulation tick.</summary>
    internal void PollGamepad()
    {
        Gamepad = IsSuppressed ? default : _gamepadInput.Capture();
    }

    internal void EndFrame()
    {
        Keyboard.EndFrame();
        Mouse.EndFrame();
        Gamepad = Gamepad with { Pressed = GamepadButton2D.None, Released = GamepadButton2D.None };
    }

    internal void SetSuppressed(bool isSuppressed)
    {
        if (_isSuppressed == isSuppressed)
            return;
        _isSuppressed = isSuppressed;
        CancelButtons();
    }

    internal void SetWindowActive(bool isActive)
    {
        _isWindowActive = isActive;
        CancelButtons();
    }

    internal void CancelButtons()
    {
        Keyboard.CancelHeld();
        Mouse.CancelHeld();
        _gamepadInput.Reset();
        Gamepad = default;
    }

    internal void SetKey(Keys key, bool isDown) => Keyboard.SetKey(key, isDown, IsSuppressed);
    internal void SetMouseButton(MouseButtons button, bool isDown) => Mouse.SetButton(button, isDown, IsSuppressed);

    // Test seam: device transitions can be injected without native hardware.
    internal void SetGamepad(GamepadState2D state) => Gamepad = IsSuppressed ? default : state;
}
