using App2d.Core.Geometry;
using App2d.Gameplay.World;
using App2d.Rendering;
using System.Numerics;
using Xunit;

namespace App2d.Gameplay.Tests.World;

public sealed class SavePoint2DTests
{
    [Fact]
    public void EnteringFiresOnceUntilPlayerLeavesAndReturns()
    {
        var savePoint = new SavePoint2D(
            new Scene2D(),
            new WorldThingSpec2D(7, WorldThingKind2D.SavePoint, "Test", true, Vector2.Zero),
            respawnGroundOffset: 32f);
        var outside = BoundsAt(new Vector2(200f, 40f));
        var inside = BoundsAt(new Vector2(0f, 40f));

        Assert.False(savePoint.Update(0f, outside));
        Assert.True(savePoint.Update(0f, inside));
        Assert.False(savePoint.Update(0.1f, inside));
        Assert.False(savePoint.Update(0f, outside));
        Assert.True(savePoint.Update(0f, inside));
    }

    [Fact]
    public void ActiveStateCanMoveBetweenBeacons()
    {
        var savePoint = new SavePoint2D(
            new Scene2D(),
            new WorldThingSpec2D(7, WorldThingKind2D.SavePoint, "Test", true, Vector2.Zero),
            respawnGroundOffset: 32f);

        savePoint.SetActive(true);
        Assert.True(savePoint.IsActive);

        savePoint.SetActive(false);
        Assert.False(savePoint.IsActive);
    }

    private static Bounds2D BoundsAt(Vector2 center) =>
        new(center - new Vector2(10f), center + new Vector2(10f));
}
