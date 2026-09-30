using App2d.Core.Geometry;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry;

/// <summary>
/// Builds an unindexed triangle stream. The vertex factory supplies rendering attributes,
/// so geometry generation has no graphics-library dependency.
/// </summary>
public sealed class TriangleMeshBuilder3D<TVertex, TMaterial>
{
    private const int MaximumVertexCount = 1_048_576;
    private readonly Func<Vector3, TMaterial, Vector2, TVertex> _vertexFactory;
    private TVertex[] _vertices;

    public TriangleMeshBuilder3D(Func<Vector3, TMaterial, Vector2, TVertex> vertexFactory, int initialCapacity = 64)
    {
        _vertexFactory = ArgGuard.RequireNotNull(vertexFactory);
        ArgGuard.ThrowIf(initialCapacity < 0 || initialCapacity > MaximumVertexCount,
            "Initial capacity must fit within the mesh limit.", nameof(initialCapacity));
        _vertices = new TVertex[Math.Max(3, initialCapacity)];
        Clear();
    }

    public ReadOnlySpan<TVertex> Vertices => _vertices.AsSpan(0, Count);
    /// <summary>Returns the backing array for APIs that submit a contiguous vertex range.</summary>
    public TVertex[] GetBuffer() => _vertices;
    public int Count { get; private set; }
    public int TriangleCount => Count / 3;
    public Vector3 Min { get; private set; }
    public Vector3 Max { get; private set; }

    public void Clear()
    {
        Count = 0;
        Min = new(float.PositiveInfinity);
        Max = new(float.NegativeInfinity);
    }

    public void Vertex(Vector3 point, TMaterial material, Vector2 uv = default)
    {
        ArgGuard.ThrowIfNotFinite(point);
        ArgGuard.ThrowIfNotFinite(uv);
        EnsureCapacity(Count + 1);
        _vertices[Count++] = _vertexFactory(point, material, uv);
        Min = Vector3.Min(Min, point);
        Max = Vector3.Max(Max, point);
    }

    public void Triangle(Vector3 a, Vector3 b, Vector3 c, TMaterial material)
    {
        EnsureCapacity(Count + 3);
        Vertex(a, material);
        Vertex(b, material);
        Vertex(c, material);
    }

    /// <summary>A flat disk or a depth-rounded dome, centered in XY.</summary>
    public void Disk(Vector3 center, float radius, TMaterial material, bool dome = false)
    {
        ArgGuard.ThrowIfNotFinite(center);
        ArgGuard.ThrowIfNotFiniteOrNegative(radius);
        if (radius == 0f) return;
        var segments = dome ? 48 : 12;
        var rings = dome ? 8 : 1;
        Vector3 At(int ring, int index)
        {
            var angle = index / (float)segments * MathF.Tau;
            var ringRadius = radius * ring / rings;
            return center + new Vector3(VertexGenerator2D.PointOnEllipse(Vector2.Zero, new(ringRadius), angle),
                dome ? -MathF.Sqrt(MathF.Max(0f, radius * radius - ringRadius * ringRadius)) : 0f);
        }
        for (var ring = 1; ring <= rings; ring++)
            for (var index = 0; index < segments; index++)
            {
                Triangle(At(ring - 1, index), At(ring, index), At(ring, index + 1), material);
                if (ring > 1)
                    Triangle(At(ring - 1, index), At(ring, index + 1), At(ring - 1, index + 1), material);
            }
    }

    /// <summary>Fills an ellipse after placing its 2D samples on a 3D surface.</summary>
    public void Ellipse(Vector2 center, Vector2 radii, Func<Vector2, Vector3> place,
        TMaterial material, int segments = 24)
    {
        ArgGuard.ThrowIfNotFinite(center);
        ArgGuard.ThrowIfNotFiniteOrNegative(radii);
        ArgGuard.ThrowIfNull(place);
        ArgGuard.ThrowIf(segments < 3, "An ellipse needs at least three segments.", nameof(segments));
        var placedCenter = place(center);
        for (var index = 0; index < segments; index++)
        {
            var start = VertexGenerator2D.PointOnEllipse(center, radii, index * MathF.Tau / segments);
            var end = VertexGenerator2D.PointOnEllipse(center, radii, (index + 1) * MathF.Tau / segments);
            Triangle(placedCenter, place(start), place(end), material);
        }
    }

