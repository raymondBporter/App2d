using ImGuiNET;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;

namespace App2d.EditorUi;

/// <summary>
/// Owns an ImGui context, its renderer and the supplied platform adapter. The caller supplies a graphics device
/// and drives Begin, its view code, and Render from any frame loop. Registered application textures stay caller-owned.
/// </summary>
public sealed class ImGuiHost : IDisposable
{
    private readonly GraphicsDevice _device;
    private readonly IImGuiPlatform _platform;
    private readonly ImGuiRenderer _renderer;
    private readonly nint _context;
    private readonly ImGuiStyle _baseStyle;
    private bool _disposed;

    public float UiScale { get; private set; }
    public float DpiScale => _platform.DpiScale;
    public float UserScale { get; set; }
    public bool WantCaptureMouse { get { MakeCurrent(); return ImGui.GetIO().WantCaptureMouse; } }
    public bool WantCaptureKeyboard { get { MakeCurrent(); return ImGui.GetIO().WantCaptureKeyboard; } }
    public bool WantTextInput { get { MakeCurrent(); return ImGui.GetIO().WantTextInput; } }

    public unsafe ImGuiHost(GraphicsDevice device, IImGuiPlatform platform, float userScale = 1)
    {
        _device = device; _platform = platform; UserScale = userScale;
        _context = ImGui.CreateContext();
        ImGui.SetCurrentContext(_context);
        _renderer = new(device);
        var io = ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
        io.NativePtr->IniFilename = null;
        ImGui.StyleColorsDark(); var style = ImGui.GetStyle();
        style.WindowRounding = 4; style.FrameRounding = 4; style.GrabRounding = 4;
        style.WindowPadding = new(12, 12); style.ItemSpacing = new(8, 8); style.FramePadding = new(7, 5);
        style.Colors[(int)ImGuiCol.WindowBg] = new(.075f, .091f, .115f, 1);
        style.Colors[(int)ImGuiCol.ChildBg] = new(.075f, .091f, .115f, 1);
        style.Colors[(int)ImGuiCol.FrameBg] = new(.13f, .16f, .20f, 1);
        style.Colors[(int)ImGuiCol.Header] = new(.13f, .31f, .34f, 1);
        style.Colors[(int)ImGuiCol.HeaderHovered] = new(.17f, .40f, .42f, 1);
        style.Colors[(int)ImGuiCol.Button] = new(.16f, .27f, .31f, 1);
        style.Colors[(int)ImGuiCol.ButtonHovered] = new(.21f, .42f, .45f, 1);
        style.Colors[(int)ImGuiCol.CheckMark] = Ui.Accent;
        style.Colors[(int)ImGuiCol.SliderGrab] = Ui.Accent;
        _baseStyle = *style.NativePtr;
        ApplyScale();
    }

    private void MakeCurrent()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ImGui.SetCurrentContext(_context);
        Ui.Scale = UiScale;
    }

    private unsafe void ApplyScale()
    {
        // Rebuild fonts only between frames, including after a monitor or user scale change.
        var scale = DpiScale * Math.Clamp(UserScale, .75f, 2);
        if (MathF.Abs(scale - UiScale) < .001f) return;
        UiScale = scale; Ui.Scale = scale;
        var style = ImGui.GetStyle(); *style.NativePtr = _baseStyle;
        style.ScaleAllSizes(scale);
        var io = ImGui.GetIO(); io.Fonts.Clear(); io.FontGlobalScale = 1;
        var fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf");
        if (File.Exists(fontPath))
        {
            io.Fonts.AddFontFromFileTTF(fontPath, 17 * scale);
        }
        else { io.Fonts.AddFontDefault(); io.FontGlobalScale = 17 * scale / 13; }
        _renderer.RebuildFont();
    }

    public nint Register(Texture2D texture) => _renderer.Register(texture);
    public void Unregister(nint id) => _renderer.Unregister(id);

    public void Begin(float seconds)
    {
        MakeCurrent(); ApplyScale();
        var io = ImGui.GetIO();
        io.DisplaySize = new Vector2(_device.PresentationParameters.BackBufferWidth, _device.PresentationParameters.BackBufferHeight);
        io.DisplayFramebufferScale = Vector2.One; io.DeltaTime = MathF.Max(seconds, 1e-5f);
        _platform.UpdateInput(io);
        ImGui.NewFrame();
    }

    public void Render()
    {
        MakeCurrent(); ImGui.Render(); _renderer.Render(ImGui.GetDrawData());
    }

    public void Dispose()
    {
        if (_disposed) return;
        var previous = ImGui.GetCurrentContext();
        ImGui.SetCurrentContext(_context);
        _disposed = true;
        _platform.Dispose(); _renderer.Dispose(); ImGui.DestroyContext(_context);
        if (previous != _context) ImGui.SetCurrentContext(previous);
    }
}
