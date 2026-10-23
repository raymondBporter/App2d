using App2d.Core;
using App2d.Core.Input;
using App2d.Trials;
using System.Numerics;

namespace App2d.Tests.Trials;

public sealed class TrialsGame2DTests
{
    private const float DeltaSeconds = 1f / 120f;

    [Fact]
    public void BikeLandsWithForkSagAndRearShockCompressionWhileBearingsStayFirm()
    {
        using var game = new TrialsGame2D();
        game.Initialize();
        Assert.Null(game.PhysicsWorld!.CollisionCallbacks);
        Assert.Equal(5, game.PhysicsWorld.Bodies.Count);
        Assert.Equal(4, game.PhysicsWorld.Constraints.Count);
        Assert.False(game.PhysicsWorld.CanCollide(game.Bike.Frame, game.Bike.RearWheel));
        Assert.False(game.PhysicsWorld.CanCollide(game.Bike.FrontWheel, game.Bike.RearWheel));
        foreach (var wheel in new[] { game.Bike.RearWheel, game.Bike.FrontWheel })
        {
            Assert.False(wheel.FreezeRotation);
            Assert.Equal(0.5f * wheel.Mass * TrialsBike2D.WheelRadius * TrialsBike2D.WheelRadius, wheel.MomentOfInertia, 3);
        }
        Advance(game, new InputState(), 600);
        foreach (var wheel in new[] { game.Bike.RearWheel, game.Bike.FrontWheel })
        {
            Assert.InRange(wheel.WorldCenterOfMass.Y, TrialsBike2D.WheelRadius - 0.2f, TrialsBike2D.WheelRadius + 0.2f);
            Assert.InRange(MathF.Abs(wheel.LinearVelocity.Y), 0f, 10f);
        }
        Assert.InRange(MathF.Abs(game.Bike.Frame.WorldObject.Transform.Rotation), 0f, 0.2f);
        Assert.InRange(Vector2.Distance(game.Bike.RearJoint.FirstWorldAnchor, game.Bike.RearJoint.SecondWorldAnchor), 0f, 0.1f);
        Assert.InRange(Vector2.Distance(game.Bike.SwingarmJoint.FirstWorldAnchor, game.Bike.SwingarmJoint.SecondWorldAnchor), 0f, 0.1f);
        Assert.InRange(Math.Abs(game.Bike.FrontJoint.LateralError), 0f, 0.1f);
        Assert.InRange(game.Bike.FrontJoint.Translation, 0.1f, 27.9f);
        Assert.InRange(game.Bike.RearShock.Length, game.Bike.RearShock.LengthLimits.Minimum, game.Bike.RearShock.RestLength - 0.1f);
    }

    [Fact]
    public void RearWheelTorqueDrivesBikeCameraFollowsAndBrakeSlowsIt()
    {
        using var game = new TrialsGame2D();
        game.Initialize();
        var input = new InputState();
        Advance(game, input, 600);
        var startX = game.Bike.Frame.WorldCenterOfMass.X;
        input.SetKey(Keys.D, true);
        Advance(game, input, 240);
        var position = game.Bike.Frame.WorldObject.Transform.Position;
        Assert.True(position.X > startX + 80f, $"Frame position {position}, velocity {game.Bike.Frame.LinearVelocity}");
        Assert.True(game.Bike.RearWheel.AngularVelocity < -1f);
        Assert.InRange(game.Camera.Position.X, position.X - 70f, position.X);
        Assert.Equal(160f, game.Camera.Position.Y);
        var speed = game.Bike.Frame.LinearVelocity.Length();
        input.SetKey(Keys.D, false);
        input.SetKey(Keys.Space, true);
        Advance(game, input, 360);
        Assert.True(game.Bike.Frame.LinearVelocity.Length() < speed * 0.25f,
            $"Before {speed}, after {game.Bike.Frame.LinearVelocity}");
    }

