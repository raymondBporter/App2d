using App2d.Core.Imaging;
using App2d.ImageLab;

namespace App2d.Tests.Imaging;

public sealed class RasterTransformProcessorTests
{
    [Fact]
    public void ChangingCutoffsReusesPreparationAndMatchesTheUncachedResult()
    {
        var source = CreateSource();
        var processor = new RasterTransformProcessor();
        var options = new RasterTransform(TransformKind.Canny, Smoothing: 1.3f);
        foreach (var weak in new[] { .003f, .015f, .025f, .007f })
        {
            var transform = options with { WeakEdge = weak, StrongEdge = weak * 3 };
            var expected = transform.Apply(source).Mask!;
            var actual = processor.Apply(source, transform).Mask!;
            Assert.Equal(expected.AsSpan().ToArray(), actual.AsSpan().ToArray());
        }
        Assert.Equal(1, processor.ChannelConversionCount);
        Assert.Equal(1, processor.CannyPreparationCount);
        Assert.Equal((long)source.Width * source.Height * 2 * sizeof(float), processor.CachedPixelBytes);
    }

    [Fact]
    public void SigmaChannelAndSourceChangesInvalidateOnlyTheirDependentStages()
    {
        var source = CreateSource();
        var processor = new RasterTransformProcessor();
        var options = new RasterTransform(TransformKind.Canny);
        processor.Apply(source, options);
        processor.Apply(source, options with { Smoothing = 2 });
        Assert.Equal(1, processor.ChannelConversionCount);
        Assert.Equal(2, processor.CannyPreparationCount);
        processor.Apply(source, options with { Channel = ChannelSource.OklabLightness });
        Assert.Equal(2, processor.ChannelConversionCount);
        Assert.Equal(3, processor.CannyPreparationCount);
        var anotherStage = source.Clone();
        processor.Apply(anotherStage, options);
        Assert.Equal(3, processor.ChannelConversionCount);
        Assert.Equal(4, processor.CannyPreparationCount);
        processor.KeepSource(null);
        Assert.Equal(0, processor.CachedPixelBytes);
        processor.Apply(anotherStage, options);
        Assert.Equal(5, processor.CannyPreparationCount);
    }

    [Fact]
    public void ScalarSourcesIgnoreDormantColorChoicesAndOtherTransformsRemainAvailable()
    {
        var source = new RasterFrame(CreateSource().GetChannel());
        var processor = new RasterTransformProcessor();
        var options = new RasterTransform(TransformKind.Canny);
        var first = processor.Apply(source, options);
        var same = processor.Apply(source, options with { Channel = ChannelSource.Alpha });
        Assert.Equal(first.Mask!.AsSpan().ToArray(), same.Mask!.AsSpan().ToArray());
        Assert.Equal(1, processor.CannyPreparationCount);
        Assert.Equal(1, processor.ChannelConversionCount);
        Assert.NotNull(processor.Apply(source, new(TransformKind.Threshold)).Mask);
        Assert.Throws<ArgumentOutOfRangeException>(() => processor.Apply(source, options with { WeakEdge = .2f, StrongEdge = .1f }));
        Assert.Equal(1, processor.CannyPreparationCount);
    }

    private static RasterFrame CreateSource()
    {
        var random = new Random(9041);
        var image = new Image2D<Color4b>(37, 29);
        for (var i = 0; i < image.Count; i++)
        {
            image.AsSpan()[i] = new((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256), (byte)255);
        }
        return new(image);
    }
}
