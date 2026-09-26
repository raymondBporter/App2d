using App2d.Core.Characters;
using App2d.Rendering.Characters;
using System.Numerics;

namespace App2d.Tests.Rendering;

public sealed class BladeSwooshTests
{
    private static (Vector3 Guard, Vector3 Tip) At(float degrees)
    {
        var (s, c) = MathF.SinCos(degrees * MathF.PI / 180);
        return (new(c * .1f, s * .1f, 0), new(c * .9f, s * .9f, 0));
    }

    private static CharacterMesh Swing(params (float Time, float Degrees, bool On)[] frames) => Swing(0, frames);

    private static CharacterMesh Swing(int sweep, params (float Time, float Degrees, bool On)[] frames)
    {
        var swoosh = new BladeSwoosh { Style = SwooshStyle.Primary with { InkWidth = 0 }, Sweep = sweep };
        foreach (var (time, degrees, on) in frames) { var (g, t) = At(degrees); swoosh.Record(time, g, t, on); }
        var mesh = new CharacterMesh(); swoosh.Build(mesh); return mesh;
    }

    [Fact]
    public void TwoFarApartFramesPutTheTipBackOnItsArc()
    {
        // One 90 degree step: a chord would sag to 0.64 at 45 degrees; the rebuilt rotation keeps the tip at 0.9.
        var mesh = Swing((0, 90, true), (1 / 60f, 0, true));
        var near45 = mesh.Vertices.ToArray().Select(v => new Vector2(v.Position.X, v.Position.Y))
            .Where(p => MathF.Abs(MathF.Atan2(p.Y, p.X) * 180 / MathF.PI - 45) < 4).Max(p => p.Length());
        Assert.InRange(near45, .88f, .905f);
        Assert.All(mesh.Vertices.ToArray(), v => Assert.True(new Vector2(v.Position.X, v.Position.Y).Length() <= .901f));
    }

    [Fact]
    public void ANearHalfTurnStepKeepsTurningTheWayTheSwingWas()
    {
        // Clockwise 60 degrees, then 170 more in one frame: the shorter way round (190 counter-clockwise) would sweep
        // the trail back over the start. The trail must stay on the clockwise side (x >= 0 half, bar the thin guard end).
        var mesh = Swing((0, 90, true), (1 / 60f, 30, true), (2 / 60f, -140, true));
        var points = mesh.Vertices.ToArray().Select(v => new Vector2(v.Position.X, v.Position.Y)).Where(p => p.Length() > .3f).ToArray();
        Assert.Contains(points, p => MathF.Abs(MathF.Atan2(p.Y, p.X) * 180 / MathF.PI + 45) < 6);
        Assert.DoesNotContain(points, p => MathF.Abs(MathF.Atan2(p.Y, p.X) * 180 / MathF.PI - 160) < 10);
    }

    [Fact]
    public void ASweepHintSendsAFirstStepOverTheTopInsteadOfUnderneath()
    {
        // 200 degrees back (pointing down-back) to 10 degrees in one step: the shorter way (170) passes underneath.
        static bool Under(CharacterMesh mesh) => mesh.Vertices.ToArray().Any(v => v.Position.Y < -.5f);
        static bool Over(CharacterMesh mesh) => mesh.Vertices.ToArray().Any(v => v.Position.Y > .5f);
        var guessed = Swing((0, 200, true), (1 / 60f, 10, true));
        Assert.True(Under(guessed) && !Over(guessed));
        var hinted = Swing(-1, (0, 200, true), (1 / 60f, 10, true));
        Assert.True(Over(hinted) && !Under(hinted));
    }

    [Fact]
    public void ASideSwingNarrowsWhereTheBladePointsAtTheCameraInsteadOfLooping()
    {
        // A blade turning about the vertical axis from pointing back to pointing forward, in two steps through pointing at
        // the camera (-Z). In the screen plane that is back, a short stub, forward: the trail must stay near the guard's
        // height rather than sweep a half circle over the top or underneath.
        var swoosh = new BladeSwoosh { Style = SwooshStyle.Primary with { InkWidth = 0 } };
        static (Vector3, Vector3) Yaw(float degrees) { var (s, c) = MathF.SinCos(degrees * MathF.PI / 180); return (Vector3.Zero, new(c * .9f, 0, -s * .9f)); }
        foreach (var (time, degrees) in new[] { (0f, 180f), (1 / 60f, 90f), (2 / 60f, 0f) }) { var (g, t) = Yaw(degrees); swoosh.Record(time, g, t, true); }
        var mesh = new CharacterMesh(); swoosh.Build(mesh);
        Assert.True(mesh.Count > 0);
        Assert.All(mesh.Vertices.ToArray(), v => Assert.InRange(v.Position.Y, -.01f, .01f));
        Assert.Contains(mesh.Vertices.ToArray(), v => v.Position.Z < -.8f);
    }

    [Fact]
    public void TheTrailClearsAfterTheSwingAndANewSwingStartsFresh()
    {
        Assert.True(Swing((0, 90, true), (1 / 60f, 0, true), (2 / 60f, -20, false)).Count > 0);
        Assert.Equal(0, Swing((0, 90, true), (1 / 60f, 0, true), (.2f, -20, false)).Count);
        // The second swing's trail never reaches back into the first.
        var second = Swing((0, 90, true), (1 / 60f, 0, true), (.1f, 0, false), (.2f, 180, true), (.2f + 1 / 60f, 170, true));
        Assert.All(second.Vertices.ToArray(), v => Assert.True(v.Position.X < 0 || v.Position.Length() < .2f));
    }

    [Fact]
    public void SwooshMarkersOpenAndCloseTheTrail()
    {
        var clip = new MotionClip { Markers = [new() { Id = PersonLoadout.SwooshMarker, Time = .05f }, new() { Id = PersonLoadout.SwooshEndMarker, Time = .1f }] };
        Assert.False(PersonLoadout.Swooshing(clip, .04f));
        Assert.True(PersonLoadout.Swooshing(clip, .05f));
        Assert.False(PersonLoadout.Swooshing(clip, .1f));
    }
}
