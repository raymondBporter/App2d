using App2d.Core;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Rendering.Vegetation;

/// <summary>
/// Shader-shaped vegetation experiment. Blade vertices are generated on the CPU for now,
/// but <see cref="VegetationWind2D.Offset"/> is deliberately expressed like a vertex shader.
/// </summary>
public sealed class VegetationPatch2D
{
    private const int SegmentCount = 5;
    private readonly Blade[] _blades;
    private readonly VegetationStyle2D _style;

    public VegetationPatch2D(
        float startX,
        float endX,
        float groundY,
        VegetationStyle2D style,
        int seed)
    {
        ArgGuard.ThrowIfNotFiniteOrLessThanOrEqual(endX, startX);
        ArgGuard.ThrowIfNotFinite(groundY);
        style.Validate();

        _style = style;
        var random = new Random(seed);
        var blades = new List<Blade>();
        var position = startX + style.Spacing * 0.5f;
        while (position < endX)
        {
            var height = float.Lerp(style.MinimumHeight, style.MaximumHeight, random.NextSingle());
            var width = float.Lerp(style.MinimumWidth, style.MaximumWidth, random.NextSingle());
            var lean = (random.NextSingle() * 2f - 1f) * style.MaximumLean;
            var phase = random.NextSingle() * MathF.Tau;
            var windResponse = float.Lerp(0.72f, 1.28f, random.NextSingle());
            var colorVariation = float.Lerp(-0.18f, 0.18f, random.NextSingle());
            var flower = random.NextSingle() < style.FlowerChance;
            blades.Add(new(
                new Vector2(position, groundY),
                height,
                width,
                lean,
                phase,
                windResponse,
                colorVariation,
                flower));
            position += style.Spacing * float.Lerp(0.68f, 1.32f, random.NextSingle());
        }
        _blades = [.. blades];
    }

    public int BladeCount => _blades.Length;

    public void Render(
        Renderer2D renderer,
        float visibleLeft,
        float visibleRight,
        VegetationWind2D wind,
        float? cutHeight = null,
        float cutRoughness = 0f)
    {
        if (cutHeight is { } height) ArgGuard.ThrowIfNotFiniteOrNegative(height);
        ArgGuard.ThrowIfNotFiniteOrNotInClosedRange(cutRoughness, 0f, 1f);
        Span<Vector2> quad = stackalloc Vector2[4];

        foreach (var blade in _blades)
        {
            if (blade.Root.X < visibleLeft - blade.Height || blade.Root.X > visibleRight + blade.Height)
                continue;

            var end = cutHeight is { } cut ? CutWeight(blade, cut, cutRoughness) : 1f;
            for (var index = 0; index < SegmentCount; index++)
            {
                var startWeight = index / (float)SegmentCount;
                var endWeight = Math.Min((index + 1f) / SegmentCount, end);
                if (startWeight >= endWeight) break;
                var section = Section(blade, startWeight, endWeight, wind);
                quad[0] = section.A; quad[1] = section.B;
                quad[2] = section.C; quad[3] = section.D;
                renderer.DrawWorldConvexPolygon(quad, section.Color);
            }

            if (blade.HasFlower && end >= 1f)
                renderer.DrawWorldCircle(Center(blade, 1f, wind), blade.Width * 1.15f, _style.FlowerColor, 1.5f);
        }
    }

    /// <summary>Captures the exact wind-bent tops at the cut line, ready to tumble independently.</summary>
    public IEnumerable<VegetationBladeTip2D> CreateClippings(float cutHeight, VegetationWind2D wind,
        float cutRoughness = 0f)
    {
        ArgGuard.ThrowIfNotFiniteOrNegative(cutHeight);
        ArgGuard.ThrowIfNotFiniteOrNotInClosedRange(cutRoughness, 0f, 1f);
        foreach (var blade in _blades)
        {
            var start = CutWeight(blade, cutHeight, cutRoughness);
            if (start >= 1f) continue;
            var sections = new List<VegetationBladeSection2D>(SegmentCount);
            for (var index = 0; index < SegmentCount; index++)
            {
                var from = Math.Max(start, index / (float)SegmentCount);
                var to = (index + 1f) / SegmentCount;
                if (from < to) sections.Add(Section(blade, from, to, wind));
            }
            yield return new(sections, blade.HasFlower ? Center(blade, 1f, wind) : null,
                blade.Width * 1.15f, _style.FlowerColor);
        }
    }

