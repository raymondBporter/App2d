using App2d.Collision;
using App2d.Gameplay.Audio;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Player;
using App2d.Physics;
using App2d.Rendering;
using App2d.Rendering.Textures;
using System.Numerics;
using Xunit;

namespace App2d.Gameplay.Tests.Persons;

public sealed class Person2DTests
{
    private const uint WorldLayer = 1u << 0;
    private const uint PlayerLayer = 1u << 1;
    private const uint EnemyLayer = 1u << 2;

    [Fact]
    public void IdenticalCommandsProduceIdenticalSimulation()
    {
        var collision = new CollisionSystem2D();
        var physics = CreatePhysics(collision);
        var traversal = TraversalMetrics2D.FromPlayerAsset(TestAssetPath.Root);
        var firstStart = Vector2.Zero;
        var secondStart = new Vector2(300f, 0f);
        var first = CreatePerson(
            collision,
            physics,
            traversal,
            firstStart,
            PlayerLayer,
            CombatFaction2D.Player);
        var second = CreatePerson(
            collision,
            physics,
            traversal,
            secondStart,
            EnemyLayer,
            CombatFaction2D.Enemy);
        var command = new PersonCommand2D(
            new PersonMovementIntent2D(
                MoveX: 1f,
                JumpPressed: false,
                JumpHeld: false,
                JumpReleased: false,
                DropThroughPressed: false,
                DashPressed: true),
            UseWeapon: false,
            AimTarget: null,
            SwitchWeapon: false);

        first.BeginFrame(0.05f);
        second.BeginFrame(0.05f);
        first.ApplyCommand(command, 0.05f);
        second.ApplyCommand(command, 0.05f);
        physics.Step(0.05f);
        first.UpdateAfterPhysics(0.05f);
        second.UpdateAfterPhysics(0.05f);

        Assert.Equal(first.Body.LinearVelocity, second.Body.LinearVelocity);
        var firstDisplacement = first.Position - firstStart;
        var secondDisplacement = second.Position - secondStart;
        Assert.Equal(firstDisplacement.X, secondDisplacement.X, 3);
        Assert.Equal(firstDisplacement.Y, secondDisplacement.Y, 3);
        Assert.Equal(first.IsDashing, second.IsDashing);
        Assert.Equal(first.Facing, second.Facing);
    }

    [Fact]
    public void CombatResolverDamagesPersonsInEitherDirection()
    {
        var collision = new CollisionSystem2D();
        var physics = CreatePhysics(collision);
        var traversal = TraversalMetrics2D.FromPlayerAsset(TestAssetPath.Root);
        var player = CreatePerson(
            collision,
            physics,
            traversal,
            new Vector2(-100f, 0f),
            PlayerLayer,
            CombatFaction2D.Player);
        var rival = CreatePerson(
            collision,
            physics,
            traversal,
            new Vector2(100f, 0f),
            EnemyLayer,
            CombatFaction2D.Enemy);
        var combat = new CombatSystem2D(collision, new SilentSounds());

        Assert.True(combat.ResolveAttack(
            rival.WorldObject,
            new object(),
            attackId: 1,
            CombatFaction2D.Player,
            EnemyLayer,
            damage: 2,
            _ => Vector2.Zero));
        Assert.Equal(3, rival.Health.Current);
        Assert.Equal(5, player.Health.Current);

        Assert.True(combat.ResolveAttack(
            player.WorldObject,
            new object(),
            attackId: 1,
            CombatFaction2D.Enemy,
            PlayerLayer,
            damage: 2,
            _ => Vector2.Zero));
        Assert.Equal(3, player.Health.Current);
    }

    [Fact]
    public void GunReleaseUsesActionTimeWithoutPresentation()
    {
        var collision = new CollisionSystem2D();
        var physics = CreatePhysics(collision);
        var traversal = TraversalMetrics2D.FromPlayerAsset(TestAssetPath.Root);
        var person = CreatePerson(
            collision,
            physics,
            traversal,
            Vector2.Zero,
            PlayerLayer,
            CombatFaction2D.Player);
        var scene = new Scene2D();
        using var textures = new TextureCache2D(TestAssetPath.Root);
        var arsenal = new PersonArsenal2D(
            scene,
            person.Body,
            textures,
            collision,
            WorldLayer,
            EnemyLayer,
            CombatFaction2D.Player,
            new CombatSystem2D(collision, new SilentSounds()),
            new SilentSounds());
        person.AttachActions(arsenal);
        var fire = new PersonCommand2D(
            default,
            UseWeapon: true,
            AimTarget: null,
            SwitchWeapon: true);

        person.Face(1f);
        person.BeginFrame(0.07f);
        person.ApplyCommand(fire, 0.07f);
        physics.Step(0.07f);
        person.UpdateAfterPhysics(0.07f);
        Assert.Empty(arsenal.GetActiveAttackHitboxes());

        person.BeginFrame(0.02f);
        person.ApplyCommand(default, 0.02f);
        physics.Step(0.02f);
        person.UpdateAfterPhysics(0.02f);
        Assert.Single(arsenal.GetActiveAttackHitboxes());
    }

    private static Person2D CreatePerson(
        CollisionSystem2D collision,
        PhysicsWorld2D physics,
        TraversalMetrics2D traversal,
        Vector2 position,
        uint layer,
        CombatFaction2D faction) =>
        new(
            collision,
            physics,
            traversal,
            position,
            layer,
            WorldLayer,
            faction);

    private static PhysicsWorld2D CreatePhysics(CollisionSystem2D collision) =>
        new(collision)
        {
            Gravity = Vector2.Zero,
            MaxSubstepSeconds = 1f / 120f,
            PositionIterations = 3,
            VelocityIterations = 2
        };

    private sealed class SilentSounds : ISoundEffectSink2D
    {
        public void Play(SoundEffect2D effect)
        {
        }
    }
}
