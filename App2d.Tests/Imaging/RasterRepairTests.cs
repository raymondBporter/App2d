using App2d.Core.Imaging;
using App2d.ImageLab;
using System.Numerics;

namespace App2d.Tests.Imaging;

public sealed class RasterRepairTests
{
    [Fact]
    public void PixelBrushDabsExactlyOnePixelAndGroupsADragAsOneHistoryStep()
    {
        var source = new RasterFrame(new Image2D<bool>(16, 16));
        var stroke = new RasterPixelStroke(source, RasterInk.White);
        stroke.MoveTo(new(3.8f, 4.2f));
        Assert.Equal(1, stroke.Frame.Mask!.AsSpan().ToArray().Count(static pixel => pixel));
        Assert.True(stroke.Frame.Mask[3, 4]);
        stroke.MoveTo(new(9.5f, 10.5f));
        Assert.Equal(1, new RasterRegions(stroke.Frame.Mask, true).Count);
        var history = new RasterHistory();
        history.Load(source, "mask");
        history.Apply(stroke.Description, stroke.Frame);
        Assert.Equal(2, history.Steps.Count);
        history.Undo();
        Assert.DoesNotContain(true, history.Current!.Mask!.AsSpan().ToArray());
        history.Redo();
        Assert.True(history.Current!.Mask![9, 10]);
        Assert.DoesNotContain(true, source.Mask!.AsSpan().ToArray());
    }

    [Theory]
    [InlineData(12, 5)]
    [InlineData(0, 5)]
    [InlineData(5, 12)]
    [InlineData(5, 0)]
    [InlineData(12, 12)]
    [InlineData(0, 12)]
    [InlineData(0, 0)]
    [InlineData(12, 0)]
    [InlineData(7, 12)]
    [InlineData(12, 7)]
    [InlineData(3, 0)]
    [InlineData(0, 3)]
    public void LinesAreOnePixelWideAndEightConnectedInEveryOctant(int x, int y)
    {
        var source = new RasterFrame(new Image2D<bool>(14, 14));
        var line = new RasterPixelStroke(source, RasterInk.White, line: true);
        line.MoveTo(new(5.5f, 5.5f));
        line.MoveTo(new(x + .5f, y + .5f));
        var mask = line.Frame.Mask!;
        Assert.True(mask[5, 5]);
        Assert.True(mask[x, y]);
        Assert.Equal(Math.Max(Math.Abs(x - 5), Math.Abs(y - 5)) + 1, mask.AsSpan().ToArray().Count(static pixel => pixel));
        Assert.Equal(1, new RasterRegions(mask, true).Count);
    }

    [Fact]
    public void MovingALineEndpointRestoresItsOldPreviewAndARepairMergesComponents()
    {
        var source = new Image2D<bool>(12, 12);
        source[4, 1] = true;
        var line = new RasterPixelStroke(new(source), RasterInk.White, line: true);
        line.MoveTo(new(1.5f, 1.5f));
        line.MoveTo(new(9.5f, 1.5f));
        line.MoveTo(new(1.5f, 9.5f));
        Assert.False(line.Frame.Mask![7, 1]);
        Assert.True(line.Frame.Mask[4, 1]); // Existing pixels survive restoration.
        Assert.True(line.Frame.Mask[1, 7]);
        Assert.False(source[1, 7]);
        var gap = new Image2D<bool>(12, 4);
        for (var x = 1; x <= 4; x++)
        {
            gap[x, 2] = true;
        }
        for (var x = 7; x <= 10; x++)
        {
            gap[x, 2] = true;
        }
        Assert.Equal(2, new RasterRegions(gap, true).Count);
        var repair = new RasterPixelStroke(new(gap), RasterInk.White, line: true);
        repair.MoveTo(new(4.5f, 2.5f));
        repair.MoveTo(new(7.5f, 2.5f));
        Assert.Equal(1, new RasterRegions(repair.Frame.Mask!, true).Count);
        Assert.False(gap[5, 2]);
    }

    [Fact]
    public void EyedropperPreservesAlphaAndRawScalarPrecisionAndPaletteUsesImageValues()
    {
        var rgba = new RasterFrame(new Image2D<Color4b>(4, 1,
            [new(30, 60, 90, 64), Color4b.Red, Color4b.Red, Color4b.Blue]));
        var ink = RasterInk.Sample(rgba, 0, 0);
        var stroke = new RasterPixelStroke(rgba, ink);
        stroke.MoveTo(new(3.2f, .2f));
        Assert.Equal(new Color4b(30, 60, 90, 64), stroke.Frame.Colors![3, 0]);
        Assert.Equal(Color4b.Blue, rgba.Colors![3, 0]);
        Assert.Equal(Color4b.Red, RasterInk.Palette(rgba)[0].Color);
        var scalar = new RasterFrame(new Image2D<float>(3, 1, [.5001f, .1f, .1f]));
        var scalarInk = RasterInk.Sample(scalar, 0, 0);
        var line = new RasterPixelStroke(scalar, scalarInk, line: true);
        line.MoveTo(new(.5f, .5f));
        line.MoveTo(new(2.5f, .5f));
        Assert.All(line.Frame.Channel!.AsSpan().ToArray(), value => Assert.Equal(.5001f, value));
        Assert.Equal(.1f, scalar.Channel![2, 0]);
        Assert.Contains(RasterInk.Palette(scalar), entry => entry.Scalar == .5001f);
        Assert.Equal([RasterInk.Black, RasterInk.White], RasterInk.Palette(new(new Image2D<bool>(2, 2))));
    }

    [Fact]
    public void NoOpLinesDoNotChangePixelsAndEndpointsClipToTheImage()
    {
        var mask = new Image2D<bool>(4, 4);
        var erase = new RasterPixelStroke(new(mask), RasterInk.Black, line: true);
        erase.MoveTo(new(.5f, .5f));
        erase.MoveTo(new(100, 100));
        Assert.False(erase.Changed);
        var paint = new RasterPixelStroke(new(mask), RasterInk.White, line: true);
        paint.MoveTo(new(.5f, .5f));
        paint.MoveTo(new(100, .5f));
        Assert.Equal(4, paint.Frame.Mask!.AsSpan().ToArray().Count(static pixel => pixel));
        Assert.Throws<ArgumentOutOfRangeException>(() => paint.MoveTo(new Vector2(float.NaN)));
    }
}