    private static float CutWeight(Blade blade, float height, float roughness) =>
        Math.Min(1f, height * (1f + roughness * MathF.Sin(blade.Phase * 3.17f)) / blade.Height);

    private VegetationBladeSection2D Section(Blade blade, float from, float to, VegetationWind2D wind)
    {
        var bottom = Center(blade, from, wind);
        var top = Center(blade, to, wind);
        var bottomRadius = new Vector2(blade.Width * 0.5f * (1f - 0.88f * from), 0f);
        var topRadius = new Vector2(blade.Width * 0.5f * (1f - 0.88f * to), 0f);
        return new(bottom - bottomRadius, top - topRadius, top + topRadius, bottom + bottomRadius,
            BladeColor((from + to) * 0.5f, blade.ColorVariation));
    }

    private static Vector2 Center(Blade blade, float weight, VegetationWind2D wind) =>
        blade.Root + new Vector2(blade.Lean * weight +
            wind.Offset(blade.Root.X, weight, blade.Phase, blade.WindResponse), blade.Height * weight);

    private XnaColor BladeColor(float heightWeight, float variation)
    {
        var color = XnaColor.Lerp(_style.RootColor, _style.TipColor, heightWeight);
        return variation >= 0f
            ? XnaColor.Lerp(color, XnaColor.White, variation)
            : XnaColor.Lerp(color, XnaColor.Black, -variation);
    }

    private readonly record struct Blade(
        Vector2 Root,
        float Height,
        float Width,
        float Lean,
        float Phase,
        float WindResponse,
        float ColorVariation,
        bool HasFlower);
}

public readonly record struct VegetationStyle2D(
    float MinimumHeight,
    float MaximumHeight,
    float MinimumWidth,
    float MaximumWidth,
    float Spacing,
    float MaximumLean,
    XnaColor RootColor,
    XnaColor TipColor,
    float FlowerChance,
    XnaColor FlowerColor)
{
    internal void Validate()
    {
        ArgGuard.ThrowIfNotFiniteOrNotPositive(MinimumHeight);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(MinimumWidth);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(Spacing);
        ArgGuard.ThrowIfNotFiniteOrLessThan(MaximumHeight, MinimumHeight);
        ArgGuard.ThrowIfNotFiniteOrLessThan(MaximumWidth, MinimumWidth);
        ArgGuard.ThrowIfNotFiniteOrNegative(MaximumLean);
        ArgGuard.ThrowIfNotFiniteOrNotInClosedRange(FlowerChance, 0f, 1f);
    }
}

/// <summary>Time-varying wind field. Height weight zero always produces an anchored root.</summary>
public readonly record struct VegetationWind2D(
    double TotalSeconds,
    float Strength,
    float Speed,
    float SpatialFrequency,
    float Gust)
{
    public float Offset(float worldX, float heightWeight, float phase, float response)
    {
        var time = (float)TotalSeconds;
        var primary = MathF.Sin(worldX * SpatialFrequency + time * Speed + phase);
        var detail = MathF.Sin(
            worldX * SpatialFrequency * 2.31f - time * Speed * 1.43f + phase * 1.71f);
        var slowGust = MathF.Sin(worldX * SpatialFrequency * 0.37f + time * Speed * 0.29f);
        var wave = primary + detail * 0.28f + slowGust * (0.16f + Gust * 0.55f);
        return Strength * response * (1f + Gust * 1.8f) * wave * heightWeight * heightWeight;
    }
}
