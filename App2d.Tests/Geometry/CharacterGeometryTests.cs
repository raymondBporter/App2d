using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class CharacterGeometryTests
{
    [Theory]
    [InlineData("ellipse", 48)]
    [InlineData("box", 36)]
    public void ContoursAndPickingShareTheRotatedOffsetFrame(string kind, int count)
    {
        var part = new PuppetPart
        {
            Kind = kind,
            A = "a",
            B = "b",
            Width = 4,
            Height = 2,
            OffsetX = 1,
            OffsetY = 2,
            Depth = .5f,
            Roundness = .5f
        };
        var contour = PartGeometry.Contour(part, World);
        Assert.Equal(count, contour.Count);
        Assert.All(contour, point => Assert.Equal(3.5f, point.Z));
        Assert.Equal(0, PartGeometry.Distance(part, World, new(12, 19, 99)), 5);
        Assert.Equal(1, PartGeometry.Distance(part, World, new(12, 17, 0)), 5);
        Assert.Equal(2, PartGeometry.Distance(part, World, new(14, 19, 0)), 5);
        foreach (var point in contour)
            Assert.InRange(PartGeometry.Distance(part, World, point), 0f, 1.00001f);
        if (kind == "ellipse")
        {
            Assert.True(Vector3.Distance(new(12, 17, 3.5f), contour[0]) < .00001f);
            Assert.True(Vector3.Distance(new(13, 19, 3.5f), contour[12]) < .00001f);
        }
        else
        {
            // Existing picking intentionally uses the box even at a rounded-off corner.
            Assert.Equal(1, PartGeometry.Distance(part, World, new(13, 17, 0)), 5);
        }
        static Vector3 World(string id) => id == "a" ? new(10, 20, 3) : new(12, 20, 6);
    }

    [Fact]
    public void StrokePickingKeepsItsMinimumWidthAndPointLikeSegmentTolerance()
    {
        var part = new PuppetPart { Kind = "stroke", A = "a", B = "b", Width = .01f, Depth = 2 };
        static Vector3 World(string id) => id == "a" ? default : new(.000001f, 0, 3);
        Assert.Equal(1, PartGeometry.Distance(part, World, new(0, .06f, 100)), 5);
        Assert.Equal(new Vector3(0, 0, 2), PartGeometry.Contour(part, World)[0]);
        Assert.Equal(new Vector3(.000001f, 0, 5), PartGeometry.Contour(part, World)[1]);
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
