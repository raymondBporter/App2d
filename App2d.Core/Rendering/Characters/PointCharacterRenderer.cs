using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace App2d.Core.Rendering.Characters;

/// <summary>GPU submission only. Caller owns targets, clearing, camera and actor ordering.</summary>
public sealed class PointCharacterRenderer : IDisposable
{
    private readonly GraphicsDevice _device;
    private readonly BasicEffect _effect;
    private readonly Dictionary<string, Texture2D> _images = new(StringComparer.OrdinalIgnoreCase);
    private readonly MeshBufferCache<CharacterMesh> _meshes = new();
    private bool _disposed;

    /// <summary>Maximum resident vertex-buffer bytes; least recently drawn meshes are released after submission.</summary>
    public long MeshCacheBudgetBytes
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            field = value;
        }
    } = 32 * 1024 * 1024;
    public long CachedMeshBytes => _meshes.Bytes;
    /// <summary>Total uploads since construction. Changing only a draw matrix does not upload vertices.</summary>
    public long MeshUploadCount { get; private set; }
    public long MeshDrawCount { get; private set; }
    public PointCharacterRenderer(GraphicsDevice device)
    {
        _device = device;
        _effect = new(device)
        {
            VertexColorEnabled = true,
            LightingEnabled = false
        };
    }

    public static Matrix Projection(int width, int height, System.Numerics.Vector2 anchor, float pixelsPerUnit)
    {
        var projection = Matrix.CreateOrthographicOffCenter(-anchor.X / pixelsPerUnit, (width - anchor.X) / pixelsPerUnit,
            -(height - anchor.Y) / pixelsPerUnit, anchor.Y / pixelsPerUnit, -64, 64);
        // Exported Z grows away from the fixed camera. XNA's orthographic helper uses the opposite convention.
        projection.M33 = 1f / 128;
        projection.M43 = .5f;
        return projection;
    }
    public void Draw(PuppetDrawing drawing, Matrix projection, Matrix world)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(drawing);
        foreach (var batch in drawing.Batches)
        {
            Texture2D? texture = null;
            if (batch.Texture is { } path)
            {
                var full = IO.FilePaths.ResolveUnderRoot(drawing.TextureRoot, path);
                if (!_images.TryGetValue(full, out texture))
                {
                    using var decoded = Textures.Texture2D.Load(full);
                    texture = new(_device, decoded.Width, decoded.Height);
                    try
                    {
                        texture.SetData(decoded.CopyPixels());
                    }
                    catch
                    {
                        texture.Dispose();
                        throw;
                    }
                    _images.Add(full, texture);
                }
            }
            Draw(drawing.Mesh, projection, world, texture, writeDepth: batch.WriteDepth, start: batch.Start, count: batch.Count, additive: batch.Blend == "additive");
        }
    }
    public void Draw(CharacterMesh mesh, Matrix projection, Matrix world, Texture2D? texture = null, bool writeDepth = true, int start = 0, int? count = null, bool additive = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(mesh);
        var vertexCount = count ?? (mesh.Count - start);
        if (start < 0 || start > mesh.Count || vertexCount < 0 || vertexCount > mesh.Count - start
            || start % 3 != 0 || vertexCount % 3 != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "The draw range must contain complete triangles within the mesh.");
        }
        if (vertexCount == 0)
        {
            return;
        }

        var buffers = GetMeshBuffers(mesh);
        var blend = _device.BlendState;
        var depth = _device.DepthStencilState;
        var raster = _device.RasterizerState;
        var sampler = _device.SamplerStates[0];
        try
        {
            _device.BlendState = additive ? BlendState.Additive : BlendState.NonPremultiplied;
            _device.DepthStencilState = writeDepth ? DepthStencilState.Default : DepthStencilState.DepthRead;
            _device.RasterizerState = RasterizerState.CullNone;
            _device.SamplerStates[0] = SamplerState.LinearClamp;
            _effect.World = world;
            _effect.View = Matrix.Identity;
            _effect.Projection = projection;
            _effect.TextureEnabled = texture is not null;
            _effect.Texture = texture;
            _device.SetVertexBuffer(buffers.Vertices);
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                // Bounded submissions work with every supported MonoGame primitive count limit.
                var end = start + vertexCount;
                for (var offset = start; offset < end; offset += 24576)
                {
                    _device.DrawPrimitives(PrimitiveType.TriangleList, offset, Math.Min(24576, end - offset) / 3);
                    MeshDrawCount++;
                }
            }
        }
        finally
        {
            _device.SetVertexBuffer(null);
            _meshes.Trim(MeshCacheBudgetBytes);
            _device.BlendState = blend;
            _device.DepthStencilState = depth;
            _device.RasterizerState = raster;
            _device.SamplerStates[0] = sampler;
        }
    }

    private MeshBuffers GetMeshBuffers(CharacterMesh mesh)
    {
        if (!_meshes.TryGet(mesh, out var buffers) || buffers.Vertices.VertexCount < mesh.Count)
        {
            // Geometric growth avoids reallocating the GPU buffer as a pose grows slightly.
            var capacity = (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)mesh.Count);
            buffers = new MeshBuffers(new DynamicVertexBuffer(_device, VertexPositionColorTexture.VertexDeclaration, capacity, BufferUsage.WriteOnly))
            {
                Version = mesh.Version - 1
            };
            _meshes.Add(mesh, buffers);
        }
        if (buffers.Version != mesh.Version)
        {
            // Discard preserves data still used by earlier draws when this mesh is rebuilt for another actor.
            ((DynamicVertexBuffer)buffers.Vertices).SetData(mesh.Buffer, 0, mesh.Count, SetDataOptions.Discard);
            buffers.Version = mesh.Version;
            MeshUploadCount++;
        }
        return buffers;
    }

    /// <summary>Releases retained meshes, for example when replacing a studio document.</summary>
    public void ClearMeshCache()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _meshes.Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _meshes.Dispose();
        foreach (var texture in _images.Values)
        {
            texture.Dispose();
        }

        _effect.Dispose();
        _disposed = true;
    }
}
