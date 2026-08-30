using App2d.Core;
using App2d.Core.Geometry;
using App2d.Core.Mathematics;
using App2d.Rendering.Textures;
using SkiaSharp;
using System.Numerics;

namespace App2d.Rendering;

public sealed class Renderer2D(Camera2D camera) : IDisposable
{
    private readonly SKFont _hudFont = new(SKTypeface.Default, 28f);
    private readonly SKPaint _hudTextPaint = new()
    {
        Color = SKColors.White,
        IsAntialias = true
    };
    private readonly SKPaint _hudBackgroundPaint = new()
    {
        Color = new SKColor(20, 28, 43, 220),
        IsAntialias = true
    };
    private readonly SKPaint _worldPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };
    private SKCanvas? _canvas;
    private FrameTime _time;
    private Bounds2D _visibleWorldBounds;

    public void BeginFrame(SKCanvas canvas, int width, int height, FrameTime time)
    {
        _canvas = canvas;
        _time = time;
        camera.SetViewport(width, height);
        _visibleWorldBounds = camera.VisibleWorldBounds;
    }

    public void Clear(SKColor color) => Canvas.Clear(color);

    public void DrawGrid(float spacing = 50f, int majorLineEvery = 5)
    {
        var visible = camera.VisibleWorldBounds;
        var (minX, maxX) = (visible.Left, visible.Right);
        var (minY, maxY) = (visible.Bottom, visible.Top);
        var firstX = (int)MathF.Floor(minX / spacing);
        var lastX = (int)MathF.Ceiling(maxX / spacing);
        var firstY = (int)MathF.Floor(minY / spacing);
        var lastY = (int)MathF.Ceiling(maxY / spacing);

        using var minorPaint = CreateStrokePaint(new SKColor(255, 255, 255, 18), 1f);
        using var majorPaint = CreateStrokePaint(new SKColor(255, 255, 255, 35), 1f);
        using var axisPaint = CreateStrokePaint(new SKColor(255, 255, 255, 85), 2f);

        for (var x = firstX; x <= lastX; x++)
        {
            var start = camera.WorldToDevice(new Vector2(x * spacing, minY));
            var end = camera.WorldToDevice(new Vector2(x * spacing, maxY));
            var paint = x == 0 ? axisPaint : x % majorLineEvery == 0 ? majorPaint : minorPaint;
            Canvas.DrawLine(start.X, start.Y, end.X, end.Y, paint);
        }

        for (var y = firstY; y <= lastY; y++)
        {
            var start = camera.WorldToDevice(new Vector2(minX, y * spacing));
            var end = camera.WorldToDevice(new Vector2(maxX, y * spacing));
            var paint = y == 0 ? axisPaint : y % majorLineEvery == 0 ? majorPaint : minorPaint;
            Canvas.DrawLine(start.X, start.Y, end.X, end.Y, paint);
        }
    }

    public void Draw(Scene2D scene)
    {
        foreach (var worldObject in scene.GetDrawOrder())
            Draw(worldObject);
    }

    public void Draw(WorldObject2D worldObject)
    {
        if (!worldObject.IsVisible)
            return;
        var worldBounds = worldObject.WorldBounds;
        if (worldBounds.IsFinite && !worldBounds.Intersects(_visibleWorldBounds))
            return;

        // This is the whole spatial pipeline: object -> world -> Skia device pixels.
        var objectToDevice =
            worldObject.Transform.LocalToWorldMatrix * camera.WorldToDeviceMatrix;

        var shaderBounds = worldObject.Shape.LocalBounds.IsFinite
            ? worldObject.Shape.LocalBounds
            : GetVisibleLocalBounds(objectToDevice);

        var shaderContext = new ShaderContext(objectToDevice, shaderBounds, _time);
        using var shaderLease = worldObject.Shader.AcquireShader(shaderContext);
        var paint = _worldPaint;
        paint.Color = worldObject.Shader.BaseColor;
        paint.Shader = shaderLease.Shader;
        paint.Style = SKPaintStyle.Fill;
        paint.StrokeWidth = 0f;
        paint.StrokeCap = SKStrokeCap.Butt;

        var skiaMatrix = ToSkiaMatrix(objectToDevice);
        Canvas.Save();
        Canvas.Concat(in skiaMatrix);
        try
        {
            DrawFilledShape(worldObject.Shape, paint, objectToDevice);
        }
        finally
        {
            Canvas.Restore();
            paint.Shader = null;
        }
    }

    private void DrawFilledShape(IShape2D shape, SKPaint paint, Matrix3x2 objectToDevice)
    {
        switch (shape)
        {
            case ConvexPolygon2D polygon:
                DrawConvexPolygon(polygon, paint);
                break;
            case Circle2D circle:
                Canvas.DrawCircle(circle.Center.X, circle.Center.Y, circle.Radius, paint);
                break;
            case Capsule2D capsule:
                DrawCapsule(capsule, paint);
                break;
            case Rectangle2D rectangle:
                Canvas.DrawRect(new SKRect(rectangle.Min.X, rectangle.Min.Y, rectangle.Max.X, rectangle.Max.Y), paint);
                break;
            case HalfSpace2D halfSpace:
                DrawHalfSpace(halfSpace, paint, objectToDevice);
                break;
            case CompositeShape2D composite:
                foreach (var part in composite.Parts)
                    DrawFilledShape(part, paint, objectToDevice);
                break;
            default:
                throw new NotSupportedException($"No renderer is registered for {shape.GetType().Name}.");
        }
    }

    public void DrawScreenLabel(string text, Vector2 topLeft)
    {
        ArgGuard.ThrowIfNullOrWhiteSpace(text);

        const float horizontalPadding = 14f;
        const float verticalPadding = 9f;
        var metrics = _hudFont.Metrics;
        var textWidth = _hudFont.MeasureText(text, _hudTextPaint);
        var textHeight = metrics.Descent - metrics.Ascent;
        var bounds = new SKRect(topLeft.X, topLeft.Y, topLeft.X + textWidth + horizontalPadding * 2f, topLeft.Y + textHeight + verticalPadding * 2f);

        Canvas.DrawRoundRect(bounds, 9f, 9f, _hudBackgroundPaint);
        Canvas.DrawText(text, topLeft.X + horizontalPadding, topLeft.Y + verticalPadding - metrics.Ascent, SKTextAlign.Left, _hudFont, _hudTextPaint);
    }

    public void DrawScreenRoundedRectangle(SKRect bounds, float radius, SKColor color, float strokeWidth = 0f)
    {
        ArgGuard.ThrowIfNegativeOrNotFinite(radius);
        ArgGuard.ThrowIfNegativeOrNotFinite(strokeWidth);
        using var paint = new SKPaint
        {
            Color = color,
            IsAntialias = true,
            StrokeWidth = strokeWidth,
            Style = strokeWidth > 0f ? SKPaintStyle.Stroke : SKPaintStyle.Fill
        };
        Canvas.DrawRoundRect(bounds, radius, radius, paint);
    }

    public void DrawScreenText(string text, Vector2 baseline, SKColor color)
    {
        ArgGuard.ThrowIfNullOrWhiteSpace(text);
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        Canvas.DrawText(text, baseline.X, baseline.Y, SKTextAlign.Left, _hudFont, paint);
    }

    public void DrawScreenTexture(Texture2D texture, SKRect bounds)
    {
        ArgGuard.ThrowIfNull(texture);
        Canvas.DrawBitmap(
            texture.Bitmap,
            bounds,
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None),
            _hudTextPaint);
    }

    public void DrawWorldCircle(Vector2 center, float radius, SKColor color, float strokeWidth = 2f)
    {
        ArgGuard.ThrowIfNegativeOrNotFinite(radius);

        var deviceCenter = camera.WorldToDevice(center);
        using var paint = CreateStrokePaint(color, strokeWidth);
        Canvas.DrawCircle(deviceCenter.X, deviceCenter.Y, radius * camera.Zoom, paint);
    }

    public void DrawWorldPolyline(ReadOnlySpan<Vector2> points, SKColor color, float strokeWidth = 2f)
    {
        if (points.Length < 2)
            return;

        using var paint = CreateStrokePaint(color, strokeWidth);
        using var pathBuilder = new SKPathBuilder();
        var first = camera.WorldToDevice(points[0]);
        pathBuilder.MoveTo(first.X, first.Y);
        foreach (var point in points[1..])
        {
            var devicePoint = camera.WorldToDevice(point);
            pathBuilder.LineTo(devicePoint.X, devicePoint.Y);
        }

        using var path = pathBuilder.Detach();
        Canvas.DrawPath(path, paint);
    }

    public void DrawShapeOutline(SpatialObject2D worldObject, SKColor color, float screenStrokeWidth = 2f)
    {
        ArgGuard.ThrowIfNull(worldObject);
        ArgGuard.ThrowIfNotPositive(screenStrokeWidth);

        var worldBounds = worldObject.WorldBounds;
        if (worldBounds.IsFinite && !worldBounds.Intersects(_visibleWorldBounds))
            return;

        var objectToDevice = worldObject.Transform.LocalToWorldMatrix * camera.WorldToDeviceMatrix;
        var localStrokeWidth = screenStrokeWidth / camera.Zoom;
        using var paint = CreateStrokePaint(color, localStrokeWidth);
        var skiaMatrix = ToSkiaMatrix(objectToDevice);
        Canvas.Save();
        Canvas.Concat(in skiaMatrix);
        try
        {
            DrawShapeOutlineCore(worldObject.Shape, paint, objectToDevice);
        }
        finally
        {
            Canvas.Restore();
        }
    }

    private void DrawShapeOutlineCore(IShape2D shape, SKPaint paint, Matrix3x2 objectToDevice)
    {
        switch (shape)
        {
            case ConvexPolygon2D polygon:
                DrawConvexPolygon(polygon, paint);
                break;
            case Circle2D circle:
                Canvas.DrawCircle(circle.Center.X, circle.Center.Y, circle.Radius, paint);
                break;
            case Capsule2D capsule:
                DrawCapsuleOutline(capsule, paint);
                break;
            case Rectangle2D rectangle:
                Canvas.DrawRect(new SKRect(rectangle.Min.X, rectangle.Min.Y, rectangle.Max.X, rectangle.Max.Y), paint);
                break;
            case HalfSpace2D halfSpace:
                DrawHalfSpaceBoundary(halfSpace, paint, objectToDevice);
                break;
            case CompositeShape2D composite:
                foreach (var part in composite.Parts)
                    DrawShapeOutlineCore(part, paint, objectToDevice);
                break;
        }
    }

    public void DrawShapeOverlay(SpatialObject2D worldObject, SKColor fillColor, SKColor outlineColor, float screenStrokeWidth = 2f)
    {
        ArgGuard.ThrowIfNull(worldObject);
        ArgGuard.ThrowIfNotPositive(screenStrokeWidth);

        var worldBounds = worldObject.WorldBounds;
        if (worldBounds.IsFinite && !worldBounds.Intersects(_visibleWorldBounds))
            return;

        var objectToDevice = worldObject.Transform.LocalToWorldMatrix * camera.WorldToDeviceMatrix;
        using var fillPaint = new SKPaint
        {
            Color = fillColor,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };
        using var outlinePaint = CreateStrokePaint(outlineColor, screenStrokeWidth / camera.Zoom);
        var skiaMatrix = ToSkiaMatrix(objectToDevice);
        Canvas.Save();
        Canvas.Concat(in skiaMatrix);
        try
        {
            DrawShape(worldObject.Shape, fillPaint, objectToDevice, drawHalfSpaceFill: true);
            DrawShape(worldObject.Shape, outlinePaint, objectToDevice, drawHalfSpaceFill: false);
        }
        finally
        {
            Canvas.Restore();
        }
    }

    public void Dispose()
    {
        _hudFont.Dispose();
        _hudTextPaint.Dispose();
        _hudBackgroundPaint.Dispose();
        _worldPaint.Dispose();
    }

    private SKCanvas Canvas => StateGuard.RequireNotNull(
        _canvas,
        "BeginFrame must be called before drawing.");

    private void DrawConvexPolygon(ConvexPolygon2D polygon, SKPaint paint)
    {
        var vertices = polygon.Vertices;
        using var pathBuilder = new SKPathBuilder();
        pathBuilder.MoveTo(vertices[0].X, vertices[0].Y);

        foreach (var vertex in vertices[1..])
            pathBuilder.LineTo(vertex.X, vertex.Y);

        pathBuilder.Close();
        using var path = pathBuilder.Detach();
        Canvas.DrawPath(path, paint);
    }

    private void DrawShape(IShape2D shape, SKPaint paint, Matrix3x2 objectToDevice, bool drawHalfSpaceFill)
    {
        switch (shape)
        {
            case ConvexPolygon2D polygon:
                DrawConvexPolygon(polygon, paint);
                break;
            case Circle2D circle:
                Canvas.DrawCircle(circle.Center.X, circle.Center.Y, circle.Radius, paint);
                break;
            case Capsule2D capsule when paint.Style == SKPaintStyle.Fill:
                DrawCapsule(capsule, paint);
                break;
            case Capsule2D capsule:
                DrawCapsuleOutline(capsule, paint);
                break;
            case Rectangle2D rectangle:
                Canvas.DrawRect(new SKRect(rectangle.Min.X, rectangle.Min.Y, rectangle.Max.X, rectangle.Max.Y), paint);
                break;
            case HalfSpace2D halfSpace when drawHalfSpaceFill:
                DrawHalfSpace(halfSpace, paint, objectToDevice);
                break;
            case HalfSpace2D halfSpace:
                DrawHalfSpaceBoundary(halfSpace, paint, objectToDevice);
                break;
            case CompositeShape2D composite:
                foreach (var part in composite.Parts)
                    DrawShape(part, paint, objectToDevice, drawHalfSpaceFill);
                break;
        }
    }

    private void DrawCapsule(Capsule2D capsule, SKPaint paint)
    {
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = capsule.Radius * 2f;
        paint.StrokeCap = SKStrokeCap.Round;
        Canvas.DrawLine(capsule.Start.X, capsule.Start.Y, capsule.End.X, capsule.End.Y, paint);
    }

    private void DrawCapsuleOutline(Capsule2D capsule, SKPaint paint)
    {
        var axis = capsule.End - capsule.Start;
        if (axis.LengthSquared() <= float.Epsilon)
        {
            Canvas.DrawCircle(capsule.Start.X, capsule.Start.Y, capsule.Radius, paint);
            return;
        }

        var normal = Vector2.Normalize(axis.PerpCcw()) * capsule.Radius;
        Canvas.DrawLine(capsule.Start.X + normal.X, capsule.Start.Y + normal.Y, capsule.End.X + normal.X, capsule.End.Y + normal.Y, paint);
        Canvas.DrawLine(capsule.Start.X - normal.X, capsule.Start.Y - normal.Y, capsule.End.X - normal.X, capsule.End.Y - normal.Y, paint);
        Canvas.DrawCircle(capsule.Start.X, capsule.Start.Y, capsule.Radius, paint);
        Canvas.DrawCircle(capsule.End.X, capsule.End.Y, capsule.Radius, paint);
    }

    private void DrawHalfSpaceBoundary(HalfSpace2D halfSpace, SKPaint paint, Matrix3x2 objectToDevice)
    {
        var visibleBounds = GetVisibleLocalBounds(objectToDevice);
        var tangent = new Vector2(-halfSpace.Normal.Y, halfSpace.Normal.X);
        var extent = visibleBounds.Size.Length();
        var center = halfSpace.Normal * halfSpace.Offset;
        var start = center - tangent * extent;
        var end = center + tangent * extent;
        Canvas.DrawLine(start.X, start.Y, end.X, end.Y, paint);
    }

    private void DrawHalfSpace(HalfSpace2D halfSpace, SKPaint paint, Matrix3x2 objectToDevice)
    {
        var visibleBounds = GetVisibleLocalBounds(objectToDevice);
        Span<Vector2> corners =
        [
            visibleBounds.Min,
            new Vector2(visibleBounds.Max.X, visibleBounds.Min.Y),
            visibleBounds.Max,
            new Vector2(visibleBounds.Min.X, visibleBounds.Max.Y)
        ];

        var tangent = new Vector2(-halfSpace.Normal.Y, halfSpace.Normal.X);
        var minTangent = float.PositiveInfinity;
        var maxTangent = float.NegativeInfinity;
        var minNormal = float.PositiveInfinity;
        foreach (var corner in corners)
        {
            var tangentProjection = Vector2.Dot(corner, tangent);
            minTangent = Math.Min(minTangent, tangentProjection);
            maxTangent = Math.Max(maxTangent, tangentProjection);
            minNormal = Math.Min(minNormal, Vector2.Dot(corner, halfSpace.Normal));
        }

        var margin = Math.Max(visibleBounds.Size.Length() * 0.1f, 10f);
        minTangent -= margin;
        maxTangent += margin;
        var deepProjection = Math.Min(minNormal, halfSpace.Offset) - margin;
        var boundaryCenter = halfSpace.Normal * halfSpace.Offset;

        Span<Vector2> vertices =
        [
            boundaryCenter + tangent * minTangent,
            boundaryCenter + tangent * maxTangent,
            halfSpace.Normal * deepProjection + tangent * maxTangent,
            halfSpace.Normal * deepProjection + tangent * minTangent
        ];

        using var pathBuilder = new SKPathBuilder();
        pathBuilder.MoveTo(vertices[0].X, vertices[0].Y);
        foreach (var vertex in vertices[1..])
            pathBuilder.LineTo(vertex.X, vertex.Y);
        pathBuilder.Close();
        using var path = pathBuilder.Detach();
        Canvas.DrawPath(path, paint);
    }

    private Bounds2D GetVisibleLocalBounds(Matrix3x2 objectToDevice)
    {
        if (!Matrix3x2.Invert(objectToDevice, out var deviceToObject))
            StateGuard.Throw("Cannot render a shape with a singular transform.");

        return new Bounds2D(Vector2.Zero, camera.ViewportSize).TransformedBy(deviceToObject);
    }

    private static SKMatrix ToSkiaMatrix(Matrix3x2 matrix) => new(
        matrix.M11, matrix.M21, matrix.M31,
        matrix.M12, matrix.M22, matrix.M32,
        0f, 0f, 1f);

    private static SKPaint CreateStrokePaint(SKColor color, float width) => new()
    {
        Color = color,
        StrokeWidth = width,
        IsAntialias = true,
        Style = SKPaintStyle.Stroke
    };
}
