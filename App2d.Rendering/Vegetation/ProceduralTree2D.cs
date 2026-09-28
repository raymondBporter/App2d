using App2d.Core.Validation;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Functions;
using App2d.Core.Mathematics;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Rendering.Vegetation;

/// <summary>Seeded, bounded branching geometry with small wind-driven leaf motion.</summary>
public sealed class ProceduralTree2D
{
    private readonly List<Branch> _branches = [];
    private readonly List<Crown> _crowns = [];
    private readonly Vector2 _root;
    private readonly float _height;
    public Bounds2D Bounds { get; }

    public ProceduralTree2D(Vector2 root, float height, int seed)
    {
        ArgGuard.ThrowIfNotFinite(root);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(height);
        _root = root;
        _height = height;
        Bounds = new(root - new Vector2(height, 0f), root + new Vector2(height, height * 1.5f));
        Grow(root, MathF.PI / 2f, height * 0.42f, height * 0.075f, 3, new Random(seed));
    }

    private void Grow(Vector2 start, float angle, float length, float width, int depth, Random random)
    {
        var end = start + Polar2D.ToCartesian(length, angle);
        _branches.Add(new(start, end, width));
        if (depth <= 1)
        {
            var radius = _height * float.Lerp(0.12f, 0.21f, random.NextSingle());
            _crowns.Add(new(end, radius, random.NextSingle() * MathF.Tau,
                XnaColor.Lerp(new(32, 87, 68), new(83, 145, 88), random.NextSingle())));
        }
        if (depth == 0) return;
        for (var side = -1; side <= 1; side += 2)
            Grow(end, angle + side * float.Lerp(0.3f, 0.65f, random.NextSingle()),
                length * float.Lerp(0.62f, 0.79f, random.NextSingle()), width * 0.61f, depth - 1, random);
    }

    public void Render(Renderer2D renderer, VegetationWind2D wind)
    {
        Span<Vector2> quad = stackalloc Vector2[4];
        foreach (var branch in _branches)
        {
            var normal = Vector2.Normalize(new Vector2(-(branch.End - branch.Start).Y, (branch.End - branch.Start).X));
            quad[0] = branch.Start - normal * branch.Width * 0.5f;
            quad[1] = branch.End - normal * branch.Width * 0.32f;
            quad[2] = branch.End + normal * branch.Width * 0.32f;
            quad[3] = branch.Start + normal * branch.Width * 0.5f;
            renderer.DrawWorldConvexPolygon(quad, new XnaColor(78, 65, 48));
            // A narrow lit face makes the trunk read against the canopy.
            quad[0] = branch.Start;
            quad[1] = branch.End;
            renderer.DrawWorldConvexPolygon(quad, new XnaColor(105, 87, 57));
        }
        Span<Vector2> leaves = stackalloc Vector2[10];
        foreach (var crown in _crowns)
        {
            var center = crown.Center + new Vector2(wind.Offset(_root.X, 1f, crown.Phase, 0.14f), 0f);
            for (var i = 0; i < leaves.Length; i++)
            {
                var angle = i * MathF.Tau / leaves.Length + crown.Phase;
                leaves[i] = VertexGenerator2D.PointOnEllipse(center, new(crown.Radius, crown.Radius * 0.78f), angle);
            }
            renderer.DrawWorldConvexPolygon(leaves, crown.Color);
        }
    }

    private readonly record struct Branch(Vector2 Start, Vector2 End, float Width);
    private readonly record struct Crown(Vector2 Center, float Radius, float Phase, XnaColor Color);
}
