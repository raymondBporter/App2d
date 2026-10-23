using App2d.Core.Imaging;
using App2d.Core.Rendering.Textures;
using System.Numerics;

namespace App2d.Tests.Imaging;

public sealed class Image2DTests
{
    [Fact]
    public void PixelsRowsAndSpansShareStorageAndSupportValueTypeMath()
    {
        var image = new Image2D<Vector2>(3, 2);
        image[2, 1] = new(4, 5);
        image[2, 1].X *= 2;
        Assert.Equal(new Vector2(8, 5), image.AsSpan()[5]);
        image.GetRowSpan(0).Fill(Vector2.One);
        Assert.Equal(Vector2.One, image[2, 0]);
        Assert.Equal(Vector2.Zero, image[0, 1]);
        Assert.Equal(6, image.Count);
        image.Fill(new(7));
        Assert.All(image.AsSpan().ToArray(), value => Assert.Equal(new Vector2(7), value));
        image.Clear();
        Assert.All(image.AsSpan().ToArray(), value => Assert.Equal(Vector2.Zero, value));
    }

    [Fact]
    public void CheckedAccessRejectsRowAliasingAndNeighborSamplingHasExplicitBackground()
    {
        var image = new Image2D<float>(3, 2);
        image[0, 1] = 42;
        Assert.Throws<ArgumentOutOfRangeException>(() => image[3, 0] = 99);
        Assert.Throws<ArgumentOutOfRangeException>(() => image[-1, 1] = 99);
        Assert.Throws<ArgumentOutOfRangeException>(() => image[0, 2] = 99);
        Assert.Throws<ArgumentOutOfRangeException>(() => image.GetRowSpan(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => image.GetRowSpan(2));
        Assert.False(image.Contains(3, 0));
        Assert.Equal(0, image.GetValueOrDefault(3, 0));
        Assert.Equal(-1, image.GetValueOrDefault(-1, 1, outside: -1));
        Assert.Equal(42, image.GetValueOrDefault(0, 1));
    }

    [Fact]
    public void CopiesOwnTheirPixelsAndDimensionsCannotOverflow()
    {
        float[] pixels = [1, 2, 3, 4, 5, 6];
        var image = new Image2D<float>(3, 2, pixels);
        var clone = image.Clone();
        pixels[0] = 9;
        image[1, 0] = 10;
        Assert.Equal(1, image[0, 0]);
        Assert.Equal(2, clone[1, 0]);
        Assert.Throws<ArgumentException>(() => new Image2D<float>(3, 2, new float[5]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Image2D<byte>(-1, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Image2D<byte>(2, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Image2D<byte>(65536, 65536));

        var emptyRows = new Image2D<float>(0, 3);
        Assert.True(emptyRows.GetRowSpan(2).IsEmpty);
        Assert.Equal(3, emptyRows.Map(static value => value > 0).Height);
        Assert.True(new Image2D<float>(3, 0).AsSpan().IsEmpty);
        Assert.Throws<ArgumentOutOfRangeException>(() => emptyRows[0, 0] = 1);
    }

    [Fact]
    public void MappingChangesPixelTypeAndFloatArithmeticDoesNotQuantizeOrImplicitlyClamp()
    {
        var input = new Image2D<byte>(3, 2, [0, 64, 128, 192, 255, 32]);
        var channel = input.Map(static value => value / 255f);
        Assert.Equal(128 / 255f, channel[2, 0]);
        channel.Transform(static value => value * 4f - 1f);
        Assert.Equal(-1, channel[0, 0]);
        Assert.Equal(3, channel[1, 1]);
        channel.Transform(static value => MathF.Pow(Math.Clamp(value, 0f, 1f), 2f));
        var binary = channel.Map(static value => value >= .5f);
        Assert.Equal([false, false, true, true, true, false], binary.AsSpan().ToArray());
        Assert.Equal(64, input[1, 0]);

        Assert.Throws<ArgumentNullException>(() => input.Map((Func<byte, bool>)null!));
        Assert.Throws<ArgumentNullException>(() => channel.Transform(null!));
        Assert.Throws<ArgumentNullException>(() => ImageOperations2D.Map<byte, bool>(null!, static _ => true));
    }

    [Fact]
    public void TexturePixelsCanBeProcessedWithoutEditingTheRendererTexture()
    {
        using var bitmap = new Bitmap(3, 2);
        bitmap.SetPixel(0, 0, Color.FromArgb(255, 255, 0, 0));
        bitmap.SetPixel(1, 0, Color.FromArgb(64, 0, 255, 0));
        bitmap.SetPixel(2, 1, Color.FromArgb(128, 0, 0, 255));
        using var texture = Texture2D.FromBitmap("image-test", bitmap);
        var colors = texture.CopyImage();
        var channel = colors.Map(static color => color.A / 255f);
        Assert.Equal(64 / 255f, channel[1, 0]);
        Assert.Equal(128 / 255f, channel[2, 1]);
        var mask = channel.Map(static value => value >= .5f);
        Assert.True(mask[0, 0]);
        Assert.False(mask[1, 0]);
        Assert.True(mask[2, 1]);
        Assert.Equal(mask.AsSpan().ToArray(), mask.Skeletonize().AsSpan().ToArray());

        colors[0, 0] = Color4b.Black;
        Assert.Equal(Color4b.Red, texture.CopyPixels()[0]);
        texture.Dispose();
        Assert.Equal(Color4b.Black, colors[0, 0]);
        Assert.Throws<ObjectDisposedException>(() => texture.CopyImage());
    }

    [Fact]
    public void WarmInPlacePixelMathDoesNotAllocate()
    {
        var image = new Image2D<float>(64, 64);
        for (var i = 0; i < 100; i++)
            Apply(image);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++)
            Apply(image);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.InRange(image[32, 32], 0f, 1f);

        static void Apply(Image2D<float> image) =>
            image.Transform(static value => Math.Clamp(value * .9f + .1f, 0f, 1f));
    }
}
