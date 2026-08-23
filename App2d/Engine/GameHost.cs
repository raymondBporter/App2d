using System.Diagnostics;
using App2d.Engine.Rendering;
using SkiaSharp.Views.Desktop;

namespace App2d.Engine;

public sealed class GameHost : IDisposable
{
    private readonly Game2D _game;
    private readonly Form _window;
    private readonly SKControl _surface;
    private readonly InputState _input = new();
    private readonly Renderer2D _renderer;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 16 };
    private readonly Stopwatch _clock = new();
    private FrameTime _frameTime;
    private double _previousTime;
    private double _nextTitleUpdateTime;
    private bool _disposed;

    public GameHost(Game2D game)
    {
        _game = game;
        _renderer = new Renderer2D(game.Camera);
        _surface = new SKControl
        {
            Dock = DockStyle.Fill,
            TabStop = true
        };
        _window = new Form
        {
            Text = game.WindowTitle,
            ClientSize = new Size(2000, 1400),
            StartPosition = FormStartPosition.CenterScreen
        };

        _window.Controls.Add(_surface);
        _input.Attach(_window, _surface);
        _surface.PaintSurface += OnPaintSurface;
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
        var deltaTime = (float)Math.Clamp(totalTime - _previousTime, 0d, 0.1d);
        _previousTime = totalTime;
        _frameTime = new FrameTime(deltaTime, totalTime, _frameTime.FrameNumber + 1);

        var canvasSize = _surface.CanvasSize;
        var deviceWidth = canvasSize.Width > 0
            ? (int)MathF.Round(canvasSize.Width)
            : _surface.ClientSize.Width;
        var deviceHeight = canvasSize.Height > 0
            ? (int)MathF.Round(canvasSize.Height)
            : _surface.ClientSize.Height;
        _input.SetDeviceMapping(_surface.ClientSize, deviceWidth, deviceHeight);
        _game.Camera.SetViewport(deviceWidth, deviceHeight);

        if (_input.WasKeyPressed(Keys.Escape))
        {
            _input.EndFrame();
            _window.Close();
            return;
        }

        _game.Update(_frameTime, _input);

        if (totalTime >= _nextTitleUpdateTime)
        {
            var title = _game.WindowTitle;
            if (!string.Equals(_window.Text, title, StringComparison.Ordinal))
                _window.Text = title;
            _nextTitleUpdateTime = totalTime + 0.25d;
        }
        _input.EndFrame();

        // Refresh invokes PaintSurface now, so every loop iteration is Update -> Render.
        _surface.Refresh();
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        _renderer.BeginFrame(e.Surface.Canvas, e.Info.Width, e.Info.Height, _frameTime);
        _game.Render(_renderer);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _timer.Dispose();
        _renderer.Dispose();
        _surface.Dispose();
        _window.Dispose();
        _game.Dispose();
    }
}
