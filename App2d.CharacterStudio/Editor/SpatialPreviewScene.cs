using App2d.Core.Characters.Authored;
using App2d.Core.Geometry.Meshes;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using XVector2 = Microsoft.Xna.Framework.Vector2;
using XVector3 = Microsoft.Xna.Framework.Vector3;

namespace App2d.CharacterStudio.Editor;

/// <summary>A transient scene made from the existing sword's physical mesh and a calibration cube. No authored data is changed.</summary>
internal sealed class SpatialPreviewScene
{
    public static readonly Color4b Background = new(16, 23, 35);
    public IReadOnlyList<SpatialPreviewObject> Objects { get; }
    private readonly TriangleMeshBuilder3D<VertexPositionColorTexture, Color4b> _ground = Mesh();

    public SpatialPreviewScene(Part? sword)
    {
        var triangles = new List<(Triangle3D Triangle, Color4b Color)>();
        foreach (var part in sword?.Geometry.Where(part => !part.Hidden && part.Mesh is not null) ?? [])
        {
            var solid = part.Mesh!;
            var placement = Matrix4x4.CreateScale(part.ScaleX, part.ScaleY, 1f) * Matrix4x4.CreateRotationZ(part.Angle)
                * Matrix4x4.CreateTranslation(part.OffsetX, part.OffsetY, 0f);
            var color = part.Material?.Fill is { } fill ? ColorExtensions.FromHexRgb(fill) : new Color4b(156, 187, 205);
            for (var i = 0; i < solid.Triangles.Count; i += 3)
            {
                Vector3 At(int index) => Vector3.Transform(solid.Vertices[index].XYZ, placement);
                triangles.Add((new(At(solid.Triangles[i]), At(solid.Triangles[i + 1]), At(solid.Triangles[i + 2])), color));
            }
        }
        var swordMesh = Mesh();
        if (triangles.Count > 0)
        {
            var points = triangles.SelectMany(face => new[] { face.Triangle.A, face.Triangle.B, face.Triangle.C }).ToArray();
            var min = points.Aggregate(Vector3.Min);
            var max = points.Aggregate(Vector3.Max);
            var span = max - min;
            var scale = 1.9f / MathF.Max(span.X, MathF.Max(span.Y, span.Z));
            var center = (min + max) / 2;
            foreach (var (triangle, color) in triangles)
                Face(swordMesh, (triangle.A - center) * scale, (triangle.B - center) * scale, (triangle.C - center) * scale, color);
        }

        var cube = Mesh();
        Vector3[] corners = [new(-.3f, -.3f, -.3f), new(.3f, -.3f, -.3f), new(.3f, .3f, -.3f), new(-.3f, .3f, -.3f),
            new(-.3f, -.3f, .3f), new(.3f, -.3f, .3f), new(.3f, .3f, .3f), new(-.3f, .3f, .3f)];
        int[] indices = [0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7, 0, 4, 7, 0, 7, 3, 1, 2, 6, 1, 6, 5, 3, 7, 6, 3, 6, 2, 0, 1, 5, 0, 5, 4];
        Color4b[] colors = [new(71, 129, 203), new(57, 185, 202), new(104, 103, 194), new(54, 146, 186), new(238, 178, 85), new(75, 91, 123)];
        for (var i = 0; i < indices.Length; i += 3)
            Face(cube, corners[indices[i]], corners[indices[i + 1]], corners[indices[i + 2]], colors[i / 6]);
        var calibration = new SpatialPreviewObject("Calibration cube", cube, new(.8f, -.75f, .1f), Vector3.Zero);
        Objects = swordMesh.Count > 0 ? [new("Sword", swordMesh, new(-.35f, .05f, 0), new(0, -.15f, 1.35f)), calibration] : [calibration];
        const float floor = -1.05f;
        Quad(_ground, new(-3, floor, -3), new(-3, floor, 3), new(3, floor, 3), new(3, floor, -3), new(30, 42, 58));
        for (var i = -12; i <= 12; i++)
        {
            var p = i * .25f;
            var width = i == 0 ? .009f : .0025f;
            var y = floor + .002f;
            Quad(_ground, new(p - width, y, -3), new(p - width, y, 3), new(p + width, y, 3), new(p + width, y, -3), i == 0 ? new(75, 124, 167) : new(49, 64, 82));
            Quad(_ground, new(-3, y, p - width), new(-3, y, p + width), new(3, y, p + width), new(3, y, p - width), i == 0 ? new(157, 84, 87) : new(49, 64, 82));
        }
    }

    public static TriangleMeshBuilder3D<VertexPositionColorTexture, Color4b> Mesh() => new(
        (point, color, uv) => new(new XVector3(point.X, point.Y, point.Z), color, new XVector2(uv.X, uv.Y)));

    private static void Face(TriangleMeshBuilder3D<VertexPositionColorTexture, Color4b> mesh, Vector3 a, Vector3 b, Vector3 c, Color4b color)
    {
        var triangle = new Triangle3D(a, b, c);
        var light = triangle.TryGetUnitNormal(out var normal) ? .5f + .5f * MathF.Max(0, Vector3.Dot(normal, Vector3.Normalize(new(.35f, .8f, 1)))) : 1f;
        mesh.Triangle(a, b, c, color.ScaleRgb(light));
    }

    private static void Quad(TriangleMeshBuilder3D<VertexPositionColorTexture, Color4b> mesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color4b color)
    {
        mesh.Triangle(a, b, c, color);
        mesh.Triangle(a, c, d, color);
    }

    public void Draw(Renderer3D renderer, Camera3D camera)
    {
        renderer.Clear(Background);
        renderer.Draw(_ground, Frame3D.Identity, camera);
        foreach (var item in Objects) renderer.Draw(item.Mesh, item.Frame, camera);
    }

    public int? Pick(Ray3D ray, out Vector3 point)
    {
        int? picked = null;
        var nearest = float.PositiveInfinity;
        point = default;
        for (var i = 0; i < Objects.Count; i++)
        {
            var item = Objects[i];
            var vertices = item.Mesh.Vertices;
            for (var j = 0; j < vertices.Length; j += 3)
            {
                Vector3 At(int index)
                {
                    var p = item.Mesh.Vertices[index].Position;
                    return item.Frame.TransformPoint(new(p.X, p.Y, p.Z));
                }
                if (!ray.TryIntersect(new Triangle3D(At(j), At(j + 1), At(j + 2)), out var distance) || distance >= nearest) continue;
                nearest = distance;
                picked = i;
                point = ray.At(distance);
            }
        }
        return picked;
    }
}

internal sealed class SpatialPreviewObject(string name, TriangleMeshBuilder3D<VertexPositionColorTexture, Color4b> mesh, Vector3 position, Vector3 rotation)
{
    private readonly Vector3 _initialPosition = position;
    private readonly Vector3 _initialRotation = rotation;
    public string Name { get; } = name;
    public TriangleMeshBuilder3D<VertexPositionColorTexture, Color4b> Mesh { get; } = mesh;
    public Vector3 Position { get; set; } = position;
    public Vector3 Rotation { get; set; } = rotation;
    public Frame3D Frame => Frame3D.FromRotation(Rotation, Position);
    public void Reset() => (Position, Rotation) = (_initialPosition, _initialRotation);
}
