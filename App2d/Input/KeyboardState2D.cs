namespace App2d.Input;

/// <summary>Physical keyboard state accumulated until the next simulation tick.</summary>
public sealed class KeyboardState2D
{
    private readonly HashSet<Keys> _down = [];
    private readonly HashSet<Keys> _pressed = [];
    private readonly HashSet<Keys> _released = [];
    private readonly HashSet<Keys> _blocked = [];

    public bool IsDown(Keys key) => _down.Contains(key);
    public bool WasPressed(Keys key) => _pressed.Contains(key);
    public bool WasReleased(Keys key) => _released.Contains(key);
    public bool IsControlDown => IsDown(Keys.ControlKey) || IsDown(Keys.LControlKey) || IsDown(Keys.RControlKey);
    public bool IsShiftDown => IsDown(Keys.ShiftKey) || IsDown(Keys.LShiftKey) || IsDown(Keys.RShiftKey);

    internal void SetKey(Keys key, bool isDown, bool suppressed)
    {
        if (!isDown) _blocked.Remove(key);
        if (suppressed)
        {
            if (isDown) _blocked.Add(key);
            return;
        }
        if (_blocked.Contains(key)) return;

        if (isDown)
        {
            if (_down.Add(key)) _pressed.Add(key);
        }
        else if (_down.Remove(key))
        {
            _released.Add(key);
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
    }
}
