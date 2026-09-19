using App2d.Diagnostics;
using App2d.Gameplay.Player;
using System.Numerics;
using Xunit;

namespace App2d.Tests.Diagnostics;

public sealed class BallisticsDebug2DTests
{
    [Fact]
    public void TenMetersPerSecondAtFortyFiveDegreesHasKnownReferenceArc()
    {
        var diagnostic = Create();
        var origin = new Vector2(300f, 80f);
        diagnostic.Launch(origin, 1f);
        var shot = diagnostic.Reference!.Value;

        Assert.Equal(2.5f, shot.Rise / 32f, 4);
        Assert.Equal(10f, shot.Range / 32f, 4);
        Assert.Equal(MathF.Sqrt(2f), shot.FlightSeconds, 4);
        AssertClose(origin + new Vector2(160f, 80f), shot.PositionAt(shot.FlightSeconds / 2f));
        AssertClose(origin + new Vector2(320f, 0f), shot.PositionAt(shot.FlightSeconds));

        var world = diagnostic.World!.Value;
        Assert.Equal(1900f, world.Gravity);
        Assert.Equal(shot.Velocity, world.Velocity);
        Assert.Equal(10f / 59.375f, world.FlightSeconds / shot.FlightSeconds, 5);
    }

    [Fact]
    public void ChangingSettingsDoesNotAlterAnAlreadyLaunchedShotAndFacingMirrorsRange()
    {
        var diagnostic = Create();
        diagnostic.Launch(new Vector2(60f, 120f), -1f);
        var original = diagnostic.Reference!.Value;
        diagnostic.SpeedTilesPerSecond = 20f;
        diagnostic.AngleDegrees = 90f;
        diagnostic.ReferenceGravityTilesPerSecondSquared = 20f;
        Assert.Equal(original, diagnostic.Reference!.Value);
        AssertClose(original.Origin + new Vector2(-320f, 0f), original.PositionAt(original.FlightSeconds));

        diagnostic.Advance(0.5f);
        diagnostic.Launch(Vector2.Zero, 1f);
        Assert.Equal(0f, diagnostic.ElapsedSeconds);
        Assert.Equal(640f, diagnostic.Reference!.Value.Gravity);
        Assert.Equal(640f, diagnostic.Reference.Value.Velocity.Y, 3);
    }

    [Fact]
    public void PlaybackIsFrameRateIndependentAndStopsAtTheLongerFlight()
    {
        var smallSteps = Create();
        var largeStep = Create();
        smallSteps.Launch(Vector2.Zero, 1f);
        largeStep.Launch(Vector2.Zero, 1f);
        for (var i = 0; i < 60; i++) smallSteps.Advance(1f / 120f);
        largeStep.Advance(0.5f);
        AssertClose(smallSteps.Reference!.Value.PositionAt(smallSteps.ElapsedSeconds),
            largeStep.Reference!.Value.PositionAt(largeStep.ElapsedSeconds));
        largeStep.Advance(10f);
        Assert.Equal(largeStep.Reference.Value.FlightSeconds, largeStep.ElapsedSeconds);
        largeStep.Clear();
        Assert.Null(largeStep.Reference);
        Assert.Null(largeStep.World);
        Assert.Equal(0f, largeStep.ElapsedSeconds);
    }

    [Fact]
    public void DiagnosticKeysRespectInputSuppression()
    {
        var input = new InputState();
        input.SetKey(Keys.F4, true);
        Assert.True(PlayerDebugInput2D.Capture(input).FireBallistics);
        input.EndFrame();
        Assert.False(PlayerDebugInput2D.Capture(input).FireBallistics);
        input.SetKey(Keys.F6, true);
        Assert.True(PlayerDebugInput2D.Capture(input).ClearBallistics);
        input.SetSuppressed(true);
        Assert.Equal(default(PlayerDebugInput2D), PlayerDebugInput2D.Capture(input));
    }

    private static BallisticsDebug2D Create() => new(TraversalMetrics2D.FromGeometry(
        new Vector2(138f), 0.635f, new Vector2(22f, 56f), 0f));

    private static void AssertClose(Vector2 expected, Vector2 actual) =>
        Assert.True(Vector2.Distance(expected, actual) < 0.001f, $"Expected {expected}, got {actual}.");
}
