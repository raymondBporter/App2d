using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Meshes;

/// <summary>An undirected edge between two vertex indices, stored in ascending order.</summary>
public readonly record struct IndexedEdge3D
{
    public IndexedEdge3D(int first, int second)
    {
        ArgGuard.ThrowIf(first < 0 || second < 0 || first == second,
            "An edge needs two distinct nonnegative indices.", nameof(first));
        A = Math.Min(first, second);
        B = Math.Max(first, second);
    }

    public int A { get; }
    public int B { get; }
}

/// <summary>Face normals and edge adjacency for an indexed 3D triangle mesh.</summary>
public sealed class TriangleMeshAnalysis3D
{
    private readonly Vector3[] _faceNormals;
    private readonly bool[] _validFaces;
    private readonly Dictionary<IndexedEdge3D, List<int>> _edgeFaces = [];

    /// <param name="handedness">+1 for winding normals or -1 when a reflected placement reverses winding.</param>
    public TriangleMeshAnalysis3D(IReadOnlyList<Vector3> vertices, IReadOnlyList<int> triangleIndices,
        int handedness = 1)
    {
        ArgGuard.ThrowIfNull(vertices);
        ArgGuard.ThrowIfNull(triangleIndices);
        ArgGuard.ThrowIf(triangleIndices.Count % 3 != 0, "Triangle indices must form complete triples.", nameof(triangleIndices));
        ArgGuard.ThrowIf(handedness is not (1 or -1), "Handedness must be +1 or -1.", nameof(handedness));
        foreach (var vertex in vertices) ArgGuard.ThrowIfNotFinite(vertex, nameof(vertices));

        _faceNormals = new Vector3[triangleIndices.Count / 3];
        _validFaces = new bool[_faceNormals.Length];
        for (var face = 0; face < _faceNormals.Length; face++)
        {
            var i = face * 3;
            var a = triangleIndices[i];
            var b = triangleIndices[i + 1];
            var c = triangleIndices[i + 2];
            ArgGuard.ThrowIf((uint)a >= (uint)vertices.Count || (uint)b >= (uint)vertices.Count ||
                (uint)c >= (uint)vertices.Count, "Triangle index is outside the vertices.", nameof(triangleIndices));
            if (!new Triangle3D(vertices[a], vertices[b], vertices[c]).TryGetUnitNormal(out var normal))
                continue;

            _faceNormals[face] = normal * handedness;
            _validFaces[face] = true;
            AddEdge(a, b, face);
            AddEdge(b, c, face);
            AddEdge(c, a, face);
        }
    }

    public int FaceCount => _faceNormals.Length;

    /// <summary>Returns false and zero for a degenerate face.</summary>
    public bool TryGetFaceNormal(int faceIndex, out Vector3 normal)
    {
        if ((uint)faceIndex >= (uint)_faceNormals.Length) throw new ArgumentOutOfRangeException(nameof(faceIndex));
        normal = _faceNormals[faceIndex];
        return _validFaces[faceIndex];
    }

    /// <summary>
    /// Yields visible boundary, silhouette, and crease edges. View direction points toward the viewer;
    /// creaseCosine is the maximum dot product between normals that still counts as a crease.
    /// </summary>
    public IEnumerable<IndexedEdge3D> FeatureEdges(Vector3 viewDirection, float creaseCosine,
        float frontFacingThreshold = 0f)
    {
        ArgGuard.ThrowIfNotFiniteOrZero(viewDirection);
        ArgGuard.ThrowIf(!float.IsFinite(creaseCosine) || creaseCosine < -1f || creaseCosine > 1f,
            "Crease cosine must be between -1 and 1.", nameof(creaseCosine));
        ArgGuard.ThrowIfNotFiniteOrNegative(frontFacingThreshold);
        var length = Math.Sqrt((double)viewDirection.X * viewDirection.X +
            (double)viewDirection.Y * viewDirection.Y + (double)viewDirection.Z * viewDirection.Z);
        var view = new Vector3((float)(viewDirection.X / length),
            (float)(viewDirection.Y / length), (float)(viewDirection.Z / length));

        foreach (var (edge, faces) in _edgeFaces)
        {
            var front = false;
            var back = false;
            foreach (var face in faces)
            {
                if (Vector3.Dot(_faceNormals[face], view) > frontFacingThreshold) front = true;
                else back = true;
            }
            if (!front) continue;
            var crease = faces.Count == 2 && Vector3.Dot(_faceNormals[faces[0]], _faceNormals[faces[1]]) < creaseCosine;
            if (faces.Count == 1 || back || crease) yield return edge;
        }
    }

    private void AddEdge(int first, int second, int face)
    {
        var edge = new IndexedEdge3D(first, second);
        if (!_edgeFaces.TryGetValue(edge, out var faces)) _edgeFaces[edge] = faces = [];
        faces.Add(face);
    }
}
