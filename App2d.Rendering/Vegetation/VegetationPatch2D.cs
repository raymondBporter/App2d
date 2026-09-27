using App2d.Core;
using App2d.Core.Mathematics;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Rendering.Vegetation;

/// <summary>Which side of the characters a piece of foliage draws on.</summary>
public enum VegetationLayer2D
{
    Back,
    Front
}

/// <summary>
/// Doodled grass: outlined tufts of a few fat blades, the odd tall clump, and five-petal flowers.
/// Blade vertices are generated on the CPU, but <see cref="VegetationWind2D.Offset"/> is
/// deliberately expressed like a vertex shader.
/// </summary>
public sealed class VegetationPatch2D
{
    public static readonly XnaColor InkColor = new(35, 48, 31);
    private const int SegmentCount = 4;
    private const float GrazedTuftChance = 0.2f;
    // Ink widths are world units relative to a 4.5-unit blade, so the doodle reads the same at any zoom.
    private const float OutlineWidth = 0.8f;
    private const float DetailOutlineWidth = 0.65f;
    private const float ClippingWidthScale = 0.45f;
    private static readonly XnaColor[] FlowerColors =
        [new(232, 69, 44), new(122, 79, 214), new(242, 194, 48), new(255, 143, 194)];
    private static readonly XnaColor FlowerCenterColor = new(255, 210, 58);
    private static readonly XnaColor StemColor = new(62, 154, 42);

