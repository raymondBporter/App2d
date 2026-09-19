using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Tests.Mathematics;

public sealed class InterpolationTests
{
    [Theory]
    [InlineData(-1f, 0f)]
    [InlineData(0f, 0f)]
    [InlineData(0.5f, 0.5f)]
    [InlineData(1f, 1f)]
    [InlineData(2f, 1f)]
    public void StepFunctionsClampAndKeepEndpoints(float input, float expected)
    {
        Assert.Equal(expected, Interpolation.LinearStep(input), 6);
        Assert.Equal(expected, Interpolation.SmoothStep(input), 6);
        Assert.Equal(expected, Interpolation.SmootherStep(input), 6);
        Assert.Equal(expected, Interpolation.SmoothestStep(input), 6);
    }

    [Fact]
    public void HigherOrdersFlattenProgressNearEndpoints()
    {
        const float progress = 0.1f;
        Assert.True(Interpolation.SmootherStep(progress) < Interpolation.SmoothStep(progress));
        Assert.True(Interpolation.SmoothestStep(progress) < Interpolation.SmootherStep(progress));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.01f)]
    [InlineData(0.1f)]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(0.75f)]
    [InlineData(0.9f)]
    [InlineData(0.99f)]
    [InlineData(1f)]
    public void StepInversesRoundTrip(float progress)
    {
        AssertInverse(progress, Interpolation.SmoothStep, Interpolation.InverseSmoothStep);
        AssertInverse(progress, Interpolation.SmootherStep, Interpolation.InverseSmootherStep);
        AssertInverse(progress, Interpolation.SmoothestStep, Interpolation.InverseSmoothestStep);
    }

    [Fact]
    public void LerpCanComposeWithAProgressMapping()
    {
        Assert.Equal(12.5f, Interpolation.Lerp(10f, 20f, 0.25f), 6);
        Assert.Equal(11.5625f, Interpolation.Lerp(10f, 20f, 0.25f, Interpolation.SmoothStep), 6);

        var vector = Interpolation.Lerp(Vector2.Zero, new Vector2(8f, -4f), 0.5f, Interpolation.SmootherStep);
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

    [Fact]
    public void StepFunctionsAreMonotonic()
    {
        AssertMonotonic(Interpolation.SmoothStep);
        AssertMonotonic(Interpolation.SmootherStep);
        AssertMonotonic(Interpolation.SmoothestStep);
    }

    private static void AssertMonotonic(Func<float, float> step)
    {
        var previous = step(0f);
        for (var sample = 1; sample <= 1000; sample++)
        {
            var current = step(sample / 1000f);
            Assert.True(current >= previous, $"Curve decreased at sample {sample}: {current} < {previous}.");
            previous = current;
        }
    }

    private static void AssertInverse(
        float progress,
        Func<float, float> step,
        Func<float, float> inverse)
    {
        var mapped = step(progress);
        var recovered = inverse(mapped);
        // Flat high-order endpoints amplify float quantization when inverted. The
        // mapped value is the exact contract; original progress is necessarily approximate.
        Assert.Equal(mapped, step(recovered), 6);
        Assert.Equal(progress, recovered, 3);
    }
}
