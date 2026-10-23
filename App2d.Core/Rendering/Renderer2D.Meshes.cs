using App2d.Core.Geometry.Meshes;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Rendering;
using App2d.Core.Rendering.Textures;
using App2d.Core.Validation;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using XnaMatrix = Microsoft.Xna.Framework.Matrix;
using XnaVector3 = Microsoft.Xna.Framework.Vector3;

namespace App2d.Rendering;

public sealed partial class Renderer2D
{
    // Small shapes remain in the triangle batch, avoiding a draw call per sprite or debug primitive.
    private const int RetainedShapeTriangleThreshold = 64;
    private readonly MeshBufferCache<MeshKey> _meshes = new();

    /// <summary>Maximum resident mesh-buffer bytes. Eviction happens after submission.</summary>
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
    /// <summary>Total retained mesh uploads since construction; matrices and solid colors do not cause uploads.</summary>
    public long MeshUploadCount { get; private set; }
    public long MeshDrawCount { get; private set; }

    /// <summary>
    /// Draws reusable local geometry with a material and local-to-world transform. Built-in materials retain
    /// indexed GPU buffers; custom vertex-color functions are evaluated at each draw in the triangle batch.
    /// Keep the mesh instance to reuse its upload. Draw order is the same as call order.
    /// </summary>
    public void DrawMesh(TriangleMesh2D mesh, IShader2D material, Matrix3x2 localToWorld)
    {
        RequireFrame();
        ArgGuard.ThrowIfNull(mesh);
        ArgGuard.ThrowIfNull(material);
        ArgGuard.Finite(localToWorld);
        SelectMaterial(material);
        DrawMeshCore(mesh, localToWorld * _camera.WorldToDeviceMatrix, mesh.Bounds, material);
    }

    public void DrawMesh(TriangleMesh2D mesh, IShader2D material) => DrawMesh(mesh, material, Matrix3x2.Identity);

    private void DrawMeshCore(TriangleMesh2D mesh, Matrix3x2 localToDevice, Rect2D bounds, IShader2D material)
    {
        if (!TryGetMeshStyle(material, out var style))
        {
            foreach (var triangle in mesh.Triangles)
            {
                Triangle(MaterialVertex(triangle.A, localToDevice, bounds, material),
                    MaterialVertex(triangle.B, localToDevice, bounds, material),
                    MaterialVertex(triangle.C, localToDevice, bounds, material));
            }
            return;
        }

        // Submit earlier primitives before the immediate mesh draw, even when the texture is unchanged.
        Flush();
        var key = new MeshKey(mesh, bounds, style);
        if (!_meshes.TryGet(key, out var buffers))
        {
            buffers = UploadMesh(mesh, bounds, material);
            _meshes.Add(key, buffers);
            MeshUploadCount++;
        }

        try
        {
            SetMaterialState();
            _effect.World = ToXnaMatrix(localToDevice);
            if (material is SolidColorShader solid)
            {
                // BasicEffect premultiplies diffuse RGB by Alpha before applying vertex colors.
                var color = solid.BaseColor.ToVector4();
                _effect.DiffuseColor = new XnaVector3(color.X, color.Y, color.Z);
                _effect.Alpha = color.W;
            }
            _device.SetVertexBuffer(buffers.Vertices);
            _device.Indices = buffers.Indices;
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                ApplyBatchSampler();
                const int trianglesPerDraw = 8192;
                for (var triangle = 0; triangle < mesh.TriangleCount; triangle += trianglesPerDraw)
                {
                    _device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, triangle * 3,
                        Math.Min(trianglesPerDraw, mesh.TriangleCount - triangle));
                    MeshDrawCount++;
                }
            }
        }
        finally
        {
            _device.SetVertexBuffer(null);
            _device.Indices = null;
            _effect.World = XnaMatrix.Identity;
            _effect.DiffuseColor = XnaVector3.One;
            _effect.Alpha = 1;
            _meshes.Trim(MeshCacheBudgetBytes);
        }
    }

    private MeshBuffers UploadMesh(TriangleMesh2D mesh, Rect2D bounds, IShader2D material)
    {
        var vertices = new VertexPositionColorTexture[mesh.Vertices.Length];
        for (var i = 0; i < vertices.Length; i++)
        {
            vertices[i] = material is SolidColorShader
                ? Vertex(mesh.Vertices[i], Color4b.White)
                : MaterialVertex(mesh.Vertices[i], Matrix3x2.Identity, bounds, material);
        }

        var vertexBuffer = new VertexBuffer(_device, VertexPositionColorTexture.VertexDeclaration,
            vertices.Length, BufferUsage.WriteOnly);
        IndexBuffer? indexBuffer = null;
        try
        {
            vertexBuffer.SetData(vertices);
            // Keep 0xffff out of 16-bit index buffers; the backend can treat it as a cut index.
            if (vertices.Length <= ushort.MaxValue)
            {
                var indices = new ushort[mesh.Indices.Length];
                for (var i = 0; i < indices.Length; i++)
                {
                    indices[i] = (ushort)mesh.Indices[i];
                }
                indexBuffer = new IndexBuffer(_device, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
                indexBuffer.SetData(indices);
            }
            else
            {
                var indices = mesh.Indices.ToArray();
                indexBuffer = new IndexBuffer(_device, IndexElementSize.ThirtyTwoBits, indices.Length, BufferUsage.WriteOnly);
                indexBuffer.SetData(indices);
            }
            return new MeshBuffers(vertexBuffer, indexBuffer);
        }
        catch
        {
            vertexBuffer.Dispose();
            indexBuffer?.Dispose();
            throw;
        }
    }

    private static bool TryGetMeshStyle(IShader2D material, out MeshStyle style)
    {
        // Only known immutable vertex-color functions can be cached. Texture bindings are selected per draw.
        style = material switch
        {
            SolidColorShader => new(0, null, false, false),
            SpriteShader2D sprite => new(1, null, sprite.FlipX, sprite.FlipY),
            TextureShader2D or LinearGradientShader => new(2, material, false, false),
            _ => default
        };
        return material is SolidColorShader or SpriteShader2D or TextureShader2D or LinearGradientShader;
    }

    private static XnaMatrix ToXnaMatrix(Matrix3x2 matrix) => new(
        matrix.M11, matrix.M12, 0, 0,
        matrix.M21, matrix.M22, 0, 0,
        0, 0, 1, 0,
        matrix.M31, matrix.M32, 0, 1);

    /// <summary>Releases retained mesh buffers. Later draws upload them again.</summary>
    public void ClearMeshCache()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _meshes.Dispose();
    }

    private readonly record struct MeshStyle(byte Kind, IShader2D? Material, bool FlipX, bool FlipY);
    private readonly record struct MeshKey(TriangleMesh2D Mesh, Rect2D Bounds, MeshStyle Style);
}
