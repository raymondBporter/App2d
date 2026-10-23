using App2d.Core.Objects;
using App2d.Editor;
using System.Numerics;

namespace App2d.Tests.Editor;

public sealed class ObjectPathPreview2DTests
{
    private static string LiftPath => Path.Combine(TestAssetPath.StaticRoot, "levels", "cavern", "objects", "lift.json");

    [Fact]
    public void PreviewFollowsTheMotionFromTheOriginAndIsEmptyWithoutMotion()
    {
        var asset = ObjectAsset2D.Load(LiftPath);
        var points = ObjectPathPreview2D.Sample(asset, new(100f, 50f), 4);
        Assert.Equal(5, points.Length);
        Assert.Equal(new Vector2(100f, 50f), points[0]);
        Assert.Equal(asset.Motion!.CreateMotion(new(100f, 50f)).Position(asset.Motion.PeriodSeconds / 2), points[2]);
        Assert.Empty(ObjectPathPreview2D.Sample(asset with { Motion = null }, Vector2.Zero));
    }
}
