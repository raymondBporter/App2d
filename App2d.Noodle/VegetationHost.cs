using App2d.Core;
using App2d.Rendering;
using App2d.Rendering.Vegetation;
using Microsoft.Xna.Framework.Graphics;
using System.Diagnostics;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Noodle;

internal sealed class VegetationHost : IDisposable
{
    private const float GroundY = -235f;
    private static readonly XnaColor BackgroundColor = new(10, 18, 29);
    private static readonly XnaColor PanelColor = new(5, 10, 18, 228);
    private static readonly XnaColor MutedColor = new(161, 180, 202);
    private static readonly XnaColor ActiveColor = new(125, 255, 175);

    private readonly Camera2D _camera = new() { Position = new Vector2(0f, -92f), Zoom = 0.95f };
    private readonly GraphicsSurface2D _surface = new() { Dock = DockStyle.Fill, TabStop = true };
    private readonly VegetationPatch2D[] _patches;
    private readonly Form _window;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 16 };
    private readonly Stopwatch _clock = new();

    private Renderer2D? _renderer;
    private FrameTime _frameTime;
    private double _previousTime;
    private double _animationTime;
    private float _windStrength = 18f;
    private float _windSpeed = 1.15f;
    private float _gust;
    private bool _paused;
    private bool _disposed;

    public VegetationHost()
    {
        _patches =
        [
            new(-650f, -205f, GroundY, new(
                42f, 86f, 3.5f, 7.5f, 8f, 16f,
                new XnaColor(28, 104, 67), new XnaColor(129, 231, 109),
                0.045f, new XnaColor(255, 150, 201)), seed: 107),
            new(-225f, 225f, GroundY, new(
                55f, 116f, 2.5f, 5.5f, 9f, 24f,
                new XnaColor(105, 82, 39), new XnaColor(244, 205, 99),
                0.025f, new XnaColor(255, 238, 172)), seed: 211),
            new(205f, 650f, GroundY, new(
                76f, 154f, 3f, 6.5f, 12f, 12f,
                new XnaColor(18, 83, 82), new XnaColor(83, 213, 176),
                0f, new XnaColor(104, 228, 190)), seed: 313)
        ];

        _window = new Form
        {
            Text = "NoodleBRO — Procedural Vegetation Lab",
            ClientSize = new Size(1240, 760),
            MinimumSize = new Size(820, 520),
            StartPosition = FormStartPosition.CenterScreen,
            KeyPreview = true
        };
        _window.Controls.Add(_surface);

        _surface.RenderFrame += Render;
        _surface.DeviceDisposing += ReleaseRenderer;
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
        var elapsed = Math.Clamp(totalTime - _previousTime, 0d, 0.05d);
        _previousTime = totalTime;
        if (!_paused)
            _animationTime += elapsed;
        _gust = MathF.Max(0f, _gust - (float)elapsed * 0.42f);
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
            DrawBackdrop(_renderer);
            DrawGround(_renderer);

            var visible = _camera.VisibleWorldBounds;
            var wind = new VegetationWind2D(
                _animationTime,
                _windStrength,
                _windSpeed,
                SpatialFrequency: 0.027f,
                _gust);
            foreach (var patch in _patches)
                patch.Render(_renderer, visible.Left, visible.Right, wind);

            DrawLabels(_renderer);
            DrawHud(_renderer, width);
        }
        finally
        {
            _renderer.EndFrame();
        }
    }

    private static void DrawBackdrop(Renderer2D renderer)
    {
        renderer.DrawWorldCircle(new Vector2(-460f, 185f), 84f, new XnaColor(49, 80, 106, 75), 3f);
        Span<Vector2> horizon =
        [
            new(-2000f, GroundY),
            new(-2000f, -95f),
            new(-1250f, -150f),
            new(-650f, -105f),
            new(-80f, -165f),
            new(560f, -110f),
            new(1200f, -160f),
            new(2000f, -100f),
            new(2000f, GroundY)
        ];
        for (var index = 1; index < horizon.Length - 1; index++)
        {
            Span<Vector2> triangle = [horizon[0], horizon[index], horizon[index + 1]];
            renderer.DrawWorldConvexPolygon(triangle, new XnaColor(27, 48, 61));
        }
    }

    private static void DrawGround(Renderer2D renderer)
    {
        Span<Vector2> ground =
        [
            new(-2000f, GroundY),
            new(2000f, GroundY),
            new(2000f, GroundY - 400f),
            new(-2000f, GroundY - 400f)
        ];
        renderer.DrawWorldConvexPolygon(ground, new XnaColor(24, 39, 38));
        Span<Vector2> groundLine = [new(-2000f, GroundY), new(2000f, GroundY)];
        renderer.DrawWorldPolyline(groundLine, new XnaColor(101, 139, 92), 4f);
    }

    private void DrawLabels(Renderer2D renderer)
    {
        DrawWorldLabel(renderer, "MEADOW + FLOWERS", new Vector2(-535f, GroundY - 22f), new XnaColor(129, 231, 109));
        DrawWorldLabel(renderer, "DRY GRASS", new Vector2(-110f, GroundY - 22f), new XnaColor(244, 205, 99));
        DrawWorldLabel(renderer, "REEDS", new Vector2(355f, GroundY - 22f), new XnaColor(83, 213, 176));
    }

    private void DrawWorldLabel(Renderer2D renderer, string text, Vector2 world, XnaColor color) =>
        renderer.DrawScreenText(text, _camera.WorldToDevice(world), color);

    private void DrawHud(Renderer2D renderer, int width)
    {
        renderer.DrawScreenRoundedRectangle(new(18f, 18f, Math.Min(width - 18f, 720f), 146f), 12f, PanelColor);
        renderer.DrawScreenText("NOODLEBRO  /  PROCEDURAL VEGETATION", new Vector2(36f, 50f), XnaColor.White);
        renderer.DrawScreenText(
            $"Wind strength: {_windStrength,5:0.0}    Speed: {_windSpeed,4:0.00}    Gust: {_gust,4:0.00}",
            new Vector2(36f, 81f), ActiveColor);
        renderer.DrawScreenText("Left/Right strength    Up/Down speed    Space gust    P pause    R reset",
            new Vector2(36f, 111f), MutedColor);
        renderer.DrawScreenText("Wheel zoom    Roots stay planted; bend increases quadratically toward each tip",
            new Vector2(36f, 138f), MutedColor);
    }

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
            case Keys.Left:
                _windStrength = MathF.Max(-42f, _windStrength - 3f);
                break;
            case Keys.Right:
                _windStrength = MathF.Min(42f, _windStrength + 3f);
                break;
            case Keys.Down:
                _windSpeed = MathF.Max(0f, _windSpeed - 0.15f);
                break;
            case Keys.Up:
                _windSpeed = MathF.Min(4f, _windSpeed + 0.15f);
                break;
            case Keys.Space:
                _gust = 1f;
                break;
            case Keys.P:
                _paused = !_paused;
                break;
            case Keys.R:
                Reset();
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

    private void Reset()
    {
        _windStrength = 18f;
        _windSpeed = 1.15f;
        _gust = 0f;
        _paused = false;
        _camera.Position = new Vector2(0f, -92f);
        _camera.Zoom = 0.95f;
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