    /// <summary>A constant-width segment with round caps, measured in XY.</summary>
    public void Line(Vector3 start, Vector3 end, float width, TMaterial material)
    {
        ArgGuard.ThrowIfNotFinite(start);
        ArgGuard.ThrowIfNotFinite(end);
        ArgGuard.ThrowIfNotFiniteOrNegative(width);
        if (width == 0f) return;
        var direction = new Vector2(end.X - start.X, end.Y - start.Y);
        var length = direction.Length();
        var radius = width / 2f;
        if (length > 1e-8f)
        {
            var offset = new Vector3(-direction.Y / length * radius, direction.X / length * radius, 0f);
            Triangle(start + offset, start - offset, end + offset, material);
            Triangle(start - offset, end - offset, end + offset, material);
        }
        Disk(start, radius, material);
        Disk(end, radius, material);
    }

    /// <summary>Fills a convex or star-shaped perimeter from its first vertex.</summary>
    public void TriangleFan(IReadOnlyList<Vector3> perimeter, TMaterial material)
    {
        ArgGuard.ThrowIfNull(perimeter);
        for (var index = 1; index < perimeter.Count - 1; index++)
            Triangle(perimeter[0], perimeter[index], perimeter[index + 1], material);
    }

    /// <summary>A polyline with width changing linearly along its distance.</summary>
    public void Path(IReadOnlyList<Vector3> points, TMaterial material, float startWidth, float? endWidth = null)
    {
        ArgGuard.ThrowIfNull(points);
        ArgGuard.ThrowIfNotFiniteOrNegative(startWidth);
        if (endWidth is { } end) ArgGuard.ThrowIfNotFiniteOrNegative(end);
        var total = 0f;
        for (var index = 1; index < points.Count; index++)
            total += Vector3.Distance(points[index - 1], points[index]);
        var distance = 0f;
        for (var index = 1; index < points.Count; index++)
        {
            var step = Vector3.Distance(points[index - 1], points[index]);
            var width = startWidth + ((endWidth ?? startWidth) - startWidth) *
                (distance + step * .5f) / MathF.Max(total, 1e-8f);
            Line(points[index - 1], points[index], width, material);
            distance += step;
        }
    }

    /// <summary>Rounds a center-to-contour sheet into negative Z; the contour is an open sequence.</summary>
    public void Shell(Vector3 center, IReadOnlyList<Vector3> contour, float depth, TMaterial material)
    {
        ArgGuard.ThrowIfNotFinite(center);
        ArgGuard.ThrowIfNull(contour);
        ArgGuard.ThrowIfNotFiniteOrNegative(depth);
        Vector3 At(float radius, int index) => Vector3.Lerp(center, contour[index], radius) -
            new Vector3(0f, 0f, depth * MathF.Sqrt(MathF.Max(0f, 1f - radius * radius)));
        for (var ring = 1; ring <= 5; ring++)
            for (var index = 0; index < contour.Count - 1; index++)
            {
                Triangle(At((ring - 1) / 5f, index), At(ring / 5f, index), At(ring / 5f, index + 1), material);
                if (ring > 1)
                    Triangle(At((ring - 1) / 5f, index), At(ring / 5f, index + 1), At((ring - 1) / 5f, index + 1), material);
            }
    }

    /// <summary>Places an indexed 2D mesh on a 3D surface without exposing its index format.</summary>
    public void Add(TriangleMesh2D mesh, Func<Vector2, Vector3> place, TMaterial material)
    {
        ArgGuard.ThrowIfNull(mesh);
        ArgGuard.ThrowIfNull(place);
        for (var index = 0; index < mesh.TriangleCount; index++)
        {
            var (a, b, c) = mesh.TriangleAt(index);
            Triangle(place(a), place(b), place(c), material);
        }
    }

    private void EnsureCapacity(int required)
    {
        if (required > MaximumVertexCount) throw new InvalidOperationException("Triangle mesh capacity exceeded.");
        if (required > _vertices.Length)
            Array.Resize(ref _vertices, Math.Min(MaximumVertexCount, Math.Max(required, _vertices.Length * 2)));
    }
}
