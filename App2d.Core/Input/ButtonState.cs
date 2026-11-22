namespace App2d.Core.Input;

/// <summary>Tracks held buttons and their transitions between input ticks.</summary>
internal sealed class ButtonState<TButton> where TButton : struct, Enum
{
    private readonly HashSet<TButton> _down = [];
    private readonly HashSet<TButton> _pressed = [];
    private readonly HashSet<TButton> _released = [];
    private readonly HashSet<TButton> _blocked = [];

    internal bool IsDown(TButton button) => _down.Contains(button);
    internal bool WasPressed(TButton button) => _pressed.Contains(button);
    internal bool WasReleased(TButton button) => _released.Contains(button);
    internal void SetButton(TButton button, bool isDown, bool suppressed)
    {
        if (!isDown)
        {
            _blocked.Remove(button);
        }

        if (suppressed)
        {
            if (isDown)
            {
                _blocked.Add(button);
            }

            return;
        }
        if (_blocked.Contains(button))
        {
            return;
        }

        if (isDown)
        {
            if (_down.Add(button))
            {
                _pressed.Add(button);
            }
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
    }
}
