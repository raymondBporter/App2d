using App2d.Core;
using App2d.Diagnostics;
using App2d.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System.Diagnostics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d;

public sealed class GameHost : IDisposable
{
    private const double FixedDeltaSeconds = 1d / 120d;
    private const double MaximumFrameSeconds = 0.1d;
    private readonly Game2D _game;
    private readonly Form _window;
    private readonly GraphicsSurface2D _surface;

    private readonly InputState _input = new();
    private Renderer2D? _renderer;
    private readonly DeveloperConsoleView _consoleView;
    private readonly Control? _editorOverlay;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 16 };
    private readonly Stopwatch _clock = new();
    private FrameTime _frameTime;
    private FrameTime _renderFrameTime;
    private double _accumulator;
    private double _previousTime;
    private double _simulationTime;
    private double _nextTitleUpdateTime;

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
        _consoleView = new DeveloperConsoleView(game.DeveloperConsole)
        {
            Visible = false
        };
        _window.Controls.Add(_consoleView);
        _editorOverlay = game.OverlayControl;
        if (_editorOverlay is not null)
        {
            _window.Controls.Add(_editorOverlay);
            _editorOverlay.Enter += OnEditorOverlayEnter;
            _editorOverlay.Leave += OnEditorOverlayLeave;
            _editorOverlay.VisibleChanged += OnEditorOverlayVisibleChanged;
            _surface.MouseDown += OnSurfaceMouseDown;
            PositionEditorOverlay();
        }
        PositionConsole();
        _window.Resize += OnWindowResize;
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
        _renderFrameTime = new FrameTime((float)elapsedSeconds, totalTime, _renderFrameTime.FrameNumber + 1);

        var deviceWidth = Math.Max(1, _surface.ClientSize.Width);
        var deviceHeight = Math.Max(1, _surface.ClientSize.Height);
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

        _game.AdvancePresentation(_renderFrameTime);

        if (totalTime >= _nextTitleUpdateTime)
        {
            var title = _game.WindowTitle;
            if (!string.Equals(_window.Text, title, StringComparison.Ordinal))
                _window.Text = title;
            _nextTitleUpdateTime = totalTime + 0.25d;
        }
        // Refresh submits and presents the MonoGame frame now. Simulation consumes real time in exact
        // 1/120-second steps, independently of this timer's render cadence.
        _surface.Refresh();
    }

    private void RenderSurface(GraphicsDevice device, int width, int height)
    {
        _renderer ??= new Renderer2D(_game.Camera, device);
        _renderer.BeginFrame(width, height, _frameTime);
        try
        {
            if (_game.DrawGraphics)
                _game.Render(_renderer);
            else
                _renderer.Clear(new XnaColor(24, 27, 36));
            _game.RenderDiagnostics(_renderer, _renderFrameTime);
        }
        finally
        {
            _renderer.EndFrame();
        }
    }

    private void ReleaseRenderer()
    {
        _renderer?.Dispose();
        _renderer = null;
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

    private void PositionEditorOverlay()
    {
        if (_editorOverlay is null)
            return;
        const int width = 420;
        _editorOverlay.Bounds = new Rectangle(
            Math.Max(0, _window.ClientSize.Width - width),
            0,
            Math.Min(width, _window.ClientSize.Width),
            _window.ClientSize.Height);
        if (_editorOverlay.Visible)
            _editorOverlay.BringToFront();
    }

    private void OnWindowResize(object? sender, EventArgs e)
    {
        PositionConsole();
        PositionEditorOverlay();
    }

    private void OnEditorOverlayEnter(object? sender, EventArgs e) => _input.SetSuppressed(true);

    private void OnEditorOverlayLeave(object? sender, EventArgs e)
    {
        if (_surface.Focused)
            _input.SetSuppressed(false);
    }

    private void OnEditorOverlayVisibleChanged(object? sender, EventArgs e)
    {
        if (_editorOverlay is { Visible: true })
        {
            PositionEditorOverlay();
            return;
        }
        _input.SetSuppressed(false);
        if (_window.Visible)
            _surface.Focus();
    }

    private void OnSurfaceMouseDown(object? sender, MouseEventArgs e) => _input.SetSuppressed(false);

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _timer.Dispose();
        _window.KeyDown -= OnWindowKeyDown;
        _window.KeyPress -= OnWindowKeyPress;
        _window.Resize -= OnWindowResize;
        if (_editorOverlay is not null)
        {
            _editorOverlay.Enter -= OnEditorOverlayEnter;
            _editorOverlay.Leave -= OnEditorOverlayLeave;
            _editorOverlay.VisibleChanged -= OnEditorOverlayVisibleChanged;
            _surface.MouseDown -= OnSurfaceMouseDown;
        }

        ReleaseRenderer();
        _consoleView.Dispose();
        _surface.Dispose();
        _window.Dispose();
        _game.Dispose();
    }
}
