using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace App2d.Rendering.Characters;

/// <summary>GPU submission only. Caller owns targets, clearing, camera and actor ordering.</summary>
public sealed class PointCharacterRenderer : IDisposable
{
    private readonly GraphicsDevice _device;
    private readonly BasicEffect _effect;
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
    public void Draw(CharacterMesh mesh, Matrix projection, Matrix world, Texture2D? texture = null, bool writeDepth = true)
    {
        if (mesh.Count == 0) return;
        var blend = _device.BlendState; var depth = _device.DepthStencilState; var raster = _device.RasterizerState; var sampler = _device.SamplerStates[0];
        try
        {
            _device.BlendState = BlendState.NonPremultiplied;
            _device.DepthStencilState = writeDepth ? DepthStencilState.Default : DepthStencilState.DepthRead;
            _device.RasterizerState = RasterizerState.CullNone;
            _device.SamplerStates[0] = SamplerState.LinearClamp;
            _effect.World = world; _effect.View = Matrix.Identity; _effect.Projection = projection;
            _effect.TextureEnabled = texture is not null; _effect.Texture = texture;
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                // Bounded submissions work with every supported MonoGame primitive count limit.
                for (var offset = 0; offset < mesh.Count; offset += 24576)
                    _device.DrawUserPrimitives(PrimitiveType.TriangleList, mesh.Buffer, offset, Math.Min(24576, mesh.Count - offset) / 3);
            }
        }
        finally { _device.BlendState = blend; _device.DepthStencilState = depth; _device.RasterizerState = raster; _device.SamplerStates[0] = sampler; }
    }
    public void Dispose() => _effect.Dispose();
}
