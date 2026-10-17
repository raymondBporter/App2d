using App2d.Core.Geometry.Operations.Vertices;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Mathematics;
using App2d.Core.Vegetation;
using App2d.Gameplay.World.Vegetation;
using App2d.Rendering;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Presentation.World.Vegetation;

/// <summary>Massive bark columns, flat branch tops, moss and layered wind-driven foliage.</summary>
public sealed class GiantTreeVisual2D
{
    private static readonly XnaColor Ink = new(37, 46, 32);
    private static readonly XnaColor Bark = new(108, 78, 49);
    private readonly List<Crown> _crowns = [];
    private readonly List<Vector2[]> _bark = [];
    private readonly List<Vector2[]> _moss = [];

    public GiantTreeVisual2D(GiantTree2D tree)
    {
        Tree = tree;
        var random = new Random(tree.Seed ^ 0x51ed2701);
        var settings = tree.Settings;
        foreach (var branch in tree.Branches)
        {
            var bounds = branch.Bounds;
            var tip = new Vector2(branch.Side < 0 ? bounds.Left : bounds.Right, bounds.Top);
            var radius = random.NextFloat(settings.FoliageRadius.Min, settings.FoliageRadius.Max);
            AddCrown(tip + new Vector2(branch.Side * radius * 0.2f, radius * 0.12f), new(radius, radius * 0.62f));
            var x = bounds.Left + 8f;
            for (var tuft = 0; tuft < 64 && x < bounds.Right - 5f; tuft++, x += random.NextFloat(14f, 25f))
            {
                var height = random.NextFloat(3f, 9f);
                _moss.Add([new(x - 3, bounds.Top), new(x + 3, bounds.Top), new(x + random.NextFloat(-3f, 3f), bounds.Top + height)]);
            }
        }

        // The crown is broad enough to sell the scale, with gaps between the lower limbs.
        var top = tree.Root + new Vector2(0, settings.Height);
        var crownRadius = Math.Max(settings.TrunkWidth, settings.FoliageRadius.Max * 1.7f);
        AddCrown(top + new Vector2(-crownRadius * 0.7f, -crownRadius * 0.15f), new(crownRadius, crownRadius * 0.65f));
        AddCrown(top + new Vector2(crownRadius * 0.7f, -crownRadius * 0.15f), new(crownRadius, crownRadius * 0.65f));
        AddCrown(top + new Vector2(0, crownRadius * 0.25f), new(crownRadius * 1.1f, crownRadius * 0.7f));

        var y = 0f;
        for (var groove = 0; groove < 256 && y < settings.Height; groove++, y += random.NextFloat(32f, 70f))
        {
            var halfWidth = settings.TrunkWidth * float.Lerp(0.46f, 0.34f, y / settings.Height);
            var x = tree.Root.X + random.NextFloat(-halfWidth, halfWidth * 0.5f);
            var width = random.NextFloat(4f, 13f);
            var length = Math.Min(random.NextFloat(30f, 110f), settings.Height - y);
            _bark.Add([new(x, tree.Root.Y + y), new(x + width, tree.Root.Y + y + length * 0.1f),
                new(x + width * 0.6f, tree.Root.Y + y + length), new(x - width * 0.3f, tree.Root.Y + y + length * 0.8f)]);
        }

        void AddCrown(Vector2 center, Vector2 radii) =>
            _crowns.Add(new(center, radii, random.NextFloat(MathF.Tau), random.NextSingle()));
    }

    public GiantTree2D Tree
    {
        get;
    }

