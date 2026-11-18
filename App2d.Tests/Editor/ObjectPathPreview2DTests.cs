using App2d.Core.Characters.Authored;
using App2d.Editor;

namespace App2d.Tests.Editor;

public sealed class ObjectPathPreview2DTests
{
    private static string LiftPath => Path.Combine(TestAssetPath.StaticRoot, "levels", "cavern", "objects", "lift.json");

    [Fact]
    public void PreviewFollowsTheMotionFromTheOriginAndIsEmptyWithoutMotion()
    {
        var asset = Part.Load(LiftPath);
        var points = ObjectPathPreview2D.Sample(asset, new(100f, 50f), 4);
        Assert.Equal(5, points.Length);
        Assert.Equal(new Vector2f(100f, 50f), points[0]);
        Vector2f expectedMidpoint = asset.Motion!.CreateMotion(new(100f, 50f)).Position(asset.Motion.PeriodSeconds / 2);
        Assert.Equal(expectedMidpoint, points[2]);
        Assert.Empty(ObjectPathPreview2D.Sample(asset with { Motion = null }, Vector2f.Zero));
    }
}
