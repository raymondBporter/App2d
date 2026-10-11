using ImGuiNET;

namespace App2d.EditorUi;

/// <summary>Event input for a WinForms graphics control. It does not need a MonoGame Game or poll its global input.</summary>
public sealed class WinFormsImGuiPlatform : IImGuiPlatform
{
    private enum EventKind { Focus, Position, Button, Wheel, Key, Character }
    private readonly record struct InputEvent(EventKind Kind, ImGuiKey Key = default, bool Down = false, int Value = 0, float X = 0, float Y = 0);
    private readonly Control _surface;
    private readonly Queue<InputEvent> _events = [];
    private readonly HashSet<ImGuiKey> _heldKeys = [];
    private readonly HashSet<int> _heldButtons = [];
    private bool _disposed;

    public WinFormsImGuiPlatform(Control surface)
    {
        _surface = surface;
        _surface.PreviewKeyDown += OnPreviewKeyDown;
        _surface.KeyDown += OnKeyDown;
        _surface.KeyUp += OnKeyUp;
        _surface.KeyPress += OnKeyPress;
        _surface.MouseMove += OnMouseMove;
        _surface.MouseDown += OnMouseDown;
        _surface.MouseUp += OnMouseUp;
        _surface.MouseWheel += OnMouseWheel;
        _surface.MouseLeave += OnMouseLeave;
        _surface.MouseCaptureChanged += OnMouseCaptureChanged;
        _surface.GotFocus += OnGotFocus;
        _surface.LostFocus += OnLostFocus;
        _events.Enqueue(new(EventKind.Focus, Down: surface.ContainsFocus));
    }

    public float DpiScale => _surface.DeviceDpi / 96f;

    public void UpdateInput(ImGuiIOPtr io)
    {
        while (_events.TryDequeue(out var input))
        {
            switch (input.Kind)
            {
                case EventKind.Focus: io.AddFocusEvent(input.Down); break;
                case EventKind.Position: io.AddMousePosEvent(input.X, input.Y); break;
                case EventKind.Button: io.AddMouseButtonEvent(input.Value, input.Down); break;
                case EventKind.Wheel: io.AddMouseWheelEvent(input.X, input.Y); break;
                case EventKind.Key: io.AddKeyEvent(input.Key, input.Down); break;
                case EventKind.Character: io.AddInputCharacterUTF16((ushort)input.Value); break;
            }
        }
    }

    private static void OnPreviewKeyDown(object? sender, PreviewKeyDownEventArgs e) => e.IsInputKey = true;
    private void OnKeyDown(object? sender, KeyEventArgs e) => Key(e, true);
    private void OnKeyUp(object? sender, KeyEventArgs e) => Key(e, false);
    private void Key(KeyEventArgs e, bool down)
    {
        if (ImGuiKeyMap.FromWinForms(e.KeyCode) is { } key) AddKey(key, down);
        AddKey(ImGuiKey.ModCtrl, e.Control);
        AddKey(ImGuiKey.ModShift, e.Shift);
        AddKey(ImGuiKey.ModAlt, e.Alt);
        AddKey(ImGuiKey.ModSuper, _heldKeys.Contains(ImGuiKey.LeftSuper) || _heldKeys.Contains(ImGuiKey.RightSuper));
    }

    private void AddKey(ImGuiKey key, bool down)
    {
        if (down) _heldKeys.Add(key); else _heldKeys.Remove(key);
        _events.Enqueue(new(EventKind.Key, Key: key, Down: down));
    }

    private void OnKeyPress(object? sender, KeyPressEventArgs e)
    {
        if (!char.IsControl(e.KeyChar)) _events.Enqueue(new(EventKind.Character, Value: e.KeyChar));
    }

    private void OnMouseMove(object? sender, MouseEventArgs e) => Position(e);
    private void Position(MouseEventArgs e) => _events.Enqueue(new(EventKind.Position, X: e.X, Y: e.Y));
    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        _surface.Focus();
        Position(e); Button(e.Button, true);
    }
    private void OnMouseUp(object? sender, MouseEventArgs e) { Position(e); Button(e.Button, false); }
    private void OnMouseWheel(object? sender, MouseEventArgs e) => _events.Enqueue(new(EventKind.Wheel, Y: e.Delta / 120f));
    private void OnMouseLeave(object? sender, EventArgs e)
    {
        if (!_surface.Capture) _events.Enqueue(new(EventKind.Position, X: -float.MaxValue, Y: -float.MaxValue));
    }
    private void Button(MouseButtons button, bool down)
    {
        var index = button switch { MouseButtons.Left => 0, MouseButtons.Right => 1, MouseButtons.Middle => 2, MouseButtons.XButton1 => 3, MouseButtons.XButton2 => 4, _ => -1 };
        if (index < 0) return;
        if (down) _heldButtons.Add(index); else _heldButtons.Remove(index);
        _events.Enqueue(new(EventKind.Button, Down: down, Value: index));
    }
    private void OnMouseCaptureChanged(object? sender, EventArgs e)
    {
        if (!_surface.Capture) ReleaseButtons();
    }
    private void ReleaseButtons()
    {
        foreach (var button in _heldButtons) _events.Enqueue(new(EventKind.Button, Value: button));
        _heldButtons.Clear();
    }
    private void OnGotFocus(object? sender, EventArgs e) => _events.Enqueue(new(EventKind.Focus, Down: true));
    private void OnLostFocus(object? sender, EventArgs e)
    {
        foreach (var key in _heldKeys) _events.Enqueue(new(EventKind.Key, Key: key));
        _heldKeys.Clear();
        ReleaseButtons();
        _events.Enqueue(new(EventKind.Focus));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _surface.PreviewKeyDown -= OnPreviewKeyDown;
        _surface.KeyDown -= OnKeyDown;
        _surface.KeyUp -= OnKeyUp;
        _surface.KeyPress -= OnKeyPress;
        _surface.MouseMove -= OnMouseMove;
        _surface.MouseDown -= OnMouseDown;
        _surface.MouseUp -= OnMouseUp;
        _surface.MouseWheel -= OnMouseWheel;
        _surface.MouseLeave -= OnMouseLeave;
        _surface.MouseCaptureChanged -= OnMouseCaptureChanged;
        _surface.GotFocus -= OnGotFocus;
        _surface.LostFocus -= OnLostFocus;
        _events.Clear();
    }
}