    [Fact]
    public void ResetRestoresEveryBodyAndBothAnchors()
    {
        using var game = new TrialsGame2D();
        game.Initialize();
        var input = new InputState();
        var spawn = game.Bike.Frame.WorldObject.Transform.Position;
        game.Bike.Translate(new(1000f, 20f));
        input.SetKey(Keys.W, true);
        Advance(game, input, 20);
        Assert.True(game.Bike.Frame.WorldObject.Transform.Rotation > 0.005f,
            $"Lean rotation {game.Bike.Frame.WorldObject.Transform.Rotation}");
        input.SetKey(Keys.D, true);
        Advance(game, input, 60);
        input.SetKey(Keys.R, true);
        Advance(game, input, 1);
        Assert.Equal(spawn.X, game.Bike.Frame.WorldObject.Transform.Position.X, 3);
        Assert.InRange(game.Bike.Frame.WorldObject.Transform.Position.Y, spawn.Y - 1f, spawn.Y);
        Assert.Equal(new Vector2(spawn.X, 160f), game.Camera.Position);
        foreach (var body in game.Bike.Bodies)
        {
            Assert.Equal(0f, body.AngularVelocity, 4);
            Assert.Equal(0f, body.LinearVelocity.X, 4);
        }
        Assert.InRange(Vector2.Distance(game.Bike.RearJoint.FirstWorldAnchor, game.Bike.RearJoint.SecondWorldAnchor), 0f, 0.001f);
        Assert.InRange(Vector2.Distance(game.Bike.FrontJoint.FirstWorldAnchor, game.Bike.FrontJoint.SecondWorldAnchor), 0f, 0.001f);
        Assert.InRange(Vector2.Distance(game.Bike.SwingarmJoint.FirstWorldAnchor, game.Bike.SwingarmJoint.SecondWorldAnchor), 0f, 0.001f);
        Assert.Equal(game.Bike.RearShock.RestLength, game.Bike.RearShock.Length, 3);
    }

    [Fact]
    public void LandingImpulseCompressesSuspensionWithoutBreakingTravelStopsOrBearings()
    {
        using var game = new TrialsGame2D();
        game.Initialize();
        var input = new InputState();
        Advance(game, input, 600);
        var sag = game.Bike.FrontJoint.Translation;
        var shockLength = game.Bike.RearShock.Length;
        var peakCompression = sag;
        var minimumShockLength = shockLength;
        game.Bike.Frame.AddImpulse(new Vector2(0f, -150f * game.Bike.Frame.Mass));
        for (var step = 0; step < 240; step++)
        {
            Advance(game, input, 1);
            peakCompression = Math.Max(peakCompression, game.Bike.FrontJoint.Translation);
            minimumShockLength = Math.Min(minimumShockLength, game.Bike.RearShock.Length);
            Assert.InRange(game.Bike.FrontJoint.Translation, -0.2f, 28.2f);
            Assert.InRange(Math.Abs(game.Bike.FrontJoint.LateralError), 0f, 0.2f);
            Assert.InRange(game.Bike.RearShock.Length, game.Bike.RearShock.LengthLimits.Minimum - 0.2f, game.Bike.RearShock.LengthLimits.Maximum + 0.2f);
            Assert.InRange(Vector2.Distance(game.Bike.SwingarmJoint.FirstWorldAnchor, game.Bike.SwingarmJoint.SecondWorldAnchor), 0f, 0.2f);
        }
        Assert.True(peakCompression > sag + 1f, $"Sag {sag}, peak {peakCompression}");
        Assert.True(minimumShockLength < shockLength - 1f, $"Resting length {shockLength}, compressed {minimumShockLength}");
    }

    private static void Advance(TrialsGame2D game, InputState input, int steps)
    {
        for (var step = 0; step < steps; step++)
        {
            var time = new FrameTime(DeltaSeconds, step * (double)DeltaSeconds, step);
            game.Update(time, input);
            game.AdvancePresentation(time);
            input.EndFrame();
        }
    }
}
