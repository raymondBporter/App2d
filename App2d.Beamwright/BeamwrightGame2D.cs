using App2d.Core;
using App2d.Core.Hosting;
using App2d.Core.Input;
using App2d.Core.Mathematics;
using App2d.Core.Rendering;
using System.Numerics;

namespace App2d.Beamwright;

/// <summary>A native desktop optical bench using the shared game host and renderer.</summary>
public sealed partial class BeamwrightGame2D(BenchSession? session = null) : Game2D
{
    private sealed record BenchButton(ScreenRectangle2D Bounds, string Label, Action Click, bool Enabled = true, bool Active = false);
    private readonly List<BenchButton> _buttons = [];
    private int _selected = -1;
    private string? _armed;
    private Vector2 _pointer;
    private Vector2 _pressPosition;
    private int _dragIndex = -1;
    private string? _rackDragType;
    private bool _dragging;
    private bool _rotateOnRelease;
    private bool _showLevels;
    private int _rackOffset;
    private float _left, _top, _cell, _sidebarWidth, _rackBottom;
    private double _time;
    private Matrix3x2 _benchToWorld;

    public BenchSession Session { get; } = session ?? new();
    public override string WindowTitle => $"Beamwright — {Session.Level.Name}";
    protected override Color4b BackgroundColor => new(18, 22, 27);

    public override void Update(FrameTime time, InputState input)
    {
        _time = time.TotalSeconds;
        _pointer = input.Mouse.PositionDevice;
        Layout();
        if (input.IsSuppressed) { ClearSelection(); return; }
        if (input.Keyboard.WasPressed(Keys.L))
        {
            if (_showLevels) _showLevels = false;
            else OpenLevelPicker();
        }
        if (_showLevels)
        {
            if (input.Mouse.WasPressed(MouseButtons.Left)) ClickButton();
            return;
        }
        if (input.Keyboard.WasPressed(Keys.B)) Load(-1);
        if (input.Keyboard.WasPressed(Keys.PageDown) || input.Keyboard.WasPressed(Keys.N) && Session.Result.Win) NextLevel();
        if (input.Keyboard.WasPressed(Keys.PageUp)) Load(Math.Max(0, Session.LevelIndex - 1));
        if (input.Keyboard.WasPressed(Keys.R)) { Session.Reset(); ClearSelection(); }
        if (input.Keyboard.WasPressed(Keys.H)) Session.RevealHint();
        if (input.Keyboard.IsDown(Keys.ControlKey) && input.Keyboard.WasPressed(Keys.Z)) { Session.Undo(); ClearSelection(); }
        if (input.Keyboard.WasPressed(Keys.Q) || input.Keyboard.WasPressed(Keys.Left)) Session.Rotate(_selected, -15);
        if (input.Keyboard.WasPressed(Keys.E) || input.Keyboard.WasPressed(Keys.Right)) Session.Rotate(_selected, 15);
        if (input.Keyboard.WasPressed(Keys.Delete) || input.Keyboard.WasPressed(Keys.Back)) { Session.Remove(_selected); ClearSelection(); }
        if (input.Keyboard.WasPressed(Keys.Tab))
        {
            var rack = RackParts().Where(p => Session.Available(p.Type) > 0).ToArray();
            if (rack.Length > 0) _armed = rack[(Array.FindIndex(rack, p => p.Type == _armed) + 1) % rack.Length].Type;
            _selected = -1;
        }
        var offered = RackParts().ToArray();
        for (var i = 0; i < Math.Min(9, offered.Length); i++)
        {
            if (input.Keyboard.WasPressed((Keys)((int)Keys.D1 + i)) && Session.Available(offered[i].Type) > 0)
            { _armed = offered[i].Type; _selected = -1; }
        }

        if (input.Mouse.WheelDelta != 0)
        {
            if (_pointer.X < _sidebarWidth)
                _rackOffset = Math.Clamp(_rackOffset - Math.Sign(input.Mouse.WheelDelta), 0, Math.Max(0, offered.Length - VisibleRackRows));
            else Session.Rotate(_selected, Math.Sign(input.Mouse.WheelDelta) * 15);
        }
        if (input.Mouse.WasPressed(MouseButtons.Right))
        {
            var cell = PointerCell();
            Session.Remove(Session.At(cell.X, cell.Y));
            ClearSelection();
        }
        if (input.Mouse.WasPressed(MouseButtons.Left))
        {
            if (ClickButton()) return;
            var cell = PointerCell();
            var index = Session.At(cell.X, cell.Y);
            if (index >= 0)
            {
                _rotateOnRelease = _selected == index;
                _selected = index;
                _armed = null;
                _dragIndex = index;
                _dragging = false;
                _pressPosition = _pointer;
            }
            else if (_armed is { } type && Session.TryPlace(type, cell.X, cell.Y))
            {
                _selected = Session.Placed.Count - 1;
                if (Session.Available(type) == 0) _armed = null;
            }
            else if (_armed is null)
            {
                _selected = -1;
            }
        }
        if ((_dragIndex >= 0 || _rackDragType is not null) && input.Mouse.IsDown(MouseButtons.Left) && Vector2.Distance(_pointer, _pressPosition) > 5)
            _dragging = true;
        if (_rackDragType is { } draggedType && input.Mouse.WasReleased(MouseButtons.Left))
        {
            var cell = PointerCell();
            if (_dragging && Session.TryPlace(draggedType, cell.X, cell.Y))
            {
                _selected = Session.Placed.Count - 1;
                if (Session.Available(draggedType) == 0) _armed = null;
            }
            _rackDragType = null;
            _dragging = false;
        }
        if (_dragIndex >= 0 && input.Mouse.WasReleased(MouseButtons.Left))
        {
            var cell = PointerCell();
            if (_dragging)
            {
                if (_pointer.X < _sidebarWidth) { Session.Remove(_dragIndex); _selected = -1; }
                else
                {
                    Session.TryMove(_dragIndex, cell.X, cell.Y);
                }
            }
            else if (_rotateOnRelease)
            {
                Session.Rotate(_dragIndex, 15);
            }

            _dragIndex = -1;
            _dragging = false;
        }
    }

