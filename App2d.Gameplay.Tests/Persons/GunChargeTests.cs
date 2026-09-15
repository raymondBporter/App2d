using App2d.Levels;
using App2d.Collision;
using App2d.Core;
using App2d.Core.Geometry;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Persons.Actions;
using App2d.Gameplay.Player;
using App2d.Physics;
using App2d.Tiles;
using System.Numerics;
using Xunit;

namespace App2d.Gameplay.Tests.Persons;

public sealed class GunChargeTests
{
    private static PersonCommand2D Hold => new(default, false, false, PrimaryActionHeld: true);
    private static PersonCommand2D Release => new(default, false, false, PrimaryActionReleased: true);
    private static PersonCommand2D Press => Hold with { UsePrimaryAction = true };

    [Theory]
    [InlineData(-1f)]
    [InlineData(1f)]
    public void ChargesForSixTenthsThenAutomaticallyFiresOnceAtMirroredBarrel(float facing)
    {
        using var game = new Fixture();
        game.Person.Face(facing);
        var start = game.Person.Position;
        game.Step(Press);
        Assert.Equal(0f, game.Person.Body.LinearVelocity.X);
        for (var i = 1; i < 71; i++) game.Step(Hold);
        Assert.True(game.Arsenal.IsChargingPrimary);
        Assert.Empty(game.Arsenal.GetActiveAttackHitboxes());
        Assert.Equal(start.X, game.Person.Position.X);
        game.Step(Hold);
        Assert.False(game.Arsenal.IsChargingPrimary);
        Assert.Equal(1, game.Shots);
        var bolt = Assert.Single(game.Arsenal.GetActiveAttackHitboxes());
        Assert.Equal(58.664f * facing, bolt.Transform.Position.X - game.Person.Position.X, 2);
        Assert.Equal(4.074f, bolt.Transform.Position.Y - game.Person.Position.Y, 2);
        var shotX = bolt.Transform.Position.X;
        game.Step(Hold);
        Assert.True((bolt.Transform.Position.X - shotX) * facing > 0f);
        for (var i = 0; i < 100; i++) game.Step(Hold);
        Assert.Equal(1, game.Shots);
        game.Step(default);
        game.Step(Press);
        Assert.True(game.Arsenal.IsChargingPrimary);
    }

