using App2d.Core.Geometry.Meshes;
using App2d.Core.Geometry.Meshes.Tessellation;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Validation;
using Microsoft.Xna.Framework.Graphics;
using System.Runtime.CompilerServices;
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
    /// <summary>How much nearer the viewer an outline sits than what it goes round, so it never fights the fill for the same depth.</summary>
    private const float InkLift = .0005f;
    // Working room for sampling a shape's outline and placing it, and for a solid's placed vertices, face normals and inked
    // edges; kept so painting a shape or a solid allocates nothing.
    private Vector2f[] _flat = new Vector2f[64];
    private Vector3f[] _placed = new Vector3f[64];
    private Vector3f[] _normals = new Vector3f[64];
    private bool[] _faces = new bool[64];
    private readonly List<IndexedEdge3D> _edges = [];

    private readonly record struct Surface(Color4b Color, float Layer);
    private readonly struct VertexFactory : IMeshVertexFactory<LayeredVertex, Surface>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LayeredVertex Create(Vector3f point, Surface surface, Vector2f uv) => new(new(point.X, point.Y, point.Z), surface.Color, new(uv.X, uv.Y), surface.Layer);
    }
    private readonly TriangleMeshBuilder3D<LayeredVertex, Surface, VertexFactory> _geometry = new(initialCapacity);
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
    public Vector3f Min => _geometry.Min;
    public Vector3f Max => _geometry.Max;

    public void Clear() { _geometry.Clear(); Layer = 0; }
    public void Vertex(Vector3f point, Color4b color, Vector2f uv = default, float layer = 0) => _geometry.Vertex(point, new(color, Layer + layer), uv);
    public void Triangle(Vector3f a, Vector3f b, Vector3f c, Color4b color) => _geometry.Triangle(a, b, c, new(color, Layer));
    public void Triangle(Vector3f a, Vector3f b, Vector3f c, Color4b color, float firstLayer, float secondLayer, float thirdLayer) =>
        _geometry.Triangle(a, b, c, new(color, Layer + firstLayer), new(color, Layer + secondLayer), new(color, Layer + thirdLayer));
    public void Disk(Vector3f center, float radius, Color4b color, bool dome = false) => _geometry.Disk(center, radius, new(color, Layer), dome);
    public void Ellipse(Vector2f center, Vector2f radii, Func<Vector2f, Vector3f> place, Color4b color, int segments = 24) =>
        _geometry.Ellipse(center, radii, place, new(color, Layer), segments);
    public void Line(Vector3f start, Vector3f end, float width, Color4b color) => _geometry.Line(start, end, width, new(color, Layer));
    public void Line(Vector3f start, Vector3f end, float width, Color4b color, float startLayer, float endLayer) =>
        _geometry.Line(start, end, width, new(color, Layer + startLayer), new(color, Layer + endLayer));

    /// <summary>Straight strokes through the points, round at every point, at <paramref name="layer"/> on top of the mesh's.</summary>
    public void Polyline(ReadOnlySpan<Vector3f> points, float width, Color4b color, bool closed = false, float layer = 0) =>
        _geometry.Polyline(points, width, new(color, Layer + layer), closed);

    /// <summary>
    /// Fills a flat shape standing in a frame, on the current layer. A polygon is filled by its own triangles, so it may be
    /// concave; any other shape is a fan of its outline.
    /// </summary>
    public void Fill(IShape2D shape, Frame3D placement, Color4b color, int roundSegments = 32)
    {
        var surface = new Surface(color, Layer);
        foreach (var triangle in shape.Triangles(Scratch(ref _flat, shape is SimplePolygon2D ? 0 : shape.GetOutlineVertCount(roundSegments)), roundSegments))
        {
            _geometry.Triangle(placement.TransformPoint(new(triangle.A, 0)), placement.TransformPoint(new(triangle.B, 0)), placement.TransformPoint(new(triangle.C, 0)), surface);
        }
    }

    /// <summary>
    /// Draws the outline of a flat shape standing in a frame, just in front of its fill and round at every corner. The width
    /// is measured after placing, so a shape tipped out of the plane keeps an even line.
    /// </summary>
    public void Outline(IShape2D shape, Frame3D placement, Color4b color, float width, int roundSegments = 32)
    {
        var count = shape.GetOutlineVertCount(roundSegments);
        var outline = shape.GetOutlineVerts(Scratch(ref _flat, count), roundSegments);
        var placed = Scratch(ref _placed, count);
        for (var i = 0; i < outline.Length; i++) placed[i] = placement.TransformPoint(new(outline[i], 0));
        Polyline(placed[..outline.Length], width, color, closed: true, -InkLift);
    }

    private static Span<T> Scratch<T>(ref T[] buffer, int count)
    {
        if (buffer.Length < count) buffer = new T[Math.Max(count, buffer.Length * 2)];
        return buffer;
    }

    /// <summary>A fan-filled polygon and/or its outline, all on the current layer.</summary>
    public void Polygon(IReadOnlyList<Vector3f> points, Color4b? fill, Color4b? ink, float width) => Polygon(new LayeredContour(points), fill, ink, width);

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
                Line(points[a], points[b], width, outline, points.LayerAt(a) - InkLift, points.LayerAt(b) - InkLift);
            }
        }
    }
    private Surface[] Attributes(LayeredContour points, Color4b color) => [.. Enumerable.Range(0, points.Count).Select(i => new Surface(color, Layer + points.LayerAt(i)))];
    public void Path(LayeredContour points, Color4b color, float startWidth, float? endWidth = null) =>
        _geometry.Path(points, new(color, Layer), startWidth, endWidth, Attributes(points, color));
    public void Ribbon(LayeredContour points, Color4b color, float width) => _geometry.Ribbon(points, new(color, Layer), width, Attributes(points, color));
    public void Shell(Vector3f center, IReadOnlyList<Vector3f> contour, float depth, Color4b color) =>
        _geometry.Shell(center, contour, depth, new(color, Layer));
    public void Add(TriangleMesh2D mesh, Func<Vector2f, Vector3f> place, Color4b color) => _geometry.Add(mesh, place, new(color, Layer));

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

        // Which faces share each edge is known once per solid; only the normals follow the placement.
        var topology = IndexedMeshTopology3D.Of(solid);
        var triangles = topology.Triangles;
        var vertices = Scratch(ref _placed, solid.Vertices.Count)[..solid.Vertices.Count];
        for (var i = 0; i < vertices.Length; i++) vertices[i] = Ensure.Finite(placement.TransformPoint(solid.Vertices[i]));
        var normals = Scratch(ref _normals, topology.FaceCount)[..topology.FaceCount];
        var faces = Scratch(ref _faces, topology.FaceCount)[..topology.FaceCount];
        topology.FaceNormals(vertices, handedness, normals, faces);
        if (fill is { } color)
        {
            for (var face = 0; face < faces.Length; face++)
            {
                if (faces[face])
                {
                    Triangle(vertices[triangles[face * 3]], vertices[triangles[face * 3 + 1]], vertices[triangles[face * 3 + 2]], color.ScaleRgb(light.Shade(normals[face])).Multiply(tint));
                }
            }
        }
        if (ink is not { } outline || inkWidth <= 0)
        {
            return;
        }

        var layer = Layer;
        Layer -= InkLift;
        _edges.Clear();
        topology.FeatureEdges(normals, faces, -Vector3f.UnitZ, .7f, 1e-5f, _edges);
        foreach (var edge in _edges)
        {
            Line(vertices[edge.A], vertices[edge.B], inkWidth, outline.Multiply(tint));
        }
        Layer = layer;
    }

    /// <summary>Copies real positions to the generic 3D renderer without adding drawing layers to Z.</summary>
    public void CopySpatialTo(TriangleMeshBuilder3D<VertexPositionColorTexture, Color4b, ColoredVertexFactory> destination)
    {
        destination.Clear();
        foreach (var v in Vertices)
            destination.Vertex(new(v.Position.X, v.Position.Y, v.Position.Z), v.Color, new(v.TextureCoordinate.X, v.TextureCoordinate.Y));
    }
}
