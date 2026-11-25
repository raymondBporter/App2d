using System.Text.Json;

namespace App2d.Tests.Mathematics.Progress;

public sealed class GaussianProfileJsonTests
{
    [Fact]
    public void AGaussianProfileAndMapsBuiltFromItRoundTrip()
    {
        var bump = Easingd.Gaussian(.5d, .1d);
        var scaled = bump.ToRange(0d, 4d);

        var json = JsonSerializer.Serialize(bump);
        var bumpAgain = JsonSerializer.Deserialize<ProgressMapd>(json)!;
        var scaledAgain = JsonSerializer.Deserialize<ProgressMapd>(JsonSerializer.Serialize(scaled))!;

        Assert.Contains("gaussian", json);
        Assert.Equal(bump.Evaluate(.3d), bumpAgain.Evaluate(.3d));
        Assert.Equal(bump.Derivative(.3d), bumpAgain.Derivative(.3d));
        Assert.Equal(scaled.Evaluate(.45d), scaledAgain.Evaluate(.45d));
    }
}