    [Theory]
    [InlineData("release")]
    [InlineData("switch")]
    [InlineData("hit")]
    [InlineData("reset")]
    [InlineData("disable")]
    public void InterruptedChargeNeverFires(string cause)
    {
        using var game = new Fixture();
        game.Step(Press);
        for (var i = 0; i < 35; i++) game.Step(Hold);
        Assert.True(game.Arsenal.IsChargingPrimary);
        var command = Hold;
        switch (cause)
        {
            case "release": command = Release; break;
            case "switch": command = Hold with { SwitchEquipment = true }; break;
            case "hit": Assert.True(game.Person.TakeDamage(1, new Vector2(-100f, 100f))); break;
            case "reset": game.Person.Reset(Vector2.Zero); break;
            case "disable": game.Person.SetSimulationEnabled(false); break;
        }
        game.Step(command);
        Assert.False(game.Arsenal.IsChargingPrimary);
        for (var i = 0; i < 90; i++) game.Step(Hold);
        Assert.Equal(0, game.Shots);
        Assert.Empty(game.Arsenal.GetActiveAttackHitboxes());
        Assert.Single(game.Events.OfType<ChargeCancelled2D>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CanChargeAndFireWithoutGroundSupport(bool startGrounded)
    {
        using var game = new Fixture();
        game.Ground.IsCollider = startGrounded;
        game.Step(Press);
        Assert.True(game.Arsenal.IsChargingPrimary);
        game.Ground.IsCollider = false;
        game.Physics.Gravity = new Vector2(0f, -100f);
        for (var i = 0; i < 90; i++) game.Step(Hold);
        Assert.False(game.Person.IsGrounded);
        Assert.True(game.Person.Position.Y < 0f);
        Assert.Equal(1, game.Shots);
        Assert.Single(game.Arsenal.GetActiveAttackHitboxes());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AlmostChargedShotIsCancelledByDamageOrSuppressedInput(bool takeDamage)
    {
        using var game = new Fixture();
        game.Step(Press);
        for (var i = 1; i < 71; i++) game.Step(Hold);
        if (takeDamage) Assert.True(game.Person.TakeDamage(1, Vector2.Zero));
        else game.Step(default); // No physical release event: e.g. console/focus loss.
        game.Step(Release);
        Assert.False(game.Arsenal.IsChargingPrimary);
        Assert.Equal(0, game.Shots);
        Assert.Empty(game.Arsenal.GetActiveAttackHitboxes());
    }

    [Theory]
    [InlineData("move", false)]
    [InlineData("jump", false)]
    [InlineData("dash", false)]
    [InlineData("drop", false)]
    [InlineData("climb", false)]
    [InlineData("move", true)]
    [InlineData("jump", true)]
    [InlineData("dash", true)]
    [InlineData("drop", true)]
    [InlineData("climb", true)]
    public void ChargingAndAutomaticFiringPreserveNormalMovement(string kind, bool chargeFirst)
    {
        using var game = new Fixture(ladder: kind == "climb");
        using var baseline = new Fixture(ladder: kind == "climb");
        game.Ground.IsOneWayPlatform = baseline.Ground.IsOneWayPlatform = kind == "drop";
        if (chargeFirst)
        {
            game.Step(Press);
            baseline.Step(default);
            for (var i = 1; i < 30; i++) { game.Step(Hold); baseline.Step(default); }
        }
        var movement = kind switch
        {
            "move" => new PersonMovementIntent2D(1, false, false, false, false, false),
            "jump" => new(0, true, true, false, false, false),
            "dash" => new(1, false, false, false, false, true),
            "drop" => new(0, false, false, false, true, false),
            _ => new(0, false, false, false, false, false, ClimbY: 1)
        };
        var start = game.Person.Position;
        for (var i = 0; i < 90; i++)
        {
            var intent = i == 0 ? movement : movement with
                { JumpPressed = false, DashPressed = false, DropThroughPressed = false };
            game.Step(Hold with { Movement = intent, UsePrimaryAction = !chargeFirst && i == 0 });
            baseline.Step(new PersonCommand2D(intent, false, false));
            Assert.Equal((chargeFirst ? 30 : 0) + i + 1 < 72, game.Arsenal.IsChargingPrimary);
            Assert.Equal(baseline.Person.Position, game.Person.Position);
            Assert.Equal(baseline.Person.Body.LinearVelocity, game.Person.Body.LinearVelocity);
        }
        Assert.NotEqual(start, game.Person.Position);
        if (kind == "climb") Assert.True(game.Person.IsClimbingLadder);
        Assert.Equal(1, game.Shots);
    }

    [Fact]
    public void ChargingShotTurnsWithPlayerBeforeFiring()
    {
        using var game = new Fixture();
        game.Step(Press);
        for (var i = 1; i < 71; i++) game.Step(Hold);
        game.Step(Hold with { Movement = new(-1, false, false, false, false, false) });
        var bullet = Assert.Single(game.Arsenal.GetActiveAttackHitboxes());
        Assert.True(bullet.Transform.Position.X < game.Person.Position.X);
        var shotX = bullet.Transform.Position.X;
        game.Step(default);
        Assert.True(bullet.Transform.Position.X < shotX);
    }

    [Fact]
    public void BarrelCannotSpawnShotBeyondThinWall()
    {
        using var game = new Fixture();
        var wall = game.Physics.AddBody(new SpatialObject2D(
            AxisAlignedRectangle2D.FromSize(new Vector2(4f, 100f))), BodyMotionType2D.Static);
        wall.WorldObject.Transform.Position = new Vector2(30f, 0f);
        wall.CollisionLayer = 1;
        game.Step(Press);
        for (var i = 1; i < 72; i++) game.Step(Hold);
        game.Step(Release);
        Assert.Equal(1, game.Shots);
        Assert.Empty(game.Arsenal.GetActiveAttackHitboxes());
        Assert.Single(game.Events.OfType<ProjectileImpact2D>());
    }

    [Fact]
    public void ProjectileImpactFactUsesTheHitLocation()
    {
        using var game = new Fixture();
        var wall = game.Physics.AddBody(new SpatialObject2D(
            AxisAlignedRectangle2D.FromSize(new Vector2(4f, 100f))), BodyMotionType2D.Static);
        wall.WorldObject.Transform.Position = new Vector2(300f, 0f);
        wall.CollisionLayer = 1;
        game.Step(Press);
        for (var i = 1; i < 110; i++) game.Step(Hold);

        var impact = Assert.Single(game.Events.OfType<ProjectileImpact2D>());
        Assert.InRange(impact.Position.X, 280f, 310f);
        Assert.True(impact.Position.X - game.Person.Position.X > 250f);
        var shot = Assert.Single(game.Events.OfType<GunFired2D>());
        Assert.InRange(shot.Position.X - game.Person.Position.X, 40f, 50f);
    }

    private sealed class Fixture : IDisposable
    {
        public PhysicsWorld2D Physics { get; }
        public PhysicsBody2D Ground { get; }
        public Person2D Person { get; }
        public PersonArsenal2D Arsenal { get; }
        public List<WeaponEvent2D> Events { get; } = [];
        public int Shots { get; private set; }

        public Fixture(bool ladder = false)
        {
            var collision = new CollisionSystem2D();
            Physics = new(collision) { Gravity = Vector2.Zero };
            var metrics = TraversalMetricsLoader2D.Load(TestAssetPath.Root);
            var map = new EditableTileMap2D(16, 64, 32f, 8);
            if (ladder)
                for (var y = 0; y < 20; y++) map.SetTileKind(0, y, TileKind2D.Ladder);
            var spawn = ladder ? new Vector2(16f - metrics.PlayerColliderCenterOffsetX, 28f) : Vector2.Zero;
            Person = new(collision, Physics, metrics,
                spawn, 2, 1, CombatFaction2D.Player, tileMap: map);
            Ground = Physics.AddBody(new SpatialObject2D(
                AxisAlignedRectangle2D.FromSize(new Vector2(1000f, 20f))), BodyMotionType2D.Static);
            Ground.WorldObject.Transform.Position = new Vector2(0, Person.WorldObject.WorldBounds.Bottom - 10f);
            Ground.CollisionLayer = 1;
            Ground.CollisionMask = 2;
            Arsenal = new(Person.Body, metrics.GunMuzzleOffset, collision, 1, 4,
                CombatFaction2D.Player, new CombatSystem2D(collision, new CombatantRegistry2D()));
            Arsenal.WeaponOccurred += Events.Add;
            Person.AttachActions(Arsenal);
            Arsenal.SelectNext();
            Arsenal.ShotStarted += () => Shots++;
        }

        public void Step(PersonCommand2D command)
        {
            const float dt = 1f / 120f;
            Person.BeginFrame(dt);
            Person.ApplyCommand(command, dt);
            Physics.Step(dt);
            Person.UpdateAfterPhysics(dt);
        }

        public void Dispose() { }
    }

}