    private readonly Blade[] _blades;
    private readonly Flower[] _flowers;
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
        // Separate stream so how grass cuts never reshuffles how it grows.
        var cutTraits = new Random(seed ^ 0x2c1b3c6d);
        var sprigs = new Random(seed ^ 0x51ed2701);
        var blades = new List<Blade>();
        var flowers = new List<Flower>();
        var position = startX + style.Spacing * 0.5f * random.NextSingle();
        while (position < endX)
        {
            var density = Density(position, style);
            var skip = random.NextSingle() >= density;
            if (sprigs.NextSingle() < style.SprigChance)
                AddSprig(blades, position + style.Spacing * (sprigs.NextSingle() - 0.5f), groundY, style, sprigs);
            var tall = random.NextSingle() < style.TallChance;
            var layer = random.NextSingle() < style.FrontChance ? VegetationLayer2D.Front : VegetationLayer2D.Back;
            var height = tall
                ? float.Lerp(style.MaximumHeight * 1.05f, style.TallHeight, random.NextSingle())
                : float.Lerp(style.MinimumHeight, style.MaximumHeight, random.NextSingle());
            // Tufts thin out and shorten toward the edge of a patch instead of stopping at a wall.
            height *= float.Lerp(0.7f, 1f, density);
            var count = 2 + random.Next(3) + (tall ? 1 : 0);
            if (skip)
            {
                position += style.Spacing * float.Lerp(0.6f, 1.45f, random.NextSingle());
                continue;
            }
            var phase = random.NextSingle() * MathF.Tau;
            var windResponse = float.Lerp(0.8f, 1.2f, random.NextSingle()) * height / style.MaximumHeight;
            // A grazed tuft only loses its tips, so a hit always shows without mowing everything flat.
            var kept = cutTraits.NextSingle() < GrazedTuftChance ? float.Lerp(0.55f, 0.8f, cutTraits.NextSingle()) : 0f;
            for (var index = 0; index < count; index++)
            {
                // Mostly near the cut line, with a long tail of blades that only lose their tips.
                var jitter = cutTraits.NextSingle() * 2f - 1f;
                jitter = jitter < 0f ? jitter * 0.5f : jitter * jitter * 0.6f;
                var offset = (index - (count - 1) * 0.5f) * style.BladeWidth * 0.9f;
                var light = index % 2 == 0;
                blades.Add(new(
                    new Vector2(position + offset, groundY),
                    height * (light ? 1f : 0.7f),
                    style.BladeWidth,
                    offset * 0.6f,
                    phase + index * 0.37f,
                    windResponse,
                    Shade(light ? style.LightColor : style.DarkColor, layer),
                    layer,
                    jitter,
                    kept));
            }
            if (density > 0.6f && random.NextSingle() < style.FlowerChance)
            {
                var flowerX = position + style.BladeWidth * (random.NextSingle() - 0.5f) * 2f;
                flowers.Add(new(new Vector2(flowerX, groundY),
                    float.Lerp(style.MaximumHeight * 1.05f, style.TallHeight * 1.1f, random.NextSingle()),
                    random.NextSingle() * MathF.Tau,
                    FlowerColors[random.Next(FlowerColors.Length)]));
            }
            position += style.Spacing * float.Lerp(0.6f, 1.45f, random.NextSingle());
        }
        _blades = [.. blades];
        _flowers = [.. flowers];
    }

    public int BladeCount => _blades.Length;

    /// <summary>A couple of tiny blades below the cut line: they never get cut, so mowed and bare ground keep some green.</summary>
    private static void AddSprig(List<Blade> blades, float x, float groundY, VegetationStyle2D style, Random random)
    {
        var height = style.MinimumHeight * float.Lerp(0.4f, 0.6f, random.NextSingle());
        var count = 2 + random.Next(2);
        var phase = random.NextSingle() * MathF.Tau;
        for (var index = 0; index < count; index++)
        {
            var offset = (index - (count - 1) * 0.5f) * style.BladeWidth * 0.8f;
            blades.Add(new(new Vector2(x + offset, groundY), height * (index % 2 == 0 ? 1f : 0.75f),
                style.BladeWidth * 0.8f, offset * 0.8f, phase + index * 0.4f, 0.2f,
                Shade(index % 2 == 0 ? style.LightColor : style.DarkColor, VegetationLayer2D.Back),
                VegetationLayer2D.Back, Uncuttable: true));
        }
    }

    /// <summary>
    /// How much grass grows at a world X, from 0 (bare) to 1 (full). Two octaves of smooth value noise,
    /// thresholded so roughly <see cref="VegetationStyle2D.Coverage"/> of the ground is grassy. It depends only
    /// on X, so neighboring patches and streamed chunks agree.
    /// </summary>
    public static float Density(float worldX, VegetationStyle2D style)
    {
        if (style.Coverage >= 1f) return 1f;
        if (style.Coverage <= 0f) return 0f;
        var noise = 0.65f * ValueNoise(worldX / style.PatchWidth) +
            0.35f * ValueNoise(worldX / (style.PatchWidth * 0.37f) + 17.3f);
        var threshold = 1f - style.Coverage;
        var t = Math.Clamp((noise - threshold + 0.1f) / 0.2f, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static float ValueNoise(float x)
    {
        var cell = MathF.Floor(x);
        var f = x - cell;
        f = f * f * (3f - 2f * f);
        return float.Lerp(Hash((int)cell), Hash((int)cell + 1), f);
    }

    private static float Hash(int value)
    {
        unchecked
        {
            var h = (uint)value * 0x9e3779b9u ^ 0x5bd1e995u;
            h = (h ^ (h >> 16)) * 0x7feb352du;
            h = (h ^ (h >> 15)) * 0x846ca68bu;
            return ((h ^ (h >> 16)) & 0xffffff) / (float)0x1000000;
        }
    }
    public int FlowerCount => _flowers.Length;

    public void Render(
        Renderer2D renderer,
        float visibleLeft,
        float visibleRight,
        VegetationWind2D wind,
        VegetationLayer2D layer = VegetationLayer2D.Back,
        float? cutHeight = null,
        float cutRoughness = 0f)
    {
        if (cutHeight is { } height) ArgGuard.ThrowIfNotFiniteOrNegative(height);
        ArgGuard.ThrowIfNotFiniteOrNotInClosedRange(cutRoughness, 0f, 1f);

        if (layer == VegetationLayer2D.Back)
            foreach (var flower in _flowers)
                if (IsVisible(flower.Root.X, flower.Height, visibleLeft, visibleRight) &&
                    !IsSevered(flower, cutHeight))
                    RenderFlower(renderer, flower, wind);

        Span<Vector2> quad = stackalloc Vector2[4];
        Span<Vector2> outline = stackalloc Vector2[SegmentCount * 2 + 3];
        foreach (var blade in _blades)
        {
            if (blade.Layer != layer || !IsVisible(blade.Root.X, blade.Height, visibleLeft, visibleRight))
                continue;

            var end = cutHeight is { } cut ? CutWeight(blade, cut, cutRoughness) : 1f;
            if (end <= 0f) continue;
            var points = Math.Min(SegmentCount, (int)MathF.Ceiling(end * SegmentCount));

            // Ink goes down first and the fill covers its inner half, so only a clean outer edge shows.
            // Left edge up, across the tip (or the cut), then the right edge down.
            var count = 0;
            for (var index = 0; index < points; index++)
                outline[count++] = Edge(blade, index / (float)SegmentCount, wind, -1f);
            outline[count++] = Edge(blade, end, wind, -1f);
            if (end < 1f) outline[count++] = Edge(blade, end, wind, 1f);
            for (var index = points - 1; index >= 0; index--)
                outline[count++] = Edge(blade, index / (float)SegmentCount, wind, 1f);
            renderer.DrawWorldPolyline(outline[..count], InkColor, Ink(renderer, OutlineWidth * 2f));

            for (var index = 0; index < points; index++)
            {
                var section = Section(blade, index / (float)SegmentCount,
                    Math.Min((index + 1f) / SegmentCount, end), wind, 1f, blade.Color);
                quad[0] = section.A; quad[1] = section.B;
                quad[2] = section.C; quad[3] = section.D;
                renderer.DrawWorldConvexPolygon(quad, section.Color);
            }
        }
    }

    /// <summary>
    /// Captures the exact wind-bent blade tops at the cut line as thin green slivers, and
    /// breaks every flower whose head is below the cut into its petals.
    /// </summary>
    public IEnumerable<VegetationBladeTip2D> CreateClippings(float cutHeight, VegetationWind2D wind,
        float cutRoughness = 0f)
    {
        ArgGuard.ThrowIfNotFiniteOrNegative(cutHeight);
        ArgGuard.ThrowIfNotFiniteOrNotInClosedRange(cutRoughness, 0f, 1f);
        foreach (var blade in _blades)
        {
            var start = CutWeight(blade, cutHeight, cutRoughness);
            if (start >= 1f) continue;
            var sections = new List<VegetationTipPolygon2D>(SegmentCount);
            for (var index = 0; index < SegmentCount; index++)
            {
                var from = Math.Max(start, index / (float)SegmentCount);
                var to = (index + 1f) / SegmentCount;
                if (from >= to) continue;
                var section = Section(blade, from, to, wind, ClippingWidthScale, _style.LightColor);
                sections.Add(new([section.A, section.B, section.C, section.D], section.Color, null, 0f));
            }
            yield return new(sections);
        }

        foreach (var flower in _flowers)
        {
            if (!IsSevered(flower, cutHeight)) continue;
            var head = FlowerHead(flower, wind);
            var scale = _style.BladeWidth / 4.5f;
            for (var petal = 0; petal < 5; petal++)
            {
                var angle = petal / 5f * MathF.Tau + flower.Phase;
                yield return new([Disc(head + Polar2D.Direction(angle) * 5.2f * scale, 3.8f * scale, flower.Color)]);
            }
            yield return new([Disc(head, 3f * scale, FlowerCenterColor)]);
        }
    }

    private void RenderFlower(Renderer2D renderer, Flower flower, VegetationWind2D wind)
    {
        var scale = _style.BladeWidth / 4.5f;
        Span<Vector2> stem = stackalloc Vector2[SegmentCount + 1];
        for (var index = 0; index <= SegmentCount; index++)
            stem[index] = FlowerPoint(flower, index / (float)SegmentCount, wind);
        renderer.DrawWorldPolyline(stem, InkColor, Ink(renderer, 1.9f));
        renderer.DrawWorldPolyline(stem, StemColor, Ink(renderer, 0.85f));

        var leafRoot = FlowerPoint(flower, 0.35f, wind);
        var leaf = new Blade(leafRoot, 8f * scale, 4f * scale, 6f * scale, flower.Phase, 0.3f, _style.LightColor,
            VegetationLayer2D.Back);
        Span<Vector2> quad = stackalloc Vector2[4];
        Span<Vector2> outline = stackalloc Vector2[5];
        for (var index = 0; index < 2; index++)
        {
            var section = Section(leaf, index * 0.5f, (index + 1) * 0.5f, wind, 1f, leaf.Color);
            quad[0] = section.A; quad[1] = section.B; quad[2] = section.C; quad[3] = section.D;
            renderer.DrawWorldConvexPolygon(quad, section.Color);
        }
        outline[0] = Edge(leaf, 0f, wind, -1f); outline[1] = Edge(leaf, 0.5f, wind, -1f);
        outline[2] = Edge(leaf, 1f, wind, -1f);
        outline[3] = Edge(leaf, 0.5f, wind, 1f); outline[4] = Edge(leaf, 0f, wind, 1f);
        renderer.DrawWorldPolyline(outline, InkColor, Ink(renderer, DetailOutlineWidth));

        var head = FlowerHead(flower, wind);
        for (var petal = 0; petal < 5; petal++)
        {
            var angle = petal / 5f * MathF.Tau + flower.Phase;
            FillDisc(renderer, head + Polar2D.Direction(angle) * 5.2f * scale, 3.8f * scale, flower.Color);
        }
        FillDisc(renderer, head, 3f * scale, FlowerCenterColor);
    }

    private void FillDisc(Renderer2D renderer, Vector2 center, float radius, XnaColor color)
    {
        var disc = Disc(center, radius, color);
        renderer.DrawWorldConvexPolygon(disc.Points, disc.Fill);
        renderer.DrawWorldCircle(center, radius, InkColor, Ink(renderer, DetailOutlineWidth));
    }

    private float Ink(Renderer2D renderer, float width) =>
        Math.Max(1f, width * _style.BladeWidth / 4.5f * renderer.PixelsPerWorldUnit);

    private static VegetationTipPolygon2D Disc(Vector2 center, float radius, XnaColor color)
    {
        var points = new Vector2[10];
        for (var index = 0; index < points.Length; index++)
            points[index] = center + Polar2D.ToCartesian(radius, index / (float)points.Length * MathF.Tau);
        return new(points, color, InkColor, DetailOutlineWidth * radius / 3.8f);
    }

    private static bool IsVisible(float x, float height, float left, float right) =>
        x >= left - height && x <= right + height;

    // One rule for both drawing and bursting, so a cut flower never also stays standing.
    private static bool IsSevered(Flower flower, float? cutHeight) =>
        cutHeight is { } cut && cut < flower.Height * 0.85f;

    private static Vector2 FlowerHead(Flower flower, VegetationWind2D wind) => FlowerPoint(flower, 1f, wind);

    private static Vector2 FlowerPoint(Flower flower, float weight, VegetationWind2D wind) =>
        flower.Root + new Vector2(wind.Offset(flower.Root.X, weight, flower.Phase, 0.6f), flower.Height * weight);

    /// <summary>
    /// Share of the blade left standing after a cut. Roughness zero cuts every blade exactly at the line;
    /// above zero, blades cut at scattered heights and some tufts are only grazed.
    /// </summary>
    private static float CutWeight(Blade blade, float height, float roughness) =>
        blade.Uncuttable ? 1f : roughness <= 0f ? Math.Min(1f, height / blade.Height)
            : Math.Min(1f, Math.Max(blade.GrazedKeep, height * (1f + roughness * blade.CutJitter) / blade.Height));

    private static Section2D Section(Blade blade, float from, float to, VegetationWind2D wind, float widthScale,
        XnaColor color)
    {
        var bottom = Center(blade, from, wind);
        var top = Center(blade, to, wind);
        var bottomRadius = new Vector2(HalfWidth(blade, from) * widthScale, 0f);
        var topRadius = new Vector2(HalfWidth(blade, to) * widthScale, 0f);
        return new(bottom - bottomRadius, top - topRadius, top + topRadius, bottom + bottomRadius, color);
    }

    private static Vector2 Edge(Blade blade, float weight, VegetationWind2D wind, float side) =>
        Center(blade, weight, wind) + new Vector2(side * HalfWidth(blade, weight), 0f);

    private static float HalfWidth(Blade blade, float weight) => blade.Width * 0.5f * (1f - 0.92f * weight);

    private static Vector2 Center(Blade blade, float weight, VegetationWind2D wind) =>
        blade.Root + new Vector2(blade.Lean * weight * weight +
            wind.Offset(blade.Root.X, weight, blade.Phase, blade.WindResponse), blade.Height * weight);

    private static XnaColor Shade(XnaColor color, VegetationLayer2D layer) =>
        layer == VegetationLayer2D.Back ? XnaColor.Lerp(color, XnaColor.Black, 0.08f) : color;

    private readonly record struct Section2D(Vector2 A, Vector2 B, Vector2 C, Vector2 D, XnaColor Color);

    private readonly record struct Blade(
        Vector2 Root,
        float Height,
        float Width,
        float Lean,
        float Phase,
        float WindResponse,
        XnaColor Color,
        VegetationLayer2D Layer,
        float CutJitter = 0f,
        float GrazedKeep = 0f,
        bool Uncuttable = false);

    private readonly record struct Flower(Vector2 Root, float Height, float Phase, XnaColor Color);
}

/// <summary>
/// Sizes are world units; ordinary tufts span Minimum..Maximum, tall clumps reach TallHeight.
/// Coverage is the rough share of ground that grows grass; PatchWidth sets how wide the grassy and bare stretches run.
/// SprigChance scatters tiny uncuttable blades everywhere, bare ground included.
/// </summary>
public readonly record struct VegetationStyle2D(
    float MinimumHeight,
    float MaximumHeight,
    float TallHeight,
    float BladeWidth,
    float Spacing,
    XnaColor DarkColor,
    XnaColor LightColor,
    float TallChance,
    float FrontChance,
    float FlowerChance,
    float Coverage = 1f,
    float PatchWidth = 1f,
    float SprigChance = 0f)
{
    internal void Validate()
    {
        ArgGuard.ThrowIfNotFiniteOrNotPositive(MinimumHeight);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(BladeWidth);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(Spacing);
        ArgGuard.ThrowIfNotFiniteOrLessThan(MaximumHeight, MinimumHeight);
        ArgGuard.ThrowIfNotFiniteOrLessThan(TallHeight, MaximumHeight);
        ArgGuard.ThrowIfNotFiniteOrNotInClosedRange(TallChance, 0f, 1f);
        ArgGuard.ThrowIfNotFiniteOrNotInClosedRange(FrontChance, 0f, 1f);
        ArgGuard.ThrowIfNotFiniteOrNotInClosedRange(FlowerChance, 0f, 1f);
        ArgGuard.ThrowIfNotFiniteOrNotInClosedRange(Coverage, 0f, 1f);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(PatchWidth);
        ArgGuard.ThrowIfNotFiniteOrNotInClosedRange(SprigChance, 0f, 1f);
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
