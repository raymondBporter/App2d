using App2d.Collision;
using App2d.Core;
using App2d.Core.Geometry;
using App2d.Gameplay.Audio;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Persons.Actions;
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

    [Fact]
    public void WallGripSwordSwingStaysOnSideAwayFromWall()
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
        AddRightGrippableWall(physics, person);
        var scene = new Scene2D();
        using var textures = new TextureCache2D(TestAssetPath.Root);
        var arsenal = CreateArsenal(scene, person, textures, collision);
        person.AttachActions(arsenal);

        person.BeginFrame(0.01f);
        person.ApplyCommand(
            WallGripCommand(useWeapon: true, switchWeapon: false),
            0.01f);
        Assert.True(person.IsWallGripping);
        Assert.Equal(-1f, person.Facing);
        physics.Step(0.01f);
        person.UpdateAfterPhysics(0.01f);

        person.BeginFrame(0.10f);
        person.ApplyCommand(
            WallGripCommand(useWeapon: false, switchWeapon: false),
            0.10f);
        physics.Step(0.10f);
        person.UpdateAfterPhysics(0.10f);

        var hitbox = Assert.Single(arsenal.GetActiveAttackHitboxes());
        Assert.True(hitbox.Transform.Position.X < person.Position.X);
    }

    [Fact]
    public void WallGripGunReleaseKeepsMuzzleAndVelocityAwayFromWall()
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
        AddRightGrippableWall(physics, person);
        var scene = new Scene2D();
        using var textures = new TextureCache2D(TestAssetPath.Root);
        var arsenal = CreateArsenal(scene, person, textures, collision);
        person.AttachActions(arsenal);

        person.BeginFrame(0.04f);
        person.ApplyCommand(
            WallGripCommand(useWeapon: true, switchWeapon: true),
            0.04f);
        Assert.True(person.IsWallGripping);
        Assert.Equal(-1f, person.Facing);
        physics.Step(0.04f);
        person.UpdateAfterPhysics(0.04f);
        Assert.Empty(arsenal.GetActiveAttackHitboxes());

        person.BeginFrame(0.05f);
        person.ApplyCommand(
            WallGripCommand(useWeapon: false, switchWeapon: false),
            0.05f);
        Assert.Equal(1f, person.Facing);
        physics.Step(0.05f);
        person.UpdateAfterPhysics(0.05f);

        var bullet = Assert.Single(arsenal.GetActiveAttackHitboxes());
        Assert.True(bullet.Transform.Position.X < person.Position.X);
        var releaseX = bullet.Transform.Position.X;

        person.BeginFrame(0.02f);
        person.ApplyCommand(
            WallGripCommand(useWeapon: false, switchWeapon: false),
            0.02f);
        physics.Step(0.02f);
        person.UpdateAfterPhysics(0.02f);

        Assert.True(bullet.Transform.Position.X < releaseX);
    }

    [Fact]
    public void DeadPersonRemainsSolidAndSettlesOnTheGround()
    {
        var collision = new CollisionSystem2D();
        var physics = CreatePhysics(collision);
        physics.Gravity = new Vector2(0f, -1_200f);
        var traversal = TraversalMetrics2D.FromPlayerAsset(TestAssetPath.Root);
        var groundObject = new SpatialObject2D(
            AxisAlignedRectangle2D.FromSize(new Vector2(600f, 20f)));
        groundObject.Transform.Position = new Vector2(0f, -10f);
        var ground = physics.AddBody(groundObject, BodyMotionType2D.Static);
        ground.CollisionLayer = WorldLayer;
        ground.CollisionMask = PlayerLayer;
        var person = CreatePerson(
            collision,
            physics,
            traversal,
            new Vector2(0f, 100f),
            PlayerLayer,
            CombatFaction2D.Player);

        Assert.True(person.TakeDamage(person.Health.Maximum, Vector2.Zero));
        Assert.False(person.IsAlive);
        Assert.True(person.Body.IsCollider);
        Assert.Equal(BodyMotionType2D.Dynamic, person.Body.MotionType);

        for (var frame = 0; frame < 120; frame++)
        {
            physics.Step(1f / 60f);
            person.UpdateAfterPhysics(1f / 60f);
        }

        Assert.True(person.IsGrounded);
        Assert.InRange(person.WorldObject.WorldBounds.Bottom, -0.01f, 0.01f);
        Assert.InRange(person.Body.LinearVelocity.Y, -0.01f, 0.01f);
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

    private static PersonArsenal2D CreateArsenal(
        Scene2D scene,
        Person2D person,
        TextureCache2D textures,
        CollisionSystem2D collision) =>
        new(
            scene,
            person.Body,
            textures,
            collision,
            WorldLayer,
            EnemyLayer,
            CombatFaction2D.Player,
            new CombatSystem2D(collision, new SilentSounds()),
            new SilentSounds());

    private static void AddRightGrippableWall(
        PhysicsWorld2D physics,
        Person2D person)
    {
        var wallObject = new SpatialObject2D(
            AxisAlignedRectangle2D.FromSize(new Vector2(20f, 200f)));
        wallObject.Transform.Position = new Vector2(
            person.WorldObject.WorldBounds.Right + 11f,
            0f);
        var wall = physics.AddBody(wallObject, BodyMotionType2D.Static);
        wall.CollisionLayer = WorldLayer;
        wall.CollisionMask = PlayerLayer;
        wall.IsWallGrippable = true;
    }

    private static PersonCommand2D WallGripCommand(
        bool useWeapon,
        bool switchWeapon) =>
        new(
            new PersonMovementIntent2D(
                MoveX: 1f,
                JumpPressed: false,
                JumpHeld: false,
                JumpReleased: false,
                DropThroughPressed: false,
                DashPressed: false),
            UseWeapon: useWeapon,
            AimTarget: new Vector2(1_000f, 0f),
            SwitchWeapon: switchWeapon);

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
