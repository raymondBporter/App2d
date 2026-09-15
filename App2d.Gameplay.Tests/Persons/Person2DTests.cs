using App2d.Levels;
using App2d.Collision;
using App2d.Core;
using App2d.Core.Geometry;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Persons.Actions;
using App2d.Gameplay.Player;
using App2d.Physics;
using System.Numerics;
using Xunit;

namespace App2d.Gameplay.Tests.Persons;

public sealed class Person2DTests
{
    private const uint WorldLayer = 1u << 0;
    private const uint PlayerLayer = 1u << 1;
    private const uint EnemyLayer = 1u << 2;
    private readonly CombatantRegistry2D _combatants = new();

    [Fact]
    public void ResetCanRestoreSavedHealthAtANewPosition()
    {
        var collision = new CollisionSystem2D();
        var physics = CreatePhysics(collision);
        var person = CreatePerson(
            collision,
            physics,
            TraversalMetricsLoader2D.Load(TestAssetPath.Root),
            Vector2.Zero,
            PlayerLayer,
            CombatFaction2D.Player);
        person.Health.Damage(4);
        var respawn = new Vector2(300f, 120f);

        person.Reset(respawn, hitPoints: 3);

        Assert.Equal(respawn, person.Position);
        Assert.Equal(3, person.Health.Current);
    }

    [Fact]
    public void IdenticalCommandsProduceIdenticalSimulation()
    {
        var collision = new CollisionSystem2D();
        var physics = CreatePhysics(collision);
        var traversal = TraversalMetricsLoader2D.Load(TestAssetPath.Root);
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
            UsePrimaryAction: false,

            SwitchEquipment: false);

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
        var traversal = TraversalMetricsLoader2D.Load(TestAssetPath.Root);
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
        var combat = new CombatSystem2D(collision, _combatants);

        Assert.True(combat.ResolveAttack(
            rival.WorldObject,
            EntityId2D.Create(),
            attackId: 1,
            CombatFaction2D.Player,
            EnemyLayer,
            damage: 2,
            _ => Vector2.Zero));
        Assert.Equal(3, rival.Health.Current);
        Assert.Equal(5, player.Health.Current);

