using App2d.Core.Imaging;
using System.Numerics;

namespace App2d.Tests.Imaging;

public sealed class PaletteQuantization2DTests
{
    [Fact]
    public void FindsColorClustersAndReconstructsWithoutChangingSource()
    {
        Vector3[] pixels = [new(.8f, 0, 0), new(1, 0, 0), new(0, 0, .8f), new(0, 0, 1)];
        var source = new Image2D<Vector3>(2, 2, pixels);
        var (palette, indices) = source.QuantizePalette(2);
        Assert.Equal(2, palette.Length);
        var reconstructed = indices.Map(index => palette[index]);
        Assert.InRange(Vector3.Distance(new(.9f, 0, 0), reconstructed[0, 0]), 0, 1e-6f);
        Assert.Equal(reconstructed[0, 0], reconstructed[1, 0]);
        Assert.InRange(Vector3.Distance(new(0, 0, .9f), reconstructed[0, 1]), 0, 1e-6f);
        Assert.Equal(reconstructed[0, 1], reconstructed[1, 1]);
        Assert.Equal(pixels, source.AsSpan().ToArray());
        var again = source.QuantizePalette(2);
        Assert.Equal(palette, again.Palette);
        Assert.Equal(indices.AsSpan().ToArray(), again.Indices.AsSpan().ToArray());
    }

    [Fact]
    public void OneColorIsWeightedByPixelFrequencyAndLargeFiniteValuesDoNotOverflow()
    {
        var source = new Image2D<Vector3>(4, 1, [Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.One]);
        Assert.Equal(new Vector3(.25f), Assert.Single(source.QuantizePalette(1).Palette));
        var extreme = new Image2D<Vector3>(2, 1, [new(float.MaxValue), new(-float.MaxValue)]);
        Assert.Equal(Vector3.Zero, Assert.Single(extreme.QuantizePalette(1).Palette));
        Assert.Equal(2, extreme.QuantizePalette(2).Palette.Length);
    }

    [Fact]
    public void OversizedRequestsKeepOnlyDistinctUsedColorsAndEmptyDimensionsSurvive()
    {
        var source = new Image2D<Vector3>(4, 1, [Vector3.One, Vector3.Zero, Vector3.One, Vector3.Zero]);
        var (palette, indices) = source.QuantizePalette(100);
        Assert.Equal(2, palette.Length);
        Assert.Equal([0, 1, 0, 1], indices.AsSpan().ToArray());
        foreach (var index in Enumerable.Range(0, palette.Length))
            Assert.Contains(index, indices.AsSpan().ToArray());

        var empty = new Image2D<Vector3>(0, 7).QuantizePalette(8);
        Assert.Empty(empty.Palette);
        Assert.Equal(7, empty.Indices.Height);
        Assert.Equal(0, empty.Indices.Count);
    }

    [Fact]
    public void LabelsUseTheReturnedCentersEvenWhenIterationsAreLimited()
    {
        var source = new Image2D<Vector3>(7, 1,
            [new(0, 0, 0), new(.1f, 0, 0), new(.3f, 0, 0), new(.4f, 0, 0),
             new(.5f, 0, 0), new(.6f, 0, 0), new(1, 0, 0)]);
        var (palette, indices) = source.QuantizePalette(2, maxIterations: 1);
        for (var i = 0; i < source.Count; i++)
        {
            var color = source.AsSpan()[i];
            var assignedDistance = Vector3.DistanceSquared(color, palette[indices.AsSpan()[i]]);
            Assert.All(palette, center => Assert.True(assignedDistance <= Vector3.DistanceSquared(color, center)));
        }
    }

    [Fact]
    public void RejectsInvalidCountsAndNonfiniteChannels()
    {
        var source = new Image2D<Vector3>(1, 1);
        Assert.Throws<ArgumentNullException>(() => PaletteQuantization2D.QuantizePalette(null!, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.QuantizePalette(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.QuantizePalette(2, maxIterations: 0));
        source[0, 0] = new(float.NaN, 0, 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => source.QuantizePalette(2));
    }
}
