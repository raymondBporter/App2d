using App2d.Core.Geometry;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Rendering.Vegetation;

/// <summary>A frozen piece of cut foliage: a blade sliver or a flower petal, in world space at release.</summary>
public sealed class VegetationBladeTip2D
{
    private readonly VegetationTipPolygon2D[] _polygons;
    private readonly Bounds2D _localBounds;
    public Vector2 ReleasePosition { get; }

    internal VegetationBladeTip2D(IReadOnlyList<VegetationTipPolygon2D> polygons)
    {
        var bounds = Bounds2D.FromPoints([.. polygons.SelectMany(p => p.Points)]);
        ReleasePosition = bounds.Center;
        _localBounds = new(bounds.Min - ReleasePosition, bounds.Max - ReleasePosition);
        _polygons = [.. polygons.Select(p => p with { Points = [.. p.Points.Select(point => point - ReleasePosition)] })];
    }

    public Bounds2D WorldBounds(Vector2 position, float rotation) =>
        _localBounds.TransformedBy(Matrix3x2.CreateRotation(rotation) * Matrix3x2.CreateTranslation(position));

    public void Render(Renderer2D renderer, Vector2 position, float rotation, float opacity)
    {
        var transform = Matrix3x2.CreateRotation(rotation) * Matrix3x2.CreateTranslation(position);
        Span<Vector2> points = stackalloc Vector2[16];
        Span<Vector2> ring = stackalloc Vector2[17];
        foreach (var polygon in _polygons)
        {
            var world = points[..polygon.Points.Length];
            for (var index = 0; index < world.Length; index++)
                world[index] = Vector2.Transform(polygon.Points[index], transform);
            renderer.DrawWorldConvexPolygon(world, Fade(polygon.Fill, opacity));
            if (polygon.Outline is not { } outline) continue;
            var closed = ring[..(world.Length + 1)];
            world.CopyTo(closed);
            closed[^1] = world[0];
            renderer.DrawWorldPolyline(closed, Fade(outline, opacity),
                Math.Max(1f, polygon.OutlineWidth * renderer.PixelsPerWorldUnit));
        }
    }

    private static XnaColor Fade(XnaColor color, float opacity)
    {
        color.A = (byte)(color.A * Math.Clamp(opacity, 0f, 1f));
        return color;
    }
}

/// <summary>One convex piece of a clipping; an outline draws it in ink like the living plant (width in world units).</summary>
internal readonly record struct VegetationTipPolygon2D(Vector2[] Points, XnaColor Fill, XnaColor? Outline, float OutlineWidth);
