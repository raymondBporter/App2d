using App2d.Core.Geometry.Operations.Area;
using App2d.Core.Geometry.Operations.Diameter;
using App2d.Core.Geometry.Shapes;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace App2d.Core.Shapes;

/// <summary>Source geometry for a union of convex parts. Construction only snapshots its input.</summary>
public readonly partial record struct CompositeShape2D : IShape2D
{
    private readonly IShape2D[]? _parts;
    private static readonly ConditionalWeakTable<IShape2D[], Dictionary<int, float>> UnionAreas = [];
    public string Kind => ShapeKinds2D.Composite;

    public CompositeShape2D(IShape2D[] parts, bool includeOverlap = true, int areaOutlineSegments = ShapeArea2D.DefaultOutlineSegments)
    {
        _parts = parts?.ToArray();
        IncludeOverlap = includeOverlap;
        AreaOutlineSegments = areaOutlineSegments;
    }

    public ReadOnlySpan<IShape2D> Parts { get => _parts; init => _parts = value.ToArray(); }
    public bool IncludeOverlap { get; init; } = true;
    public int AreaOutlineSegments { get; init; } = ShapeArea2D.DefaultOutlineSegments;
    public CompositeShape2D() { }

    public float Area
    {
        get
        {
            if (IncludeOverlap)
            {
                var area = 0f;
                foreach (var part in Parts) area += part.Area;
                return area;
            }
            var areas = UnionAreas.GetValue(_parts!, static _ => []);
            lock (areas)
            {
                if (!areas.TryGetValue(AreaOutlineSegments, out var area))
                {
                    area = ShapeArea2D.Union(_parts!.Cast<IConvexShape2D>().ToArray(), AreaOutlineSegments);
                    areas[AreaOutlineSegments] = area;
                }
                return area;
            }
        }
    }
    public float Diameter => ShapeDiameter2D.Calculate(this);
    public bool ContainsPoint(Vector2 point)
    {
        foreach (var part in Parts) if (part.ContainsPoint(point)) return true;
        return false;
    }
}
