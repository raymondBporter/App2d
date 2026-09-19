using App2d.Core;
using App2d.Core.Geometry;
using App2d.Rendering.Textures;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;
using XnaMatrix = Microsoft.Xna.Framework.Matrix;
using XnaVector2 = Microsoft.Xna.Framework.Vector2;
using XnaVector3 = Microsoft.Xna.Framework.Vector3;
using Texture2D = App2d.Rendering.Textures.Texture2D;
using GpuTexture = Microsoft.Xna.Framework.Graphics.Texture2D;

namespace App2d.Rendering;

/// <summary>Ordered GPU triangle batches using MonoGame's XNA graphics API.</summary>
public sealed class Renderer2D : IDisposable
{
    private readonly Camera2D _camera;
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
    private Bounds2D _visibleWorldBounds;

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
        ArgGuard.ThrowIfNotPositive(width);
        ArgGuard.ThrowIfNotPositive(height);
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
            ReleaseTexture(oldest.Value);
        _frameActive = false;
    }

    public void Clear(XnaColor color)
    {
        RequireFrame();
        Flush();
        _device.Clear(color);
    }

    public void Draw(Scene2D scene)
    {
        foreach (var item in scene.GetDrawOrder()) Draw(item);
    }

    public void Draw(WorldObject2D worldObject)
    {
        RequireFrame();
        if (!worldObject.IsVisible || IsCulled(worldObject)) return;
        var matrix = worldObject.Transform.LocalToWorldMatrix * _camera.WorldToDeviceMatrix;
        var bounds = worldObject.Shape.LocalBounds.IsFinite ? worldObject.Shape.LocalBounds : GetVisibleLocalBounds(matrix);
        if (worldObject.Shader is SpriteShader2D)
            StateGuard.ThrowIf(!worldObject.Shape.LocalBounds.IsFinite, "Sprites require finite local bounds.");
        SelectMaterial(worldObject.Shader);
        FillShape(worldObject.Shape, matrix, bounds, worldObject.Shader);
    }

    private void SelectMaterial(IShader2D shader)
    {
        switch (shader)
        {
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
                    pixels[i] = XnaColor.FromNonPremultiplied(pixels[i].R, pixels[i].G, pixels[i].B, pixels[i].A);
                gpu.SetData(pixels);
            }
            catch { gpu.Dispose(); throw; }
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
        if (_batchTexture == texture && _batchSampler == sampler) return;
        Flush();
        _batchTexture = texture;
        _batchSampler = sampler;
    }

    private void Flush()
    {
        if (_vertexCount == 0) return;
        _device.BlendState = BlendState.AlphaBlend;
        _device.DepthStencilState = DepthStencilState.None;
        _device.RasterizerState = RasterizerState.CullNone;
        _effect.TextureEnabled = _batchTexture is not null;
        _effect.Texture = _batchTexture;
        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            if (_batchSampler is not null) _device.SamplerStates[0] = _batchSampler;
            _device.DrawUserPrimitives(PrimitiveType.TriangleList, _vertices, 0, _vertexCount / 3);
        }
        _vertexCount = 0;
    }

    private void Triangle(VertexPositionColorTexture a, VertexPositionColorTexture b, VertexPositionColorTexture c)
    {
        if (_vertexCount + 3 > _vertices.Length) Flush();
        _vertices[_vertexCount++] = a;
        _vertices[_vertexCount++] = b;
        _vertices[_vertexCount++] = c;
    }

    private static VertexPositionColorTexture Vertex(Vector2 point, XnaColor color, Vector2 uv = default) =>
        new(new XnaVector3(point.X, point.Y, 0),
            XnaColor.FromNonPremultiplied(color.R, color.G, color.B, color.A), new XnaVector2(uv.X, uv.Y));

    private static VertexPositionColorTexture MaterialVertex(Vector2 local, Matrix3x2 matrix, Bounds2D bounds, IShader2D shader)
    {
        var uv = Vector2.Zero;
        switch (shader)
        {
            case SpriteShader2D sprite:
                uv = (local - bounds.Min) / bounds.Size;
                if (sprite.FlipX) uv.X = 1f - uv.X;
                if (!sprite.FlipY) uv.Y = 1f - uv.Y;
                break;
            case TextureShader2D tile:
                // Preserve local-origin tiling; unlike sprites, tiled images use positive local Y.
                uv = local / tile.TileSize;
                break;
        }
        return Vertex(Vector2.Transform(local, matrix), shader.GetVertexColor(local, bounds), uv);
    }

    private void FillPolygon(ReadOnlySpan<Vector2> points, Matrix3x2 matrix, Bounds2D bounds, IShader2D shader)
    {
        var first = MaterialVertex(points[0], matrix, bounds, shader);
        for (var index = 1; index < points.Length - 1; index++)
            Triangle(first, MaterialVertex(points[index], matrix, bounds, shader), MaterialVertex(points[index + 1], matrix, bounds, shader));
    }

    private void FillShape(IShape2D shape, Matrix3x2 matrix, Bounds2D bounds, IShader2D shader)
    {
        if (shape is CompositeShape2D composite)
        {
            foreach (var part in composite.Parts) FillShape(part, matrix, bounds, shader);
            return;
        }
        if (shape is ConvexPolygon2D polygon) { FillPolygon(polygon.Vertices, matrix, bounds, shader); return; }
        Span<Vector2> points = stackalloc Vector2[260];
        var count = GetShapePoints(shape, matrix, points);
        if (count >= 3) FillPolygon(points[..count], matrix, bounds, shader);
    }

    private int GetShapePoints(IShape2D shape, Matrix3x2 matrix, Span<Vector2> points)
    {
        switch (shape)
        {
            case Rectangle2D rectangle:
                points[0] = rectangle.Min;
                points[1] = new(rectangle.Max.X, rectangle.Min.Y);
                points[2] = rectangle.Max;
                points[3] = new(rectangle.Min.X, rectangle.Max.Y);
                return 4;
            case Circle2D circle:
                var segments = CurveSegments(circle.Radius, matrix);
                for (var i = 0; i < segments; i++)
                {
                    var angle = i * MathF.Tau / segments;
                    points[i] = circle.Center + circle.Radius * new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                }
                return segments;
            case Capsule2D capsule:
                var halfSegments = CurveSegments(capsule.Radius, matrix) / 2;
                var axis = capsule.End - capsule.Start;
                var direction = MathF.Atan2(axis.Y, axis.X);
                for (var i = 0; i <= halfSegments; i++)
                {
                    var endAngle = direction - MathF.PI / 2 + i * MathF.PI / halfSegments;
                    var startAngle = direction + MathF.PI / 2 + i * MathF.PI / halfSegments;
                    points[i] = capsule.End + capsule.Radius * new Vector2(MathF.Cos(endAngle), MathF.Sin(endAngle));
                    points[halfSegments + 1 + i] = capsule.Start + capsule.Radius * new Vector2(MathF.Cos(startAngle), MathF.Sin(startAngle));
                }
                return 2 * (halfSegments + 1);
            case HalfSpace2D halfSpace:
                var visible = GetVisibleLocalBounds(matrix);
                Span<Vector2> corners = [visible.Min, new(visible.Max.X, visible.Min.Y), visible.Max, new(visible.Min.X, visible.Max.Y)];
                var tangent = new Vector2(-halfSpace.Normal.Y, halfSpace.Normal.X);
                var minT = float.PositiveInfinity;
                var maxT = float.NegativeInfinity;
                var minN = float.PositiveInfinity;
                foreach (var corner in corners)
                {
                    var t = Vector2.Dot(corner, tangent);
                    minT = Math.Min(minT, t);
                    maxT = Math.Max(maxT, t);
                    minN = Math.Min(minN, Vector2.Dot(corner, halfSpace.Normal));
                }
                var margin = Math.Max(visible.Size.Length() * 0.1f, 10f);
                minT -= margin;
                maxT += margin;
                var deep = Math.Min(minN, halfSpace.Offset) - margin;
                points[0] = halfSpace.Normal * halfSpace.Offset + tangent * minT;
                points[1] = halfSpace.Normal * halfSpace.Offset + tangent * maxT;
                points[2] = halfSpace.Normal * deep + tangent * maxT;
                points[3] = halfSpace.Normal * deep + tangent * minT;
                return 4;
            default:
                throw new NotSupportedException($"No renderer is registered for {shape.GetType().Name}.");
        }
    }

    private static int CurveSegments(float radius, Matrix3x2 matrix)
    {
        var scale = Math.Max(new Vector2(matrix.M11, matrix.M12).Length(), new Vector2(matrix.M21, matrix.M22).Length());
        return Math.Clamp((int)MathF.Ceiling(MathF.PI * MathF.Sqrt(Math.Max(1f, radius * scale) * 2f)), 16, 128);
    }

    public void DrawScreenRoundedRectangle(ScreenRectangle2D bounds, float radius, XnaColor color, float strokeWidth = 0f)
    {
        RequireFrame();
        ArgGuard.ThrowIfNegativeOrNotFinite(radius);
        ArgGuard.ThrowIfNegativeOrNotFinite(strokeWidth);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        SelectBatch(null, null);
        radius = Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2);
        Span<Vector2> points = stackalloc Vector2[36];
        var count = 0;
        for (var corner = 0; corner < 4; corner++)
        {
            var center = new Vector2(corner is 0 or 3 ? bounds.Right - radius : bounds.Left + radius,
                corner < 2 ? bounds.Bottom - radius : bounds.Top + radius);
            for (var i = 0; i <= 8; i++)
            {
                var angle = (corner + i / 8f) * MathF.PI / 2;
                points[count++] = center + radius * new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            }
        }
        if (strokeWidth > 0) StrokePolygon(points[..count], Matrix3x2.Identity, color, strokeWidth);
        else
        {
            var center = Vertex(new Vector2(bounds.MidX, bounds.MidY), color);
            for (var i = 0; i < count; i++)
                Triangle(center, Vertex(points[i], color), Vertex(points[(i + 1) % count], color));
        }
    }

    public void DrawScreenLabel(string text, Vector2 topLeft)
    {
        ArgGuard.ThrowIfNullOrWhiteSpace(text);
        DrawScreenRoundedRectangle(new(topLeft.X, topLeft.Y, topLeft.X + _font.Measure(text) + 28f, topLeft.Y + _font.LineHeight + 18f),
            9f, new XnaColor(20, 28, 43, 220));
        DrawScreenText(text, topLeft + new Vector2(14, 9 + _font.Ascent), XnaColor.White);
    }

    public void DrawScreenText(string text, Vector2 baseline, XnaColor color)
    {
        RequireFrame();
        ArgGuard.ThrowIfNullOrWhiteSpace(text);
        SelectTexture(_font.Texture, new(TextureAddressMode.Clamp, TextureAddressMode.Clamp, TextureFilter.Linear));
        var cursor = baseline - new Vector2(4, _font.Ascent + 4);
        foreach (var character in text)
        {
            if (character == '\n') { cursor.X = baseline.X - 4; cursor.Y += _font.LineHeight; continue; }
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
        TexturedQuad(bounds, new(0, 0, texture.Width, texture.Height), texture.Width, texture.Height, XnaColor.White);
    }

    /// <summary>Draws a transient convex world-space polygon without creating a scene object.</summary>
    public void DrawWorldConvexPolygon(ReadOnlySpan<Vector2> points, XnaColor color)
    {
        RequireFrame();
        ArgGuard.ThrowIfTooShort(points, 3);
        foreach (var point in points)
            ArgGuard.ThrowIfNotFinite(point, nameof(points));

        SelectBatch(null, null);
        var matrix = _camera.WorldToDeviceMatrix;
        var first = Vertex(Vector2.Transform(points[0], matrix), color);
        for (var index = 1; index < points.Length - 1; index++)
            Triangle(
                first,
                Vertex(Vector2.Transform(points[index], matrix), color),
                Vertex(Vector2.Transform(points[index + 1], matrix), color));
    }

    private void TexturedQuad(ScreenRectangle2D bounds, ScreenRectangle2D source, int width, int height, XnaColor color)
    {
        var a = Vertex(new(bounds.Left, bounds.Top), color, new(source.Left / width, source.Top / height));
        var b = Vertex(new(bounds.Right, bounds.Top), color, new(source.Right / width, source.Top / height));
        var c = Vertex(new(bounds.Right, bounds.Bottom), color, new(source.Right / width, source.Bottom / height));
        var d = Vertex(new(bounds.Left, bounds.Bottom), color, new(source.Left / width, source.Bottom / height));
        Triangle(a, b, c);
        Triangle(a, c, d);
    }

    public void DrawWorldCircle(Vector2 center, float radius, XnaColor color, float strokeWidth = 2f)
    {
        RequireFrame();
        ArgGuard.ThrowIfNegativeOrNotFinite(radius);
        ArgGuard.ThrowIfNotPositive(strokeWidth);
        SelectBatch(null, null);
        Span<Vector2> points = stackalloc Vector2[128];
        var segments = CurveSegments(radius, _camera.WorldToDeviceMatrix);
        for (var i = 0; i < segments; i++)
        {
            var angle = i * MathF.Tau / segments;
            points[i] = center + radius * new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        }
        StrokePolygon(points[..segments], _camera.WorldToDeviceMatrix, color, strokeWidth);
    }

    public void DrawWorldPolyline(ReadOnlySpan<Vector2> points, XnaColor color, float strokeWidth = 2f)
    {
        RequireFrame();
        ArgGuard.ThrowIfNotPositive(strokeWidth);
        SelectBatch(null, null);
        for (var i = 1; i < points.Length; i++)
            Line(_camera.WorldToDevice(points[i - 1]), _camera.WorldToDevice(points[i]), color, strokeWidth);
    }

    public void DrawGrid(float spacing = 50f, int majorLineEvery = 5)
    {
        RequireFrame();
        ArgGuard.ThrowIfNotPositive(spacing);
        ArgGuard.ThrowIfNotPositive(majorLineEvery);
        SelectBatch(null, null);
        var visible = _camera.VisibleWorldBounds;
        for (var x = (int)MathF.Floor(visible.Left / spacing); x <= (int)MathF.Ceiling(visible.Right / spacing); x++)
            Line(_camera.WorldToDevice(new(x * spacing, visible.Bottom)), _camera.WorldToDevice(new(x * spacing, visible.Top)),
                new XnaColor(255, 255, 255, x == 0 ? 85 : x % majorLineEvery == 0 ? 35 : 18), x == 0 ? 2 : 1);
        for (var y = (int)MathF.Floor(visible.Bottom / spacing); y <= (int)MathF.Ceiling(visible.Top / spacing); y++)
            Line(_camera.WorldToDevice(new(visible.Left, y * spacing)), _camera.WorldToDevice(new(visible.Right, y * spacing)),
                new XnaColor(255, 255, 255, y == 0 ? 85 : y % majorLineEvery == 0 ? 35 : 18), y == 0 ? 2 : 1);
    }

    public void DrawShapeOutline(SpatialObject2D item, XnaColor color, float screenStrokeWidth = 2f)
    {
        RequireFrame();
        ArgGuard.ThrowIfNull(item);
        ArgGuard.ThrowIfNotPositive(screenStrokeWidth);
        if (IsCulled(item)) return;
        SelectBatch(null, null);
        OutlineShape(item.Shape, item.Transform.LocalToWorldMatrix * _camera.WorldToDeviceMatrix, color, screenStrokeWidth);
    }

    public void DrawShapeOverlay(SpatialObject2D item, XnaColor fillColor, XnaColor outlineColor, float screenStrokeWidth = 2f)
    {
        RequireFrame();
        ArgGuard.ThrowIfNull(item);
        ArgGuard.ThrowIfNotPositive(screenStrokeWidth);
        if (IsCulled(item)) return;
        var matrix = item.Transform.LocalToWorldMatrix * _camera.WorldToDeviceMatrix;
        var bounds = item.Shape.LocalBounds.IsFinite ? item.Shape.LocalBounds : GetVisibleLocalBounds(matrix);
        SelectBatch(null, null);
        FillShape(item.Shape, matrix, bounds, new SolidColorShader(fillColor));
        OutlineShape(item.Shape, matrix, outlineColor, screenStrokeWidth);
    }

    private void OutlineShape(IShape2D shape, Matrix3x2 matrix, XnaColor color, float width)
    {
        if (shape is CompositeShape2D composite)
        {
            foreach (var part in composite.Parts) OutlineShape(part, matrix, color, width);
            return;
        }
        if (shape is ConvexPolygon2D polygon) { StrokePolygon(polygon.Vertices, matrix, color, width); return; }
        Span<Vector2> points = stackalloc Vector2[260];
        var count = GetShapePoints(shape, matrix, points);
        if (shape is HalfSpace2D) Line(Vector2.Transform(points[0], matrix), Vector2.Transform(points[1], matrix), color, width);
        else StrokePolygon(points[..count], matrix, color, width);
    }

    private void StrokePolygon(ReadOnlySpan<Vector2> points, Matrix3x2 matrix, XnaColor color, float width)
    {
        for (var i = 0; i < points.Length; i++)
            Line(Vector2.Transform(points[i], matrix), Vector2.Transform(points[(i + 1) % points.Length], matrix), color, width);
    }

    private void Line(Vector2 start, Vector2 end, XnaColor color, float width)
    {
        var axis = end - start;
        if (axis.LengthSquared() <= float.Epsilon) return;
        var normal = Vector2.Normalize(new Vector2(-axis.Y, axis.X)) * width * 0.5f;
        var a = Vertex(start + normal, color);
        var b = Vertex(end + normal, color);
        var c = Vertex(end - normal, color);
        var d = Vertex(start - normal, color);
        Triangle(a, b, c);
        Triangle(a, c, d);
    }

    private bool IsCulled(SpatialObject2D item) => item.WorldBounds.IsFinite && !item.WorldBounds.Intersects(_visibleWorldBounds);
    private Bounds2D GetVisibleLocalBounds(Matrix3x2 matrix)
    {
        if (!Matrix3x2.Invert(matrix, out var inverse)) StateGuard.Throw("Cannot render a shape with a singular transform.");
        return new Bounds2D(Vector2.Zero, _camera.ViewportSize).TransformedBy(inverse);
    }
    private void RequireFrame()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        StateGuard.ThrowIf(!_frameActive, "BeginFrame must be called before drawing.");
    }

    private void ReleaseTexture(Texture2D texture)
    {
        if (!_textures.Remove(texture, out var resident)) return;
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
        if (_disposed) return;
        _vertexCount = 0;
        _batchTexture = null;
        while (_recentTextures.First is { } node) ReleaseTexture(node.Value);
        foreach (var sampler in _samplers.Values) sampler.Dispose();
        _font.Dispose();
        _effect.Dispose();
        _disposed = true;
    }

    private readonly record struct SamplerKey(TextureAddressMode X, TextureAddressMode Y, TextureFilter Filter);
    private sealed record Residency(GpuTexture Texture, LinkedListNode<Texture2D> Node, long Bytes);
}
