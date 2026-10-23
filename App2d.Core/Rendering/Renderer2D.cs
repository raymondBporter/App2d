using App2d.Core;
using App2d.Core.Geometry.Meshes;
using App2d.Core.Geometry.Operations.Vertices;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Rendering;
using App2d.Core.Rendering.Characters;
using App2d.Core.Rendering.Textures;
using App2d.Core.Shapes;
using App2d.Core.Spatial;
using App2d.Core.Validation;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using GpuTexture = Microsoft.Xna.Framework.Graphics.Texture2D;
using Texture2D = App2d.Core.Rendering.Textures.Texture2D;
using XnaMatrix = Microsoft.Xna.Framework.Matrix;
using XnaVector2 = Microsoft.Xna.Framework.Vector2;
using XnaVector3 = Microsoft.Xna.Framework.Vector3;

namespace App2d.Rendering;

/// <summary>Ordered GPU triangle batches using MonoGame's XNA graphics API.</summary>
public sealed partial class Renderer2D : IDisposable
{
    private Camera2D _camera;
    private readonly GraphicsDevice _device;
    private readonly BasicEffect _effect;
    private readonly FontAtlas2D _font = new();
    private readonly VertexPositionColorTexture[] _vertices = new VertexPositionColorTexture[8190];
    private readonly Dictionary<Texture2D, Residency> _textures = [];
    private readonly LinkedList<Texture2D> _recentTextures = [];
    private readonly Dictionary<SamplerKey, SamplerState> _samplers = [];
    private GpuTexture? _batchTexture;
    private SamplerState? _batchSampler;
    private int _vertexCount;
    private long _textureBytes;
    private bool _frameActive;
    private bool _disposed;
    private Rect2D _visibleWorldBounds;

    public Renderer2D(Camera2D camera, GraphicsDevice device)
    {
        _camera = ArgGuard.RequireNotNull(camera);
        _device = ArgGuard.RequireNotNull(device);
        _effect = new BasicEffect(device) { VertexColorEnabled = true, World = XnaMatrix.Identity, View = XnaMatrix.Identity };
    }

