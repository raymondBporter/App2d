using System.Numerics;

namespace App2d.Engine;

public sealed class InputState
{
    private readonly HashSet<Keys> _keysDown = [];
    private readonly HashSet<Keys> _keysPressed = [];
    private readonly HashSet<Keys> _keysReleased = [];
    private readonly HashSet<MouseButtons> _mouseButtonsDown = [];
    private readonly HashSet<MouseButtons> _mouseButtonsPressed = [];
    private readonly HashSet<MouseButtons> _mouseButtonsReleased = [];
    private Vector2 _mouseClientPosition;
    private Vector2 _clientToDeviceScale = Vector2.One;
    private bool _isSuppressed;

    public Vector2 MousePositionDevice => _mouseClientPosition * _clientToDeviceScale;
    public float MouseWheelDelta { get; private set; }
    public bool IsControlDown =>
        IsKeyDown(Keys.ControlKey) ||
        IsKeyDown(Keys.LControlKey) ||
        IsKeyDown(Keys.RControlKey);

    public bool IsKeyDown(Keys key) => _keysDown.Contains(key);
    public bool WasKeyPressed(Keys key) => _keysPressed.Contains(key);
    public bool WasKeyReleased(Keys key) => _keysReleased.Contains(key);
    public bool IsMouseDown(MouseButtons button) => _mouseButtonsDown.Contains(button);
    public bool WasMousePressed(MouseButtons button) => _mouseButtonsPressed.Contains(button);
    public bool WasMouseReleased(MouseButtons button) => _mouseButtonsReleased.Contains(button);

    internal void Attach(Form window, Control surface)
    {
        window.KeyPreview = true;
        window.KeyDown += (_, e) => SetKey(e.KeyCode, true);
        window.KeyUp += (_, e) => SetKey(e.KeyCode, false);
        window.Deactivate += (_, _) => ResetButtons();

        surface.PreviewKeyDown += (_, e) =>
        {
            if (e.KeyCode is Keys.Left or Keys.Right or Keys.Up or Keys.Down)
                e.IsInputKey = true;
        };
        surface.MouseMove += (_, e) => _mouseClientPosition = new Vector2(e.X, e.Y);
        surface.MouseDown += (_, e) =>
        {
            surface.Focus();
            _mouseClientPosition = new Vector2(e.X, e.Y);
            SetMouseButton(e.Button, true);
        };
        surface.MouseUp += (_, e) =>
        {
            _mouseClientPosition = new Vector2(e.X, e.Y);
            SetMouseButton(e.Button, false);
        };
        surface.MouseWheel += (_, e) => MouseWheelDelta += e.Delta;
    }

    internal void SetDeviceMapping(Size clientSize, int deviceWidth, int deviceHeight)
    {
        _clientToDeviceScale = new Vector2(
            clientSize.Width > 0 ? deviceWidth / (float)clientSize.Width : 1f,
            clientSize.Height > 0 ? deviceHeight / (float)clientSize.Height : 1f);
    }

    internal void EndFrame()
    {
        _keysPressed.Clear();
        _keysReleased.Clear();
        _mouseButtonsPressed.Clear();
        _mouseButtonsReleased.Clear();
        MouseWheelDelta = 0f;
    }

    internal void SetSuppressed(bool isSuppressed)
    {
        _isSuppressed = isSuppressed;
        ResetButtons();
        EndFrame();
    }

    private void SetKey(Keys key, bool isDown)
    {
        if (_isSuppressed)
            return;

        if (isDown)
        {
            if (_keysDown.Add(key))
                _keysPressed.Add(key);
        }
        else if (_keysDown.Remove(key))
        {
            _keysReleased.Add(key);
        }
    }

    private void SetMouseButton(MouseButtons button, bool isDown)
    {
        if (_isSuppressed)
            return;

        if (isDown)
        {
            if (_mouseButtonsDown.Add(button))
                _mouseButtonsPressed.Add(button);
        }
        else if (_mouseButtonsDown.Remove(button))
        {
            _mouseButtonsReleased.Add(button);
        }
    }

    private void ResetButtons()
    {
        _keysDown.Clear();
        _mouseButtonsDown.Clear();
    }
}
