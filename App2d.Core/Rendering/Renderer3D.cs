using App2d.Core.Geometry.Meshes;
using App2d.Core.Mathematics.Transforms;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace App2d.Core.Rendering;

/// <summary>
/// Retained, vertex-colored triangle submission with a shared scene depth buffer. No character or rig dependencies.
/// The caller binds a depth-enabled render target, clears once per scene, and owns transparent draw ordering.
/// </summary>
public sealed class Renderer3D : IDisposable
{
    private readonly GraphicsDevice _device;
    private readonly BasicEffect _effect;
    private readonly MeshBufferCache<TriangleMeshBuilder3D<VertexPositionColorTexture, Color4b>> _meshes = new();
    private bool _disposed;

    public Renderer3D(GraphicsDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        _device = device;
        _effect = new(device) { VertexColorEnabled = true, LightingEnabled = false };
    }

    public long MeshCacheBudgetBytes { get; set => field = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value)); } = 32 * 1024 * 1024;
    public long CachedMeshBytes => _meshes.Bytes;
    public long MeshUploadCount { get; private set; }
    public long MeshDrawCount { get; private set; }

    public void Clear(Color4b color)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, color, 1f, 0);
    }

    public void Draw(TriangleMeshBuilder3D<VertexPositionColorTexture, Color4b> mesh, Frame3D world, Camera3D camera,
        Color4b? tint = null, bool writeDepth = true, bool alphaBlend = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(camera);
        if (mesh.Count % 3 != 0) throw new ArgumentException("A triangle mesh needs complete triangles.", nameof(mesh));
        if (mesh.Count == 0) return;
        var buffers = Buffers(mesh);
        var blend = _device.BlendState;
        var depth = _device.DepthStencilState;
        var raster = _device.RasterizerState;
        var color = tint ?? Color4b.White;
        try
        {
            _device.BlendState = alphaBlend ? BlendState.NonPremultiplied : BlendState.Opaque;
            _device.DepthStencilState = writeDepth ? DepthStencilState.Default : DepthStencilState.DepthRead;
            _device.RasterizerState = RasterizerState.CullNone;
            _effect.World = DeviceMatrix(world.Matrix);
            _effect.View = DeviceMatrix(camera.View);
            _effect.Projection = DeviceMatrix(camera.Projection);
            _effect.DiffuseColor = color.ToVector3();
            _effect.Alpha = color.A / 255f;
            _device.SetVertexBuffer(buffers.Vertices);
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                for (var offset = 0; offset < mesh.Count; offset += 24576)
                {
                    _device.DrawPrimitives(PrimitiveType.TriangleList, offset, Math.Min(24576, mesh.Count - offset) / 3);
                    MeshDrawCount++;
                }
            }
        }
        finally
        {
            _device.SetVertexBuffer(null);
            _device.BlendState = blend;
            _device.DepthStencilState = depth;
            _device.RasterizerState = raster;
            _meshes.Trim(MeshCacheBudgetBytes);
        }
    }

    private MeshBuffers Buffers(TriangleMeshBuilder3D<VertexPositionColorTexture, Color4b> mesh)
    {
        if (!_meshes.TryGet(mesh, out var buffers) || buffers.Vertices.VertexCount < mesh.Count)
        {
            var capacity = (int)BitOperations.RoundUpToPowerOf2((uint)mesh.Count);
            buffers = new(new DynamicVertexBuffer(_device, VertexPositionColorTexture.VertexDeclaration, capacity, BufferUsage.WriteOnly)) { Version = mesh.Version - 1 };
            _meshes.Add(mesh, buffers);
        }
        if (buffers.Version != mesh.Version)
        {
            ((DynamicVertexBuffer)buffers.Vertices).SetData(mesh.GetBuffer(), 0, mesh.Count, SetDataOptions.Discard);
            buffers.Version = mesh.Version;
            MeshUploadCount++;
        }
        return buffers;
    }

    private static Matrix DeviceMatrix(Matrix4x4 m) => new(m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24,
        m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44);

    public void Dispose()
    {
        if (_disposed) return;
        _meshes.Dispose();
        _effect.Dispose();
        _disposed = true;
    }
}
