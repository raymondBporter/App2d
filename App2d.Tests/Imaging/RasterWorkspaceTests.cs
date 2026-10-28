using App2d.Core.Imaging;
using App2d.ImageLab;

namespace App2d.Tests.Imaging;

public sealed class RasterWorkspaceTests
{
    [Fact]
    public void HistoryKeepsOriginalAndRedoUntilANewTransformBranches()
    {
        var pixels = new Image2D<float>(2, 1, [.2f, .8f]);
        var history = new RasterHistory();
        history.Load(new(pixels), "test");
        pixels[0, 0] = 99;
        Assert.Equal(.2f, history.Current!.Channel![0, 0]);
        var invert = new RasterTransform(TransformKind.Invert);
        history.Apply(invert, invert.Apply(history.Current));
        var threshold = new RasterTransform(TransformKind.Threshold, Threshold: .5f);
        history.Apply(threshold, threshold.Apply(history.Current!));
        Assert.Equal([true, false], history.Current!.Mask!.AsSpan().ToArray());
        history.Undo();
        Assert.True(history.CanRedo);
        history.Redo();
        Assert.NotNull(history.Current.Mask);
        history.Select(0);
        Assert.True(history.CanRedo);
        history.Apply(threshold, threshold.Apply(history.Current!));
        Assert.Equal(2, history.Steps.Count);
        Assert.False(history.CanRedo);
        Assert.Equal([false, true], history.Current!.Mask!.AsSpan().ToArray());
        Assert.Equal([.2f, .8f], history.Steps[0].Frame.Channel!.AsSpan().ToArray());
    }

    [Fact]
    public void ProcessingKeepsScalarPrecisionAndUsesExplicitAlphaOrWhiteComposite()
    {
        var colors = new Image2D<Color4b>(2, 1, [new(0, 0, 0, 0), new(0, 0, 0, 128)]);
        var source = new RasterFrame(colors);
        var alpha = new RasterTransform(TransformKind.SingleChannel, ChannelSource.Alpha).Apply(source);
        var luminance = new RasterTransform().Apply(source);
        Assert.Equal(0, alpha.Channel![0, 0]);
        Assert.Equal(128 / 255f, alpha.Channel[1, 0]);
        Assert.Equal(1, luminance.Channel![0, 0]);
        Assert.Equal(1 - 128 / 255f, luminance.Channel[1, 0], 6);
        var precise = new RasterFrame(new Image2D<float>(1, 1, [.5001f]));
        _ = precise.DisplayPixels();
        var threshold = new RasterTransform(TransformKind.Threshold, Threshold: .5002f);
        Assert.False(threshold.Apply(precise).Mask![0, 0]);
        Assert.Equal(.5001f, precise.Channel![0, 0]);
        Assert.Equal((byte)128, colors[1, 0].A);
    }

    [Fact]
    public void PaletteIgnoresHiddenColorsAndPreservesPartialCoverage()
    {
        var colors = new Image2D<Color4b>(4, 1,
            [new(0, 255, 0, 0), new(255, 0, 0, 128), new(255, 0, 0, 255), new(0, 0, 255, 255)]);
        var reduced = new RasterTransform(TransformKind.Palette, Colors: 2).Apply(new(colors));
        Assert.Equal(colors.AsSpan().ToArray(), reduced.Colors!.AsSpan().ToArray());
        Assert.Contains("2 visible palette colors", reduced.Details);
        var transparent = new Image2D<Color4b>(2, 2);
        Assert.Equal("0 visible colors", new RasterTransform(TransformKind.Palette).Apply(new(transparent)).Details);
    }