    private int VisibleRackRows => Math.Max(1, (int)((_rackBottom - 176) / 49));
    private IEnumerable<PartDefinition> RackParts() => Session.Content.Pieces.Where(p => Session.Level.Sandbox || Session.Level.Inv.ContainsKey(p.Type));
    private Vector2i PointerCell() => new((int)MathF.Floor((_pointer.X - _left) / _cell), (int)MathF.Floor((_pointer.Y - _top) / _cell));
    private static bool Contains(ScreenRectangle2D rect, Vector2 point) => point.X >= rect.Left && point.X <= rect.Right && point.Y >= rect.Top && point.Y <= rect.Bottom;

    private bool ClickButton()
    {
        foreach (var button in _buttons)
        {
            if (!Contains(button.Bounds, _pointer)) continue;
            if (button.Enabled) button.Click();
            return true;
        }
        return false;
    }

    private void ClearSelection()
    {
        _selected = -1;
        _armed = null;
        _dragIndex = -1;
        _rackDragType = null;
        _dragging = false;
    }

    private void Load(int index)
    {
        Session.LoadLevel(index);
        ClearSelection();
        _rackOffset = 0;
        _showLevels = false;
    }

    private void NextLevel() => Load(Session.LevelIndex < 0 ? 0 : Math.Min(Session.LevelIndex + 1, Session.Content.Levels.Length - 1));

    internal void OpenLevelPicker()
    {
        _showLevels = true;
        ClearSelection();
    }

