using App2d.Gameplay.Player;
using App2d.Gameplay.World;
using App2d.Tiles;
using System.Numerics;
using Xunit;

namespace App2d.Gameplay.Tests.World;

public sealed class AuthoredWorldThing2DTests
{
    [Fact]
    public void AuthoredSpawnAndGoalUseTheirExactPositions()
    {
        var spawn = new Vector2(123f, -45f);
        var savePoint = new Vector2(456f, 15f);
        var goal = new Vector2(987f, 65f);
        WorldThingSpec2D[] things =
        [
            new(1, WorldThingKind2D.PlayerSpawn, "Start", true, spawn),
            new(2, WorldThingKind2D.SavePoint, "Midway", true, savePoint),
            new(3, WorldThingKind2D.Goal, "Exit", true, goal)
        ];

        var level = new SideScrollerLevel2D(
            DefaultTraversal(),
            ValidMap(),
            _ => 1,
            worldThings: things);

        Assert.Equal(spawn, level.SpawnPoint);
        Assert.Equal(goal.X, level.GoalX);
        Assert.Equal(goal.Y, level.GoalGroundY);
        Assert.Equal(3, level.GoalThing?.ThingId);
        Assert.Equal(savePoint, Assert.Single(level.SavePointThings).Position);
        Assert.Equal(2, level.FindSavePoint(2)?.ThingId);
        Assert.Null(level.FindSavePoint(999));
    }

    [Fact]
    public void EmptyThingLayerKeepsEditorBootableWithoutCreatingAnImplicitGoal()
    {
        var level = new SideScrollerLevel2D(DefaultTraversal(), ValidMap(), _ => 1);

        Assert.True(float.IsFinite(level.SpawnPoint.X));
        Assert.True(float.IsFinite(level.SpawnPoint.Y));
        Assert.True(float.IsPositiveInfinity(level.GoalX));
        Assert.Null(level.GoalThing);
        Assert.Empty(level.SavePointThings);
    }

    private static TraversalMetrics2D DefaultTraversal() =>
        (TraversalMetrics2D)Activator.CreateInstance(typeof(TraversalMetrics2D), nonPublic: true)!;

    private static EditableTileMap2D ValidMap() =>
        new(
            SideScrollerLevel2D.WorldWidthTiles,
            SideScrollerLevel2D.WorldHeightTiles,
            32f,
            SideScrollerLevel2D.ChunkSizeTiles,
            SideScrollerLevel2D.WorldOrigin);
}
