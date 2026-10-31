using App2d.Core.Geometry.Operations.Vertices;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry.Meshes;

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
    /// <summary>Changes whenever vertices are written or cleared, including rebuilds with the same count.</summary>
    public long Version { get; private set; }
    /// <summary>Returns the backing array for submission. Callers must not modify it.</summary>
    public TVertex[] GetBuffer() => _vertices;
    public int Count { get; private set; }
    public int TriangleCount => Count / 3;
    public Vector3 Min { get; private set; }
    public Vector3 Max { get; private set; }

    public void Clear()
    {
        Version++;
        Count = 0;
        Min = new(float.PositiveInfinity);
        Max = new(float.NegativeInfinity);
    }

    public void Vertex(Vector3 point, TMaterial material, Vector2 uv = default)
    {
        ArgGuard.Finite(point);
        ArgGuard.Finite(uv);
        EnsureCapacity(Count + 1);
        var vertex = _vertexFactory(point, material, uv);
        _vertices[Count++] = vertex;
        Version++;
        Min = Vector3.Min(Min, point);
        Max = Vector3.Max(Max, point);
    }

    public void Triangle(Vector3 a, Vector3 b, Vector3 c, TMaterial material)
        => Triangle(a, b, c, material, material, material);

    /// <summary>A triangle with independently supplied vertex attributes.</summary>
    public void Triangle(Vector3 a, Vector3 b, Vector3 c, TMaterial first, TMaterial second, TMaterial third)
    {
        EnsureCapacity(Count + 3);
        Vertex(a, first);
        Vertex(b, second);
        Vertex(c, third);
    }

    /// <summary>A flat disk or a depth-rounded dome, centered in XY.</summary>
    public void Disk(Vector3 center, float radius, TMaterial material, bool dome = false)
    {
        ArgGuard.Finite(center);
        ArgGuard.NonNegative(radius);
        if (radius == 0f)
        {
            return;
        }

        var segments = dome ? 48 : 12;
        var rings = dome ? 8 : 1;
        var stride = VertexGenerator2D.UnitCircleSegments / segments;
        Vector3 At(int ring, int index)
        {
            var ringRadius = radius * ring / rings;
            return center + new Vector3(VertexGenerator2D.UnitCircle[index * stride] * ringRadius,
                dome ? -MathF.Sqrt(MathF.Max(0f, radius * radius - ringRadius * ringRadius)) : 0f);
        }
        for (var ring = 1; ring <= rings; ring++)
        {
            for (var index = 0; index < segments; index++)
            {
                Triangle(At(ring - 1, index), At(ring, index), At(ring, index + 1), material);
                if (ring > 1)
                {
                    Triangle(At(ring - 1, index), At(ring, index + 1), At(ring - 1, index + 1), material);
                }
            }
        }
    }

    /// <summary>Fills an ellipse after placing its 2D samples on a 3D surface.</summary>
    public void Ellipse(Vector2 center, Vector2 radii, Func<Vector2, Vector3> place,
        TMaterial material, int segments = 24)
    {
        ArgGuard.Finite(center);
        ArgGuard.NonNegative(radii);
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
        => Line(start, end, width, material, material);

    public void Line(Vector3 start, Vector3 end, float width, TMaterial startMaterial, TMaterial endMaterial)
    {
        ArgGuard.Finite(start);
        ArgGuard.Finite(end);
        ArgGuard.NonNegative(width);
        if (width == 0f)
        {
            return;
        }

        var direction = new Vector2(end.X - start.X, end.Y - start.Y);
        var length = direction.Length();
        var radius = width / 2f;
        if (length > 1e-8f)
        {
            var offset = new Vector3(-direction.Y / length * radius, direction.X / length * radius, 0f);
            Triangle(start + offset, start - offset, end + offset, startMaterial, startMaterial, endMaterial);
            Triangle(start - offset, end - offset, end + offset, startMaterial, endMaterial, endMaterial);
        }
        Disk(start, radius, startMaterial);
        Disk(end, radius, endMaterial);
    }

    /// <summary>Fills a perimeter visible from its first vertex using a triangle fan.</summary>
    public void TriangleFan(IReadOnlyList<Vector3> perimeter, TMaterial material)
    {
        ArgGuard.ThrowIfNull(perimeter);
        foreach (var (a, b, c) in MeshTopology.TriangleFan(perimeter.Count))
        {
            Triangle(perimeter[a], perimeter[b], perimeter[c], material);
        }
    }

    /// <summary>A polyline with width changing linearly along its distance.</summary>
    public void Path(IReadOnlyList<Vector3> points, TMaterial material, float startWidth, float? endWidth = null, IReadOnlyList<TMaterial>? pointMaterials = null)
    {
        ArgGuard.ThrowIfNull(points);
        ArgGuard.NonNegative(startWidth);
        if (pointMaterials is not null && pointMaterials.Count != points.Count)
            throw new ArgumentException("Provide one material per path point.", nameof(pointMaterials));
        if (endWidth is { } end)
        {
            ArgGuard.NonNegative(end);
        }

        var total = 0f;
        foreach (var (a, b) in MeshTopology.Edges(points.Count, closed: false))
        {
            total += Vector3.Distance(points[a], points[b]);
        }

        var distance = 0f;
        foreach (var (a, b) in MeshTopology.Edges(points.Count, closed: false))
        {
            var step = Vector3.Distance(points[a], points[b]);
            var width = startWidth + ((endWidth ?? startWidth) - startWidth) *
                (distance + step * .5f) / MathF.Max(total, 1e-8f);
            Line(points[a], points[b], width, pointMaterials is null ? material : pointMaterials[a], pointMaterials is null ? material : pointMaterials[b]);
            distance += step;
        }
    }

    /// <summary>A constant-width ribbon in XY with shared cross-sections and round end caps. Intended for sampled smooth paths.</summary>
    public void Ribbon(IReadOnlyList<Vector3> points, TMaterial material, float width, IReadOnlyList<TMaterial>? pointMaterials = null)
    {
        ArgGuard.RequireNotNull(points);
        ArgGuard.NonNegative(width);
        if (pointMaterials is not null && pointMaterials.Count != points.Count)
            throw new ArgumentException("Provide one material per ribbon point.", nameof(pointMaterials));
        if (points.Count == 0 || width == 0) return;
        var radius = width / 2;
        Vector3 Offset(int i)
        {
            var delta = points[Math.Min(i + 1, points.Count - 1)] - points[Math.Max(0, i - 1)];
            var tangent = new Vector2(delta.X, delta.Y);
            if (tangent.LengthSquared() < 1e-12f && i + 1 < points.Count)
            { delta = points[i + 1] - points[i]; tangent = new(delta.X, delta.Y); }
            tangent = tangent.LengthSquared() > 1e-12f ? Vector2.Normalize(tangent) : Vector2.UnitY;
            return new(-tangent.Y * radius, tangent.X * radius, 0);
        }
        var previous = Offset(0);
        for (var i = 1; i < points.Count; i++)
        {
            var next = Offset(i);
            var startMaterial = pointMaterials is null ? material : pointMaterials[i - 1];
            var endMaterial = pointMaterials is null ? material : pointMaterials[i];
            Triangle(points[i - 1] + previous, points[i - 1] - previous, points[i] + next, startMaterial, startMaterial, endMaterial);
            Triangle(points[i - 1] - previous, points[i] - next, points[i] + next, startMaterial, endMaterial, endMaterial);
            previous = next;
        }
        Disk(points[0], radius, pointMaterials is null ? material : pointMaterials[0]);
        Disk(points[^1], radius, pointMaterials is null ? material : pointMaterials[^1]);
    }

    /// <summary>Rounds a center-to-contour sheet into negative Z; the contour is an open sequence.</summary>
    public void Shell(Vector3 center, IReadOnlyList<Vector3> contour, float depth, TMaterial material)
    {
        ArgGuard.Finite(center);
        ArgGuard.ThrowIfNull(contour);
        ArgGuard.NonNegative(depth);
        Vector3 At(float radius, int index) => Vector3.Lerp(center, contour[index], radius) -
            new Vector3(0f, 0f, depth * MathF.Sqrt(MathF.Max(0f, 1f - radius * radius)));
        for (var ring = 1; ring <= 5; ring++)
        {
            for (var index = 0; index < contour.Count - 1; index++)
            {
                Triangle(At((ring - 1) / 5f, index), At(ring / 5f, index), At(ring / 5f, index + 1), material);
                if (ring > 1)
                {
                    Triangle(At((ring - 1) / 5f, index), At(ring / 5f, index + 1), At((ring - 1) / 5f, index + 1), material);
                }
            }
        }
    }

    /// <summary>Places an indexed 2D mesh on a 3D surface without exposing its index format.</summary>
    public void Add(TriangleMesh2D mesh, Func<Vector2, Vector3> place, TMaterial material)
    {
        ArgGuard.ThrowIfNull(mesh);
        ArgGuard.ThrowIfNull(place);
        foreach (var (a, b, c) in mesh.Triangles)
        {
            Triangle(place(a), place(b), place(c), material);
        }
    }

    private void EnsureCapacity(int required)
    {
        if (required > MaximumVertexCount)
        {
            throw new InvalidOperationException("Triangle mesh capacity exceeded.");
        }

        if (required > _vertices.Length)
        {
            Array.Resize(ref _vertices, Math.Min(MaximumVertexCount, Math.Max(required, _vertices.Length * 2)));
        }
    }
}
