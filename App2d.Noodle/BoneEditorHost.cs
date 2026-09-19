using App2d.Core;
using App2d.Noodle.Rigging;
using App2d.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System.Diagnostics;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Noodle;

internal sealed class BoneEditorHost : IDisposable
{
    private readonly RigDocument2D _document = RigDocument2D.CreateStarterPerson();
    private readonly Camera2D _camera = new() { Position = new Vector2(0f, -55f), Zoom = 1.05f };
    private readonly GraphicsSurface2D _surface = new() { Dock = DockStyle.Fill, TabStop = true };
    private readonly BoneEditorPanel _panel;
    private readonly Form _window;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 16 };
    private readonly Stopwatch _clock = new();

    private Renderer2D? _renderer;
    private FrameTime _frameTime;
    private double _previousTime;
    private bool _panning;
    private Point _previousMouse;
    private bool _disposed;

    public BoneEditorHost()
    {
        _panel = new BoneEditorPanel(_document);
        _window = new Form
        {
            Text = "App2d Entity Editor — Bones",
            ClientSize = new Size(1380, 840),
            MinimumSize = new Size(980, 640),
            StartPosition = FormStartPosition.CenterScreen,
            KeyPreview = true
        };
        _window.Controls.Add(_surface);
        _window.Controls.Add(_panel);
        _panel.BringToFront();

        _surface.RenderFrame += Render;
        _surface.DeviceDisposing += ReleaseRenderer;
        _surface.MouseDown += OnMouseDown;
        _surface.MouseMove += OnMouseMove;
        _surface.MouseUp += OnMouseUp;
        _surface.MouseWheel += OnMouseWheel;
        _panel.DocumentChanged += RefreshSurface;
        _panel.SelectionChanged += RefreshSurface;
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
            _renderer.Clear(new XnaColor(17, 22, 33));
            _renderer.DrawGrid(50f, 5);
            RigDocumentRenderer2D.Render(_renderer, _document, _panel.SelectedItem);
            _renderer.DrawScreenRoundedRectangle(new(18f, 18f, 575f, 101f), 12f,
                new XnaColor(9, 14, 24, 225));
            _renderer.DrawScreenText("APP2D ENTITY EDITOR  /  BONES", new Vector2(36f, 51f), XnaColor.White);
            _renderer.DrawScreenText("Build hierarchy -> add shape -> attach to bone -> edit properties",
                new Vector2(36f, 83f), new XnaColor(170, 185, 207));
        }
        finally
        {
            _renderer.EndFrame();
        }
    }

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Middle)
            return;
        _panning = true;
        _previousMouse = e.Location;
        _surface.Capture = true;
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        if (!_panning)
            return;
        var delta = new Vector2(e.X - _previousMouse.X, e.Y - _previousMouse.Y);
        _camera.Position -= new Vector2(delta.X, -delta.Y) / _camera.Zoom;
        _previousMouse = e.Location;
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Middle)
            return;
        _panning = false;
        _surface.Capture = false;
    }

    private void OnMouseWheel(object? sender, MouseEventArgs e)
    {
        var devicePoint = new Vector2(e.X, e.Y);
        var beforeZoom = _camera.DeviceToWorld(devicePoint);
        _camera.Zoom *= e.Delta > 0 ? 1.1f : 1f / 1.1f;
        _camera.Position += beforeZoom - _camera.DeviceToWorld(devicePoint);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Escape)
            return;
        _window.Close();
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
    private void RefreshSurface() => _surface.Refresh();

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
        _panel.DocumentChanged -= RefreshSurface;
        _panel.SelectionChanged -= RefreshSurface;
        _window.KeyDown -= OnKeyDown;
        _window.Resize -= OnResize;
        _window.Shown -= OnShown;
        _window.FormClosed -= OnFormClosed;
        ReleaseRenderer();
        _timer.Dispose();
        _panel.Dispose();
        _surface.Dispose();
        _window.Dispose();
    }
}
