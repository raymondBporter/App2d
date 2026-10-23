using App2d.Core.Geometry.Meshes;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using XVector2 = Microsoft.Xna.Framework.Vector2;
using XVector3 = Microsoft.Xna.Framework.Vector3;

namespace App2d.Core.Rendering.Characters;

/// <summary>XNA vertex adapter for reusable triangle geometry in character units.</summary>
public sealed class CharacterMesh(int initialCapacity = 32768)
{
    private readonly TriangleMeshBuilder3D<VertexPositionColorTexture, Color4b> _geometry =
        new((point, color, uv) => new(new XVector3(point.X, point.Y, point.Z), color, new XVector2(uv.X, uv.Y)), initialCapacity);

    public ReadOnlySpan<VertexPositionColorTexture> Vertices => _geometry.Vertices;
    internal VertexPositionColorTexture[] Buffer => _geometry.GetBuffer();
    public int Count => _geometry.Count;
    public long Version => _geometry.Version;
    public int TriangleCount => _geometry.TriangleCount;
    public Vector3 Min => _geometry.Min;
    public Vector3 Max => _geometry.Max;

    public void Clear() => _geometry.Clear();
    public void Vertex(Vector3 point, Color4b color, Vector2 uv = default) => _geometry.Vertex(point, color, uv);
    public void Triangle(Vector3 a, Vector3 b, Vector3 c, Color4b color) => _geometry.Triangle(a, b, c, color);
    public void Disk(Vector3 center, float radius, Color4b color, bool dome = false) => _geometry.Disk(center, radius, color, dome);
    public void Ellipse(Vector2 center, Vector2 radii, Func<Vector2, Vector3> place, Color4b color, int segments = 24) =>
        _geometry.Ellipse(center, radii, place, color, segments);
    public void Line(Vector3 start, Vector3 end, float width, Color4b color) => _geometry.Line(start, end, width, color);

    public void Polygon(IReadOnlyList<Vector3> points, Color4b? fill, Color4b? ink, float width)
    {
        if (fill is { } color)
        {
            _geometry.TriangleFan(points, color);
        }

        if (ink is { } outline)
        {
            foreach (var (a, b) in MeshTopology.Edges(points.Count))
            {
                _geometry.Line(points[a] - new Vector3(0f, 0f, .0005f),
                    points[b] - new Vector3(0f, 0f, .0005f), width, outline);
            }
        }
    }

    public void Path(IReadOnlyList<Vector3> points, Color4b color, float startWidth, float? endWidth = null) =>
        _geometry.Path(points, color, startWidth, endWidth);

    public void Shell(Vector3 center, IReadOnlyList<Vector3> contour, float depth, Color4b color) =>
        _geometry.Shell(center, contour, depth, color);

    public void Add(TriangleMesh2D mesh, Func<Vector2, Vector3> place, Color4b color) =>
        _geometry.Add(mesh, place, color);
}
