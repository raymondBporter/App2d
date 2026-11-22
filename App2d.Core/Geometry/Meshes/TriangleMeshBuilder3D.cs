using App2d.Core.Geometry.Primitives;
using App2d.Core.Validation;
using System.Runtime.CompilerServices;

namespace App2d.Core.Geometry.Meshes;

/// <summary>Makes one mesh vertex from a point, the material it is drawn in and a texture coordinate.</summary>
public interface IMeshVertexFactory<TVertex, TMaterial>
{
    static abstract TVertex Create(Vector3f point, TMaterial material, Vector2f uv);
}

/// <summary>
/// Builds an unindexed triangle stream. The vertex factory supplies rendering attributes,
/// so geometry generation has no graphics-library dependency. The factory is a type, not a delegate, so making a
/// vertex can compile to plain code in the builder. A mesh is built one vertex at a time and calls are most of what a
/// vertex costs, so the few small methods on that path are inlined by request and a factory's Create should be too.
/// </summary>
public sealed class TriangleMeshBuilder3D<TVertex, TMaterial, TFactory> where TFactory : IMeshVertexFactory<TVertex, TMaterial>
{
    private const int MaximumVertexCount = 1_048_576;
    private TVertex[] _vertices;

    public TriangleMeshBuilder3D(int initialCapacity = 64)
    {
        Ensure.NonNegative(initialCapacity);
        Ensure.LessThanOrEqual(initialCapacity, MaximumVertexCount);
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
    public Vector3f Min { get; private set; }
    public Vector3f Max { get; private set; }

    public void Clear()
    {
        Version++;
        Count = 0;
        Min = new(float.PositiveInfinity);
        Max = new(float.NegativeInfinity);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Vertex(Vector3f point, TMaterial material, Vector2f uv = default)
    {
        Ensure.Finite(point);
        Ensure.Finite(uv);
        EnsureCapacity(Count + 1);
        var vertex = TFactory.Create(point, material, uv);
        _vertices[Count++] = vertex;
        Version++;
        Min = Vector3f.Min(Min, point);
        Max = Vector3f.Max(Max, point);
    }

    public void Triangle(Vector3f a, Vector3f b, Vector3f c, TMaterial material)
        => Triangle(a, b, c, material, material, material);

    /// <summary>A triangle with independently supplied vertex attributes.</summary>
    public void Triangle(Vector3f a, Vector3f b, Vector3f c, TMaterial first, TMaterial second, TMaterial third)
    {
        EnsureCapacity(Count + 3);
        Vertex(a, first);
        Vertex(b, second);
        Vertex(c, third);
    }

    /// <summary>A flat disk or a depth-rounded dome, centered in XY.</summary>
    public void Disk(Vector3f center, float radius, TMaterial material, bool dome = false)
    {
        Ensure.Finite(center);
        Ensure.NonNegative(radius);
        if (radius == 0f)
        {
            return;
        }

        if (!dome)
        {
            // A flat disk is a fan round its centre. It is written out plainly because it is drawn at every end and corner of
            // every line: as a local function taking the centre by reference it is not always inlined, and then costs twice as much.
            const int sides = 12;
            const int step = VertexGenerator2D.UnitCircleSegments / sides;
            var previous = center + new Vector3f(VertexGenerator2D.UnitCircle[0] * radius, 0f);
            for (var side = 1; side <= sides; side++)
            {
                var next = center + new Vector3f(VertexGenerator2D.UnitCircle[side * step] * radius, 0f);
                Triangle(center, previous, next, material);
                previous = next;
            }
            return;
        }

        const int segments = 48;
        const int rings = 8;
        const int stride = VertexGenerator2D.UnitCircleSegments / segments;
        Vector3f At(int ring, int index)
        {
            var ringRadius = radius * ring / rings;
            return center + new Vector3f(VertexGenerator2D.UnitCircle[index * stride] * ringRadius,
                -MathF.Sqrt(MathF.Max(0f, radius * radius - ringRadius * ringRadius)));
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
    public void Ellipse(Vector2f center, Vector2f radii, Func<Vector2f, Vector3f> place,
        TMaterial material, int segments = 24)
    {
        Ensure.Finite(center);
        Ensure.NonNegative(radii);
        Ensure.ThrowIfNull(place);
        Ensure.GreaterThanOrEqual(segments, 3);
        var placedCenter = place(center);
        for (var index = 0; index < segments; index++)
        {
            var start = VertexGenerator2D.PointOnEllipse(center, radii, index * MathF.Tau / segments);
            var end = VertexGenerator2D.PointOnEllipse(center, radii, (index + 1) * MathF.Tau / segments);
            Triangle(placedCenter, place(start), place(end), material);
        }
    }

    /// <summary>A constant-width segment with round caps, measured in XY.</summary>
    public void Line(Vector3f start, Vector3f end, float width, TMaterial material)
        => Line(start, end, width, material, material);

    public void Line(Vector3f start, Vector3f end, float width, TMaterial startMaterial, TMaterial endMaterial)
    {
        Ensure.Finite(start);
        Ensure.Finite(end);
        Ensure.NonNegative(width);
        if (width == 0f)
        {
            return;
        }

        var radius = width / 2f;
        Band(start, end, radius, startMaterial, endMaterial);
        Disk(start, radius, startMaterial);
        Disk(end, radius, endMaterial);
    }

    /// <summary>
    /// Constant-width straight segments through the points, measured in XY and round at every point. It covers exactly what
    /// a <see cref="Line(Vector3f, Vector3f, float, TMaterial)"/> per segment covers, with the disk two segments share drawn once.
    /// </summary>
    public void Polyline(ReadOnlySpan<Vector3f> points, float width, TMaterial material, bool closed = false)
    {
        Ensure.NonNegative(width);
        if (width == 0f)
        {
            return;
        }

        var radius = width / 2f;
        foreach (var (a, b) in MeshTopology.Edges(points.Length, closed))
        {
            Band(points[a], points[b], radius, material, material);
        }
        foreach (var point in points)
        {
            Disk(point, radius, material);
        }
    }

    /// <summary>The straight part of a segment: a rectangle of the given half-width in XY. A segment with no length has none.</summary>
    private void Band(Vector3f start, Vector3f end, float radius, TMaterial startMaterial, TMaterial endMaterial)
    {
        var direction = new Vector2f(end.X - start.X, end.Y - start.Y);
        var length = direction.Length();
        if (length <= 1e-8f)
        {
            return;
        }

        var offset = new Vector3f(-direction.Y / length * radius, direction.X / length * radius, 0f);
        Triangle(start + offset, start - offset, end + offset, startMaterial, startMaterial, endMaterial);
        Triangle(start - offset, end - offset, end + offset, startMaterial, endMaterial, endMaterial);
    }

    /// <summary>Fills a perimeter visible from its first vertex using a triangle fan.</summary>
    public void TriangleFan(IReadOnlyList<Vector3f> perimeter, TMaterial material)
    {
        Ensure.ThrowIfNull(perimeter);
        foreach (var (a, b, c) in MeshTopology.TriangleFan(perimeter.Count))
        {
            Triangle(perimeter[a], perimeter[b], perimeter[c], material);
        }
    }

    /// <summary>A polyline with width changing linearly along its distance.</summary>
    public void Path(IReadOnlyList<Vector3f> points, TMaterial material, float startWidth, float? endWidth = null, IReadOnlyList<TMaterial>? pointMaterials = null)
    {
        Ensure.ThrowIfNull(points);
        Ensure.NonNegative(startWidth);
        if (pointMaterials is not null && pointMaterials.Count != points.Count)
            throw new ArgumentException("Provide one material per path point.", nameof(pointMaterials));
        if (endWidth is { } end)
        {
            Ensure.NonNegative(end);
        }

        var total = 0f;
        foreach (var (a, b) in MeshTopology.Edges(points.Count, closed: false))
        {
            total += Vector3f.Distance(points[a], points[b]);
        }

        var distance = 0f;
        foreach (var (a, b) in MeshTopology.Edges(points.Count, closed: false))
        {
            var step = Vector3f.Distance(points[a], points[b]);
            var width = startWidth + ((endWidth ?? startWidth) - startWidth) *
                (distance + step * .5f) / MathF.Max(total, 1e-8f);
            Line(points[a], points[b], width, pointMaterials is null ? material : pointMaterials[a], pointMaterials is null ? material : pointMaterials[b]);
            distance += step;
        }
    }

    /// <summary>A constant-width ribbon in XY with shared cross-sections and round end caps. Intended for sampled smooth paths.</summary>
    public void Ribbon(IReadOnlyList<Vector3f> points, TMaterial material, float width, IReadOnlyList<TMaterial>? pointMaterials = null)
    {
        Ensure.RequireNotNull(points);
        Ensure.NonNegative(width);
        if (pointMaterials is not null && pointMaterials.Count != points.Count)
            throw new ArgumentException("Provide one material per ribbon point.", nameof(pointMaterials));
        if (points.Count == 0 || width == 0) return;
        var radius = width / 2;
        Vector3f Offset(int i)
        {
            var delta = points[Math.Min(i + 1, points.Count - 1)] - points[Math.Max(0, i - 1)];
            var tangent = new Vector2f(delta.X, delta.Y);
            if (tangent.LengthSquared() < 1e-12f && i + 1 < points.Count)
            { delta = points[i + 1] - points[i]; tangent = new(delta.X, delta.Y); }
            tangent = tangent.LengthSquared() > 1e-12f ? Vector2f.Normalize(tangent) : Vector2f.UnitY;
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
    public void Shell(Vector3f center, IReadOnlyList<Vector3f> contour, float depth, TMaterial material)
    {
        Ensure.Finite(center);
        Ensure.ThrowIfNull(contour);
        Ensure.NonNegative(depth);
        Vector3f At(float radius, int index) => Vector3f.Lerp(center, contour[index], radius) -
            new Vector3f(0f, 0f, depth * MathF.Sqrt(MathF.Max(0f, 1f - radius * radius)));
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
    public void Add(TriangleMesh2D mesh, Func<Vector2f, Vector3f> place, TMaterial material)
    {
        Ensure.ThrowIfNull(mesh);
        Ensure.ThrowIfNull(place);
        foreach (var (a, b, c) in mesh.Triangles)
        {
            Triangle(place(a), place(b), place(c), material);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureCapacity(int required)
    {
        if (required > _vertices.Length)
        {
            Grow(required);
        }
    }

    /// <summary>The rare path, kept out of line so the check made before every vertex stays small. The buffer never exceeds the maximum, so a count past it always arrives here.</summary>
    private void Grow(int required)
    {
        Ensure.LessThanOrEqual(required, MaximumVertexCount);
        Array.Resize(ref _vertices, Math.Min(MaximumVertexCount, Math.Max(required, _vertices.Length * 2)));
    }
}
