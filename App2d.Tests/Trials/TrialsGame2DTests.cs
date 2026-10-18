using App2d.Core;
using App2d.Core.Input;
using App2d.Core.Shapes;
using App2d.Trials;
using System.Numerics;

namespace App2d.Tests.Trials;

public sealed class TrialsGame2DTests
{
    private const float DeltaSeconds = 1f / 120f;

    [Fact]
    public void PlayerFallsOntoGroundAndCanJumpOnlyWhileSupported()
    {
        using var game = new TrialsGame2D();
        game.Initialize();
        var input = new InputState();
        var radius = Assert.IsType<Circle2D>(game.Player.WorldObject.Shape).Radius;
        Assert.Null(game.PhysicsWorld!.CollisionCallbacks);
        Assert.Equal(0.5f * game.Player.Mass * radius * radius, game.Player.MomentOfInertia);
        var spawnHeight = game.Player.WorldObject.Transform.Position.Y;
        Advance(game, input, 30);
        Assert.InRange(game.Player.WorldObject.Transform.Position.Y, radius, spawnHeight - 1f);
        Advance(game, input, 210);
        Assert.Equal(radius, game.Player.WorldObject.Transform.Position.Y, 3);
        Assert.Equal(0f, game.Player.LinearVelocity.Y, 3);

        input.SetKey(Keys.Space, true);
        Advance(game, input, 1);
        Assert.True(game.Player.WorldObject.Transform.Position.Y > radius);
        Assert.True(game.Player.LinearVelocity.Y > 0f);

        input.SetKey(Keys.Space, false);
        Advance(game, input, 5);
        var risingSpeed = game.Player.LinearVelocity.Y;
        input.SetKey(Keys.Space, true);
        Advance(game, input, 1);
        Assert.InRange(game.Player.LinearVelocity.Y, 0f, risingSpeed - 1f);
    }

    [Fact]
    public void MovementCameraAndResetWorkAwayFromTheSpawn()
    {
        using var game = new TrialsGame2D();
        game.Initialize();
        var input = new InputState();
        var spawn = game.Player.WorldObject.Transform.Position;
        var initialCamera = game.Camera.Position;
        input.SetKey(Keys.D, true);
        Advance(game, input, 240);
        var position = game.Player.WorldObject.Transform.Position;
        Assert.True(position.X > 500f);
        Assert.InRange(game.Camera.Position.X, position.X - 50f, position.X);
        Assert.Equal(initialCamera.Y, game.Camera.Position.Y);

        input.SetKey(Keys.D, false);
        Advance(game, input, 120);
        Assert.InRange(MathF.Abs(game.Player.LinearVelocity.X), 0f, 0.01f);
        input.SetKey(Keys.R, true);
        Advance(game, input, 1);
        Assert.Equal(spawn.X, game.Player.WorldObject.Transform.Position.X);
        Assert.InRange(game.Player.WorldObject.Transform.Position.Y, spawn.Y - 1f, spawn.Y);
        Assert.Equal(0f, game.Player.LinearVelocity.X);
        Assert.Equal(initialCamera, game.Camera.Position);
        Assert.Equal(2, game.PhysicsWorld!.Bodies.Count);
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
