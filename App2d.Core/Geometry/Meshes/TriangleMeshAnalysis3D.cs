using App2d.Core.Validation;
using System.Runtime.CompilerServices;

namespace App2d.Core.Geometry.Meshes;

/// <summary>An undirected edge between two vertex indices, stored in ascending order.</summary>
public readonly record struct IndexedEdge3D
{
    public IndexedEdge3D(int first, int second)
    {
        Ensure.NonNegative(first);
        Ensure.NonNegative(second);
        Ensure.NotEqual(first, second);
        A = Math.Min(first, second);
        B = Math.Max(first, second);
    }

    public int A { get; }
    public int B { get; }
}

/// <summary>
/// Which faces meet at each edge of an indexed triangle mesh. It depends only on the indices, so it never changes as the
/// mesh moves and is worked out once per mesh; what does change, the face normals, is passed in.
/// </summary>
public sealed class IndexedMeshTopology3D
{
    private static readonly ConditionalWeakTable<IndexedMesh3D, IndexedMeshTopology3D> Cached = [];
    private readonly int[] _triangles;
    private readonly IndexedEdge3D[] _edges;
    // The faces of edge i are _edgeFaces[_firstFace[i].._firstFace[i + 1]].
    private readonly int[] _firstFace;
    private readonly int[] _edgeFaces;

    /// <summary>Edges are kept in the order the faces first use them. A face that names a vertex twice has no edge between the two.</summary>
    public IndexedMeshTopology3D(IReadOnlyList<int> triangleIndices, int vertexCount)
    {
        Ensure.ThrowIfNull(triangleIndices);
        Ensure.ThrowIf(triangleIndices.Count % 3 != 0, "Triangle indices must form complete triples.", nameof(triangleIndices));
        _triangles = [.. triangleIndices];
        var faces = new Dictionary<IndexedEdge3D, List<int>>();
        void Add(int first, int second, int face)
        {
            if (first == second) return;
            var edge = new IndexedEdge3D(first, second);
            if (!faces.TryGetValue(edge, out var list)) faces[edge] = list = [];
            if (!list.Contains(face)) list.Add(face);
        }
        for (var face = 0; face < FaceCount; face++)
        {
            var (a, b, c) = (_triangles[face * 3], _triangles[face * 3 + 1], _triangles[face * 3 + 2]);
            Ensure.ThrowIf((uint)a >= (uint)vertexCount || (uint)b >= (uint)vertexCount || (uint)c >= (uint)vertexCount, "Triangle index is outside the vertices.", nameof(triangleIndices));
            Add(a, b, face);
            Add(b, c, face);
            Add(c, a, face);
        }
        _edges = [.. faces.Keys];
        _firstFace = new int[_edges.Length + 1];
        var all = new List<int>();
        for (var i = 0; i < _edges.Length; i++)
        {
            _firstFace[i] = all.Count;
            all.AddRange(faces[_edges[i]]);
        }
        _firstFace[^1] = all.Count;
        _edgeFaces = [.. all];
    }

    /// <summary>The topology of a mesh, worked out the first time it is asked for.</summary>
    public static IndexedMeshTopology3D Of(IndexedMesh3D mesh) => Cached.GetValue(mesh, static mesh => new(mesh.Triangles, mesh.Vertices.Count));

    public int FaceCount => _triangles.Length / 3;
    /// <summary>Three vertex indices per face.</summary>
    public ReadOnlySpan<int> Triangles => _triangles;

    /// <summary>
    /// Writes each face's unit normal for the vertices where they are now, and whether it has one: a face with no area has
    /// none. <paramref name="handedness"/> is +1 for winding normals or -1 when a reflected placement reverses winding.
    /// </summary>
    public void FaceNormals(ReadOnlySpan<Vector3f> vertices, int handedness, Span<Vector3f> normals, Span<bool> valid)
    {
        Ensure.ThrowIf(handedness is not (1 or -1), "Handedness must be +1 or -1.", nameof(handedness));
        for (var face = 0; face < FaceCount; face++)
        {
            valid[face] = new Triangle3D(vertices[_triangles[face * 3]], vertices[_triangles[face * 3 + 1]], vertices[_triangles[face * 3 + 2]]).TryGetUnitNormal(out var normal);
            normals[face] = valid[face] ? normal * handedness : default;
        }
    }

