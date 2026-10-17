using App2d.CharacterSeeds;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Operations.Vertices;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class CharacterGeometryTests
{
    [Fact]
    public void SeededCurveThicknessDoesNotChangeItsControlAttachments()
    {
        var model = PterosaurTemplate.Model();
        var rest = model.Controls.ToDictionary(c => c.Id, c => c.Rest.XYZ);
        foreach (var part in model.Parts.Where(p => p.Geometry is ICurve2D))
        {
            var points = PartContours2D.Contour(part, id => rest[id]);
            Assert.Equal(1f, part.ScaleX);
            Assert.Equal(1f, part.ScaleY);
            Assert.True(part.RenderMaterial.StrokeWidth > 0);
            Assert.Equal(rest[part.A] + new Vector3(0, 0, part.Depth), points[0]);
            Assert.True(Vector3.Distance(rest[part.B!] + new Vector3(0, 0, part.Depth), points[^1]) < 1e-6f);
        }
    }

    public static TheoryData<IShape2D> Shapes =>
    [
        Circle2D.Builder.Create(.7f, new(.2f, -.3f)),
        Ellipse2D.Builder.Create(new(.4f, .8f)),
        Rectangle2D.Builder.FromSize(new(4, 2)),
        AxisAlignedRectangle2D.Builder.FromSize(new(3, 1)),
        RoundedRectangle2D.Builder.FromSize(4, 2, .3f),
        Capsule2D.Builder.Create(new(-1, 0), new(1, 0), .2f),
        Triangle2D.Builder.Create(new(0, 0), new(2, 0), new(0, 1)),
        ConvexPolygon2D.Builder.Create([new(0, 0), new(2, 0), new(0, 1)]),
        SimplePolygon2D.Builder.Create([new(0, 0), new(2, 0), new(0, 1)]),
    ];

    [Theory, MemberData(nameof(Shapes))]
    public void UnscaledPartsUseTheirGeometryDimensions(IShape2D shape)
    {
        var part = new PuppetPart { A = "origin", Geometry = shape };
        var vertices = new Vector2[shape.GetOutlineVertCount(48)];
        shape.GetOutlineVerts(vertices, 48);
        Assert.Equal(vertices, PartContours2D.Contour(part, _ => Vector3.Zero).Select(p => new Vector2(p.X, p.Y)));
    }

    [Fact]
    public void CurvePlacementAndStrokeAppearanceAreIndependentAndPickingUsesBoth()
    {
        var part = new PuppetPart
        {
            A = "a",
            B = "b",
            Geometry = LineSegmentCurve2D.Builder.Create(Vector2.Zero, Vector2.UnitY),
            ScaleX = 2,
            ScaleY = .5f,
            Angle = MathF.PI / 2,
            OffsetX = 1,
            OffsetY = .25f,
            Material = new() { StrokeWidth = .1f },
        };
        static Vector3 World(string id) => id == "a" ? new(3, 4, 0) : new(3, 6, 0);
        var points = PartContours2D.Contour(part, World);
        Assert.True(Vector3.Distance(new(5, 4.5f, 0), points[0]) < 1e-6f);
        Assert.True(Vector3.Distance(new(4, 4.5f, 0), points[1]) < 1e-6f);
        Assert.All(points, p => Assert.InRange(PartPicking2D.Distance(part, World, p), 0, 1e-5f));

        var thick = part with { Material = part.RenderMaterial with { StrokeWidth = .8f } };
        Assert.Equal(points, PartContours2D.Contour(thick, World));
        var probe = new Vector3(4.5f, 4.7f, 0);
        Assert.True(PartPicking2D.Distance(part, World, probe) > 1);
        Assert.True(PartPicking2D.Distance(thick, World, probe) < 1);
        var drawing = new PuppetDrawing();
        drawing.Build("#000000", .045f, [part], World);
        var thinHeight = drawing.Mesh.Max.Y - drawing.Mesh.Min.Y;
        drawing.Build("#000000", .045f, [thick], World);
        Assert.True(drawing.Mesh.Max.Y - drawing.Mesh.Min.Y > thinHeight * 5);
    }

    [Fact]
    public void CurvePickingUsesTheCurveDistanceInItsAttachmentFrame()
    {
        var part = new PuppetPart
        {
            A = "a",
            B = "b",
            Material = new() { StrokeWidth = .2f },
            Geometry = new LineSegmentCurve2D { Start = new(.5f, 0), End = new(.5f, 1) }
        };
        static Vector3 World(string id) => id == "a" ? new(2, 3, 0) : new(2, 5, 0);
        Assert.Equal(0, PartPicking2D.Distance(part, World, new(3, 4, 0)), 5);
        Assert.Equal(5, PartPicking2D.Distance(part, World, new(4, 4, 0)), 5);
        Assert.Equal(0, PartPicking2D.Distance(part, _ => new(2, 3, 0), new(2, 3, 0)), 5);
    }

    [Theory]
    [InlineData("ellipse", 48)]
    [InlineData("rounded-rectangle", 36)]
    public void ContoursAndPickingShareTheRotatedOffsetFrame(string kind, int count)
    {
        var part = new PuppetPart
        {
            Geometry = kind == "ellipse" ? GeometryJson2D.CreateDefault(kind) : RoundedRectangle2D.Builder.FromSize(4, 2, .25f),
            A = "a",
            B = "b",
            ScaleX = kind == "ellipse" ? 4 : 1,
            ScaleY = kind == "ellipse" ? 2 : 1,
            OffsetX = 1,
            OffsetY = 2,
            Depth = .5f
        };
        var contour = PartContours2D.Contour(part, World);
        Assert.Equal(count, contour.Count);
        Assert.All(contour, point => Assert.Equal(3.5f, point.Z));
        Assert.True(PartPicking2D.Distance(part, World, new(12, 19, 99)) <= 1);
        Assert.True(PartPicking2D.Distance(part, World, new(12, 17, 0)) <= 1.00001f);
        Assert.True(PartPicking2D.Distance(part, World, new(14, 19, 0)) > 1);
        foreach (var point in contour)
        {
            Assert.InRange(PartPicking2D.Distance(part, World, point), 0f, 1.00001f);
        }

        if (kind == "ellipse")
        {
            Assert.True(Vector3.Distance(new(12, 17, 3.5f), contour[0]) < .00001f);
            Assert.True(Vector3.Distance(new(13, 19, 3.5f), contour[12]) < .00001f);
        }
        else
        {
            Assert.True(PartPicking2D.Distance(part, World, new(13, 17, 0)) > 1);
        }
        static Vector3 World(string id) => id == "a" ? new(10, 20, 3) : new(12, 20, 6);
    }

    [Fact]
    public void StrokePickingKeepsItsMinimumWidthAndPointLikeSegmentTolerance()
    {
        var part = new PuppetPart { Geometry = new LineSegmentCurve2D { Start = Vector2.Zero, End = Vector2.UnitY }, A = "a", B = "b", Material = new() { StrokeWidth = .01f }, Depth = 2 };
        static Vector3 World(string id) => id == "a" ? default : new(.000001f, 0, 3);
        Assert.Equal(1, PartPicking2D.Distance(part, World, new(0, .06f, 100)), 5);
        Assert.Equal(new Vector3(0, 0, 2), PartContours2D.Contour(part, World)[0]);
        Assert.Equal(new Vector3(.000001f, 0, 5), PartContours2D.Contour(part, World)[1]);
    }

    [Fact]
    public void EntityRegionsKeepTranslatedAndTouchingOverlapBehavior()
    {
        var region = EntityRegion.Box("hurt", new(2, 3), new(4, 2));
        Assert.Equal(new Vector2(0, 2), region.Outline()[0]);
        Assert.True(region.Overlaps(region, new(10, 20), new(14, 20)));
        Assert.False(region.Overlaps(region, new(10, 20), new(14.01f, 20)));
    }
}
