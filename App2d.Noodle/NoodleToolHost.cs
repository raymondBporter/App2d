using App2d.Core;
using App2d.Core.Rendering;
using App2d.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System.Diagnostics;

namespace App2d.Noodle;

/// <summary>WinForms window and GPU surface shared by the Noodle tools. Views declare controls; scenes own editing commands.</summary>
internal abstract class NoodleToolHost : IDisposable
{
    protected readonly Camera2D Camera;
    protected readonly Form Window;
    private readonly GraphicsSurface2D _surface = new() { Dock = DockStyle.Fill, TabStop = true, DepthStencilFormat = DepthFormat.None };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 16 };
    private readonly Stopwatch _clock = new();
    private ImGuiHost? _gui;
    private Renderer2D? _renderer;
    private FrameTime _time;
    private long _updatedFrame = -1;
    private double _previousTime;
    private Action? _afterFrame;
    private bool _closeRequested;
    private bool _rendering;
    private bool _disposed;

    protected NoodleToolHost(string title, Size size, Camera2D camera)
    {
        Camera = camera;
        Window = new Form { Text = title, ClientSize = size, MinimumSize = new Size(920, 620), StartPosition = FormStartPosition.CenterScreen };
        Window.Controls.Add(_surface);
        _surface.RenderFrame += Render;
        _surface.DeviceDisposing += ReleaseDevice;
        Window.Shown += OnShown;
        Window.FormClosed += OnClosed;
        _timer.Tick += OnTick;
    }

    public void Run()
    {
        _clock.Start();
        _timer.Start();
        Application.Run(Window);
    }
    protected abstract void DrawControls();
    protected abstract void DrawScene(Renderer2D renderer, FrameTime time);
    protected virtual void HandleSceneInput(bool mouseCaptured, bool keyboardCaptured)
    {
    }
    protected virtual void UpdateScene(FrameTime time, bool keyboardCaptured)
    {
    }
    protected void Close() => _closeRequested = true;
    protected void AfterFrame(Action action) => _afterFrame = action;

    private void OnTick(object? sender, EventArgs e)
    {
        var now = _clock.Elapsed.TotalSeconds;
        _time = new FrameTime((float)Math.Clamp(now - _previousTime, 0, .05), now, _time.FrameNumber + 1);
        _previousTime = now;
        _surface.Refresh();
        var action = _afterFrame;
        _afterFrame = null;
        action?.Invoke();
        if (_closeRequested)
        {
            Window.Close();
        }
    }

    private void Render(GraphicsDevice device, int width, int height)
    {
        if (_rendering)
        {
            return;
        }

        _rendering = true;
        try
        {
            _gui ??= new ImGuiHost(device, new WinFormsImGuiPlatform(_surface));
            _gui.Begin(_time.DeltaSeconds);
            var sidebar = Ui.Sidebar("Tools", DrawControls, 360);
            var canvasWidth = Math.Max(1, width - (int)sidebar);
            Camera.SetViewport(canvasWidth, height);
            var io = ImGui.GetIO();
            var mouseCaptured = _gui.WantCaptureMouse || io.MousePos.X >= canvasWidth;
            HandleSceneInput(mouseCaptured, _gui.WantCaptureKeyboard);
            if (_updatedFrame != _time.FrameNumber)
            {
                _updatedFrame = _time.FrameNumber;
                UpdateScene(_time, _gui.WantCaptureKeyboard);
            }
            _renderer ??= new Renderer2D(Camera, device);
            _renderer.BeginFrame(canvasWidth, height, _time);
            try
            {
                DrawScene(_renderer, _time);
            }
            finally
            {
                _renderer.EndFrame();
            }
            _gui.Render();
        }
        finally
        {
            _rendering = false;
        }
    }

    protected void ZoomAtPointer(float min = .1f, float max = 10f)
    {
        var io = ImGui.GetIO();
        if (io.MouseWheel == 0)
        {
            return;
        }

        var before = Camera.DeviceToWorld(io.MousePos);
        Camera.Zoom = Math.Clamp(Camera.Zoom * MathF.Pow(1.1f, io.MouseWheel), min, max);
        Camera.Position += before - Camera.DeviceToWorld(io.MousePos);
    }

    // Render the actual tool view on a supplied device, without starting a window loop or saving documents.
    internal void RenderStudy(GraphicsDevice device, int width, int height)
    {
        _time = new FrameTime(1f / 60, _time.TotalSeconds + 1d / 60, _time.FrameNumber + 1);
        Render(device, width, height);
    }

    private void OnShown(object? sender, EventArgs e) => _surface.Focus();
    private void OnClosed(object? sender, FormClosedEventArgs e) => _timer.Stop();
    private void ReleaseDevice()
    {
        _gui?.Dispose();
        _gui = null;
        _renderer?.Dispose();
        _renderer = null;
    }
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
        Window.Shown -= OnShown;
        Window.FormClosed -= OnClosed;
        _surface.RenderFrame -= Render;
        _surface.DeviceDisposing -= ReleaseDevice;
        ReleaseDevice();
        _timer.Dispose();
        _surface.Dispose();
        Window.Dispose();
    }
}
