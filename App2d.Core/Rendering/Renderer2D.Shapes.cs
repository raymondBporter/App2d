using App2d.Core.Geometry.Meshes;
using App2d.Core.Geometry.Operations.Bounds;
using App2d.Core.Geometry.Operations.Intersection;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Rendering;
using App2d.Core.Spatial;
using App2d.Core.Validation;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Rendering;

public sealed partial class Renderer2D
{
    /// <summary>Draws a shape in world coordinates, with a fill, an outline, or both.</summary>
    public void DrawShape(IShape2D shape, XnaColor? fillColor = null, XnaColor? outlineColor = null,
        float screenStrokeWidth = 2f) =>
        DrawShape(shape, Matrix3x2.Identity, fillColor, outlineColor, screenStrokeWidth);

    /// <summary>Draws a local shape through a local-to-world transform.</summary>
    public void DrawShape(IShape2D shape, Matrix3x2 localToWorld, XnaColor? fillColor = null, XnaColor? outlineColor = null, float screenStrokeWidth = 2f)
    {
        RequireFrame();
        ArgGuard.ThrowIfNull(shape);
        ValidateShapeStyle(fillColor, outlineColor, screenStrokeWidth);
        ArgGuard.Finite(localToWorld);
        var bounds = ShapeBounds2D.Calculate(shape);
        var matrix = localToWorld * _camera.WorldToDeviceMatrix;
        DrawShapeCore(shape, matrix, bounds, fillColor, outlineColor, screenStrokeWidth);
    }

    /// <summary>Draws the shape and transform of an existing spatial object.</summary>
    public void DrawShape(SpatialObject2D item, XnaColor? fillColor = null, XnaColor? outlineColor = null,
        float screenStrokeWidth = 2f)
    {
        RequireFrame();
        ArgGuard.ThrowIfNull(item);
        ValidateShapeStyle(fillColor, outlineColor, screenStrokeWidth);
        if (IsCulled(item))
        {
            return;
        }

        var matrix = item.Transform.Matrix * _camera.WorldToDeviceMatrix;
        DrawShapeCore(item.Shape, matrix, item.LocalBounds, fillColor, outlineColor, screenStrokeWidth);
    }

    private void DrawShapeCore(IShape2D shape, Matrix3x2 matrix, Rect2D bounds,
        XnaColor? fillColor, XnaColor? outlineColor, float width)
    {
        SelectBatch(null, null);
        if (fillColor is { } fill)
        {
            FillShape(shape, matrix, bounds.IsFinite ? bounds : GetVisibleLocalBounds(matrix), new SolidColorShader(fill));
        }

        if (outlineColor is { } outline)
        {
            OutlineShape(shape, matrix, bounds, outline, width);
        }
    }

    /// <summary>Draws an indexed triangle mesh. The outline follows edges used by exactly one triangle.</summary>
    public void DrawTriangleMesh(TriangleMesh2D mesh, XnaColor? fillColor = null, XnaColor? outlineColor = null,
        float screenStrokeWidth = 2f) =>
        DrawTriangleMesh(mesh, Matrix3x2.Identity, fillColor, outlineColor, screenStrokeWidth);

    /// <summary>Draws an indexed mesh through a local-to-world transform.</summary>
    public void DrawTriangleMesh(TriangleMesh2D mesh, Matrix3x2 localToWorld, XnaColor? fillColor = null,
        XnaColor? outlineColor = null, float screenStrokeWidth = 2f)
    {
        RequireFrame();
        ArgGuard.ThrowIfNull(mesh);
        ValidateShapeStyle(fillColor, outlineColor, screenStrokeWidth);
        ArgGuard.Finite(localToWorld);
        SelectBatch(null, null);
        var matrix = localToWorld * _camera.WorldToDeviceMatrix;
        if (fillColor is { } fill)
        {
            DrawMeshCore(mesh, matrix, mesh.Bounds, new SolidColorShader(fill));
        }
        if (outlineColor is not { } outline)
        {
            return;
        }

        foreach (var edge in mesh.BoundarySegments)
        {
            Line(Vector2.Transform(edge.Start, matrix), Vector2.Transform(edge.End, matrix),
                outline, screenStrokeWidth);
        }
    }