    public void BeginFrame(int width, int height, FrameTime time)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        StateGuard.ThrowIf(_frameActive, "EndFrame must be called before beginning another frame.");
        ArgGuard.Positive(width);
        ArgGuard.Positive(height);
        _camera.SetViewport(width, height);
        _visibleWorldBounds = _camera.VisibleWorldBounds;
        _device.Viewport = new Viewport(0, 0, width, height);
        _effect.Projection = XnaMatrix.CreateOrthographicOffCenter(0, width, height, 0, 0, 1);
        _frameActive = true;
    }

    public void EndFrame()
    {
        RequireFrame();
        Flush();
        _batchTexture = null;
        _batchSampler = null;
        // Trim only after submission, so a queued batch never references an evicted texture.
        while (_textureBytes > TextureMemoryBudget2D.GpuResourceCacheBytes && _recentTextures.First is { } oldest)
        {
            ReleaseTexture(oldest.Value);
        }
        _meshes.Trim(MeshCacheBudgetBytes);

        _frameActive = false;
    }

    public void Clear(Color4b color)
    {
        RequireFrame();
        Flush();
        _device.Clear(color);
    }

    /// <summary>Device pixels per world unit for the current camera, for strokes that should scale with zoom.</summary>
    public float PixelsPerWorldUnit => _camera.PixelsPerWorldUnit;

    public void Draw(Scene2D scene)
    {
        foreach (var item in scene.GetDrawOrder())
        {
            Draw(item);
        }
    }

    /// <summary>Draws only the objects whose z-index falls in the inclusive range, so other passes can interleave.</summary>
    public void Draw(Scene2D scene, int minimumZIndex, int maximumZIndex)
    {
        foreach (var item in scene.GetDrawOrder())
        {
            if (item.ZIndex >= minimumZIndex && item.ZIndex <= maximumZIndex)
            {
                Draw(item);
            }
        }
    }

    public void Draw(WorldObject2D worldObject)
    {
        RequireFrame();
        if (!worldObject.IsVisible || IsCulled(worldObject))
        {
            return;
        }

        if (worldObject.Shader is AuthoredCharacterShader authored)
        {
            Flush();
            DrawAuthored(worldObject, authored);
            return;
        }
        var matrix = worldObject.Transform.Matrix * _camera.WorldToDeviceMatrix;
        var bounds = worldObject.LocalBounds.IsFinite ? worldObject.LocalBounds : GetVisibleLocalBounds(matrix);
        if (worldObject.Shader is SpriteShader2D)
        {
            StateGuard.ThrowIf(!worldObject.LocalBounds.IsFinite, "Sprites require finite local bounds.");
        }

        SelectMaterial(worldObject.Shader);
        if (worldObject.Shader is ShapeMaterialShader2D material)
        {
            if (material.HasFill) FillShape(worldObject.Shape, matrix, bounds, material);
            if (material.Material.Outline is not null && material.OutlineWidth > 0)
            {
                SelectBatch(null, null);
                OutlineShape(worldObject.Shape, matrix, bounds, material.OutlineColor, material.OutlineWidth * PixelsPerWorldUnit);
            }
            return;
        }
        FillShape(worldObject.Shape, matrix, bounds, worldObject.Shader);
    }

    private void SelectMaterial(IShader2D shader)
    {
        switch (shader)
        {
            case ShapeMaterialShader2D { Texture: { } texture }:
                SelectTexture(texture, new SamplerKey(TextureAddressMode.Clamp, TextureAddressMode.Clamp, TextureFilter.Linear));
                break;
            case SpriteShader2D sprite:
                SelectTexture(sprite.Texture, new SamplerKey(TextureAddressMode.Clamp, TextureAddressMode.Clamp, sprite.FilterMode));
                break;
            case TextureShader2D tile:
                SelectTexture(tile.Texture, new SamplerKey(tile.TileModeX, tile.TileModeY, tile.FilterMode));
                break;
            default:
                SelectBatch(null, null);
                break;
        }
    }

    private void SelectTexture(Texture2D texture, SamplerKey key)
    {
        ObjectDisposedException.ThrowIf(texture.IsDisposed, texture);
        if (!_textures.TryGetValue(texture, out var resident))
        {
            var gpu = new GpuTexture(_device, texture.Width, texture.Height, false, SurfaceFormat.Color);
            try
            {
                var pixels = texture.CopyPixels();
                for (var i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = Color4b.FromNonPremultiplied(pixels[i].R, pixels[i].G, pixels[i].B, pixels[i].A);
                }

                gpu.SetData(pixels);
            }
            catch
            {
                gpu.Dispose();
                throw;
            }
            resident = new Residency(gpu, _recentTextures.AddLast(texture), (long)texture.Width * texture.Height * 4);
            _textures.Add(texture, resident);
            texture.Disposed += ReleaseTexture;
            _textureBytes += resident.Bytes;
        }
        else
        {
            _recentTextures.Remove(resident.Node);
            _recentTextures.AddLast(resident.Node);
        }
        if (!_samplers.TryGetValue(key, out var sampler))
        {
            sampler = new SamplerState { AddressU = key.X, AddressV = key.Y, Filter = key.Filter };
            _samplers.Add(key, sampler);
        }
        SelectBatch(resident.Texture, sampler);
    }

    private void SelectBatch(GpuTexture? texture, SamplerState? sampler)
    {
        if (_batchTexture == texture && _batchSampler == sampler)
        {
            return;
        }

        Flush();
        _batchTexture = texture;
        _batchSampler = sampler;
    }

    private void Flush()
    {
        if (_vertexCount == 0)
        {
            return;
        }

        SetMaterialState();
        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            ApplyBatchSampler();

            _device.DrawUserPrimitives(PrimitiveType.TriangleList, _vertices, 0, _vertexCount / 3);
        }
        _vertexCount = 0;
    }

    private void SetMaterialState()
    {
        _device.BlendState = BlendState.AlphaBlend;
        _device.DepthStencilState = DepthStencilState.None;
        _device.RasterizerState = RasterizerState.CullNone;
        _effect.TextureEnabled = _batchTexture is not null;
        _effect.Texture = _batchTexture;
    }

    private void ApplyBatchSampler()
    {
        if (_batchSampler is not null)
        {
            _device.SamplerStates[0] = _batchSampler;
        }
    }

    private void Triangle(VertexPositionColorTexture a, VertexPositionColorTexture b, VertexPositionColorTexture c)
    {
        if (_vertexCount + 3 > _vertices.Length)
        {
            Flush();
        }

        _vertices[_vertexCount++] = a;
        _vertices[_vertexCount++] = b;
        _vertices[_vertexCount++] = c;
    }

    private static VertexPositionColorTexture Vertex(Vector2 point, Color4b color, Vector2 uv = default) =>
        new(new XnaVector3(point.X, point.Y, 0),
            Color4b.FromNonPremultiplied(color.R, color.G, color.B, color.A), new XnaVector2(uv.X, uv.Y));

    private static VertexPositionColorTexture MaterialVertex(Vector2 local, Matrix3x2 matrix, Rect2D bounds, IShader2D shader)
    {
        var uv = Vector2.Zero;
        switch (shader)
        {
            case ShapeMaterialShader2D { Texture: not null }:
                uv = (local - bounds.Min) / bounds.Size;
                uv.Y = 1f - uv.Y;
                break;
            case SpriteShader2D sprite:
                uv = (local - bounds.Min) / bounds.Size;
                if (sprite.FlipX)
                {
                    uv.X = 1f - uv.X;
                }

                if (!sprite.FlipY)
                {
                    uv.Y = 1f - uv.Y;
                }

                break;
            case TextureShader2D tile:
                // Preserve local-origin tiling; unlike sprites, tiled images use positive local Y.
                uv = tile.ImageOrigin is { } origin
                    ? new Vector2(local.X - origin.X, origin.Y - local.Y) / tile.TileSize
                    : local / tile.TileSize;
                break;
        }
        return Vertex(Vector2.Transform(local, matrix), shader.GetVertexColor(local, bounds), uv);
    }

    private void FillShape(IShape2D shape, Matrix3x2 matrix, Rect2D bounds, IShader2D shader)
    {
        if (shape is HeightFieldShape2D field)
        {
            FillShape(field.Polygon, matrix, bounds, shader);
            return;
        }
        if (shape is CompositeShape2D composite)
        {
            foreach (var part in composite.Parts)
            {
                FillShape(part, matrix, bounds, shader);
            }

            return;
        }
        if (shape is SimplePolygon2D polygon && polygon.Mesh.TriangleCount >= RetainedShapeTriangleThreshold
            && TryGetMeshStyle(shader, out _))
        {
            DrawMeshCore(polygon.Mesh, matrix, bounds, shader);
            return;
        }
        Span<Vector2> points = stackalloc Vector2[260];
        var hasFirst = false;
        var firstPoint = Vector2.Zero;
        var firstVertex = default(VertexPositionColorTexture);
        foreach (var triangle in shape.Triangles(points, shape.RoundSegmentsFor(matrix), clipBounds: bounds))
        {
            if (!hasFirst || firstPoint != triangle.A)
            {
                firstPoint = triangle.A;
                firstVertex = MaterialVertex(firstPoint, matrix, bounds, shader);
                hasFirst = true;
            }
            Triangle(firstVertex,
                MaterialVertex(triangle.B, matrix, bounds, shader), MaterialVertex(triangle.C, matrix, bounds, shader));
        }
    }

    public void DrawScreenRoundedRectangle(ScreenRectangle2D bounds, float radius, Color4b color, float strokeWidth = 0f)
    {
        RequireFrame();
        ArgGuard.NonNegative(radius);
        ArgGuard.NonNegative(strokeWidth);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        SelectBatch(null, null);
        Span<Vector2> points = stackalloc Vector2[36];
        var count = VertexGenerator2D.WriteRoundedRectangle(points,
            new(bounds.Left, bounds.Top), new(bounds.Right, bounds.Bottom), radius);
        if (strokeWidth > 0)
        {
            StrokePolygon(points[..count], Matrix3x2.Identity, color, strokeWidth);
        }
        else
        {
            var center = Vertex(new Vector2(bounds.MidX, bounds.MidY), color);
            for (var i = 0; i < count; i++)
            {
                Triangle(center, Vertex(points[i], color), Vertex(points[(i + 1) % count], color));
            }
        }
    }

    public void DrawScreenLabel(string text, Vector2 topLeft)
    {
        ArgGuard.ThrowIfNullOrWhiteSpace(text);
        DrawScreenRoundedRectangle(new(topLeft.X, topLeft.Y, topLeft.X + _font.Measure(text) + 28f, topLeft.Y + _font.LineHeight + 18f),
            9f, DebugTheme.LabelBackground);
        DrawScreenText(text, topLeft + new Vector2(14, 9 + _font.Ascent), DebugTheme.LabelText);
    }

    public void DrawScreenText(string text, Vector2 baseline, Color4b color)
    {
        RequireFrame();
        ArgGuard.ThrowIfNullOrWhiteSpace(text);
        SelectTexture(_font.Texture, new(TextureAddressMode.Clamp, TextureAddressMode.Clamp, TextureFilter.Linear));
        var cursor = baseline - new Vector2(4, _font.Ascent + 4);
        foreach (var character in text)
        {
            if (character == '\n')
            {
                cursor.X = baseline.X - 4;
                cursor.Y += _font.LineHeight;
                continue;
            }
            var glyph = _font.GetGlyph(character);
            TexturedQuad(new(cursor.X, cursor.Y, cursor.X + glyph.Bounds.Width, cursor.Y + glyph.Bounds.Height),
                glyph.Bounds, _font.Texture.Width, _font.Texture.Height, color);
            cursor.X += glyph.Advance;
        }
    }

    public void DrawScreenTexture(Texture2D texture, ScreenRectangle2D bounds)
    {
        RequireFrame();
        ArgGuard.ThrowIfNull(texture);
        SelectTexture(texture, new(TextureAddressMode.Clamp, TextureAddressMode.Clamp, TextureFilter.Linear));
        TexturedQuad(bounds, new(0, 0, texture.Width, texture.Height), texture.Width, texture.Height, Color4b.White);
    }

    /// <summary>Draws a transient convex world-space polygon without creating a scene object.</summary>
    public void DrawWorldConvexPolygon(ReadOnlySpan<Vector2> points, Color4b color) =>
        DrawWorldConvexPolygon(points, color, null, 0f);

    /// <summary>Draws a transient convex world-space polygon with optional fill and outline in world units.</summary>
    public void DrawWorldConvexPolygon(ReadOnlySpan<Vector2> points, Color4b? fillColor,
        Color4b? outlineColor = null, float worldStrokeWidth = 0f)
    {
        RequireFrame();
        ArgGuard.ThrowIfTooShort(points, 3);
        if (fillColor is null && outlineColor is null)
        {
            throw new ArgumentException("Provide a fill color, an outline color, or both.", nameof(fillColor));
        }

        foreach (var point in points)
        {
            ArgGuard.Finite(point, nameof(points));
        }

        SelectBatch(null, null);
        var matrix = _camera.WorldToDeviceMatrix;
        if (fillColor is { } fill)
        {
            foreach (var triangle in MeshTopology.TriangleFan<Vector2>(points))
            {
                Triangle(
                    Vertex(Vector2.Transform(triangle.A, matrix), fill),
                    Vertex(Vector2.Transform(triangle.B, matrix), fill),
                    Vertex(Vector2.Transform(triangle.C, matrix), fill));
            }
        }

        if (outlineColor is { } outline && worldStrokeWidth > 0f)
        {
            var screenWidth = Math.Max(1f, worldStrokeWidth * _camera.PixelsPerWorldUnit);
            for (var i = 0; i < points.Length; i++)
            {
                var next = (i + 1) % points.Length;
                Line(Vector2.Transform(points[i], matrix), Vector2.Transform(points[next], matrix), outline, screenWidth);
            }
        }
    }

    private void TexturedQuad(ScreenRectangle2D bounds, ScreenRectangle2D source, int width, int height, Color4b color)
    {
        var a = Vertex(new(bounds.Left, bounds.Top), color, new(source.Left / width, source.Top / height));
        var b = Vertex(new(bounds.Right, bounds.Top), color, new(source.Right / width, source.Top / height));
        var c = Vertex(new(bounds.Right, bounds.Bottom), color, new(source.Right / width, source.Bottom / height));
        var d = Vertex(new(bounds.Left, bounds.Bottom), color, new(source.Left / width, source.Bottom / height));
        Triangle(a, b, c);
        Triangle(a, c, d);
    }

    public void DrawWorldCircle(Vector2 center, float radius, Color4b color, float strokeWidth = 2f)
    {
        RequireFrame();
        ArgGuard.NonNegative(radius);
        ArgGuard.Positive(strokeWidth);
        SelectBatch(null, null);
        Span<Vector2> points = stackalloc Vector2[128];
        var segments = ShapeTessellation2D.CurveSegments(radius, _camera.WorldToDeviceMatrix);
        VertexGenerator2D.WriteCircle(points[..segments], center, radius);
        StrokePolygon(points[..segments], _camera.WorldToDeviceMatrix, color, strokeWidth);
    }

    /// <summary>Draws a world circle with stroke width specified in world units, scaling with camera zoom.</summary>
    public void DrawWorldThickCircle(Vector2 center, float radius, Color4b color, float worldStrokeWidth)
    {
        RequireFrame();
        ArgGuard.Positive(worldStrokeWidth);
        var screenWidth = Math.Max(1f, worldStrokeWidth * _camera.PixelsPerWorldUnit);
        DrawWorldCircle(center, radius, color, screenWidth);
    }

    public void DrawWorldPolyline(ReadOnlySpan<Vector2> points, Color4b color, float strokeWidth = 2f,
        LineCap2D cap = LineCap2D.Butt)
    {
        RequireFrame();
        ArgGuard.Positive(strokeWidth);
        ValidateLineCap(cap);
        foreach (var point in points)
        {
            ArgGuard.Finite(point, nameof(points));
        }

        SelectBatch(null, null);
        for (var i = 1; i < points.Length; i++)
        {
            Line(_camera.WorldToDevice(points[i - 1]), _camera.WorldToDevice(points[i]), color, strokeWidth,
                i == 1 ? cap : LineCap2D.Butt,
                i == points.Length - 1 ? cap : LineCap2D.Butt);
        }
    }

    /// <summary>Draws a polyline with stroke width specified in world units, scaling with camera zoom.</summary>
    public void DrawWorldThickPolyline(ReadOnlySpan<Vector2> points, float worldStrokeWidth, Color4b color,
        LineCap2D cap = LineCap2D.Butt)
    {
        RequireFrame();
        ArgGuard.Positive(worldStrokeWidth);
        ValidateLineCap(cap);
        var screenWidth = Math.Max(1f, worldStrokeWidth * _camera.PixelsPerWorldUnit);
        DrawWorldPolyline(points, color, screenWidth, cap);
    }

    public void DrawGrid(float spacing = 50f, int majorLineEvery = 5)
    {
        RequireFrame();
        ArgGuard.Positive(spacing);
        ArgGuard.Positive(majorLineEvery);
        SelectBatch(null, null);
        var visible = _camera.VisibleWorldBounds;
        for (var x = (int)MathF.Floor(visible.Left / spacing); x <= (int)MathF.Ceiling(visible.Right / spacing); x++)
        {
            Line(_camera.WorldToDevice(new(x * spacing, visible.Bottom)), _camera.WorldToDevice(new(x * spacing, visible.Top)),
                x == 0 ? DebugTheme.GridAxis : x % majorLineEvery == 0 ? DebugTheme.GridMajor : DebugTheme.GridMinor, x == 0 ? 2 : 1);
        }

        for (var y = (int)MathF.Floor(visible.Bottom / spacing); y <= (int)MathF.Ceiling(visible.Top / spacing); y++)
        {
            Line(_camera.WorldToDevice(new(visible.Left, y * spacing)), _camera.WorldToDevice(new(visible.Right, y * spacing)),
                y == 0 ? DebugTheme.GridAxis : y % majorLineEvery == 0 ? DebugTheme.GridMajor : DebugTheme.GridMinor, y == 0 ? 2 : 1);
        }
    }

    public void DrawShapeOutline(SpatialObject2D item, Color4b color, float screenStrokeWidth = 2f) =>
        DrawShape(item, outlineColor: color, screenStrokeWidth: screenStrokeWidth);

    public void DrawShapeOverlay(SpatialObject2D item, Color4b fillColor, Color4b outlineColor, float screenStrokeWidth = 2f) =>
        DrawShape(item, fillColor, outlineColor, screenStrokeWidth);

    private void OutlineShape(IShape2D shape, Matrix3x2 matrix, Rect2D bounds, Color4b color, float width)
    {
        if (shape is CompositeShape2D composite)
        {
            foreach (var part in composite.Parts)
            {
                OutlineShape(part, matrix, bounds, color, width);
            }

            return;
        }
        Span<Vector2> points = stackalloc Vector2[260];
        foreach (var segment in shape.OutlineSegments(points, shape.RoundSegmentsFor(matrix),
            clipBounds: bounds.IsFinite ? bounds : GetVisibleLocalBounds(matrix)))
        {
            Line(Vector2.Transform(segment.Start, matrix), Vector2.Transform(segment.End, matrix), color, width);
        }
    }

    private void StrokePolygon(ReadOnlySpan<Vector2> points, Matrix3x2 matrix, Color4b color, float width)
    {
        foreach (var segment in MeshTopology.Segments<Vector2>(points))
        {
            Line(Vector2.Transform(segment.Start, matrix), Vector2.Transform(segment.End, matrix), color, width);
        }
    }

    private void Line(Vector2 start, Vector2 end, Color4b color, float width,
        LineCap2D startCap = LineCap2D.Butt, LineCap2D endCap = LineCap2D.Butt) =>
        Line(start, end, color, width, width, startCap, endCap);

    private void Line(Vector2 start, Vector2 end, Color4b color, float startWidth, float endWidth,
        LineCap2D startCap = LineCap2D.Butt, LineCap2D endCap = LineCap2D.Butt)
    {
        Span<Vector2> vertices = stackalloc Vector2[StrokeTessellation2D.MaximumSegmentVertices];
        var count = StrokeTessellation2D.WriteSegment(vertices, start, end, startWidth, endWidth, startCap, endCap);
        foreach (var triangle in MeshTopology.TriangleList<Vector2>(vertices[..count]))
        {
            Triangle(Vertex(triangle.A, color), Vertex(triangle.B, color), Vertex(triangle.C, color));
        }
    }

    private bool IsCulled(SpatialObject2D item) => item.WorldBounds.IsFinite && !item.WorldBounds.Intersects(_visibleWorldBounds);
    private Rect2D GetVisibleLocalBounds(Matrix3x2 matrix)
    {
        if (!Matrix3x2.Invert(matrix, out var inverse))
        {
            StateGuard.Throw("Cannot render a shape with a singular transform.");
        }

        return new Rect2D(Vector2.Zero, _camera.ViewportSize).TransformedBy(inverse);
    }
    private void RequireFrame()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        StateGuard.ThrowIf(!_frameActive, "BeginFrame must be called before drawing.");
    }

    private void ReleaseTexture(Texture2D texture)
    {
        if (!_textures.Remove(texture, out var resident))
        {
            return;
        }

        if (_batchTexture == resident.Texture)
        {
            Flush();
            _batchTexture = null;
        }
        texture.Disposed -= ReleaseTexture;
        _textureBytes -= resident.Bytes;
        _recentTextures.Remove(resident.Node);
        resident.Texture.Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        DisposeCharacters();
        _meshes.Dispose();
        _vertexCount = 0;
        _batchTexture = null;
        while (_recentTextures.First is { } node)
        {
            ReleaseTexture(node.Value);
        }

        foreach (var sampler in _samplers.Values)
        {
            sampler.Dispose();
        }

        _font.Dispose();
        _effect.Dispose();
        _disposed = true;
    }

    private readonly record struct SamplerKey(TextureAddressMode X, TextureAddressMode Y, TextureFilter Filter);
    private sealed record Residency(GpuTexture Texture, LinkedListNode<Texture2D> Node, long Bytes);
}
