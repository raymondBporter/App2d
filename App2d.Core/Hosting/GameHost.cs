using App2d.Core.Input;
using App2d.Core.Rendering;
using App2d.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System.Diagnostics;

namespace App2d.Core.Hosting;

public sealed class GameHost : IDisposable
{
    private const double FixedDeltaSeconds = 1d / 120d;
    private const double MaximumFrameSeconds = 0.1d;
    private readonly Game2D _game;
    private readonly Form _window;
    private readonly GraphicsSurface2D _surface;

    private readonly InputState _input = new();
    private Renderer2D? _renderer;
    private readonly IGameToolUi2D? _toolUi;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 16 };
    private readonly Stopwatch _clock = new();
    private FrameTime _frameTime;
    private FrameTime _renderFrameTime;
    private double _accumulator;
    private double _previousTime;
    private double _simulationTime;
    private double _nextTitleUpdateTime;
    private long _advancedFrameNumber = -1;
    private bool _closeRequested;

    private bool _disposed;

    public GameHost(Game2D game)
    {
        _game = game;
        _surface = new GraphicsSurface2D();
        _surface.RenderFrame += RenderSurface;
        _surface.DeviceDisposing += ReleaseRenderer;
        _surface.Dock = DockStyle.Fill;
        _surface.TabStop = true;
        _window = new Form
        {
            Text = game.WindowTitle,
            ClientSize = new Size(2000, 1400),
            StartPosition = FormStartPosition.CenterScreen,
            WindowState = FormWindowState.Maximized
        };

        _window.Controls.Add(_surface);
        _input.Attach(_window, _surface);
        _toolUi = game.ToolUi;
        _toolUi?.Attach(_surface);
        _timer.Tick += OnTick;
        _window.FormClosed += (_, _) => _timer.Stop();
        _window.Shown += (_, _) => _surface.Focus();
    }

    public void Run()
    {
        _game.Initialize();
        _clock.Start();
        _timer.Start();
        Application.Run(_window);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var totalTime = _clock.Elapsed.TotalSeconds;
        var elapsedSeconds = Math.Clamp(totalTime - _previousTime, 0d, MaximumFrameSeconds);
        _previousTime = totalTime;
        _accumulator = Math.Min(_accumulator + elapsedSeconds, MaximumFrameSeconds);
        _renderFrameTime = new FrameTime((float)elapsedSeconds, totalTime, _renderFrameTime.FrameNumber + 1);

        var deviceWidth = Math.Max(1, _surface.ClientSize.Width);
        var deviceHeight = Math.Max(1, _surface.ClientSize.Height);
        _input.SetDeviceMapping(_surface.ClientSize, deviceWidth, deviceHeight);
        _game.Camera.SetViewport(deviceWidth, deviceHeight);

        // Painting drives the UI first, then simulation, then the world and tool UI rendering.
        _surface.Refresh();
        // Minimized surfaces do not paint, but the fixed simulation still advances.
        if (_advancedFrameNumber != _renderFrameTime.FrameNumber)
        {
            AdvanceGame();
        }

        if (_closeRequested)
        {
            _window.Close();
        }
    }

    private void AdvanceGame()
    {
        _advancedFrameNumber = _renderFrameTime.FrameNumber;
        if (_input.Keyboard.WasPressed(Keys.Escape))
        {
            _input.EndFrame();
            _closeRequested = true;
            return;
        }

        while (_accumulator >= FixedDeltaSeconds)
        {
            _input.PollGamepad();
            _simulationTime += FixedDeltaSeconds;
            _frameTime = new FrameTime(
                (float)FixedDeltaSeconds,
                _simulationTime,
                _frameTime.FrameNumber + 1);
            _game.Update(_frameTime, _input);
            _input.EndFrame();
            _accumulator -= FixedDeltaSeconds;
        }

        _game.AdvancePresentation(_renderFrameTime);

        if (_renderFrameTime.TotalSeconds >= _nextTitleUpdateTime)
        {
            var title = _game.WindowTitle;
            if (!string.Equals(_window.Text, title, StringComparison.Ordinal))
            {
                _window.Text = title;
            }

            _nextTitleUpdateTime = _renderFrameTime.TotalSeconds + 0.25d;
        }
    }

    private void RenderSurface(GraphicsDevice device, int width, int height)
    {
        _toolUi?.BeginFrame(device, _renderFrameTime);
        _input.SetToolCapture(_toolUi?.CaptureKeyboard ?? false, _toolUi?.CaptureMouse ?? false);
        if (_advancedFrameNumber != _renderFrameTime.FrameNumber)
        {
            AdvanceGame();
        }

        _renderer ??= new Renderer2D(_game.Camera, device);
        _renderer.BeginFrame(width, height, _frameTime);
        try
        {
            _game.RenderFrame(_renderer, _renderFrameTime);
        }
        finally
        {
            _renderer.EndFrame();
        }
        _toolUi?.Render();
    }

    private void ReleaseRenderer()
    {
        _toolUi?.ReleaseDevice();
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
        _timer.Dispose();
        ReleaseRenderer();
        _toolUi?.Dispose();
        _surface.Dispose();
        _window.Dispose();
        _game.Dispose();
    }
}
