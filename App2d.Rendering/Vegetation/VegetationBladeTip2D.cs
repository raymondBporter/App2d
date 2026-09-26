using App2d.Core.Geometry;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Rendering.Vegetation;

/// <summary>A frozen slice of an actual grass blade, with its original taper and color.</summary>
public sealed class VegetationBladeTip2D
{
    private readonly VegetationBladeSection2D[] _sections;
    private readonly Vector2? _flower;
    private readonly float _flowerRadius;
    private readonly XnaColor _flowerColor;
    private readonly Bounds2D _localBounds;
    public Vector2 ReleasePosition { get; }

    internal VegetationBladeTip2D(List<VegetationBladeSection2D> sections,
        Vector2? flower, float flowerRadius, XnaColor flowerColor)
    {
        var points = sections.SelectMany(s => new[] { s.A, s.B, s.C, s.D }).ToList();
        if (flower is { } center)
        {
            points.Add(center - new Vector2(flowerRadius));
            points.Add(center + new Vector2(flowerRadius));
        }
        var bounds = Bounds2D.FromPoints(points.ToArray());
        ReleasePosition = bounds.Center;
        _localBounds = new(bounds.Min - ReleasePosition, bounds.Max - ReleasePosition);
        _sections = [.. sections.Select(s => new VegetationBladeSection2D(
            s.A - ReleasePosition, s.B - ReleasePosition, s.C - ReleasePosition, s.D - ReleasePosition, s.Color))];
        _flower = flower - ReleasePosition;
        _flowerRadius = flowerRadius;
        _flowerColor = flowerColor;
    }

    public Bounds2D WorldBounds(Vector2 position, float rotation) =>
        _localBounds.TransformedBy(Matrix3x2.CreateRotation(rotation) * Matrix3x2.CreateTranslation(position));

    public void Render(Renderer2D renderer, Vector2 position, float rotation, float opacity)
    {
        var transform = Matrix3x2.CreateRotation(rotation) * Matrix3x2.CreateTranslation(position);
        Span<Vector2> quad = stackalloc Vector2[4];
        foreach (var section in _sections)
        {
            quad[0] = Vector2.Transform(section.A, transform);
            quad[1] = Vector2.Transform(section.B, transform);
            quad[2] = Vector2.Transform(section.C, transform);
            quad[3] = Vector2.Transform(section.D, transform);
            renderer.DrawWorldConvexPolygon(quad, Fade(section.Color, opacity));
        }
        if (_flower is { } flower)
            renderer.DrawWorldCircle(Vector2.Transform(flower, transform), _flowerRadius, Fade(_flowerColor, opacity), 1.5f);
    }

    private static XnaColor Fade(XnaColor color, float opacity)
    {
        color.A = (byte)(color.A * Math.Clamp(opacity, 0f, 1f));
        return color;
    }
}

internal readonly record struct VegetationBladeSection2D(Vector2 A, Vector2 B, Vector2 C, Vector2 D, XnaColor Color);
