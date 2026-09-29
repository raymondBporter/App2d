using App2d.Core;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Functions;
using App2d.Core.Shapes;
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
    public void DrawShape(IShape2D shape, Matrix3x2 localToWorld, XnaColor? fillColor = null,
        XnaColor? outlineColor = null, float screenStrokeWidth = 2f)
    {
        RequireFrame();
        ArgGuard.ThrowIfNull(shape);
        ValidateShapeStyle(fillColor, outlineColor, screenStrokeWidth);
        ValidateTransform(localToWorld);
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
        if (IsCulled(item)) return;
        var matrix = item.Transform.LocalToWorldMatrix * _camera.WorldToDeviceMatrix;
        DrawShapeCore(item.Shape, matrix, item.LocalBounds, fillColor, outlineColor, screenStrokeWidth);
    }

    private void DrawShapeCore(IShape2D shape, Matrix3x2 matrix, Bounds2D bounds,
        XnaColor? fillColor, XnaColor? outlineColor, float width)
    {
        SelectBatch(null, null);
        if (fillColor is { } fill)
            FillShape(shape, matrix, bounds.IsFinite ? bounds : GetVisibleLocalBounds(matrix), new SolidColorShader(fill));
        if (outlineColor is { } outline)
            OutlineShape(shape, matrix, outline, width);
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
        ValidateTransform(localToWorld);
        SelectBatch(null, null);
        var matrix = localToWorld * _camera.WorldToDeviceMatrix;
        var vertices = mesh.Vertices;
        var indices = mesh.Indices;
        if (fillColor is { } fill)
        {
            for (var i = 0; i < indices.Length; i += 3)
                Triangle(Vertex(Vector2.Transform(vertices[indices[i]], matrix), fill),
                    Vertex(Vector2.Transform(vertices[indices[i + 1]], matrix), fill),
                    Vertex(Vector2.Transform(vertices[indices[i + 2]], matrix), fill));
        }
        if (outlineColor is not { } outline) return;

        var edgeCounts = new Dictionary<(int, int), int>();
        for (var i = 0; i < indices.Length; i += 3)
        {
            CountEdge(indices[i], indices[i + 1]);
            CountEdge(indices[i + 1], indices[i + 2]);
            CountEdge(indices[i + 2], indices[i]);
        }
        foreach (var (edge, count) in edgeCounts)
        {
            if (count != 1) continue;
            Line(Vector2.Transform(vertices[edge.Item1], matrix), Vector2.Transform(vertices[edge.Item2], matrix),
                outline, screenStrokeWidth);
        }
        return;

        void CountEdge(int first, int second)
        {
            var edge = first < second ? (first, second) : (second, first);
            edgeCounts.TryGetValue(edge, out var count);
            edgeCounts[edge] = count + 1;
        }
    }

    /// <summary>Draws a finite world-space segment with pixel-sized width and optional caps.</summary>
    public void DrawWorldSegment(Vector2 start, Vector2 end, XnaColor color,
        float screenStrokeWidth = 2f, LineCap2D cap = LineCap2D.Butt)
    {
        RequireFrame();
        ArgGuard.ThrowIfNotFinite(start);
        ArgGuard.ThrowIfNotFinite(end);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(screenStrokeWidth);
        ValidateLineCap(cap);
        SelectBatch(null, null);
        Line(_camera.WorldToDevice(start), _camera.WorldToDevice(end), color, screenStrokeWidth, cap, cap);
    }

    /// <summary>Draws the visible part of an infinite world-space line.</summary>
    public void DrawWorldLine(Line2D line, XnaColor color, float screenStrokeWidth = 2f)
    {
        RequireFrame();
        if (!line.IsValid) throw new ArgumentException("The line needs a valid direction.", nameof(line));
        ArgGuard.ThrowIfNotFiniteOrNotPositive(screenStrokeWidth);
        if (!ClipLinearGeometry(line.Origin, line.Direction, false, screenStrokeWidth,
            out var start, out var end, out _)) return;
        SelectBatch(null, null);
        Line(start, end, color, screenStrokeWidth);
    }

    /// <summary>Draws the visible part of a world-space ray. Its origin can have a cap.</summary>
    public void DrawWorldRay(Ray2D ray, XnaColor color, float screenStrokeWidth = 2f,
        LineCap2D originCap = LineCap2D.Butt)
    {
        RequireFrame();
        if (!ray.IsValid) throw new ArgumentException("The ray needs a valid direction.", nameof(ray));
        ArgGuard.ThrowIfNotFiniteOrNotPositive(screenStrokeWidth);
        ValidateLineCap(originCap);
        if (!ClipLinearGeometry(ray.Origin, ray.Direction, true, screenStrokeWidth,
            out var start, out var end, out var originVisible)) return;
        SelectBatch(null, null);
        Line(start, end, color, screenStrokeWidth, originVisible ? originCap : LineCap2D.Butt);
    }

    private bool ClipLinearGeometry(Vector2 origin, Vector2 direction, bool forwardOnly, float width,
        out Vector2 start, out Vector2 end, out bool originVisible)
    {
        var deviceOrigin = _camera.WorldToDevice(origin);
        var deviceDirection = Vector2.TransformNormal(direction, _camera.WorldToDeviceMatrix);
        var min = forwardOnly ? 0d : double.NegativeInfinity;
        var max = double.PositiveInfinity;
        var padding = width * 0.5f + 1f;
        if (!ClipAxis(deviceOrigin.X, deviceDirection.X, -padding, _camera.ViewportSize.X + padding, ref min, ref max) ||
            !ClipAxis(deviceOrigin.Y, deviceDirection.Y, -padding, _camera.ViewportSize.Y + padding, ref min, ref max) ||
            min > max)
        {
            start = end = default;
            originVisible = false;
            return false;
        }
        originVisible = forwardOnly && min == 0d;
        start = deviceOrigin + deviceDirection * (float)min;
        end = deviceOrigin + deviceDirection * (float)max;
        return true;
    }

    private static bool ClipAxis(double origin, double direction, double low, double high,
        ref double min, ref double max)
    {
        if (direction == 0d) return origin >= low && origin <= high;
        var first = (low - origin) / direction;
        var second = (high - origin) / direction;
        min = Math.Max(min, Math.Min(first, second));
        max = Math.Min(max, Math.Max(first, second));
        return min <= max;
    }

    private void FillDisk(Vector2 center, float radius, XnaColor color)
    {
        const int segments = 20;
        var middle = Vertex(center, color);
        var previous = Vertex(center + new Vector2(radius, 0), color);
        for (var i = 1; i <= segments; i++)
        {
            var angle = i * MathF.Tau / segments;
            var next = Vertex(center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius, color);
            Triangle(middle, previous, next);
            previous = next;
        }
    }

    private void FillRoundCap(Vector2 center, Vector2 outward, float radius, XnaColor color)
    {
        const int segments = 10;
        var tangent = new Vector2(-outward.Y, outward.X);
        var middle = Vertex(center, color);
        var previous = Vertex(center + tangent * radius, color);
        for (var i = 1; i <= segments; i++)
        {
            var angle = i * MathF.PI / segments;
            var next = Vertex(center + (tangent * MathF.Cos(angle) + outward * MathF.Sin(angle)) * radius, color);
            Triangle(middle, previous, next);
            previous = next;
        }
    }

    private static void ValidateShapeStyle(XnaColor? fillColor, XnaColor? outlineColor, float width)
    {
        if (fillColor is null && outlineColor is null)
            throw new ArgumentException("Provide a fill color, an outline color, or both.", nameof(fillColor));
        if (outlineColor is not null) ArgGuard.ThrowIfNotFiniteOrNotPositive(width);
    }

    private static void ValidateLineCap(LineCap2D cap)
    {
        if (cap is < LineCap2D.Butt or > LineCap2D.Round)
            throw new ArgumentOutOfRangeException(nameof(cap));
    }

    private static void ValidateTransform(Matrix3x2 transform)
    {
        if (!float.IsFinite(transform.M11) || !float.IsFinite(transform.M12) ||
            !float.IsFinite(transform.M21) || !float.IsFinite(transform.M22) ||
            !float.IsFinite(transform.M31) || !float.IsFinite(transform.M32))
            throw new ArgumentException("The transform must be finite.", nameof(transform));
    }
}
