using App2d.Core.Imaging;
using App2d.ImageLab;
using System.Numerics;

namespace App2d.Tests.Imaging;

public sealed class RasterBrushTests
{
    [Fact]
    public void AContinuousDragHasNoGapsAndCommitsAsOneUndoableStep()
    {
        var history = new RasterHistory();
        history.Load(new(new Image2D<bool>(24, 8)), "mask");
        var stroke = new RasterBrushStroke(history.Current!.Mask!, 1, foreground: true);
        stroke.MoveTo(new(1.5f, 3.5f));
        stroke.MoveTo(new(20.5f, 3.5f));
        Assert.True(stroke.Changed);
        Assert.All(stroke.Frame.Mask!.GetRowSpan(3).Slice(1, 20).ToArray(), Assert.True);
        Assert.DoesNotContain(true, history.Current.Mask!.AsSpan().ToArray());
        Assert.Single(history.Steps);
        history.Apply(stroke.Description, stroke.Frame);
        Assert.Equal(2, history.Steps.Count);
        history.Undo();
        Assert.DoesNotContain(true, history.Current!.Mask!.AsSpan().ToArray());
        history.Redo();
        Assert.True(history.Current!.Mask![10, 3]);
    }

    [Fact]
    public void ErasingClipsAtTheImageBorderAndDoesNotChangeTheInput()
    {
        var mask = new Image2D<bool>(8, 8);
        mask.Fill(true);
        var stroke = new RasterBrushStroke(mask, 2, foreground: false);
        Assert.True(stroke.MoveTo(new(-.5f, -.5f)));
        Assert.False(stroke.Frame.Mask![0, 0]);
        Assert.True(stroke.Frame.Mask[7, 7]);
        stroke.MoveTo(new(-20, -20));
        Assert.True(mask[0, 0]);
        var blank = new RasterBrushStroke(new Image2D<bool>(8, 8), 2, foreground: false);
        Assert.False(blank.MoveTo(new(3, 3)));
        Assert.False(blank.Changed);
        Assert.Throws<ArgumentOutOfRangeException>(() => new RasterBrushStroke(mask, 0, true));
        Assert.Throws<ArgumentOutOfRangeException>(() => stroke.MoveTo(new Vector2(float.NaN)));
    }

    [Fact]
    public void ABrushBranchReplacesRedoWithoutAlteringEarlierMasks()
    {
        var mask = new Image2D<bool>(10, 10);
        mask.Fill(true);
        var history = new RasterHistory();
        history.Load(new(mask), "mask");
        var first = new RasterBrushStroke(history.Current!.Mask!, 1, false);
        first.MoveTo(new(2.5f, 2.5f));
        history.Apply(first.Description, first.Frame);
        history.Undo();
        var replacement = new RasterBrushStroke(history.Current!.Mask!, 1, false);
        replacement.MoveTo(new(7.5f, 7.5f));
        history.Apply(replacement.Description, replacement.Frame);
        Assert.False(history.CanRedo);
        Assert.True(history.Current!.Mask![2, 2]);
        Assert.False(history.Current.Mask[7, 7]);
        Assert.True(history.Steps[0].Frame.Mask![7, 7]);
    }

    [Fact]
    public void ChannelChoicesExtractHsvAndOklabFromTheOriginalColors()
    {
        var frame = new RasterFrame(new Image2D<Color4b>(2, 1, [Color4b.Red, new(128, 128, 128)]));
        Assert.Equal(1, frame.GetChannel(ChannelSource.HsvSaturation)[0, 0]);
        Assert.Equal(0, frame.GetChannel(ChannelSource.HsvSaturation)[1, 0]);
        Assert.Equal(128 / 255f, frame.GetChannel(ChannelSource.HsvValue)[1, 0]);
        Assert.Equal(.62795536f, frame.GetChannel(ChannelSource.OklabLightness)[0, 0], 6);
        Assert.Equal(.72486306f, frame.GetChannel(ChannelSource.OklabA)[0, 0], 6);
        Assert.Equal(.5f, frame.GetChannel(ChannelSource.OklabB)[1, 0], 5);
    }
}
