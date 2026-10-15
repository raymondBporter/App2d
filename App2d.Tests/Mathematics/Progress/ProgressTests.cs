using App2d.Core.Mathematics;

namespace App2d.Tests.Mathematics.Progress;

public sealed class ProgressTests
{
    [Theory]
    [InlineData(-1f, 0f)]
    [InlineData(0f, 0f)]
    [InlineData(0.5f, 0.5f)]
    [InlineData(1f, 1f)]
    [InlineData(2f, 1f)]
    public void StepFunctionsClampAndKeepEndpoints(float input, float expected)
    {
        Assert.Equal(expected, Clamping.Clamp01(input), 6);
        Assert.Equal(expected, App2d.Core.Mathematics.Progress.Progress.SmoothStep(input), 6);
        Assert.Equal(expected, App2d.Core.Mathematics.Progress.Progress.SmootherStep(input), 6);
        Assert.Equal(expected, App2d.Core.Mathematics.Progress.Progress.SmoothestStep(input), 6);
    }

    [Fact]
    public void HigherOrdersFlattenProgressNearEndpoints()
    {
        const float progress = 0.1f;
        Assert.True(App2d.Core.Mathematics.Progress.Progress.SmootherStep(progress) < App2d.Core.Mathematics.Progress.Progress.SmoothStep(progress));
        Assert.True(App2d.Core.Mathematics.Progress.Progress.SmoothestStep(progress) < App2d.Core.Mathematics.Progress.Progress.SmootherStep(progress));
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
        AssertInverse(progress, App2d.Core.Mathematics.Progress.Progress.SmoothStep, App2d.Core.Mathematics.Progress.Progress.InverseSmoothStep);
        AssertInverse(progress, App2d.Core.Mathematics.Progress.Progress.SmootherStep, App2d.Core.Mathematics.Progress.Progress.InverseSmootherStep);
        AssertInverse(progress, App2d.Core.Mathematics.Progress.Progress.SmoothestStep, App2d.Core.Mathematics.Progress.Progress.InverseSmoothestStep);
    }

    [Fact]
    public void StepFunctionsAreMonotonic()
    {
        AssertMonotonic(App2d.Core.Mathematics.Progress.Progress.SmoothStep);
        AssertMonotonic(App2d.Core.Mathematics.Progress.Progress.SmootherStep);
        AssertMonotonic(App2d.Core.Mathematics.Progress.Progress.SmoothestStep);
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
