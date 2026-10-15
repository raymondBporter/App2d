using App2d.Core.Mathematics;

namespace App2d.Tests.Mathematics;

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
        Assert.Equal(expected, Progress.Clamp01(input), 6);
        Assert.Equal(expected, Progress.SmoothStep(input), 6);
        Assert.Equal(expected, Progress.SmootherStep(input), 6);
        Assert.Equal(expected, Progress.SmoothestStep(input), 6);
    }

    [Fact]
    public void HigherOrdersFlattenProgressNearEndpoints()
    {
        const float progress = 0.1f;
        Assert.True(Progress.SmootherStep(progress) < Progress.SmoothStep(progress));
        Assert.True(Progress.SmoothestStep(progress) < Progress.SmootherStep(progress));
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
        AssertInverse(progress, Progress.SmoothStep, Progress.InverseSmoothStep);
        AssertInverse(progress, Progress.SmootherStep, Progress.InverseSmootherStep);
        AssertInverse(progress, Progress.SmoothestStep, Progress.InverseSmoothestStep);
    }

    [Fact]
    public void StepFunctionsAreMonotonic()
    {
        AssertMonotonic(Progress.SmoothStep);
        AssertMonotonic(Progress.SmootherStep);
        AssertMonotonic(Progress.SmoothestStep);
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
