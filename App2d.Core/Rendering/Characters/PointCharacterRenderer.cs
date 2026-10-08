using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace App2d.Core.Rendering.Characters;

/// <summary>GPU submission only. Caller owns targets, clearing, camera and actor ordering.</summary>
public sealed class PointCharacterRenderer : IDisposable
{
    private readonly GraphicsDevice _device;
    private readonly BasicEffect _effect;
    private readonly Dictionary<string, Texture2D> _images = new(StringComparer.OrdinalIgnoreCase);
    public PointCharacterRenderer(GraphicsDevice device)
    { _device = device; _effect = new(device) { VertexColorEnabled = true, LightingEnabled = false }; }

    public static Matrix Projection(int width, int height, System.Numerics.Vector2 anchor, float pixelsPerUnit)
    {
        var projection = Matrix.CreateOrthographicOffCenter(-anchor.X / pixelsPerUnit, (width - anchor.X) / pixelsPerUnit,
            -(height - anchor.Y) / pixelsPerUnit, anchor.Y / pixelsPerUnit, -64, 64);
        // Exported Z grows away from the fixed camera. XNA's orthographic helper uses the opposite convention.
        projection.M33 = 1f / 128; projection.M43 = .5f;
        return projection;
    }
    public void Draw(PuppetDrawing drawing, Matrix projection, Matrix world)
    {
        foreach (var batch in drawing.Batches)
        {
            Texture2D? texture = null;
            if (batch.Texture is { } path)
            {
                var full = App2d.Core.IO.FilePaths.ResolveUnderRoot(drawing.TextureRoot, path);
                if (!_images.TryGetValue(full, out texture))
                {
                    using var decoded = App2d.Core.Rendering.Textures.Texture2D.Load(full);
                    texture = new(_device, decoded.Width, decoded.Height);
                    try { texture.SetData(decoded.CopyPixels()); } catch { texture.Dispose(); throw; }
                    _images.Add(full, texture);
                }
            }
            Draw(drawing.Mesh, projection, world, texture, writeDepth: batch.WriteDepth, start: batch.Start, count: batch.Count, additive: batch.Blend == "additive");
        }
    }
    public void Draw(CharacterMesh mesh, Matrix projection, Matrix world, Texture2D? texture = null, bool writeDepth = true, int start = 0, int? count = null, bool additive = false)
    {
        if (mesh.Count == 0) return;
        var blend = _device.BlendState; var depth = _device.DepthStencilState; var raster = _device.RasterizerState; var sampler = _device.SamplerStates[0];
        try
        {
            _device.BlendState = additive ? BlendState.Additive : BlendState.NonPremultiplied;
            _device.DepthStencilState = writeDepth ? DepthStencilState.Default : DepthStencilState.DepthRead;
            _device.RasterizerState = RasterizerState.CullNone;
            _device.SamplerStates[0] = SamplerState.LinearClamp;
            _effect.World = world; _effect.View = Matrix.Identity; _effect.Projection = projection;
            _effect.TextureEnabled = texture is not null; _effect.Texture = texture;
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                // Bounded submissions work with every supported MonoGame primitive count limit.
                var end = start + (count ?? mesh.Count);
                for (var offset = start; offset < end; offset += 24576)
                    _device.DrawUserPrimitives(PrimitiveType.TriangleList, mesh.Buffer, offset, Math.Min(24576, end - offset) / 3);
            }
        }
        finally { _device.BlendState = blend; _device.DepthStencilState = depth; _device.RasterizerState = raster; _device.SamplerStates[0] = sampler; }
    }
    public void Dispose() { foreach (var texture in _images.Values) texture.Dispose(); _effect.Dispose(); }
}