    /// <summary>
    /// Adds the visible boundary, silhouette and crease edges to <paramref name="into"/>. View direction points toward the
    /// viewer; creaseCosine is the maximum dot product between normals that still counts as a crease. Faces without a normal
    /// are left out, as if they were not there.
    /// </summary>
    public void FeatureEdges(ReadOnlySpan<Vector3f> normals, ReadOnlySpan<bool> valid, Vector3f viewDirection, float creaseCosine, float frontFacingThreshold, List<IndexedEdge3D> into)
    {
        Ensure.NonZero(viewDirection);
        Ensure.InRange(creaseCosine, -1f, 1f);
        Ensure.NonNegative(frontFacingThreshold);
        var wideView = viewDirection.ToVector3d();
        var view = (wideView / wideView.Length()).ToVector3f();
        for (var i = 0; i < _edges.Length; i++)
        {
            var (front, back, count, first, second) = (false, false, 0, 0, 0);
            for (var k = _firstFace[i]; k < _firstFace[i + 1]; k++)
            {
                var face = _edgeFaces[k];
                if (!valid[face]) continue;
                if (count == 0) first = face;
                else if (count == 1) second = face;
                count++;
                if (Vector3f.Dot(normals[face], view) > frontFacingThreshold) front = true;
                else back = true;
            }
            if (!front) continue;
            var crease = count == 2 && Vector3f.Dot(normals[first], normals[second]) < creaseCosine;
            if (count == 1 || back || crease) into.Add(_edges[i]);
        }
    }
}

/// <summary>Face normals and edge adjacency for an indexed 3D triangle mesh at one placement.</summary>
public sealed class TriangleMeshAnalysis3D
{
    private readonly IndexedMeshTopology3D _topology;
    private readonly Vector3f[] _faceNormals;
    private readonly bool[] _validFaces;

    /// <param name="handedness">+1 for winding normals or -1 when a reflected placement reverses winding.</param>
    public TriangleMeshAnalysis3D(IReadOnlyList<Vector3f> vertices, IReadOnlyList<int> triangleIndices, int handedness = 1)
    {
        Ensure.ThrowIfNull(vertices);
        foreach (var vertex in vertices)
        {
            Ensure.Finite(vertex, nameof(vertices));
        }

        _topology = new(triangleIndices, vertices.Count);
        _faceNormals = new Vector3f[_topology.FaceCount];
        _validFaces = new bool[_topology.FaceCount];
        _topology.FaceNormals(vertices as Vector3f[] ?? [.. vertices], handedness, _faceNormals, _validFaces);
    }

    public int FaceCount => _faceNormals.Length;

    /// <summary>Returns false and zero for a degenerate face.</summary>
    public bool TryGetFaceNormal(int faceIndex, out Vector3f normal)
    {
        Ensure.Index(faceIndex, _faceNormals.Length);

        normal = _faceNormals[faceIndex];
        return _validFaces[faceIndex];
    }

    /// <summary>
    /// The visible boundary, silhouette, and crease edges. View direction points toward the viewer;
    /// creaseCosine is the maximum dot product between normals that still counts as a crease.
    /// </summary>
    public IReadOnlyList<IndexedEdge3D> FeatureEdges(Vector3f viewDirection, float creaseCosine, float frontFacingThreshold = 0f)
    {
        var edges = new List<IndexedEdge3D>();
        _topology.FeatureEdges(_faceNormals, _validFaces, viewDirection, creaseCosine, frontFacingThreshold, edges);
        return edges;
    }
}
