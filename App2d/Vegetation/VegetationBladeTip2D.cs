using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Transforms;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Rendering;
using App2d.Rendering;

namespace App2d.Vegetation;

/// <summary>A frozen piece of cut foliage: a blade sliver or a flower petal, in world space at release.</summary>
public sealed class VegetationBladeTip2D
{
    private readonly VegetationTipPolygon2D[] _polygons;
    private readonly Rect2f _localBounds;
    public Vector2f ReleasePosition { get; }

    internal VegetationBladeTip2D(IReadOnlyList<VegetationTipPolygon2D> polygons)
    {
        var bounds = Rect2f.FromPoints([.. polygons.SelectMany(p => p.Points)]);
        ReleasePosition = bounds.Center;
        _localBounds = Rect2f.Builder.Create(bounds.Min - ReleasePosition, bounds.Max - ReleasePosition);
        _polygons = [.. polygons.Select(p => p with { Points = [.. p.Points.Select(point => point - ReleasePosition)] })];
    }

    public Rect2f WorldBounds(Vector2f position, float rotation) =>
        _localBounds.TransformedBy(new RigidTransform2D(position, rotation).Matrix);

    public void Render(Renderer2D renderer, Vector2f position, float rotation, float opacity)
    {
        var transform = new RigidTransform2D(position, rotation).Matrix;
        Span<Vector2f> points = stackalloc Vector2f[16];
        Span<Vector2f> ring = stackalloc Vector2f[17];
        foreach (var polygon in _polygons)
        {
            var world = points[..polygon.Points.Length];
            VertexTransforms2D.Transform(polygon.Points, world, transform);

            renderer.DrawWorldConvexPolygon(world, polygon.Fill.ScaleAlpha(opacity));
            if (polygon.Outline is not { } outline)
            {
                continue;
            }

            var closed = ring[..(world.Length + 1)];
            world.CopyTo(closed);
            closed[^1] = world[0];
            renderer.DrawWorldThickPolyline(closed, polygon.OutlineWidth, outline.ScaleAlpha(opacity));
        }
    }
}

/// <summary>One convex piece of a clipping; an outline draws it in ink like the living plant (width in world units).</summary>
internal readonly record struct VegetationTipPolygon2D(Vector2f[] Points, Color4b Fill, Color4b? Outline, float OutlineWidth);
