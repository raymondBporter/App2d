using App2d.Core.Validation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace App2d.Core.Rendering;

/// <summary>
/// GPU submission of <see cref="LayeredMesh"/> through a fixed orthographic camera that turns each vertex's layer into depth.
/// The caller owns targets, colour clearing, camera and draw ordering; <see cref="ClearDepth"/> starts a new depth layer.
/// </summary>
public class LayeredMeshRenderer : IDisposable
{
    private readonly BasicEffect _effect;
    private readonly MeshBufferCache<LayeredMesh> _meshes = new();

    public LayeredMeshRenderer(GraphicsDevice device)
    {
        Device = device;
        _effect = new(device)
        {
            VertexColorEnabled = true,
            LightingEnabled = false
        };
    }

    protected GraphicsDevice Device { get; }
    protected bool Disposed { get; private set; }

    /// <summary>Maximum resident vertex-buffer bytes; least recently drawn meshes are released after submission.</summary>
    public long MeshCacheBudgetBytes
    {
        get;
        set
        {
            Ensure.NonNegative(value);
            field = value;
        }
    } = 32 * 1024 * 1024;
    public long CachedMeshBytes => _meshes.Bytes;
    /// <summary>Total uploads since construction. Changing only a draw matrix does not upload vertices.</summary>
    public long MeshUploadCount { get; private set; }
    public long MeshDrawCount { get; private set; }

    /// <summary>Device pixels (y down) to clip space, keeping layer depth: model z from -64 (nearest) to 64 maps to 0..1.</summary>
    public static Matrix DeviceProjection(int width, int height)
    {
        var projection = Matrix.CreateOrthographicOffCenter(0, width, height, 0, -64, 64);
        // Exported Z grows away from the fixed camera. XNA's orthographic helper uses the opposite convention.
        projection.M33 = 1f / 128;
        projection.M43 = .5f;
        return projection;
    }

    /// <summary>Model units (y up) to device pixels: the model origin lands on the anchor; depth is left in model units.</summary>
    public static Matrix View(System.Numerics.Vector2 anchor, float pixelsPerUnit)
        => Matrix.CreateScale(pixelsPerUnit, -pixelsPerUnit, 1) * Matrix.CreateTranslation(anchor.X, anchor.Y, 0);

    /// <summary>A fixed camera on a target of the given size: <see cref="View"/> over <see cref="DeviceProjection"/>.</summary>
    public static Matrix Projection(int width, int height, System.Numerics.Vector2 anchor, float pixelsPerUnit)
        => View(anchor, pixelsPerUnit) * DeviceProjection(width, height);

    /// <summary>Starts a new depth layer: later draws no longer test against anything drawn before.</summary>
    public void ClearDepth()
    {
        ObjectDisposedException.ThrowIf(Disposed, this);
        Device.Clear(ClearOptions.DepthBuffer, Color4b.Transparent, 1, 0);
    }

    public void Draw(LayeredMesh mesh, Matrix projection, Matrix world, Texture2D? texture = null, bool writeDepth = true, int start = 0, int? count = null, bool additive = false)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);
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
        var blend = Device.BlendState;
        var depth = Device.DepthStencilState;
        var raster = Device.RasterizerState;
        var sampler = Device.SamplerStates[0];
        try
        {
            Device.BlendState = additive ? BlendState.Additive : BlendState.NonPremultiplied;
            Device.DepthStencilState = writeDepth ? DepthStencilState.Default : DepthStencilState.DepthRead;
            Device.RasterizerState = RasterizerState.CullNone;
            Device.SamplerStates[0] = SamplerState.LinearClamp;
            _effect.World = world;
            _effect.View = Matrix.Identity;
            _effect.Projection = projection;
            _effect.TextureEnabled = texture is not null;
            _effect.Texture = texture;
            Device.SetVertexBuffer(buffers.Vertices);
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                // Bounded submissions work with every supported MonoGame primitive count limit.
                var end = start + vertexCount;
                for (var offset = start; offset < end; offset += 24576)
                {
                    Device.DrawPrimitives(PrimitiveType.TriangleList, offset, Math.Min(24576, end - offset) / 3);
                    MeshDrawCount++;
                }
            }
        }
        finally
        {
            Device.SetVertexBuffer(null);
            _meshes.Trim(MeshCacheBudgetBytes);
            Device.BlendState = blend;
            Device.DepthStencilState = depth;
            Device.RasterizerState = raster;
            Device.SamplerStates[0] = sampler;
        }
    }

    private MeshBuffers GetMeshBuffers(LayeredMesh mesh)
    {
        if (!_meshes.TryGet(mesh, out var buffers) || buffers.Vertices.VertexCount < mesh.Count)
        {
            // Geometric growth avoids reallocating the GPU buffer as a pose grows slightly.
            var capacity = (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)mesh.Count);
            buffers = new MeshBuffers(new DynamicVertexBuffer(Device, VertexPositionColorTexture.VertexDeclaration, capacity, BufferUsage.WriteOnly))
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
        ObjectDisposedException.ThrowIf(Disposed, this);
        _meshes.Dispose();
    }

    public void Dispose()
    {
        if (Disposed)
        {
            return;
        }
        Dispose(true);
        Disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases GPU resources; overrides release their own, then call the base.</summary>
    protected virtual void Dispose(bool disposing)
    {
        _meshes.Dispose();
        _effect.Dispose();
    }
}
