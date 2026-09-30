using App2d.Core.Validation;
using App2d.Core.Geometry;
using App2d.Core.Mathematics;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Core.Characters.Authored;

/// <summary>A convex collision region in XY: hurt, attack and movement shapes derived from an evaluated pose.</summary>
public sealed record EntityRegion(string Id, IReadOnlyList<Vector2> Points)
{
    // A polygon uses its perimeter as its core. Circles and capsules keep an exact
    // point/segment core and radius; Points are only their drawn outline.
    private IReadOnlyList<Vector2>? _core;
    private float _radius;

    public bool Overlaps(EntityRegion other, Vector2 position, Vector2 otherPosition)
    {
        ArgGuard.ThrowIfNull(other);
        if (_radius == 0 && other._radius == 0)
            return Intersection2D.ConvexPolygonsOverlap(Points, other.Points, position, otherPosition);

        var firstCore = _core ?? Points;
        var secondCore = other._core ?? other.Points;
        Span<Vector2> first = firstCore.Count <= 64 ? stackalloc Vector2[firstCore.Count] : new Vector2[firstCore.Count];
        Span<Vector2> second = secondCore.Count <= 64 ? stackalloc Vector2[secondCore.Count] : new Vector2[secondCore.Count];
        for (var i = 0; i < first.Length; i++) first[i] = firstCore[i] + position;
        for (var i = 0; i < second.Length; i++) second[i] = secondCore[i] + otherPosition;
        return Distance2D.SignedDistanceBetweenConvexPolygons(first, second, _radius, other._radius) <= 0;
    }

    /// <summary>Tests the region against a placed physics shape without requesting contact details.</summary>
    public bool Overlaps(SpatialObject2D other, Vector2 position = default)
    {
        ArgGuard.ThrowIfNull(other);
        ArgGuard.ThrowIfNotFinite(position);
        var shape = ToShape();
        var bounds = ShapeBounds2D.Calculate(shape).TranslatedBy(position);
        if (!bounds.Intersects(other.WorldBounds)) return false;
        Similarity2D.TryFromMatrix(Matrix3x2.CreateTranslation(position), out var pose);
        return ShapeDistance2D.Distance(shape, pose, other.Shape, other.CollisionPose) == 0;
    }

    public static EntityRegion Box(string id, Vector2 center, Vector2 size)
    {
        var points = new Vector2[4];
        VertexGenerator2D.WriteRectangle(points, center - size / 2, center + size / 2);
        return new(id, points);
    }

    public static EntityRegion Circle(string id, Vector2 center, float radius)
    {
        _ = new Circle2D(radius, center);
        var points = new Vector2[24];
        VertexGenerator2D.WriteCircle(points, center, radius);
        return new(id, points) { _core = [center], _radius = radius };
    }

    public static EntityRegion Capsule(string id, Vector2 start, Vector2 end, float radius)
    {
        _ = new Capsule2D(start, end, radius);
        var points = new Vector2[26];
        VertexGenerator2D.WriteCapsule(points, start, end, radius, 12);
        return new(id, points) { _core = [start, end], _radius = radius };
    }

    public EntityRegion Scaled(float scale)
    {
        ArgGuard.ThrowIfNotFiniteOrNotPositive(scale);
        return new(Id, [.. Points.Select(point => point * scale)])
        {
            _core = _core is null ? null : [.. _core.Select(point => point * scale)],
            _radius = _radius * scale
        };
    }

    public IShape2D ToShape() => _core?.Count switch
    {
        1 => new Circle2D(_radius, _core[0]),
        2 => new Capsule2D(_core[0], _core[1], _radius),
        _ => new ConvexPolygon2D(Points)
    };
}
