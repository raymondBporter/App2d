using App2d.Core.Geometry.Primitives;
using App2d.Gameplay.World;

namespace App2d.Tests.Gameplay.World;

public sealed class SavePoint2DTests
{
    [Fact]
    public void EnteringFiresOnceUntilPlayerLeavesAndReturns()
    {
        var savePoint = new SavePoint2D(
            new WorldThingSpec2D(7, WorldThingKind2D.SavePoint, "Test", true, Vector2f.Zero),
            respawnGroundOffset: 32f);
        var outside = BoundsAt(new Vector2f(200f, 40f));
        var inside = BoundsAt(new Vector2f(0f, 40f));

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
            new WorldThingSpec2D(7, WorldThingKind2D.SavePoint, "Test", true, Vector2f.Zero),
            respawnGroundOffset: 32f);

        savePoint.SetActive(true);
        Assert.True(savePoint.IsActive);

        savePoint.SetActive(false);
        Assert.False(savePoint.IsActive);
    }

    private static Rect2f BoundsAt(Vector2f center) =>
        Rect2f.Builder.Create(center - new Vector2f(10f), center + new Vector2f(10f));
}