        Assert.True(combat.ResolveAttack(
            player.WorldObject,
            EntityId2D.Create(),
            attackId: 1,
            CombatFaction2D.Enemy,
            PlayerLayer,
            damage: 2,
            _ => Vector2.Zero));
        Assert.Equal(3, player.Health.Current);
    }

    [Theory]
    [InlineData(false, 0.08f, 1)]
    [InlineData(true, 0.15f, 2)]
    public void UnarmedActionsDamageHostilePersons(
        bool useKick,
        float activeTime,
        int expectedDamage)
    {
        var collision = new CollisionSystem2D();
        var physics = CreatePhysics(collision);
        var traversal = TraversalMetricsLoader2D.Load(TestAssetPath.Root);
        var attacker = CreatePerson(
            collision,
            physics,
            traversal,
            Vector2.Zero,
            PlayerLayer,
            CombatFaction2D.Player);
        var target = CreatePerson(
            collision,
            physics,
            traversal,
            new Vector2(58f, 0f),
            EnemyLayer,
            CombatFaction2D.Enemy);
        var actions = new UnarmedPersonActions2D(
            attacker.Body,
            CombatFaction2D.Player,
            EnemyLayer,
            new CombatSystem2D(collision, _combatants));
        attacker.AttachActions(actions);

        attacker.BeginFrame(activeTime);
        attacker.ApplyCommand(
            new PersonCommand2D(
                default,
                UsePrimaryAction: !useKick,

                SwitchEquipment: false,
                UseSecondaryAction: useKick),
            activeTime);
        attacker.UpdateAfterPhysics(activeTime);

        Assert.Equal(target.Health.Maximum - expectedDamage, target.Health.Current);
        Assert.Single(actions.GetActiveAttackHitboxes());
    }

    [Fact]
    public void UnarmedActionsReportPunchAndKickPresentationCues()
    {
        var collision = new CollisionSystem2D();
        var physics = CreatePhysics(collision);
        var person = CreatePerson(
            collision,
            physics,
            TraversalMetricsLoader2D.Load(TestAssetPath.Root),
            Vector2.Zero,
            PlayerLayer,
            CombatFaction2D.Player);
        var actions = new UnarmedPersonActions2D(
            person.Body,
            CombatFaction2D.Player,
            EnemyLayer,
            new CombatSystem2D(collision, _combatants));
        var cues = new List<UnarmedAttackKind2D>();
        actions.AttackStarted += (kind, _) => cues.Add(kind);

        actions.UsePrimary(1f);
        actions.Reset();
        actions.UseSecondary(1f);

        Assert.Equal(
            [UnarmedAttackKind2D.Punch, UnarmedAttackKind2D.Kick],
            cues);
    }

    [Fact]
    public void GroundedGunReleaseUsesChargeTimeWithoutPresentation()
    {
        var collision = new CollisionSystem2D();
        var physics = CreatePhysics(collision);
        var traversal = TraversalMetricsLoader2D.Load(TestAssetPath.Root);
        var person = CreatePerson(
            collision,
            physics,
            traversal,
            Vector2.Zero,
            PlayerLayer,
            CombatFaction2D.Player);
        var arsenal = new PersonArsenal2D(
            person.Body,
            TraversalMetricsLoader2D.Load(TestAssetPath.Root).GunMuzzleOffset,
            collision,
            WorldLayer,
            EnemyLayer,
            CombatFaction2D.Player,
            new CombatSystem2D(collision, _combatants));
        person.AttachActions(arsenal);
        var fire = new PersonCommand2D(
            default,
            UsePrimaryAction: true,

            SwitchEquipment: true,
            PrimaryActionHeld: true);

        AddGroundSupport(physics, person);
        person.Face(1f);
        person.BeginFrame(0.59f);
        person.ApplyCommand(fire, 0.59f);
        physics.Step(0.59f);
        person.UpdateAfterPhysics(0.59f);
        Assert.Empty(arsenal.GetActiveAttackHitboxes());

        person.BeginFrame(0.01f);
        person.ApplyCommand(fire with { UsePrimaryAction = false, SwitchEquipment = false }, 0.01f);
        physics.Step(0.01f);
        person.UpdateAfterPhysics(0.01f);
        Assert.Single(arsenal.GetActiveAttackHitboxes());
    }

    [Fact]
    public void WallGripSwordSwingStaysOnSideAwayFromWall()
    {
        var collision = new CollisionSystem2D();
        var physics = CreatePhysics(collision);
        var traversal = TraversalMetricsLoader2D.Load(TestAssetPath.Root);
        var person = CreatePerson(
            collision,
            physics,
            traversal,
            Vector2.Zero,
            PlayerLayer,
            CombatFaction2D.Player);
        AddRightGrippableWall(physics, person);
        var arsenal = CreateArsenal(person, collision);
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
    public void WallGripCanChargeAndFireGunAwayFromWall()
    {
        var collision = new CollisionSystem2D();
        var physics = CreatePhysics(collision);
        var traversal = TraversalMetricsLoader2D.Load(TestAssetPath.Root);
        var person = CreatePerson(
            collision,
            physics,
            traversal,
            Vector2.Zero,
            PlayerLayer,
            CombatFaction2D.Player);
        AddRightGrippableWall(physics, person);
        var arsenal = CreateArsenal(person, collision);
        person.AttachActions(arsenal);

        person.BeginFrame(0.04f);
        person.ApplyCommand(
            WallGripCommand(useWeapon: true, switchWeapon: true) with { PrimaryActionHeld = true },
            0.04f);
        Assert.True(person.IsWallGripping);

        physics.Step(0.04f);
        person.UpdateAfterPhysics(0.04f);
        Assert.Empty(arsenal.GetActiveAttackHitboxes());

        for (var i = 0; i < 72; i++)
        {
            person.BeginFrame(1f / 120f);
            person.ApplyCommand(WallGripCommand(false, false) with { PrimaryActionHeld = true }, 1f / 120f);
            physics.Step(1f / 120f);
            person.UpdateAfterPhysics(1f / 120f);
        }
        Assert.False(arsenal.IsChargingPrimary);
        var bullet = Assert.Single(arsenal.GetActiveAttackHitboxes());
        Assert.True(bullet.Transform.Position.X < person.Position.X);
    }

    [Fact]
    public void BufferedJumpIntentBouncesOffWallWithoutGripping()
    {
        var collision = new CollisionSystem2D();
        var physics = CreatePhysics(collision);
        var traversal = TraversalMetricsLoader2D.Load(TestAssetPath.Root);
        var person = CreatePerson(
            collision,
            physics,
            traversal,
            Vector2.Zero,
            PlayerLayer,
            CombatFaction2D.Player);
        AddGroundSupport(physics, person);

        person.BeginFrame(0.01f);
        person.ApplyCommand(default, 0.01f);
        Assert.True(person.IsGrounded);

        person.WorldObject.Transform.Position += new Vector2(0f, 50f);
        AddRightGrippableWall(physics, person, gap: 10f);

        person.BeginFrame(0.01f);
        person.ApplyCommand(
            MovementCommand(moveX: 1f, jumpPressed: true, jumpHeld: true),
            0.01f);
        Assert.False(person.IsWallGripping);
        Assert.True(person.Body.LinearVelocity.Y > 0f);

        person.WorldObject.Transform.Position += new Vector2(9f, 0f);
        person.BeginFrame(0.01f);
        person.ApplyCommand(
            MovementCommand(moveX: 1f, jumpPressed: false, jumpHeld: true),
            0.01f);

        Assert.False(person.IsWallGripping);
        Assert.Equal(-traversal.WallJumpHorizontalSpeed, person.Body.LinearVelocity.X);
        Assert.Equal(traversal.JumpSpeed, person.Body.LinearVelocity.Y);
    }

    [Fact]
    public void DeadPersonRemainsSolidAndSettlesOnTheGround()
    {
        var collision = new CollisionSystem2D();
        var physics = CreatePhysics(collision);
        physics.Gravity = new Vector2(0f, -1_200f);
        var traversal = TraversalMetricsLoader2D.Load(TestAssetPath.Root);
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

    private Person2D CreatePerson(
        CollisionSystem2D collision,
        PhysicsWorld2D physics,
        TraversalMetrics2D traversal,
        Vector2 position,
        uint layer,
        CombatFaction2D faction)
    {
        var person = new Person2D(
            collision,
            physics,
            traversal,
            position,
            layer,
            WorldLayer,
            faction);
        _combatants.Register(person);
        return person;
    }

    private PersonArsenal2D CreateArsenal(
        Person2D person,
        CollisionSystem2D collision) =>
        new(
            person.Body,
            TraversalMetricsLoader2D.Load(TestAssetPath.Root).GunMuzzleOffset,
            collision,
            WorldLayer,
            EnemyLayer,
            CombatFaction2D.Player,
            new CombatSystem2D(collision, _combatants));

    private static void AddRightGrippableWall(
        PhysicsWorld2D physics,
        Person2D person,
        float gap = 1f)
    {
        var wallObject = new SpatialObject2D(
            AxisAlignedRectangle2D.FromSize(new Vector2(20f, 200f)));
        wallObject.Transform.Position = new Vector2(
            person.WorldObject.WorldBounds.Right + 10f + gap,
            0f);
        var wall = physics.AddBody(wallObject, BodyMotionType2D.Static);
        wall.CollisionLayer = WorldLayer;
        wall.CollisionMask = PlayerLayer;
        wall.IsWallGrippable = true;
    }

    private static void AddGroundSupport(
        PhysicsWorld2D physics,
        Person2D person)
    {
        var groundObject = new SpatialObject2D(
            AxisAlignedRectangle2D.FromSize(new Vector2(200f, 20f)));
        groundObject.Transform.Position = new Vector2(
            person.Position.X,
            person.WorldObject.WorldBounds.Bottom - 10f);
        var ground = physics.AddBody(groundObject, BodyMotionType2D.Static);
        ground.CollisionLayer = WorldLayer;
        ground.CollisionMask = PlayerLayer;
    }

    private static PersonCommand2D MovementCommand(
        float moveX,
        bool jumpPressed,
        bool jumpHeld) =>
        new(
            new PersonMovementIntent2D(
                MoveX: moveX,
                JumpPressed: jumpPressed,
                JumpHeld: jumpHeld,
                JumpReleased: false,
                DropThroughPressed: false,
                DashPressed: false),
            UsePrimaryAction: false,

            SwitchEquipment: false);

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
            UsePrimaryAction: useWeapon,

            SwitchEquipment: switchWeapon);

    private static PhysicsWorld2D CreatePhysics(CollisionSystem2D collision) =>
        new(collision)
        {
            Gravity = Vector2.Zero,
            MaxSubstepSeconds = 1f / 120f,
            PositionIterations = 3,
            VelocityIterations = 2
        };

}
