using App2d.Core.Geometry.Primitives;
using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry.Meshes;

/// <summary>An indexed set of filled triangles in XY, independent of rendering and authoring.</summary>
public sealed class TriangleMesh2D
{
    private readonly Vector2[] _vertices;
    private readonly int[] _indices;
    private Segment<int>[]? _boundaryEdges;

    public TriangleMesh2D(IEnumerable<Vector2> vertices, IEnumerable<int> indices)
    {
        ArgGuard.ThrowIfNull(vertices);
        ArgGuard.ThrowIfNull(indices);
        _vertices = [.. vertices];
        _indices = [.. indices];
        ArgGuard.ThrowIf(_vertices.Length < 3, "A triangle mesh needs at least three vertices.", nameof(vertices));
        ArgGuard.ThrowIf(_indices.Length == 0 || _indices.Length % 3 != 0,
            "Triangle indices must form complete triples.", nameof(indices));
        foreach (var vertex in _vertices)
        {
            ArgGuard.Finite(vertex, nameof(vertices));
        }

        foreach (var index in _indices)
        {
            ArgGuard.Index(index, _vertices.Length, nameof(indices));
        }

        var area = 0d;
        foreach (var (a, b, c) in Triangles)
        {
            var twice = new Vector2d(a).Orientation(new(b), new(c));
            ArgGuard.ThrowIf(twice == 0, "Triangle vertices must not be collinear.", nameof(indices));
            area += Math.Abs(twice) / 2;
        }
        ArgGuard.ThrowIf(area > float.MaxValue, "Triangle mesh area is too large.", nameof(vertices));
        Area = (float)area;
        Bounds = Rect2D.FromPoints(_vertices);
    }

    public ReadOnlySpan<Vector2> Vertices => _vertices;
    public ReadOnlySpan<int> Indices => _indices;
    /// <summary>A borrowed, allocation-free view of the mesh's vertex triples in index-buffer order.</summary>
    public IndexedTriangleEnumerable<Vector2> Triangles => MeshTopology.IndexedTriangles(_vertices, _indices);
    /// <summary>Cached index topology, independent of vertex positions and rendering.</summary>
    public ReadOnlySpan<Segment<int>> BoundaryEdges => _boundaryEdges ??= MeshTopology.BoundaryEdges(_indices);
    public SegmentVertexEnumerable<Segment<Vector2>, Vector2> BoundarySegments => MeshTopology.IndexedSegments(_vertices, BoundaryEdges);
    public int TriangleCount => _indices.Length / 3;
    /// <summary>The sum of triangle areas; overlaps are counted once per triangle.</summary>
    public float Area { get; }
    public Rect2D Bounds { get; }

    public Triangle<Vector2> TriangleAt(int index)
    {
        ArgGuard.Index(index, TriangleCount);

        var i = index * 3;
        return new(_vertices[_indices[i]], _vertices[_indices[i + 1]], _vertices[_indices[i + 2]]);
    }

    /// <summary>Contains a point when it lies in any triangle, including an edge.</summary>
    public bool ContainsPoint(Vector2 point, double edgeTolerance = 0)
    {
        ArgGuard.Finite(point);
        ArgGuard.NonNegative(edgeTolerance);
        if (edgeTolerance == 0 && !Bounds.Contains(point))
        {
            return false;
        }

        foreach (var triangle in Triangles)
        {
            if (triangle.Contains(point, edgeTolerance))
            {
                return true;
            }
        }
        return false;
    }

}
