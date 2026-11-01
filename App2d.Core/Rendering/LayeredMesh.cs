using App2d.Core.Geometry.Meshes;
using App2d.Core.Mathematics.Transforms;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using XVector2 = Microsoft.Xna.Framework.Vector2;
using XVector3 = Microsoft.Xna.Framework.Vector3;

namespace App2d.Core.Rendering;

/// <summary>Physical vertex position and independent compositing layer.</summary>
public readonly record struct LayeredVertex(XVector3 Position, Color4b Color, XVector2 TextureCoordinate, float Layer);

/// <summary>
/// A triangle mesh whose vertices carry a draw-order layer beside their real position, for cutout art drawn by a fixed camera.
/// Only the fixed-camera GPU adapter encodes Layer into depth; positions and bounds never include it.
/// </summary>
public sealed class LayeredMesh(int initialCapacity = 32768)
{
    private readonly record struct Surface(Color4b Color, float Layer);
    private readonly TriangleMeshBuilder3D<LayeredVertex, Surface> _geometry =
        new((p, surface, uv) => new(new(p.X, p.Y, p.Z), surface.Color, new(uv.X, uv.Y), surface.Layer), initialCapacity);
    private VertexPositionColorTexture[] _buffer = [];
    private long _bufferVersion = -1;

    /// <summary>Layer offset for subsequently emitted primitives. It never changes positions or bounds.</summary>
    public float Layer { get; set; }
    public ReadOnlySpan<LayeredVertex> Vertices => _geometry.Vertices;
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

    /// <summary>A fan-filled polygon and/or its outline, all on the current layer.</summary>
    public void Polygon(IReadOnlyList<Vector3> points, Color4b? fill, Color4b? ink, float width) => Polygon(new LayeredContour(points), fill, ink, width);

    /// <summary>A fan-filled polygon and/or its outline, each point on its own layer. The outline sits just in front of the fill.</summary>
    public void Polygon(LayeredContour points, Color4b? fill, Color4b? ink, float width)
    {
        if (fill is { } color)
        {
            foreach (var (a, b, c) in MeshTopology.TriangleFan(points.Count))
            {
                Triangle(points[a], points[b], points[c], color, points.LayerAt(a), points.LayerAt(b), points.LayerAt(c));
            }
        }
        if (ink is { } outline)
        {
            foreach (var (a, b) in MeshTopology.Edges(points.Count))
            {
                Line(points[a], points[b], width, outline, points.LayerAt(a) - .0005f, points.LayerAt(b) - .0005f);
            }
        }
    }
    private Surface[] Attributes(LayeredContour points, Color4b color) => [.. Enumerable.Range(0, points.Count).Select(i => new Surface(color, Layer + points.LayerAt(i)))];
    public void Path(LayeredContour points, Color4b color, float startWidth, float? endWidth = null) =>
        _geometry.Path(points, new(color, Layer), startWidth, endWidth, Attributes(points, color));
    public void Ribbon(LayeredContour points, Color4b color, float width) => _geometry.Ribbon(points, new(color, Layer), width, Attributes(points, color));
    public void Shell(Vector3 center, IReadOnlyList<Vector3> contour, float depth, Color4b color) =>
        _geometry.Shell(center, contour, depth, new(color, Layer));
    public void Add(TriangleMesh2D mesh, Func<Vector2, Vector3> place, Color4b color) => _geometry.Add(mesh, place, new(color, Layer));

    /// <summary>
    /// A placed solid with one shade per face and ink on the silhouette and creases a camera looking down -Z sees. Coplanar
    /// triangulation never becomes ink. <paramref name="tint"/> multiplies after shading. A collapsed placement draws nothing.
    /// </summary>
    public void Solid(IndexedMesh3D solid, Frame3D placement, Color4b? fill, Color4b? ink, float inkWidth, DirectionalLight3D light, Color4b? tint = null)
    {
        if (!Orientation3D.TryGetHandedness(placement.XAxis, placement.YAxis, placement.ZAxis, out var handedness))
        {
            return;
        }

        var vertices = solid.Vertices.Select(placement.TransformPoint).ToArray();
        var analysis = new TriangleMeshAnalysis3D(vertices, solid.Triangles, handedness);
        if (fill is { } color)
        {
            for (var face = 0; face < solid.FaceCount; face++)
            {
                if (analysis.TryGetFaceNormal(face, out var normal))
                {
                    Triangle(vertices[solid.Triangles[face * 3]], vertices[solid.Triangles[face * 3 + 1]], vertices[solid.Triangles[face * 3 + 2]], color.ScaleRgb(light.Shade(normal)).Multiply(tint));
                }
            }
        }
        if (ink is not { } outline || inkWidth <= 0)
        {
            return;
        }

        var layer = Layer;
        Layer -= .0005f;
        foreach (var edge in analysis.FeatureEdges(-Vector3.UnitZ, .7f, 1e-5f))
        {
            Line(vertices[edge.A], vertices[edge.B], inkWidth, outline.Multiply(tint));
        }
        Layer = layer;
    }

    /// <summary>Copies real positions to the generic 3D renderer without adding drawing layers to Z.</summary>
    public void CopySpatialTo(TriangleMeshBuilder3D<VertexPositionColorTexture, Color4b> destination)
    {
        destination.Clear();
        foreach (var v in Vertices)
            destination.Vertex(new(v.Position.X, v.Position.Y, v.Position.Z), v.Color, new(v.TextureCoordinate.X, v.TextureCoordinate.Y));
    }
}
