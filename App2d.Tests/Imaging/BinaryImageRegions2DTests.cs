using App2d.Core.Imaging;

namespace App2d.Tests.Imaging;

public sealed class BinaryImageRegions2DTests
{
    [Fact]
    public void ComponentsDistinguishDiagonalConnectivityAndIncludeBackgroundCount()
    {
        var source = new Image2D<bool>(3, 2, [true, false, false, false, true, false]);
        var (Labels, PixelCounts) = source.LabelComponents();
        Assert.Equal([4, 2], PixelCounts);
        Assert.Equal([1, 0, 0, 0, 1, 0], Labels.AsSpan().ToArray());
        var four = source.LabelComponents(eightConnected: false);
        Assert.Equal([4, 1, 1], four.PixelCounts);
        Assert.Equal([1, 0, 0, 0, 2, 0], four.Labels.AsSpan().ToArray());
    }

    [Fact]
    public void SizeFilteringKeepsAttachedFuzzUntilItsBridgeIsErased()
    {
        var source = Parse("####....", "...#..#.", "..##....");
        var groups = source.LabelComponents();
        Assert.Equal(7, groups.PixelCounts[1]);
        Assert.Equal(1, groups.PixelCounts[2]);
        var filtered = groups.Labels.Map(id => id != 0 && groups.PixelCounts[id] >= 3);
        Assert.True(filtered[2, 2]); // Connected fuzz cannot be removed by a component-size rule.
        Assert.False(filtered[6, 1]);

        source[3, 1] = false; // A manual cut splits the main stroke from the two-pixel branch.
        var (Labels, PixelCounts) = source.LabelComponents(eightConnected: false);
        Assert.Equal(4, PixelCounts.Length);
        Assert.Equal([4, 1, 2], PixelCounts[1..]);
    }

    [Fact]
    public void HoleFillingPreservesOutsideAndSourceButAnOpenBoundaryLeaks()
    {
        var source = Parse(".....", ".###.", ".#.#.", ".###.", ".....");
        var filled = source.FillHoles();
        Assert.True(filled[2, 2]);
        Assert.False(source[2, 2]);
        Assert.False(filled[0, 0]);
        source[2, 1] = false;
        Assert.False(source.FillHoles()[2, 2]);
    }

    [Fact]
    public void ImageBorderAndDiagonalForegroundContactsCanEncloseBackground()
    {
        var source = Parse("###", "#.#", "###");
        Assert.All(source.FillHoles().AsSpan().ToArray(), Assert.True);
        var diagonal = Parse(".#.", "#.#", ".#.");
        Assert.True(diagonal.FillHoles()[1, 1]);
        Assert.False(diagonal.FillHoles()[0, 0]);
    }

    [Fact]
    public void EmptyInputsKeepDimensionsAndRejectNull()
    {
        var source = new Image2D<bool>(0, 7);
        Assert.Equal([0], source.LabelComponents().PixelCounts);
        Assert.Equal(7, source.LabelComponents().Labels.Height);
        Assert.Equal(7, source.FillHoles().Height);
        Assert.Equal(4, new Image2D<bool>(4, 0).FillHoles().Width);
        Assert.Throws<ArgumentNullException>(() => BinaryImageRegions2D.LabelComponents(null!));
        Assert.Throws<ArgumentNullException>(() => BinaryImageRegions2D.FillHoles(null!));
    }

    private static Image2D<bool> Parse(params string[] rows) =>
        new(rows[0].Length, rows.Length, rows.SelectMany(row => row.Select(c => c == '#')).ToArray());
}