    /// <summary>Draws a finite world-space segment with pixel-sized width and optional caps.</summary>
    public void DrawWorldSegment(Vector2 start, Vector2 end, XnaColor color,
        float screenStrokeWidth = 2f, LineCap2D cap = LineCap2D.Butt)
    {
        RequireFrame();
        ArgGuard.Finite(start);
        ArgGuard.Finite(end);
        ArgGuard.Positive(screenStrokeWidth);
        ValidateLineCap(cap);
        SelectBatch(null, null);
        Line(_camera.WorldToDevice(start), _camera.WorldToDevice(end), color, screenStrokeWidth, cap, cap);
    }

    /// <summary>Draws a segment with width specified in world units, scaling with camera zoom.</summary>
    public void DrawWorldThickSegment(Vector2 start, Vector2 end, float worldWidth, XnaColor color,
        LineCap2D cap = LineCap2D.Butt) =>
        DrawWorldThickSegment(start, end, worldWidth, worldWidth, color, cap, cap);

    /// <summary>Draws a tapered segment with start and end widths specified in world units, scaling with camera zoom.</summary>
    public void DrawWorldThickSegment(Vector2 start, Vector2 end, float startWorldWidth, float endWorldWidth,
        XnaColor color, LineCap2D startCap = LineCap2D.Butt, LineCap2D endCap = LineCap2D.Butt)
    {
        RequireFrame();
        ArgGuard.Finite(start);
        ArgGuard.Finite(end);
        ArgGuard.Positive(startWorldWidth);
        ArgGuard.Positive(endWorldWidth);
        ValidateLineCap(startCap);
        ValidateLineCap(endCap);
        SelectBatch(null, null);
        var ppu = _camera.PixelsPerWorldUnit;
        Line(_camera.WorldToDevice(start), _camera.WorldToDevice(end), color,
            Math.Max(1f, startWorldWidth * ppu), Math.Max(1f, endWorldWidth * ppu), startCap, endCap);
    }

    /// <summary>Draws the visible part of an infinite world-space line.</summary>
    public void DrawWorldLine(Line2D line, XnaColor color, float screenStrokeWidth = 2f)
    {
        RequireFrame();
        if (!line.IsValid)
        {
            throw new ArgumentException("The line needs a valid direction.", nameof(line));
        }

        ArgGuard.Positive(screenStrokeWidth);
        var deviceLine = new Line2D(_camera.WorldToDevice(line.Origin),
            Vector2.TransformNormal(line.Direction, _camera.WorldToDeviceMatrix));
        if (!deviceLine.TryClipToRectangle(PaddedDeviceBounds(screenStrokeWidth), out var start, out var end))
        {
            return;
        }

        SelectBatch(null, null);
        Line(start, end, color, screenStrokeWidth);
    }

    /// <summary>Draws the visible part of a world-space ray. Its origin can have a cap.</summary>
    public void DrawWorldRay(Ray2D ray, XnaColor color, float screenStrokeWidth = 2f,
        LineCap2D originCap = LineCap2D.Butt)
    {
        RequireFrame();
        if (!ray.IsValid)
        {
            throw new ArgumentException("The ray needs a valid direction.", nameof(ray));
        }

        ArgGuard.Positive(screenStrokeWidth);
        ValidateLineCap(originCap);
        var deviceOrigin = _camera.WorldToDevice(ray.Origin);
        var bounds = PaddedDeviceBounds(screenStrokeWidth);
        var deviceRay = new Ray2D(deviceOrigin,
            Vector2.TransformNormal(ray.Direction, _camera.WorldToDeviceMatrix));
        if (!deviceRay.TryClipToRectangle(bounds, out var start, out var end))
        {
            return;
        }

        SelectBatch(null, null);
        Line(start, end, color, screenStrokeWidth, bounds.Contains(deviceOrigin) ? originCap : LineCap2D.Butt);
    }

    private Rect2D PaddedDeviceBounds(float width)
    {
        var padding = width * 0.5f + 1f;
        return new Rect2D(new(-padding), _camera.ViewportSize + new Vector2(padding));
    }

    private static void ValidateShapeStyle(XnaColor? fillColor, XnaColor? outlineColor, float width)
    {
        if (fillColor is null && outlineColor is null)
        {
            throw new ArgumentException("Provide a fill color, an outline color, or both.", nameof(fillColor));
        }

        if (outlineColor is not null)
        {
            ArgGuard.Positive(width);
        }
    }

    private static void ValidateLineCap(LineCap2D cap)
    {
        if (cap is < LineCap2D.Butt or > LineCap2D.Round)
        {
            throw new ArgumentOutOfRangeException(nameof(cap));
        }
    }

}
