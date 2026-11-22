using App2d.Core.Characters;
using App2d.Core.Characters.Attachments;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Kinematics;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Rendering;

namespace App2d.Tests.Rendering;

/// <summary>Flat shapes and strokes built into a <see cref="LayeredMesh"/>, and the rigid pieces painted with them.</summary>
public sealed class LayeredMeshShapeTests
{
    private static readonly Frame3D Placed = new(new(10, 5, 0), Vector3f.UnitX, Vector3f.UnitY, Vector3f.UnitZ);

    private static HashSet<(Microsoft.Xna.Framework.Vector3, Microsoft.Xna.Framework.Vector3, Microsoft.Xna.Framework.Vector3)> Triangles(LayeredMesh mesh)
    {
        var vertices = mesh.Vertices;
        var triangles = new HashSet<(Microsoft.Xna.Framework.Vector3, Microsoft.Xna.Framework.Vector3, Microsoft.Xna.Framework.Vector3)>();
        for (var i = 0; i < vertices.Length; i += 3) triangles.Add((vertices[i].Position, vertices[i + 1].Position, vertices[i + 2].Position));
        return triangles;
    }

    /// <summary>The area of every triangle of one colour, and whether all of them pass a test on their middle point.</summary>
    private static (float Area, bool AllInside) Measure(LayeredMesh mesh, Color4b color, Func<Vector2f, bool> inside)
    {
        var vertices = mesh.Vertices;
        var (area, all) = (0f, true);
        for (var i = 0; i < vertices.Length; i += 3)
        {
            if (vertices[i].Color != color) continue;
            Vector2f a = new(vertices[i].Position.X, vertices[i].Position.Y), b = new(vertices[i + 1].Position.X, vertices[i + 1].Position.Y), c = new(vertices[i + 2].Position.X, vertices[i + 2].Position.Y);
            area += MathF.Abs((b - a).Cross(c - a)) / 2;
            all &= inside((a + b + c) / 3);
        }
        return (area, all);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void APolylineCoversWhatALinePerSegmentCoversWithEachSharedDiskOnce(bool closed)
    {
        Vector3f[] points = [new(0, 0, 0), new(4, 0, 0), new(4, 3, 0), new(1, 5, 0)];
        var edges = closed ? 4 : 3;
        var (lines, polyline) = (new LayeredMesh(64), new LayeredMesh(64));
        for (var i = 0; i < edges; i++) lines.Line(points[i], points[(i + 1) % 4], .5f, Color4b.Black);
        polyline.Polyline(points, .5f, Color4b.Black, closed);

        Assert.True(Triangles(lines).SetEquals(Triangles(polyline)));
        // Two triangles a segment and twelve a disk, where a line per segment draws two disks each.
        Assert.Equal((edges * 2 + 4 * 12) * 3, polyline.Count);
        Assert.Equal(edges * (2 + 2 * 12) * 3, lines.Count);
    }

    [Fact]
    public void AFilledConcavePolygonStaysInsideItselfWhereverItIsPlaced()
    {
        // An L whose first corner cannot see all of it: a fan from that corner would cover the notch.
        var shape = SimplePolygon2D.Builder.Create([new(32, -24), new(32, 0), new(16, 0), new(16, 24), new(-32, 24), new(-32, -24)]);
        var mesh = new LayeredMesh(64);
        mesh.Fill(shape, Placed, Color4b.White);

        var (area, inside) = Measure(mesh, Color4b.White, point => shape.ContainsPoint(point - new Vector2f(10, 5)));
        Assert.True(inside);
        Assert.Equal(shape.Area, area, 2);
    }

    [Fact]
    public void AnOutlineHasOneDiskPerCornerAndSitsJustInFrontOfTheLayer()
    {
        var mesh = new LayeredMesh(64) { Layer = 2 };
        mesh.Outline(Rectangle2D.Builder.Create(new(-2, -1), new(2, 1)), Placed, Color4b.Black, .5f);

        Assert.Equal((4 * 2 + 4 * 12) * 3, mesh.Count);
        Assert.All(mesh.Vertices.ToArray(), vertex => Assert.InRange(vertex.Layer, 1.999f, 1.99999f));
        // Half the line's width lies outside the rectangle, placed at (10, 5).
        Assert.Equal(7.75f, mesh.Min.X, 4);
        Assert.Equal(6.25f, mesh.Max.Y, 4);
    }

    [Fact]
    public void APatchIsCutToItsShapeAndCutAgainForACopyWithAnotherShape()
    {
        var skeleton = new Skeleton2D(new Joint2D("item", Skeleton2D.NoParent, default)) { Mounts = [Mount2D.On("item", 0)] };
        var red = ColorExtensions.FromHexRgb("#ff0000");
        var piece = new MountedShape("hide", 0, Rectangle2D.Builder.Create(new(-2, -1), new(2, 1)), new() { Fill = "#ffffff" })
        {
            Patches = [new("#ff0000", [new(1, -3), new(5, -3), new(5, 3), new(1, 3)])],
        };
        LayeredMesh Painted(MountedShape shape)
        {
            var mesh = new LayeredMesh(64);
            shape.Paint(mesh, CharacterPose.Rest(skeleton).Posed);
            return mesh;
        }

        // The patch reaches past the rectangle on three sides; only the part over it is painted.
        var (area, inside) = Measure(Painted(piece), red, point => point.X is > 1 and < 2 && point.Y is > -1 and < 1);
        Assert.True(inside);
        Assert.Equal(2f, area, 4);
        // Painting again uses the same cut.
        Assert.True(Painted(piece).Vertices.SequenceEqual(Painted(piece).Vertices));
        // A copy with a wider shape is another piece and gets a cut of its own.
        var wider = piece with { Shape = Rectangle2D.Builder.Create(new(-4, -1), new(4, 1)) };
        Assert.Equal(6f, Measure(Painted(wider), red, _ => true).Area, 4);
        Assert.Equal(2f, Measure(Painted(piece), red, _ => true).Area, 4);
    }
}
