using App2d.Core.Meshes;
using App2d.Core.Geometry;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using Color = Microsoft.Xna.Framework.Color;
using XVector2 = Microsoft.Xna.Framework.Vector2;
using XVector3 = Microsoft.Xna.Framework.Vector3;

namespace App2d.Rendering.Characters;

/// <summary>XNA vertex adapter for reusable triangle geometry in character units.</summary>
public sealed class CharacterMesh(int initialCapacity = 32768)
{
    private readonly TriangleMeshBuilder3D<VertexPositionColorTexture, Color> _geometry =
        new((point, color, uv) => new(new XVector3(point.X, point.Y, point.Z), color, new XVector2(uv.X, uv.Y)), initialCapacity);

    public ReadOnlySpan<VertexPositionColorTexture> Vertices => _geometry.Vertices;
    internal VertexPositionColorTexture[] Buffer => _geometry.GetBuffer();
    public int Count => _geometry.Count;
    public int TriangleCount => _geometry.TriangleCount;
    public Vector3 Min => _geometry.Min;
    public Vector3 Max => _geometry.Max;

    public void Clear() => _geometry.Clear();
    public void Vertex(Vector3 point, Color color, Vector2 uv = default) => _geometry.Vertex(point, color, uv);
    public void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color) => _geometry.Triangle(a, b, c, color);
    public void Disk(Vector3 center, float radius, Color color, bool dome = false) => _geometry.Disk(center, radius, color, dome);
    public void Ellipse(Vector2 center, Vector2 radii, Func<Vector2, Vector3> place, Color color, int segments = 24) =>
        _geometry.Ellipse(center, radii, place, color, segments);
    public void Line(Vector3 start, Vector3 end, float width, Color color) => _geometry.Line(start, end, width, color);

    public void Polygon(IReadOnlyList<Vector3> points, Color? fill, Color? ink, float width)
    {
        if (fill is { } color) _geometry.TriangleFan(points, color);
        if (ink is { } outline)
            for (var index = 0; index < points.Count; index++)
                _geometry.Line(points[index] - new Vector3(0f, 0f, .0005f),
                    points[(index + 1) % points.Count] - new Vector3(0f, 0f, .0005f), width, outline);
    }

    public void Path(IReadOnlyList<Vector3> points, Color color, float startWidth, float? endWidth = null) =>
        _geometry.Path(points, color, startWidth, endWidth);

    public void Shell(Vector3 center, IReadOnlyList<Vector3> contour, float depth, Color color) =>
        _geometry.Shell(center, contour, depth, color);

    public void Add(TriangleMesh2D mesh, Func<Vector2, Vector3> place, Color color) =>
        _geometry.Add(mesh, place, color);
}
