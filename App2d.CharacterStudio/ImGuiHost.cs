using System.Runtime.InteropServices;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NVector2 = System.Numerics.Vector2;
using Color = Microsoft.Xna.Framework.Color;
using Keys = Microsoft.Xna.Framework.Input.Keys;
using ButtonState = Microsoft.Xna.Framework.Input.ButtonState;

namespace App2d.CharacterStudio;

/// <summary>MonoGame input and GPU adapter. Character rendering never depends on this class.</summary>
internal sealed class ImGuiHost : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct GuiVertex : IVertexType
    {
        public Vector2 Position;
        public Vector2 UV;
        public Color Color;
        public static readonly VertexDeclaration Declaration = new(20,
            new VertexElement(0, VertexElementFormat.Vector2, VertexElementUsage.Position, 0),
            new VertexElement(8, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
            new VertexElement(16, VertexElementFormat.Color, VertexElementUsage.Color, 0));
        public readonly VertexDeclaration VertexDeclaration => Declaration;
    }
    private readonly Game _game;
    private readonly GameWindow _window;
    private readonly GraphicsDevice _device;
    private readonly BasicEffect _effect;
    private readonly RasterizerState _rasterizer = new() { CullMode = CullMode.None, ScissorTestEnable = true };
    private readonly Dictionary<nint, Texture2D> _textures = [];
    private readonly nint _context;
    private Texture2D? _font;
    private nint _fontId;
    private readonly ImGuiStyle _baseStyle;
    private GuiVertex[] _vertices = new GuiVertex[8192];
    private short[] _indices = new short[16384];
    private int _nextTexture = 1, _wheel;
    private static readonly Keys[] KeysToPoll = Enum.GetValues<Keys>();
    private bool _disposed;
    public float UiScale { get; private set; }
    public float DpiScale => System.Windows.Forms.Control.FromHandle(_window.Handle)?.DeviceDpi / 96f ?? 1;
    public float UserScale { get; set; } = 1;

    public unsafe ImGuiHost(Game game, float userScale = 1)
    {
        _game = game; _window = game.Window; _device = game.GraphicsDevice; _context = ImGui.CreateContext();
        _effect = new(_device) { TextureEnabled = true, VertexColorEnabled = true };
        var io = ImGui.GetIO(); io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
        unsafe { io.NativePtr->IniFilename = null; }
        UserScale = userScale;
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
        style.Colors[(int)ImGuiCol.CheckMark] = new(.38f, .84f, .73f, 1);
        style.Colors[(int)ImGuiCol.SliderGrab] = new(.38f, .84f, .73f, 1);
        _baseStyle = *style.NativePtr;
        ApplyScale(); _window.TextInput += TextInput;
    }
    private unsafe void ApplyScale()
    {
        // Control sizes depend on monitor DPI and the user's preference, never the window size.
        var scale = DpiScale * Math.Clamp(UserScale, .75f, 2);
        if (MathF.Abs(scale - UiScale) < .001f) return;
        UiScale = scale;
        var style = ImGui.GetStyle(); *style.NativePtr = _baseStyle;
        style.ScaleAllSizes(UiScale);
        var io = ImGui.GetIO();
        if (_font is not null) { Unregister(_fontId); _font.Dispose(); }
        io.Fonts.Clear(); io.FontGlobalScale = 1;
        var fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf");
        if (File.Exists(fontPath)) io.Fonts.AddFontFromFileTTF(fontPath, 17 * UiScale);
        else { io.Fonts.AddFontDefault(); io.FontGlobalScale = 17 * UiScale / 13; }
        BuildFont();
    }
    private void TextInput(object? sender, TextInputEventArgs args) { if (!char.IsControl(args.Character)) ImGui.GetIO().AddInputCharacter(args.Character); }
    private unsafe void BuildFont()
    {
        var io = ImGui.GetIO(); io.Fonts.GetTexDataAsRGBA32(out nint pixels, out var width, out var height, out _);
        var bytes = new byte[width * height * 4]; Marshal.Copy(pixels, bytes, 0, bytes.Length);
        _font = new(_device, width, height); _font.SetData(bytes); _fontId = Register(_font); io.Fonts.SetTexID(_fontId); io.Fonts.ClearTexData();
    }
    public nint Register(Texture2D texture) { var id = (nint)_nextTexture++; _textures.Add(id, texture); return id; }
    public void Unregister(nint id) => _textures.Remove(id);
    public void Begin(float seconds)
    {
        ImGui.SetCurrentContext(_context); var io = ImGui.GetIO();
        // Font atlases can only be rebuilt between frames, including after a monitor change.
        ApplyScale();
        io.DisplaySize = new(_device.PresentationParameters.BackBufferWidth, _device.PresentationParameters.BackBufferHeight);
        io.DisplayFramebufferScale = NVector2.One; io.DeltaTime = MathF.Max(seconds, 1e-5f);
        var mouse = Mouse.GetState(); var keys = Keyboard.GetState(); var active = _game.IsActive;
        io.AddFocusEvent(active); io.AddMousePosEvent(active ? mouse.X : -float.MaxValue, active ? mouse.Y : -float.MaxValue);
        io.AddMouseButtonEvent(0, active && mouse.LeftButton == ButtonState.Pressed); io.AddMouseButtonEvent(1, active && mouse.RightButton == ButtonState.Pressed); io.AddMouseButtonEvent(2, active && mouse.MiddleButton == ButtonState.Pressed);
        if (active) io.AddMouseWheelEvent(0, (mouse.ScrollWheelValue - _wheel) / 120f); _wheel = mouse.ScrollWheelValue;
        foreach (var key in KeysToPoll) if (Map(key) is { } mapped) io.AddKeyEvent(mapped, active && keys.IsKeyDown(key));
        io.AddKeyEvent(ImGuiKey.ModCtrl, active && (keys.IsKeyDown(Keys.LeftControl) || keys.IsKeyDown(Keys.RightControl)));
        io.AddKeyEvent(ImGuiKey.ModShift, active && (keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift)));
        io.AddKeyEvent(ImGuiKey.ModAlt, active && (keys.IsKeyDown(Keys.LeftAlt) || keys.IsKeyDown(Keys.RightAlt)));
        io.AddKeyEvent(ImGuiKey.ModSuper, active && (keys.IsKeyDown(Keys.LeftWindows) || keys.IsKeyDown(Keys.RightWindows)));
        ImGui.NewFrame();
    }
    private static ImGuiKey? Map(Keys key) => key switch
    {
        >= Keys.A and <= Keys.Z => ImGuiKey.A + (key - Keys.A), >= Keys.D0 and <= Keys.D9 => ImGuiKey._0 + (key - Keys.D0),
        >= Keys.F1 and <= Keys.F12 => ImGuiKey.F1 + (key - Keys.F1),
        Keys.Tab => ImGuiKey.Tab, Keys.Enter => ImGuiKey.Enter, Keys.Escape => ImGuiKey.Escape, Keys.Space => ImGuiKey.Space,
        Keys.Left => ImGuiKey.LeftArrow, Keys.Right => ImGuiKey.RightArrow, Keys.Up => ImGuiKey.UpArrow, Keys.Down => ImGuiKey.DownArrow,
        Keys.Back => ImGuiKey.Backspace, Keys.Delete => ImGuiKey.Delete, Keys.Home => ImGuiKey.Home, Keys.End => ImGuiKey.End,
        Keys.PageUp => ImGuiKey.PageUp, Keys.PageDown => ImGuiKey.PageDown, Keys.Insert => ImGuiKey.Insert,
        Keys.LeftControl => ImGuiKey.LeftCtrl, Keys.RightControl => ImGuiKey.RightCtrl,
        Keys.LeftShift => ImGuiKey.LeftShift, Keys.RightShift => ImGuiKey.RightShift,
        Keys.LeftAlt => ImGuiKey.LeftAlt, Keys.RightAlt => ImGuiKey.RightAlt,
        _ => null
    };
    public unsafe void Render()
    {
        ImGui.Render(); var data = ImGui.GetDrawData(); if (data.TotalVtxCount == 0) return;
        var oldBlend = _device.BlendState; var oldDepth = _device.DepthStencilState; var oldRaster = _device.RasterizerState; var oldScissor = _device.ScissorRectangle; var oldSampler = _device.SamplerStates[0];
        try
        {
            _device.BlendState = BlendState.NonPremultiplied; _device.DepthStencilState = DepthStencilState.None; _device.RasterizerState = _rasterizer; _device.SamplerStates[0] = SamplerState.LinearClamp;
            _effect.World = Matrix.Identity; _effect.View = Matrix.Identity;
            _effect.Projection = Matrix.CreateOrthographicOffCenter(data.DisplayPos.X, data.DisplayPos.X + data.DisplaySize.X, data.DisplayPos.Y + data.DisplaySize.Y, data.DisplayPos.Y, -1, 1);
            for (var listIndex = 0; listIndex < data.CmdListsCount; listIndex++)
            {
                var list = data.CmdLists[listIndex];
                if (_vertices.Length < list.VtxBuffer.Size) Array.Resize(ref _vertices, list.VtxBuffer.Size * 2);
                if (_indices.Length < list.IdxBuffer.Size) Array.Resize(ref _indices, list.IdxBuffer.Size * 2);
                for (var i = 0; i < list.VtxBuffer.Size; i++) { var v = list.VtxBuffer[i]; _vertices[i] = new() { Position = new(v.pos.X, v.pos.Y), UV = new(v.uv.X, v.uv.Y), Color = new Color { PackedValue = v.col } }; }
                for (var i = 0; i < list.IdxBuffer.Size; i++) _indices[i] = unchecked((short)list.IdxBuffer[i]);
                for (var i = 0; i < list.CmdBuffer.Size; i++)
                {
                    var command = list.CmdBuffer[i];
                    if (command.UserCallback != 0) throw new NotSupportedException("Custom ImGui draw callbacks are not supported.");
                    var clip = command.ClipRect;
                    var left = Math.Max(0, (int)(clip.X - data.DisplayPos.X)); var top = Math.Max(0, (int)(clip.Y - data.DisplayPos.Y));
                    var right = Math.Min((int)data.DisplaySize.X, (int)MathF.Ceiling(clip.Z - data.DisplayPos.X)); var bottom = Math.Min((int)data.DisplaySize.Y, (int)MathF.Ceiling(clip.W - data.DisplayPos.Y));
                    if (right <= left || bottom <= top || command.ElemCount == 0) continue;
                    _device.ScissorRectangle = new(left, top, right - left, bottom - top);
                    _effect.Texture = _textures[command.TextureId];
                    foreach (var pass in _effect.CurrentTechnique.Passes)
                    {
                        pass.Apply(); _device.SamplerStates[0] = SamplerState.LinearClamp;
                        _device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, _vertices, (int)command.VtxOffset, list.VtxBuffer.Size - (int)command.VtxOffset,
                            _indices, (int)command.IdxOffset, (int)command.ElemCount / 3, GuiVertex.Declaration);
                    }
                }
            }
        }
        finally { _device.BlendState = oldBlend; _device.DepthStencilState = oldDepth; _device.RasterizerState = oldRaster; _device.ScissorRectangle = oldScissor; _device.SamplerStates[0] = oldSampler; }
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true; _window.TextInput -= TextInput;
        _font?.Dispose(); _effect.Dispose(); _rasterizer.Dispose(); ImGui.DestroyContext(_context);
    }
}
