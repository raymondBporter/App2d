namespace App2d.Core.Input;

/// <summary>Physical mouse state in device pixel coordinates.</summary>
public sealed class MouseState2D
{
    private readonly ButtonState<MouseButtons> _buttons = new();
    private Vector2f _clientPosition;
    private Vector2f _clientToDeviceScale = Vector2f.One;

    public Vector2f PositionDevice => _clientPosition * _clientToDeviceScale;
    public float WheelDelta { get; private set; }
    public bool IsDown(MouseButtons button) => _buttons.IsDown(button);
    public bool WasPressed(MouseButtons button) => _buttons.WasPressed(button);
    public bool WasReleased(MouseButtons button) => _buttons.WasReleased(button);

    internal void SetClientPosition(Vector2f position) => _clientPosition = position;

    internal void SetDeviceMapping(Size clientSize, int deviceWidth, int deviceHeight)
    {
        _clientToDeviceScale = new Vector2f(
            clientSize.Width > 0 ? deviceWidth / (float)clientSize.Width : 1f,
            clientSize.Height > 0 ? deviceHeight / (float)clientSize.Height : 1f);
    }

    internal void AddWheelDelta(float delta) => WheelDelta += delta;

    internal void SetButton(MouseButtons button, bool isDown, bool suppressed) => _buttons.SetButton(button, isDown, suppressed);

    internal void CancelHeld()
    {
        _buttons.CancelHeld();
        WheelDelta = 0f;
    }

    internal void EndFrame()
    {
        _buttons.EndFrame();
        WheelDelta = 0f;
    }
}
