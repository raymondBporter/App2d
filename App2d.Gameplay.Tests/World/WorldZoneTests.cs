using App2d.Gameplay.World;
using App2d.Core;
using App2d.Levels;
using App2d.Physics;
using App2d.Tiles;
using System.Numerics;
using Xunit;

namespace App2d.Gameplay.Tests.World;

public sealed class WorldZoneTests
{
    [Fact]
    public void SharedBordersHaveOneOwnerAndOverlapPriorityIsIndependentOfOrder()
    {
        var left = new WorldZone2D("left", "Left", new(new(-10, -10), new(0, 10)));
        var right = new WorldZone2D("right", "Right", new(new(0, -10), new(10, 10)));
        Assert.Same(left, WorldZone2D.FindAt([left, right], new(-.01f, 0)));
        Assert.Same(right, WorldZone2D.FindAt([left, right], Vector2.Zero));
        Assert.Null(WorldZone2D.FindAt([left, right], new(10, 0)));
        Assert.Null(WorldZone2D.FindAt([left, right], new(0, 10)));
        var special = new WorldZone2D("special", "Special", new(new(-2, -2), new(2, 2)), 10);
        Assert.Same(special, WorldZone2D.FindAt([special, right, left], Vector2.Zero));
        Assert.Same(special, WorldZone2D.FindAt([left, right, special], Vector2.Zero));
        var tie = new WorldZone2D("a", "Tie", special.Bounds, 10);
        Assert.Same(tie, WorldZone2D.FindAt([special, tie], Vector2.Zero));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, float.NaN)]
    [InlineData(float.PositiveInfinity, 1)]
    public void DegenerateAndNonFiniteZonesAreRejected(float x, float y) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorldZone2D("test", "Test", new(Vector2.Zero, new(x, y))));

    [Fact]
    public void ZoneFileRejectsDuplicatesAndInvalidBoundsButOldLevelsMayHaveNoFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"zones-{Guid.NewGuid():N}.json");
        try
        {
            Assert.Empty(WorldZoneFile2D.Load(path));
            const string entry = """{"id":"a","name":"A","minX":0,"minY":0,"maxX":10,"maxY":20,"priority":0}""";
            File.WriteAllText(path, "{\"version\":1,\"zones\":[" + entry + "]}");
            Assert.True(Assert.Single(WorldZoneFile2D.Load(path)).Contains(new(5, 5)));
            File.WriteAllText(path, "{\"version\":1,\"zones\":[" + entry + "," + entry + "]}");
            Assert.Throws<InvalidDataException>(() => WorldZoneFile2D.Load(path));
            File.WriteAllText(path, "{\"version\":1,\"zones\":[" + entry.Replace("\"maxX\":10", "\"maxX\":0") + "]}");
            Assert.Throws<InvalidDataException>(() => WorldZoneFile2D.Load(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void WholeLevelZonesSurviveTerrainStreamingAndContentIsShared()
    {
        var map = new EditableTileMap2D(640, 96, 32, 32, SideScrollerLevel2D.WorldOrigin, ["dark-cave"]);
        var zone = new WorldZone2D("far", "Far zone", new(new(15000, -640), new(19968, 2432)));
        using var level = new SideScrollerLevel2D(TraversalMetricsLoader2D.Load(TestAssetPath.Root), map, _ => 1, zones: [zone]);
        var physics = new PhysicsWorld2D();
        level.CreateSimulation(physics.CollisionSystem, physics, new EntityIdAllocator2D(), 1, 2, 4);
        var content = level.CaptureContent();
        Assert.Same(zone, Assert.Single(content.Zones));
        Assert.Same(content, level.CaptureContent());
        var jsonOptions = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
        var json = System.Text.Json.JsonSerializer.Serialize(content.Zones, jsonOptions);
        var received = System.Text.Json.JsonSerializer.Deserialize<WorldZone2D[]>(json, jsonOptions);
        Assert.Equal(zone, Assert.Single(received!));
        level.UpdateStreaming(new(16000, 0));
        Assert.Same(zone, Assert.Single(level.CaptureContent().Zones));
        Assert.Same(zone, Assert.Single(content.Zones));
    }
}