    public void Render(Renderer2D renderer, Rect2D visible, VegetationWind2D wind)
    {
        if (!visible.Intersects(Tree.Bounds))
        {
            return;
        }

        Span<Vector2> leaves = stackalloc Vector2[14];
        foreach (var crown in _crowns)
        {
            if (!visible.Intersects(Rect2D.FromSize(crown.Radii * 2f + new Vector2(12f), crown.Center)))
            {
                continue;
            }

            var center = crown.Center + new Vector2(wind.Offset(crown.Center.X, 1f, crown.Phase, 0.1f), 0);
            WriteLeaves(center, crown.Radii, crown.Phase, leaves);
            renderer.DrawWorldConvexPolygon(leaves, Ink);
            WriteLeaves(center + new Vector2(0, 2f), crown.Radii * 0.96f, crown.Phase, leaves);
            renderer.DrawWorldConvexPolygon(leaves, XnaColor.Lerp(new(40, 96, 62), new(80, 137, 67), crown.Shade));
            WriteLeaves(center + crown.Radii * new Vector2(-0.12f, 0.25f), crown.Radii * new Vector2(0.7f, 0.52f), crown.Phase, leaves);
            renderer.DrawWorldConvexPolygon(leaves, XnaColor.Lerp(new(64, 122, 64), new(113, 157, 79), crown.Shade));
        }

        var root = Tree.Root;
        var width = Tree.Settings.TrunkWidth;
        var height = Tree.Settings.Height;
        Span<Vector2> wood =
        [
            root + new Vector2(-width * 0.5f, 0),
            root + new Vector2(width * 0.5f, 0),
            root + new Vector2(width * 0.38f, height),
            root + new Vector2(-width * 0.38f, height),
        ];
        renderer.DrawWorldConvexPolygon(wood, Bark);
        renderer.DrawWorldSegment(wood[0], wood[3], Ink, 2f);
        renderer.DrawWorldSegment(wood[1], wood[2], Ink, 2f);
        // A broad illuminated face keeps a giant column readable at normal play zoom.
        wood[1] = root + new Vector2(-width * 0.1f, 0);
        wood[2] = root + new Vector2(-width * 0.06f, height);
        renderer.DrawWorldConvexPolygon(wood, new XnaColor(136, 99, 59));
        foreach (var bark in _bark)
        {
            if (visible.Top >= bark[0].Y && visible.Bottom <= bark[2].Y)
            {
                renderer.DrawWorldConvexPolygon(bark, new XnaColor(82, 61, 43));
            }
        }

        foreach (var branch in Tree.Branches)
        {
            var b = branch.Bounds;
            if (!visible.Intersects(b))
            {
                continue;
            }

            wood[0] = new(b.Left, b.Top);
            wood[1] = new(b.Right, b.Top);
            wood[2] = new(b.Right, branch.Side > 0 ? b.Top - b.Height * 0.35f : b.Bottom);
            wood[3] = new(b.Left, branch.Side < 0 ? b.Top - b.Height * 0.35f : b.Bottom);
            renderer.DrawWorldConvexPolygon(wood, Bark);
            renderer.DrawWorldSegment(wood[0], wood[1], Ink, 2f);
            renderer.DrawWorldSegment(wood[2], wood[3], Ink, 1.5f);
            renderer.DrawWorldSegment(new(b.Left + 2f, b.Top - 3f), new(b.Right - 2f, b.Top - 3f), new XnaColor(161, 120, 65), 1.5f);
        }
        foreach (var moss in _moss)
        {
            if (visible.Contains(moss[0]) || visible.Contains(moss[2]))
            {
                renderer.DrawWorldConvexPolygon(moss, new XnaColor(101, 147, 57));
            }
        }

        Span<Vector2> flare = stackalloc Vector2[3];
        for (var side = -1; side <= 1; side += 2)
        {
            flare[0] = root + new Vector2(side * width * 0.75f, 0);
            flare[1] = root + new Vector2(side * width * 0.42f, Math.Min(width * 0.6f, height * 0.08f));
            flare[2] = root + new Vector2(side * width * 0.15f, 0);
            renderer.DrawWorldConvexPolygon(flare, Bark);
        }
    }

    private static void WriteLeaves(Vector2 center, Vector2 radii, float phase, Span<Vector2> points)
    {
        for (var i = 0; i < points.Length; i++)
        {
            var angle = i * MathF.Tau / points.Length;
            points[i] = VertexGenerator2D.PointOnEllipse(center, radii, angle + phase);
        }
    }

    private readonly record struct Crown(Vector2 Center, Vector2 Radii, float Phase, float Shade);
}
