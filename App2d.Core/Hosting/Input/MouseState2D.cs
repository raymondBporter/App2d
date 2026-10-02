using System.Numerics;

namespace App2d.Input;

/// <summary>Physical mouse state in device pixel coordinates.</summary>
public sealed class MouseState2D
{
    private readonly HashSet<MouseButtons> _down = [];
    private readonly HashSet<MouseButtons> _pressed = [];
    private readonly HashSet<MouseButtons> _released = [];
    private readonly HashSet<MouseButtons> _blocked = [];
    private Vector2 _clientPosition;
    private Vector2 _clientToDeviceScale = Vector2.One;

    public Vector2 PositionDevice => _clientPosition * _clientToDeviceScale;
    public float WheelDelta { get; private set; }
    public bool IsDown(MouseButtons button) => _down.Contains(button);
    public bool WasPressed(MouseButtons button) => _pressed.Contains(button);
    public bool WasReleased(MouseButtons button) => _released.Contains(button);

    internal void SetClientPosition(Vector2 position) => _clientPosition = position;

    internal void SetDeviceMapping(Size clientSize, int deviceWidth, int deviceHeight)
    {
        _clientToDeviceScale = new Vector2(
            clientSize.Width > 0 ? deviceWidth / (float)clientSize.Width : 1f,
            clientSize.Height > 0 ? deviceHeight / (float)clientSize.Height : 1f);
    }

    internal void AddWheelDelta(float delta) => WheelDelta += delta;

    internal void SetButton(MouseButtons button, bool isDown, bool suppressed)
    {
        if (!isDown) _blocked.Remove(button);
        if (suppressed)
        {
            if (isDown) _blocked.Add(button);
            return;
        }
        if (_blocked.Contains(button)) return;

        if (isDown)
        {
            if (_down.Add(button)) _pressed.Add(button);
        }
        else if (_down.Remove(button))
        {
            _released.Add(button);
        }
    }

    internal void CancelHeld()
    {
        _blocked.UnionWith(_down);
        _down.Clear();
        EndFrame();
    }

    internal void EndFrame()
    {
        _pressed.Clear();
        _released.Clear();
        WheelDelta = 0f;
    }
}
