using App2d.Core.Imaging;
using App2d.ImageLab;

namespace App2d.Tests.Imaging;

public sealed class RasterRegionsTests
{
    [Fact]
    public void HoverCanFindThinNearbyLinesWithoutMergingComponentsOrPickingErasedPixels()
    {
        var mask = new Image2D<bool>(12, 8);
        mask[2, 2] = mask[2, 3] = mask[9, 2] = true;
        var regions = new RasterRegions(mask, true);
        var left = regions.Labels[2, 2];
        var right = regions.Labels[9, 2];
        Assert.NotEqual(left, right);
        Assert.Equal(left, regions.PickRegion(new(3.4f, 2.5f), 2, mask));
        Assert.Equal(right, regions.PickRegion(new(8.8f, 2.5f), 2, mask));
        Assert.Equal(0, regions.PickRegion(new(6, 7), 1, mask));
        var preview = mask.Clone();
        preview[9, 2] = false;
        Assert.Equal(0, regions.PickRegion(new(9.5f, 2.5f), 2, preview));
        Assert.True(mask[9, 2]);
        Assert.Equal(2, regions.Count);
    }

    [Fact]
    public void LargestNRetainsExactlyTheLargestComponentsAndSupportsNAboveTheCount()
    {
        var mask = Groups();
        var source = new RasterFrame(mask);
        var options = new RasterTransform(TransformKind.LargestRegion);
        Assert.Equal(9, Count(options.Apply(source)));
        Assert.Equal(16, Count((options with { LargestRegions = 2 }).Apply(source)));
        Assert.Equal(19, Count((options with { LargestRegions = 3 }).Apply(source)));
        Assert.Equal(20, Count((options with { LargestRegions = 4 }).Apply(source)));
        Assert.Equal(20, Count((options with { LargestRegions = 100 }).Apply(source)));
        Assert.Equal(20, mask.AsSpan().ToArray().Count(static pixel => pixel));
        Assert.Throws<ArgumentOutOfRangeException>(() => (options with { LargestRegions = 0 }).Apply(source));
        Assert.Equal(0, Count(options.Apply(new(new Image2D<bool>(4, 4)))));
    }

    [Fact]
    public void EqualSizesUseStableDiscoveryOrderAndConnectivityMatchesTheLabels()
    {
        var mask = new Image2D<bool>(4, 4);
        mask[0, 0] = mask[1, 1] = true;
        var eight = new RasterRegions(mask, true);
        var four = new RasterRegions(mask, false);
        Assert.Equal(1, eight.Count);
        Assert.Equal(eight.Labels[0, 0], eight.Labels[1, 1]);
        Assert.Equal(2, four.Count);
        Assert.NotEqual(four.Labels[0, 0], four.Labels[1, 1]);
        var options = new RasterTransform(TransformKind.LargestRegion, EightConnected: false);
        var kept = options.Apply(new(mask)).Mask!;
        Assert.True(kept[0, 0]);
        Assert.False(kept[1, 1]);
        Assert.Equal(2, Count((options with { EightConnected = true }).Apply(new(mask))));
    }

    [Fact]
    public void LargestNSliderReusesLabelsAndConnectivityOrSourceChangesInvalidateThem()
    {
        var source = new RasterFrame(Groups());
        var processor = new RasterTransformProcessor();
        var options = new RasterTransform(TransformKind.LargestRegion, LargestRegions: 2);
        Assert.Equal(16, Count(processor.Apply(source, options)));
        Assert.Equal(19, Count(processor.Apply(source, options with { LargestRegions = 3 })));
        Assert.Equal(1, processor.RegionLabelingCount);
        Assert.Equal(16, Count(processor.Apply(source, new(TransformKind.RemoveSmallRegions, MinimumPixels: 5))));
        Assert.Equal(1, processor.RegionLabelingCount);
        processor.Apply(source, options with { EightConnected = false });
        Assert.Equal(2, processor.RegionLabelingCount);
        processor.Apply(source.Clone(), options);
        Assert.Equal(3, processor.RegionLabelingCount);
        processor.KeepSource(null);
        Assert.Equal(0, processor.CachedPixelBytes);
    }

    private static Image2D<bool> Groups()
    {
        var mask = new Image2D<bool>(12, 16);
        var sizes = new[] { 9, 7, 3, 1 };
        for (var group = 0; group < sizes.Length; group++)
        {
            for (var x = 1; x <= sizes[group]; x++)
            {
                mask[x, group * 4] = true;
            }
        }
        return mask;
    }

    private static int Count(RasterFrame frame) => frame.Mask!.AsSpan().ToArray().Count(static pixel => pixel);
}
