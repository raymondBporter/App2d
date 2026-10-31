using App2d.Core.Characters.Geometry;
using App2d.Core.Geometry.Meshes;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using XVector2 = Microsoft.Xna.Framework.Vector2;
using XVector3 = Microsoft.Xna.Framework.Vector3;

namespace App2d.Core.Rendering.Characters;

/// <summary>Physical vertex position and independent compositing layer.</summary>
public readonly record struct CharacterVertex(XVector3 Position, Color4b Color, XVector2 TextureCoordinate, float Layer);

/// <summary>Character geometry. Only the fixed-camera GPU adapter encodes Layer into depth.</summary>
public sealed class CharacterMesh(int initialCapacity = 32768)
{
    private readonly record struct Surface(Color4b Color, float Layer);
    private readonly TriangleMeshBuilder3D<CharacterVertex, Surface> _geometry =
        new((p, surface, uv) => new(new(p.X, p.Y, p.Z), surface.Color, new(uv.X, uv.Y), surface.Layer), initialCapacity);
    private VertexPositionColorTexture[] _buffer = [];
    private long _bufferVersion = -1;

    /// <summary>Layer offset for subsequently emitted primitives. It never changes positions or bounds.</summary>
    public float Layer { get; set; }
    public ReadOnlySpan<CharacterVertex> Vertices => _geometry.Vertices;
    internal VertexPositionColorTexture[] Buffer
    {
        get
        {
            if (_buffer.Length < Count) Array.Resize(ref _buffer, _geometry.GetBuffer().Length);
            if (_bufferVersion != Version)
            {
                for (var i = 0; i < Count; i++)
                {
                    var v = Vertices[i];
                    _buffer[i] = new(v.Position + new XVector3(0, 0, v.Layer), v.Color, v.TextureCoordinate);
                }
                _bufferVersion = Version;
            }
            return _buffer;
        }
    }
    public int Count => _geometry.Count;
    public long Version => _geometry.Version;
    public int TriangleCount => _geometry.TriangleCount;
    public Vector3 Min => _geometry.Min;
    public Vector3 Max => _geometry.Max;

    public void Clear() { _geometry.Clear(); Layer = 0; }
    public void Vertex(Vector3 point, Color4b color, Vector2 uv = default, float layer = 0) => _geometry.Vertex(point, new(color, Layer + layer), uv);
    public void Triangle(Vector3 a, Vector3 b, Vector3 c, Color4b color) => _geometry.Triangle(a, b, c, new(color, Layer));
    public void Triangle(Vector3 a, Vector3 b, Vector3 c, Color4b color, float firstLayer, float secondLayer, float thirdLayer) =>
        _geometry.Triangle(a, b, c, new(color, Layer + firstLayer), new(color, Layer + secondLayer), new(color, Layer + thirdLayer));
    public void Disk(Vector3 center, float radius, Color4b color, bool dome = false) => _geometry.Disk(center, radius, new(color, Layer), dome);
    public void Ellipse(Vector2 center, Vector2 radii, Func<Vector2, Vector3> place, Color4b color, int segments = 24) =>
        _geometry.Ellipse(center, radii, place, new(color, Layer), segments);
    public void Line(Vector3 start, Vector3 end, float width, Color4b color) => _geometry.Line(start, end, width, new(color, Layer));
    public void Line(Vector3 start, Vector3 end, float width, Color4b color, float startLayer, float endLayer) =>
        _geometry.Line(start, end, width, new(color, Layer + startLayer), new(color, Layer + endLayer));

    public void Polygon(IReadOnlyList<Vector3> points, Color4b? fill, Color4b? ink, float width)
    {
        if (fill is { } color)
        {
            foreach (var (a, b, c) in MeshTopology.TriangleFan(points.Count))
                if (points is PartContour contour) Triangle(points[a], points[b], points[c], color, contour.LayerAt(a), contour.LayerAt(b), contour.LayerAt(c));
                else Triangle(points[a], points[b], points[c], color);
        }
        if (ink is { } outline)
        {
            foreach (var (a, b) in MeshTopology.Edges(points.Count))
            {
                var first = (points as PartContour)?.LayerAt(a) ?? 0;
                var second = (points as PartContour)?.LayerAt(b) ?? 0;
                Line(points[a], points[b], width, outline, first - .0005f, second - .0005f);
            }
        }
    }
    private Surface[]? Attributes(IReadOnlyList<Vector3> points, Color4b color) => points is PartContour contour
        ? [.. Enumerable.Range(0, points.Count).Select(i => new Surface(color, Layer + contour.LayerAt(i)))] : null;
    public void Path(IReadOnlyList<Vector3> points, Color4b color, float startWidth, float? endWidth = null) =>
        _geometry.Path(points, new(color, Layer), startWidth, endWidth, Attributes(points, color));
    public void Ribbon(IReadOnlyList<Vector3> points, Color4b color, float width)
    {
        _geometry.Ribbon(points, new(color, Layer), width, Attributes(points, color));
    }
    public void Shell(Vector3 center, IReadOnlyList<Vector3> contour, float depth, Color4b color) =>
        _geometry.Shell(center, contour, depth, new(color, Layer));
    public void Add(TriangleMesh2D mesh, Func<Vector2, Vector3> place, Color4b color) => _geometry.Add(mesh, place, new(color, Layer));

    /// <summary>Copies real positions to the generic 3D renderer without adding drawing layers to Z.</summary>
    public void CopySpatialTo(TriangleMeshBuilder3D<VertexPositionColorTexture, Color4b> destination)
    {
        destination.Clear();
        foreach (var v in Vertices)
            destination.Vertex(new(v.Position.X, v.Position.Y, v.Position.Z), v.Color, new(v.TextureCoordinate.X, v.TextureCoordinate.Y));
    }
}
