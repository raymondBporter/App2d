using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Runtime.InteropServices;
using Color = Microsoft.Xna.Framework.Color;

namespace App2d.EditorUi;

/// <summary>Renders ImGui draw lists through an existing MonoGame graphics device. It owns no window or frame loop.</summary>
internal sealed class ImGuiRenderer : IDisposable
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
    private readonly GraphicsDevice _device;
    private readonly BasicEffect _effect;
    private readonly RasterizerState _rasterizer = new() { CullMode = CullMode.None, ScissorTestEnable = true };
    private readonly Dictionary<nint, Texture2D> _textures = [];
    private Texture2D? _font;
    private nint _fontId;
    private GuiVertex[] _vertices = new GuiVertex[8192];
    private short[] _indices = new short[16384];
    private int _nextTexture = 1;

    public ImGuiRenderer(GraphicsDevice device)
    {
        _device = device;
        _effect = new(device) { TextureEnabled = true, VertexColorEnabled = true };
    }

    public unsafe void RebuildFont()
    {
        if (_font is not null) { Unregister(_fontId); _font.Dispose(); }
        var io = ImGui.GetIO(); io.Fonts.GetTexDataAsRGBA32(out nint pixels, out var width, out var height, out _);
        var bytes = new byte[width * height * 4]; Marshal.Copy(pixels, bytes, 0, bytes.Length);
        _font = new(_device, width, height); _font.SetData(bytes); _fontId = Register(_font); io.Fonts.SetTexID(_fontId); io.Fonts.ClearTexData();
    }
    public nint Register(Texture2D texture) { var id = (nint)_nextTexture++; _textures.Add(id, texture); return id; }
    public void Unregister(nint id) => _textures.Remove(id);
    public void Render(ImDrawDataPtr data)
    {
        if (data.TotalVtxCount == 0) return;
        var oldBlend = _device.BlendState; var oldDepth = _device.DepthStencilState; var oldRaster = _device.RasterizerState; var oldScissor = _device.ScissorRectangle; var oldSampler = _device.SamplerStates[0];
        var oldViewport = _device.Viewport;
        try
        {
            _device.Viewport = new Viewport(0, 0, (int)data.DisplaySize.X, (int)data.DisplaySize.Y);
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
        finally { _device.Viewport = oldViewport; _device.BlendState = oldBlend; _device.DepthStencilState = oldDepth; _device.RasterizerState = oldRaster; _device.ScissorRectangle = oldScissor; _device.SamplerStates[0] = oldSampler; }
    }
    public void Dispose()
    {
        _font?.Dispose();
        _effect.Dispose();
        _rasterizer.Dispose();
        _textures.Clear();
    }
}
