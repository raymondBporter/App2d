using App2d.Core.Imaging;
using System.Numerics;

namespace App2d.Tests.Imaging;

public sealed class ColorSpaces2DTests
{
    [Fact]
    public void PrimariesMatchPublishedOklabConversion()
    {
        AssertClose(new(.62795536f, .22486306f, .12584630f), ColorSpaces2D.SrgbToOklab(Vector3.UnitX));
        AssertClose(new(.86643961f, -.23388757f, .17949848f), ColorSpaces2D.SrgbToOklab(Vector3.UnitY));
        AssertClose(new(.45201372f, -.03245698f, -.31152815f), ColorSpaces2D.SrgbToOklab(Vector3.UnitZ));
        AssertClose(new(1, 0, 0), ColorSpaces2D.SrgbToOklab(Vector3.One));
        Assert.Equal(Vector3.Zero, ColorSpaces2D.SrgbToOklab(Vector3.Zero));
    }

    [Fact]
    public void RoundTripsIncludeBothSrgbTransferBranchesAndDoNotClampOutput()
    {
        Vector3[] samples = [new(.01f, .04f, .05f), new(.2f, .5f, .8f), new(.95f, .33f, .1f)];
        foreach (var color in samples)
            AssertClose(color, ColorSpaces2D.OklabToSrgb(ColorSpaces2D.SrgbToOklab(color)));

        Assert.True(ColorSpaces2D.OklabToSrgb(new(.7f, .4f, .4f)).X > 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => ColorSpaces2D.SrgbToOklab(new(-.1f, 0, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => ColorSpaces2D.SrgbToOklab(new(float.NaN, 0, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => ColorSpaces2D.OklabToSrgb(new(0, float.PositiveInfinity, 0)));
    }

    private static void AssertClose(Vector3 expected, Vector3 actual) =>
        Assert.InRange(Vector3.Distance(expected, actual), 0, 2e-6f);
}
