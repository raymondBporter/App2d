using App2d.Core;
using App2d.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System.Diagnostics;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Noodle;

internal sealed class NoodleEditorHost : IDisposable
{
    private static readonly XnaColor BackgroundColor = new(17, 22, 33);
    private static readonly XnaColor TargetColor = new(117, 255, 178);
    private static readonly XnaColor MutedTextColor = new(170, 185, 207);

    private readonly Camera2D _camera = new() { Position = new Vector2(20f, -55f), Zoom = 1.05f };
    private readonly NoodlePerson2D _person = new();
    private readonly JointShowcase2D _jointShowcase = new();
    private readonly GraphicsSurface2D _surface = new() { Dock = DockStyle.Fill, TabStop = true };
    private readonly Form _window;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 16 };
    private readonly Stopwatch _clock = new();

    private Renderer2D? _renderer;
    private FrameTime _frameTime;
    private double _previousTime;
    private bool _draggingTarget;
    private bool _showWeights;
    private bool _disposed;

    public NoodleEditorHost()
    {
        _window = new Form
        {
            Text = "NoodleBRO — Full Body 2D Rig Lab",
            ClientSize = new Size(1180, 760),
            MinimumSize = new Size(780, 520),
            StartPosition = FormStartPosition.CenterScreen,
            KeyPreview = true
        };
        _window.Controls.Add(_surface);

        _surface.RenderFrame += Render;
        _surface.DeviceDisposing += ReleaseRenderer;
        _surface.MouseDown += OnMouseDown;
        _surface.MouseMove += OnMouseMove;
        _surface.MouseUp += OnMouseUp;
        _surface.MouseWheel += OnMouseWheel;
        _window.KeyDown += OnKeyDown;
        _window.Resize += OnResize;
        _window.Shown += OnShown;
        _window.FormClosed += OnFormClosed;
        _timer.Tick += OnTick;
    }

    public void Run()
    {
        _clock.Start();
        _timer.Start();
        Application.Run(_window);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var totalTime = _clock.Elapsed.TotalSeconds;
        var elapsed = Math.Clamp(totalTime - _previousTime, 0d, 0.1d);
        _previousTime = totalTime;
        _frameTime = new FrameTime((float)elapsed, totalTime, _frameTime.FrameNumber + 1);
        _surface.Refresh();
    }

    private void Render(GraphicsDevice device, int width, int height)
    {
        _renderer ??= new Renderer2D(_camera, device);
        _renderer.BeginFrame(width, height, _frameTime);
        try
        {
            _renderer.Clear(BackgroundColor);
            _renderer.DrawGrid(50f, 5);
            var targetLimb = _person.TargetLimb;
            _renderer.DrawWorldCircle(targetLimb.Shoulder, targetLimb.UpperLength + targetLimb.LowerLength,
                new XnaColor(117, 255, 178, 45), 1f);
            _person.Render(_renderer, _showWeights);
            DrawTarget(_renderer);
            _person.RenderControls(_renderer);
            _jointShowcase.Render(_renderer, _camera, _frameTime.TotalSeconds);
            DrawHud(_renderer, width);
        }
        finally
        {
            _renderer.EndFrame();
        }
    }

    private void DrawTarget(Renderer2D renderer)
    {
        var targetLimb = _person.TargetLimb;
        Span<Vector2> reachLine = [targetLimb.Pose.End, targetLimb.Target];
        if (!targetLimb.Pose.ReachesTarget)
            renderer.DrawWorldPolyline(reachLine, new XnaColor(255, 118, 118, 150), 2f);

    }

    private void DrawHud(Renderer2D renderer, int width)
    {
        var targetLimb = _person.TargetLimb;
        renderer.DrawScreenRoundedRectangle(new(18f, 18f, Math.Min(width - 18f, 650f), 162f),
            12f, new XnaColor(9, 14, 24, 225));
        renderer.DrawScreenText("NOODLEBRO  /  2D RIG LAB", new Vector2(36f, 51f), XnaColor.White);
        renderer.DrawScreenText("Right click: select hand / foot    Left drag: move selected IK",
            new Vector2(36f, 83f), MutedTextColor);
        renderer.DrawScreenText(
            $"Flip joint: Space    Weights: {(_showWeights ? "ON" : "OFF")} [W]    Reset: R    IK: {(targetLimb.Pose.ReachesTarget ? "reached" : "clamped")}",
            new Vector2(36f, 113f), _showWeights ? TargetColor : MutedTextColor);
        renderer.DrawScreenText($"Selected IK control: {_person.SelectedControlName}",
            new Vector2(36f, 143f), TargetColor);
    }

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            var worldPosition = _camera.DeviceToWorld(new Vector2(e.X, e.Y));
            _person.SelectNearestControl(worldPosition);
            _surface.Refresh();
            return;
        }

        if (e.Button != MouseButtons.Left)
            return;

        _draggingTarget = true;
        _surface.Capture = true;
        MoveTarget(e.Location);
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        if (_draggingTarget)
            MoveTarget(e.Location);
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
            return;

        _draggingTarget = false;
        _surface.Capture = false;
    }

    private void MoveTarget(Point location) =>
        _person.TargetLimb.SetTarget(_camera.DeviceToWorld(new Vector2(location.X, location.Y)));

    private void OnMouseWheel(object? sender, MouseEventArgs e)
    {
        var beforeZoom = _camera.DeviceToWorld(new Vector2(e.X, e.Y));
        _camera.Zoom *= e.Delta > 0 ? 1.1f : 1f / 1.1f;
        var afterZoom = _camera.DeviceToWorld(new Vector2(e.X, e.Y));
        _camera.Position += beforeZoom - afterZoom;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Space:
                _person.TargetLimb.FlipBend();
                break;
            case Keys.R:
                _person.Reset();
                _camera.Position = new Vector2(20f, -55f);
                _camera.Zoom = 1.05f;
                break;
            case Keys.W:
                _showWeights = !_showWeights;
                break;
            case Keys.Escape:
                _window.Close();
                break;
            default:
                return;
        }

        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private void OnResize(object? sender, EventArgs e) =>
        _camera.SetViewport(Math.Max(1, _surface.ClientSize.Width), Math.Max(1, _surface.ClientSize.Height));

    private void OnShown(object? sender, EventArgs e)
    {
        OnResize(sender, e);
        _surface.Focus();
    }

    private void OnFormClosed(object? sender, FormClosedEventArgs e) => _timer.Stop();

    private void ReleaseRenderer()
    {
        _renderer?.Dispose();
        _renderer = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
        _surface.RenderFrame -= Render;
        _surface.DeviceDisposing -= ReleaseRenderer;
        _surface.MouseDown -= OnMouseDown;
        _surface.MouseMove -= OnMouseMove;
        _surface.MouseUp -= OnMouseUp;
        _surface.MouseWheel -= OnMouseWheel;
        _window.KeyDown -= OnKeyDown;
        _window.Resize -= OnResize;
        _window.Shown -= OnShown;
        _window.FormClosed -= OnFormClosed;
        ReleaseRenderer();
        _timer.Dispose();
        _surface.Dispose();
        _window.Dispose();
    }
}
