using App2d.Core.Imaging;
using System.Numerics;

namespace App2d.Tests.Imaging;

public sealed class ImageEdges2DTests
{
    [Fact]
    public void VectorizedBlurMatchesScalarConvolutionAcrossBordersAndPartialVectors()
    {
        var random = new Random(1049);
        foreach (var width in new[] { 1, 2, 7, Vector<float>.Count, Vector<float>.Count + 1, 37 })
        {
            var image = new Image2D<float>(width, 11);
            for (var i = 0; i < image.Count; i++)
            {
                image.AsSpan()[i] = random.NextSingle();
            }
            foreach (var sigma in new[] { .2f, 1.2f, 4f })
            {
                var expected = ScalarBlur(image, sigma);
                var actual = image.GaussianBlur(sigma);
                Assert.Equal(expected.AsSpan().ToArray(), actual.AsSpan().ToArray());
            }
        }
    }

    private static Image2D<float> ScalarBlur(Image2D<float> source, float sigma)
    {
        var radius = (int)MathF.Ceiling(3 * sigma);
        var weights = Enumerable.Range(-radius, 2 * radius + 1)
            .Select(offset => MathF.Exp(-.5f * MathF.Pow(offset / sigma, 2))).ToArray();
        var sum = 0f;
        foreach (var weight in weights)
        {
            sum += weight;
        }
        for (var i = 0; i < weights.Length; i++)
        {
            weights[i] /= sum;
        }
        var temporary = new Image2D<float>(source.Width, source.Height);
        var output = new Image2D<float>(source.Width, source.Height);
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                for (var offset = -radius; offset <= radius; offset++)
                {
                    temporary[x, y] += source[Math.Clamp(x + offset, 0, source.Width - 1), y] * weights[offset + radius];
                }
            }
        }
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                for (var offset = -radius; offset <= radius; offset++)
                {
                    output[x, y] += temporary[x, Math.Clamp(y + offset, 0, source.Height - 1)] * weights[offset + radius];
                }
            }
        }
        return output;
    }

    [Fact]
    public void HysteresisFollowsWeakChainsButRejectsIsolatedWeakEdgesAndZeroBackground()
    {
        var strengths = new Image2D<float>(12, 4);
        strengths[1, 1] = .8f;
        for (var x = 2; x < 8; x++)
        {
            strengths[x, 1] = .2f;
        }
        strengths[8, 2] = .2f; // Diagonal continuation.
        strengths[10, 1] = .2f; // Disconnected weak edge.
        strengths[9, 2] = .05f; // Below the weak cutoff.
        var result = strengths.HysteresisThreshold(.1f, .5f);
        Assert.True(result[8, 2]);
        Assert.False(result[10, 1]);
        Assert.False(result[9, 2]);
        Assert.Equal(8, result.AsSpan().ToArray().Count(static pixel => pixel));
        Assert.Equal(.2f, strengths[8, 2]);
        Assert.DoesNotContain(true, new Image2D<float>(4, 4).HysteresisThreshold(0, 0).AsSpan().ToArray());
    }

    [Fact]
    public void GaussianPreservesConstantsAtBordersAndSymmetricallySpreadsAnImpulse()
    {
        var constant = new Image2D<float>(7, 5);
        constant.Fill(.4f);
        Assert.All(constant.GaussianBlur(2).AsSpan().ToArray(), value => Assert.Equal(.4f, value, 6));
        var impulse = new Image2D<float>(15, 15);
        impulse[7, 7] = 1;
        var blurred = impulse.GaussianBlur(1);
        Assert.Equal(1, blurred.AsSpan().ToArray().Sum(), 5);
        Assert.InRange(blurred[7, 7], .1f, .2f);
        Assert.Equal(blurred[6, 7], blurred[8, 7]);
        Assert.Equal(blurred[7, 6], blurred[7, 8]);
        var copy = impulse.GaussianBlur(0);
        copy[7, 7] = 0;
        Assert.Equal(1, impulse[7, 7]);
    }

    [Fact]
    public void CannyThinsAStraightStepWithoutAddingAFrameOrChangingInput()
    {
        var source = new Image2D<float>(24, 18);
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 12; x < source.Width; x++)
            {
                source[x, y] = .8f;
            }
        }
        var edges = source.CannyEdges(.02f, .06f, 1);
        for (var y = 0; y < source.Height; y++)
        {
            Assert.Equal(1, edges.GetRowSpan(y).ToArray().Count(static pixel => pixel));
            Assert.Contains(true, edges.GetRowSpan(y).Slice(10, 4).ToArray());
        }
        Assert.Equal(.8f, source[23, 17]);
        Assert.Equal(0, source[0, 0]);
        source.Fill(.5f);
        Assert.DoesNotContain(true, source.CannyEdges().AsSpan().ToArray());
    }

    [Fact]
    public void CannyRetainsAFaintContinuationThatAStrongCutoffAloneWouldLose()
    {
        var source = new Image2D<float>(64, 64);
        for (var y = 0; y < source.Height; y++)
        {
            var contrast = .8f - .72f * Math.Clamp((y - 10) / 30f, 0, 1);
            for (var x = 32; x < source.Width; x++)
            {
                source[x, y] = contrast;
            }
        }
        var linked = source.CannyEdges(.005f, .06f, 1);
        var strongOnly = source.CannyEdges(.06f, .06f, 1);
        Assert.Contains(true, linked.GetRowSpan(55).Slice(30, 4).ToArray());
        Assert.DoesNotContain(true, strongOnly.GetRowSpan(55).ToArray());
    }

    [Fact]
    public void EdgeOperationsHandleEmptyAndTinyImagesAndRejectInvalidParameters()
    {
        Assert.Equal(7, new Image2D<float>(0, 7).CannyEdges().Height);
        Assert.Equal(7, new Image2D<float>(7, 0).GaussianBlur(1).Width);
        Assert.False(new Image2D<float>(1, 1, [.5f]).CannyEdges()[0, 0]);
        var image = new Image2D<float>(3, 3);
        Assert.Throws<ArgumentOutOfRangeException>(() => image.CannyEdges(.1f, .05f));
        Assert.Throws<ArgumentOutOfRangeException>(() => image.CannyEdges(float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => image.GaussianBlur(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => image.GaussianBlur(float.PositiveInfinity));
        image[0, 0] = float.NaN;
        Assert.Throws<ArgumentOutOfRangeException>(() => image.CannyEdges());
        image[0, 0] = -.1f;
        Assert.Throws<ArgumentOutOfRangeException>(() => image.HysteresisThreshold(.01f, .1f));
    }

    [Fact]
    public void HsvUsesTurnsAndHandlesGrayBlackAndTheRedSeam()
    {
        Assert.Equal(new Vector3(0, 1, 1), ColorSpaces2D.SrgbToHsv(Vector3.UnitX));
        Assert.Equal(new Vector3(1f / 3, 1, 1), ColorSpaces2D.SrgbToHsv(Vector3.UnitY));
        Assert.Equal(new Vector3(2f / 3, 1, 1), ColorSpaces2D.SrgbToHsv(Vector3.UnitZ));
        Assert.Equal(Vector3.Zero, ColorSpaces2D.SrgbToHsv(Vector3.Zero));
        Assert.Equal(new Vector3(0, 0, .5f), ColorSpaces2D.SrgbToHsv(new(.5f)));
        Assert.InRange(ColorSpaces2D.SrgbToHsv(new(1, 0, .01f)).X, .99f, 1);
        Assert.InRange(ColorSpaces2D.SrgbToHsv(new(1, .01f, 0)).X, 0, .01f);
        Assert.Throws<ArgumentOutOfRangeException>(() => ColorSpaces2D.SrgbToHsv(new(-1, 0, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => ColorSpaces2D.SrgbToHsv(new(float.NaN, 0, 0)));
    }
}
