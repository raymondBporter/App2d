using App2d.Core.Imaging;
using System.Numerics;

namespace App2d.Tests.Imaging;

public sealed class ImageGradients2DTests
{
    [Fact]
    public void SobelMeasuresLinearRampsInImageCoordinates()
    {
        var source = new Image2D<float>(5, 4);
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
                source[x, y] = 2 * x - 3 * y + 10;
        }

        var gradient = source.Sobel();
        Assert.Equal(new Vector2(2, -3), gradient[2, 2]);
        Assert.Equal(new Vector2(1, -1.5f), gradient[0, 0]);
        Assert.Equal(10, source[0, 0]);
    }

    [Fact]
    public void ConstantImagesHaveNoArtificialBorderAndThinImagesWork()
    {
        var source = new Image2D<float>(1, 5);
        source.Fill(.75f);
        Assert.All(source.Sobel().AsSpan().ToArray(), value => Assert.Equal(Vector2.Zero, value));
        var colors = new Image2D<Vector3>(5, 1);
        colors.Fill(new(.2f, .6f, .9f));
        Assert.All(colors.SobelMagnitude().AsSpan().ToArray(), value => Assert.Equal(0, value));
        Assert.Equal(0, new Image2D<float>(0, 7).Sobel().Width);
        Assert.Equal(7, new Image2D<Vector3>(0, 7).SobelMagnitude().Height);
        Assert.Throws<ArgumentNullException>(() => ImageGradients2D.Sobel(null!));
        Assert.Throws<ArgumentNullException>(() => ImageGradients2D.SobelMagnitude(null!));
    }

    [Fact]
    public void ColorEdgesSurviveWhenGrayscaleWouldEraseThem()
    {
        // These two RGB colors have the same linear luminance under these weights.
        var source = new Image2D<Vector3>(4, 3);
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
                source[x, y] = x < 2 ? Vector3.UnitX : new(0, .2126f / .7152f, 0);
        }

        var gray = source.Map(static c => .2126f * c.X + .7152f * c.Y + .0722f * c.Z);
        Assert.InRange(gray.Sobel()[1, 1].Length(), 0, 1e-7f);
        var magnitude = source.SobelMagnitude();
        Assert.InRange(magnitude[1, 1], .52f, .53f);
        Assert.Equal(0, magnitude[0, 1]);
        Assert.Equal(0, magnitude[3, 1]);
    }
}