    private void Layout()
    {
        var width = Camera.ViewportSize.X;
        var height = Camera.ViewportSize.Y;
        Camera.ReferenceViewportHeight = null;
        Camera.Zoom = 1;
        Camera.Position = new(width / 2, -height / 2);
        _sidebarWidth = width < 1000 ? 230 : 270;
        _rackBottom = height - 181;
        var areaLeft = _sidebarWidth + 36;
        var availableWidth = Math.Max(100, width - areaLeft - 36);
        var availableHeight = Math.Max(100, height - 245);
        _cell = Math.Min(availableWidth / Session.Level.W, availableHeight / Session.Level.H);
        _left = areaLeft + (availableWidth - _cell * Session.Level.W) / 2;
        _top = 139 + (availableHeight - _cell * Session.Level.H) / 2;
        _benchToWorld = Matrix3x2.CreateScale(_cell, -_cell) * Matrix3x2.CreateTranslation(_left, -_top);
        _buttons.Clear();
        if (_showLevels) { LayoutPicker(width, height); return; }

        AddButton(new(width - 238, 24, width - 134, 62), "Levels [L]", OpenLevelPicker);
        AddButton(new(width - 124, 24, width - 20, 62), "Free bench", () => Load(-1), active: Session.Level.Sandbox);
        AddButton(new(20, 92, 92, 126), "< Prev", () => Load(Math.Max(0, Session.LevelIndex - 1)), Session.LevelIndex > 0);
        AddButton(new(102, 92, _sidebarWidth - 20, 126), "Next >", NextLevel, Session.LevelIndex < Session.Content.Levels.Length - 1);
        var parts = RackParts().ToArray();
        _rackOffset = Math.Clamp(_rackOffset, 0, Math.Max(0, parts.Length - VisibleRackRows));
        for (var row = 0; row < VisibleRackRows && row + _rackOffset < parts.Length; row++)
        {
            var part = parts[row + _rackOffset];
            var y = 176 + row * 49;
            AddButton(new(16, y, _sidebarWidth - 16, y + 42), part.Name,
                () =>
                {
                    _armed = _armed == part.Type ? null : part.Type;
                    _rackDragType = _armed;
                    _pressPosition = _pointer;
                    _dragging = false;
                    _selected = -1;
                }, Session.Available(part.Type) > 0, _armed == part.Type);
        }
        var footer = height - 162;
        AddButton(new(16, footer, _sidebarWidth - 16, footer + 34), "Undo [Ctrl+Z]", () => { Session.Undo(); ClearSelection(); }, Session.CanUndo);
        AddButton(new(16, footer + 43, _sidebarWidth - 16, footer + 77), "Reset [R]", () => { Session.Reset(); ClearSelection(); });
        AddButton(new(16, footer + 86, _sidebarWidth - 16, footer + 120), "Hint [H]", Session.RevealHint, Session.HintCount < Session.Level.Solution.Length);
        if (Session.Result.Win && !Session.Level.Sandbox && Session.LevelIndex < Session.Content.Levels.Length - 1)
            AddButton(new(width - 216, height - 74, width - 24, height - 30), "Next bench [N]  >", NextLevel);
    }

    private void LayoutPicker(float width, float height)
    {
        var left = Math.Max(20, (width - 1120) / 2);
        var panelWidth = width - left * 2;
        var top = Math.Max(20, (height - 630) / 2);
        var rowHeight = Math.Min(42, (height - top * 2 - 102) / 12);
        AddButton(new(width - left - 112, top + 16, width - left - 20, top + 50), "Close [L]", () => _showLevels = false);
        var columnWidth = (panelWidth - 40) / 3;
        for (var i = 0; i < Session.Content.Levels.Length; i++)
        {
            var index = i;
            var x = left + 20 + i / 12 * columnWidth;
            var y = top + 80 + i % 12 * rowHeight;
            var level = Session.Content.Levels[i];
            var label = $"{i + 1:00}  {level.Name}";
            AddButton(new(x, y, x + columnWidth - 10, y + rowHeight - 5), label,
                () => Load(index), active: Session.LevelIndex == i);
        }
    }

    private void AddButton(ScreenRectangle2D bounds, string label, Action click, bool enabled = true, bool active = false) =>
        _buttons.Add(new(bounds, label, click, enabled, active));
}
