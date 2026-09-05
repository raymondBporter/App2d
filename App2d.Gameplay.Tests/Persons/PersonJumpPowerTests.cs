using App2d.Collision;
using App2d.Core;
using App2d.Core.Geometry;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Player;
using App2d.Physics;
using System.Numerics;
using Xunit;

namespace App2d.Gameplay.Tests.Persons;

public sealed class PersonJumpPowerTests
{
    private const uint WorldLayer = 1u << 0;
    private const uint PlayerLayer = 1u << 1;

    [Fact]
    public void HeldJumpReportsIncreasingPowerUntilReleased()
    {
        var collision = new CollisionSystem2D();
        var traversal = TraversalMetrics2D.FromPlayerAsset(TestAssetPath.Root);
        var physics = new PhysicsWorld2D(collision)
        {
            Gravity = new Vector2(0f, -traversal.Gravity),
            MaxSubstepSeconds = 1f / 120f,
            PositionIterations = 3,
            VelocityIterations = 2
        };
        AddGround(physics);
        var person = new Person2D(
            collision,
            physics,
            traversal,
            new Vector2(0f, traversal.PlayerColliderSize.Y * 0.5f),
            PlayerLayer,
            WorldLayer,
            CombatFaction2D.Player);

        person.BeginFrame(1f / 120f);
        person.ApplyCommand(default, 1f / 120f);
        Assert.True(person.IsGrounded);

        person.BeginFrame(1f / 120f);
        person.ApplyCommand(JumpCommand(pressed: true, held: true), 1f / 120f);

        Assert.True(person.IsSustainingJump);
        Assert.Equal(0f, person.JumpPower);

        physics.Step(0.08f);
        person.UpdateAfterPhysics(0.08f);

        Assert.True(person.IsSustainingJump);
        Assert.InRange(person.JumpPower, 0.05f, 0.5f);

        person.BeginFrame(1f / 120f);
        person.ApplyCommand(JumpCommand(pressed: false, held: false, released: true), 1f / 120f);

        Assert.False(person.IsSustainingJump);
        Assert.Equal(0f, person.JumpPower);
    }

    private static PersonCommand2D JumpCommand(
        bool pressed,
        bool held,
        bool released = false) =>
        new(
            new PersonMovementIntent2D(
                MoveX: 0f,
                JumpPressed: pressed,
                JumpHeld: held,
                JumpReleased: released,
                DropThroughPressed: false,
                DashPressed: false),
            UsePrimaryAction: false,
            AimTarget: null,
            SwitchEquipment: false);

    private static void AddGround(PhysicsWorld2D physics)
    {
        var groundObject = new SpatialObject2D(
            AxisAlignedRectangle2D.FromSize(new Vector2(600f, 20f)));
        groundObject.Transform.Position = new Vector2(0f, -10f);
        var ground = physics.AddBody(groundObject, BodyMotionType2D.Static);
        ground.CollisionLayer = WorldLayer;
        ground.CollisionMask = PlayerLayer;
    }
}
