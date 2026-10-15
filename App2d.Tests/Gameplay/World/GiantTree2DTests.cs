using App2d.Gameplay.World.Vegetation;
using App2d.Core.Geometry;
using System.Numerics;

namespace App2d.Tests.Gameplay.World;

public sealed class GiantTree2DTests
{
    [Fact]
    public void SeededPlatformsStayTheSameWhenOnlyFoliageChanges()
    {
        var settings = new GiantTreeSettings2D();
        var first = new GiantTree2D(new(300, -256), settings, 3107);
        var copy = new GiantTree2D(first.Root, settings, first.Seed);
        var leafy = new GiantTree2D(first.Root, settings with { FoliageRadius = new(100, 200) }, first.Seed);
        var different = new GiantTree2D(first.Root, settings, 8821);
        Assert.Equal([.. first.Branches], [.. copy.Branches]);
        Assert.Equal([.. first.Branches], [.. leafy.Branches]);
        Assert.NotEqual([.. first.Branches], [.. different.Branches]);
    }

    [Theory]
    [InlineData(3107)]
    [InlineData(8821)]
    [InlineData(4219)]
    [InlineData(6503)]
    public void BranchesRespectLengthAndJumpSpacingAndFitTheWholeTreeBounds(int seed)
    {
        var tree = new GiantTree2D(new(300, -256), new(), seed);
        Assert.NotEmpty(tree.Branches);
        Assert.Contains(tree.Branches, branch => branch.Side == -1);
        Assert.Contains(tree.Branches, branch => branch.Side == 1);
        var previousY = tree.Root.Y;
        foreach (var level in tree.Branches.GroupBy(branch => branch.Bounds.Top))
        {
            Assert.InRange(level.Key - previousY, tree.Settings.BranchGap.Min - 0.001f, tree.Settings.BranchGap.Max + 0.001f);
            previousY = level.Key;
            foreach (var branch in level)
            {
                var tipX = branch.Side < 0 ? branch.Bounds.Left : branch.Bounds.Right;
                var length = Math.Abs(tipX - tree.Root.X) - tree.Settings.TrunkWidth * 0.5f;
                Assert.InRange(length, tree.Settings.BranchLength.Min - 0.001f, tree.Settings.BranchLength.Max + 0.001f);
                Assert.Equal(tree.Settings.BranchThickness, branch.Bounds.Height, 3);
                Assert.True(tree.Bounds.Contains(branch.Bounds));
            }
        }
    }

    [Fact]
    public void InvalidAndUnboundedGenerationInputsAreRejectedBeforeGrowing()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GiantTree2D(new(float.NaN, 0), new(), 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GiantTree2D(Vector2.Zero, new() { BranchGap = new(0, 10) }, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GiantTree2D(Vector2.Zero, new() { BranchLength = new(100, 50) }, 1));
        Assert.Throws<ArgumentException>(() => new GiantTree2D(Vector2.Zero, new() { Height = 1_000_000 }, 1));
    }
}
