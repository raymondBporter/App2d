using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Tests.Mathematics;

public sealed class InterpolationTests
{
    [Fact]
    public void LerpCanComposeWithAProgressMapping()
    {
        Assert.Equal(12.5f, Interpolation.Lerp(10f, 20f, 0.25f), 6);
        Assert.Equal(11.5625f, Interpolation.Lerp(10f, 20f, 0.25f, Core.Mathematics.Progress.Progress.SmoothStep), 6);

        var vector = Interpolation.Lerp(Vector2.Zero, new Vector2(8f, -4f), 0.5f, Core.Mathematics.Progress.Progress.SmootherStep);
        Assert.Equal(new Vector2(4f, -2f), vector);
    }

    [Fact]
    public void InverseLerpCanBeClampedOrUnclamped()
    {
        Assert.Equal(0.25f, Interpolation.InverseLerp(10f, 20f, 12.5f), 6);
        Assert.Equal(-1f, Interpolation.InverseLerp(10f, 20f, 0f), 6);
        Assert.Equal(0f, Interpolation.InverseLerpClamped(10f, 20f, 0f), 6);
        Assert.Equal(0f, Interpolation.InverseLerp(4f, 4f, 100f), 6);
    }
}
