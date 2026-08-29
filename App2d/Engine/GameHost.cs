using System.Diagnostics;
using App2d.Engine.Diagnostics;
using App2d.Engine.Rendering;
using App2d.Engine.Rendering.Textures;
using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace App2d.Engine;

public sealed class GameHost : IDisposable
{
    private const double FixedDeltaSeconds = 1d / 120d;
    private const double MaximumFrameSeconds = 0.1d;
    private readonly Game2D _game;
    private readonly Form _window;
    private readonly Control _surface;
    private readonly SKControl? _rasterSurface;
    private readonly SKGLControl? _gpuSurface;
    private readonly InputState _input = new();
    private readonly Renderer2D _renderer;
    private readonly DeveloperConsoleView _consoleView;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 16 };
    private readonly Stopwatch _clock = new();
    private FrameTime _frameTime;
    private FrameTime _renderFrameTime;
    private double _accumulator;
    private double _previousTime;
    private double _simulationTime;
    private double _nextTitleUpdateTime;
    private bool _gpuCacheConfigured;
    private bool _disposed;

    public GameHost(Game2D game)
    {
        _game = game;
        _renderer = new Renderer2D(game.Camera);
        if (string.Equals(
                Environment.GetEnvironmentVariable("APP2D_RENDER_BACKEND"),
                "raster",
                StringComparison.OrdinalIgnoreCase))
        {
            _rasterSurface = new SKControl();
            _surface = _rasterSurface;
            _rasterSurface.PaintSurface += OnPaintRasterSurface;
        }
        else
        {
            _gpuSurface = new SKGLControl();
            _surface = _gpuSurface;
            _gpuSurface.PaintSurface += OnPaintGpuSurface;
        }
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
        _consoleView = new DeveloperConsoleView(game.DeveloperConsole)
        {
            Visible = false
        };
        _window.Controls.Add(_consoleView);
        PositionConsole();
        _window.Resize += (_, _) => PositionConsole();
        _window.KeyDown += OnWindowKeyDown;
        _window.KeyPress += OnWindowKeyPress;
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
        if (_consoleView.IsOpen && _surface.Focused)
            _consoleView.FocusInput();

        var totalTime = _clock.Elapsed.TotalSeconds;
        var elapsedSeconds = Math.Clamp(totalTime - _previousTime, 0d, MaximumFrameSeconds);
        _previousTime = totalTime;
        _accumulator = Math.Min(_accumulator + elapsedSeconds, MaximumFrameSeconds);
        _renderFrameTime = new FrameTime(
            (float)elapsedSeconds,
            totalTime,
            _renderFrameTime.FrameNumber + 1);

        var canvasSize = _gpuSurface?.CanvasSize ?? _rasterSurface!.CanvasSize;
        var deviceWidth = canvasSize.Width > 0
            ? (int)MathF.Round(canvasSize.Width)
            : _surface.ClientSize.Width;
        var deviceHeight = canvasSize.Height > 0
            ? (int)MathF.Round(canvasSize.Height)
            : _surface.ClientSize.Height;
        _input.SetDeviceMapping(_surface.ClientSize, deviceWidth, deviceHeight);
        _game.Camera.SetViewport(deviceWidth, deviceHeight);

        if (!_consoleView.IsOpen && _input.WasKeyPressed(Keys.Escape))
        {
            _input.EndFrame();
            _window.Close();
            return;
        }

        while (_accumulator >= FixedDeltaSeconds)
        {
            _simulationTime += FixedDeltaSeconds;
            _frameTime = new FrameTime(
                (float)FixedDeltaSeconds,
                _simulationTime,
                _frameTime.FrameNumber + 1);
            _game.Update(_frameTime, _input);
            _input.EndFrame();
            _accumulator -= FixedDeltaSeconds;
        }

        if (totalTime >= _nextTitleUpdateTime)
        {
            var title = _game.WindowTitle;
            if (!string.Equals(_window.Text, title, StringComparison.Ordinal))
                _window.Text = title;
            _nextTitleUpdateTime = totalTime + 0.25d;
        }
        // Refresh invokes PaintSurface now. Simulation consumes real time in exact
        // 1/120-second steps, independently of this timer's render cadence.
        _surface.Refresh();
    }

    private void OnPaintRasterSurface(object? sender, SKPaintSurfaceEventArgs e) =>
        RenderSurface(e.Surface, e.Info.Width, e.Info.Height);

    private void OnPaintGpuSurface(object? sender, SKPaintGLSurfaceEventArgs e)
    {
        if (!_gpuCacheConfigured)
        {
            _gpuSurface!.GRContext.SetResourceCacheLimit(
                TextureMemoryBudget2D.GpuResourceCacheBytes);
            _gpuCacheConfigured = true;
        }
        RenderSurface(e.Surface, e.Info.Width, e.Info.Height);
    }

    private void RenderSurface(SKSurface surface, int width, int height)
    {
        _renderer.BeginFrame(surface.Canvas, width, height, _frameTime);
        if (_game.DrawGraphics)
        {
            _game.Render(_renderer);
        }
        else
        {
            _renderer.Clear(new SKColor(24, 27, 36));
        }
        _game.RenderDiagnostics(_renderer, _renderFrameTime);
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Oemtilde)
        {
            SetConsoleOpen(!_consoleView.IsOpen);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Escape && _consoleView.IsOpen)
        {
            SetConsoleOpen(false);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (_consoleView.IsOpen)
        {
            _consoleView.FocusInput();
            if (_consoleView.HandleCommandKey(e.KeyCode))
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }
    }

    private void OnWindowKeyPress(object? sender, KeyPressEventArgs e)
    {
        if (_consoleView.IsOpen && _consoleView.InsertCharacter(e.KeyChar))
            e.Handled = true;
    }

    private void SetConsoleOpen(bool isOpen)
    {
        _input.SetSuppressed(isOpen);
        if (isOpen)
        {
            _consoleView.Open();
            // The toggle originates from the game surface. Move focus after that
            // key event has fully unwound so the surface cannot reclaim it.
            _window.BeginInvoke((Action)_consoleView.FocusInput);
        }
        else
        {
            _consoleView.CloseAndClearFocus();
            _surface.Focus();
        }
    }

    private void PositionConsole()
    {
        var height = Math.Clamp((int)(_window.ClientSize.Height * 0.42f), 240, 520);
        _consoleView.Bounds = new Rectangle(
            0,
            Math.Max(0, _window.ClientSize.Height - height),
            _window.ClientSize.Width,
            Math.Min(height, _window.ClientSize.Height));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _timer.Dispose();
        _window.KeyDown -= OnWindowKeyDown;
        _window.KeyPress -= OnWindowKeyPress;
        _gpuSurface?.PaintSurface -= OnPaintGpuSurface;
        _rasterSurface?.PaintSurface -= OnPaintRasterSurface;
        _renderer.Dispose();
        _consoleView.Dispose();
        _surface.Dispose();
        _window.Dispose();
        _game.Dispose();
    }
}