    [Fact]
    public void ContoursKeepInteriorHolesAndFillHolesIsAnIndependentUndoableStep()
    {
        var mask = new Image2D<bool>(7, 7);
        mask.Fill(true);
        mask[3, 3] = false;
        var history = new RasterHistory();
        history.Load(new(mask), "region");
        var contours = new RasterTransform(TransformKind.Contours);
        var traced = contours.Apply(history.Current!);
        Assert.Contains("2 boundary loops", traced.Details);
        Assert.False(traced.Mask![3, 3]);
        Assert.True(traced.Mask[3, 2]);
        Assert.False(traced.Mask[1, 1]);
        Assert.True(traced.Mask[0, 0]);
        var fill = new RasterTransform(TransformKind.FillHoles);
        history.Apply(fill, fill.Apply(history.Current!));
        Assert.True(history.Current!.Mask![3, 3]);
        Assert.Contains("1 boundary loops", contours.Apply(history.Current).Details);
        history.Undo();
        Assert.False(history.Current!.Mask![3, 3]);
        Assert.False(mask[3, 3]);
    }

    [Fact]
    public void RegionCleanupHonorsConnectivityAndLargestRegionOnEmptyMasks()
    {
        var mask = new Image2D<bool>(4, 4);
        mask[0, 0] = mask[1, 1] = mask[3, 3] = true;
        var remove = new RasterTransform(TransformKind.RemoveSmallRegions, MinimumPixels: 2);
        var eight = remove.Apply(new(mask)).Mask!;
        Assert.True(eight[0, 0]);
        Assert.True(eight[1, 1]);
        Assert.False(eight[3, 3]);
        Assert.DoesNotContain(true, (remove with { EightConnected = false }).Apply(new(mask)).Mask!.AsSpan().ToArray());
        var largest = new RasterTransform(TransformKind.LargestRegion);
        Assert.Equal(eight.AsSpan().ToArray(), largest.Apply(new(mask)).Mask!.AsSpan().ToArray());
        Assert.DoesNotContain(true, largest.Apply(new(new Image2D<bool>(4, 4))).Mask!.AsSpan().ToArray());
        Assert.NotNull(new RasterTransform(TransformKind.Contours).UnavailableReason(new(new Image2D<float>(1, 1))));
    }

    [Fact]
    public void PngRoundTripPreservesDimensionsColorsAndAlphaWithoutLockingTheSource()
    {
        var path = Path.Combine(Path.GetTempPath(), $"app2d-image-lab-{Guid.NewGuid():N}.png");
        try
        {
            var pixels = new Image2D<Color4b>(2, 2, [new(30, 60, 90, 128), Color4b.Red, Color4b.Blue, Color4b.White]);
            new RasterFrame(pixels).SavePng(path);
            var loaded = RasterFrame.Load(path);
            Assert.Equal(2, loaded.Width);
            Assert.Equal(2, loaded.Height);
            Assert.Equal(pixels.AsSpan().ToArray(), loaded.Colors!.AsSpan().ToArray());
            loaded.SavePng(path);
            Assert.Equal(pixels.AsSpan().ToArray(), RasterFrame.Load(path).Colors!.AsSpan().ToArray());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task PreviewDropsAnOldRequestAndReportsInvalidOperationsWithoutEditingInput()
    {
        var source = new RasterFrame(new Image2D<float>(1, 1, [.25f]));
        var preview = new RasterPreview();
        preview.Change(source, new(TransformKind.Invert));
        await Task.Delay(180);
        preview.Poll(); // Launch the old request but do not publish it.
        preview.Change(source, new(TransformKind.Threshold, Threshold: .5f));
        Assert.Null(preview.Frame);
        var timeout = DateTime.UtcNow.AddSeconds(5);
        while (preview.Pending && DateTime.UtcNow < timeout)
        {
            preview.Poll();
            await Task.Delay(20);
        }
        Assert.False(preview.Pending);
        Assert.False(preview.Frame!.Mask![0, 0]);
        Assert.Equal(.25f, source.Channel![0, 0]);
        preview.Change(source, new(TransformKind.FillHoles));
        Assert.False(preview.Pending);
        Assert.Null(preview.Frame);
        Assert.Contains("Threshold", preview.Error);
    }
}
