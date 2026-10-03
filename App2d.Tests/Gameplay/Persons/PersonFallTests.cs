using App2d.Contracts.Combat;
using App2d.Contracts.Persons;
using App2d.Contracts.Player;
using App2d.Core;
using App2d.Core.Collision;
using App2d.Core.Geometry;
using App2d.Core.Physics;
using App2d.Core.Shapes;
using App2d.Gameplay.Persons;
using App2d.Gameplay.World;
using App2d.Levels;
using System.Numerics;

namespace App2d.Tests.Gameplay.Persons;

public sealed class PersonFallTests
{
    private static readonly TraversalMetrics2D Metrics = TraversalMetricsLoader2D.Load(TestAssetPath.Root);
    private const float Dt = 1f / 120f;

    [Fact]
    public void OrdinaryJumpAndShortFallKeepTheirGravity()
    {
        foreach (var velocity in new[] { Metrics.JumpSpeed, 0f, -Metrics.JumpSpeed })
            Assert.Equal(velocity - Metrics.Gravity * Dt, Metrics.AdvanceVerticalSpeed(velocity, Metrics.Gravity, Dt));
        var (person, physics) = Create(1000);
        person.Body.LinearVelocity = new(0, -600);
        Step(person, physics, new() { MoveX = 1 });
        Assert.Equal(-600 - Metrics.Gravity * Dt, person.Body.LinearVelocity.Y, 3);
        Assert.Equal(Metrics.AirAcceleration * Dt, person.Body.LinearVelocity.X, 3);
    }

    [Fact]
    public void FinalTenPercentTakesSecondsAndRetainsAirControl()
    {
        var (person, physics) = Create(10000);
        float atHalfSecond = 0, atOneSecond = 0;
        for (var i = 1; i <= 360; i++)
        {
            Step(person, physics, new() { MoveX = 1 });
            if (i == 60) atHalfSecond = -person.Body.LinearVelocity.Y;
            if (i == 120) atOneSecond = -person.Body.LinearVelocity.Y;
        }
        Assert.InRange(atHalfSecond, Metrics.MaximumFallSpeed * .8f, Metrics.MaximumFallSpeed * .9f);
        Assert.InRange(atOneSecond, Metrics.HardLandingSpeed, Metrics.MaximumFallSpeed * .98f);
        Assert.InRange(-person.Body.LinearVelocity.Y, Metrics.MaximumFallSpeed * .99f, Metrics.MaximumFallSpeed - .1f);
        Assert.Equal(Metrics.RunSpeed, person.Body.LinearVelocity.X, 3);
        var beforeTurn = person.Body.LinearVelocity.X;
        Step(person, physics, new() { MoveX = -1 });
        Assert.Equal(beforeTurn - Metrics.AirAcceleration * Dt, person.Body.LinearVelocity.X, 3);
    }

    [Fact]
    public void FallCurveDoesNotDependOnTickSize()
    {
        var single = Metrics.AdvanceVerticalSpeed(0, Metrics.Gravity, 3);
        var stepped = 0f;
        for (var i = 0; i < 360; i++) stepped = Metrics.AdvanceVerticalSpeed(stepped, Metrics.Gravity, Dt);
        Assert.InRange(MathF.Abs(single - stepped), 0, .01f);
    }

    [Theory]
    [InlineData(4, false)]
    [InlineData(12, false)]
    [InlineData(24, true)]
    [InlineData(48, true)]
    [InlineData(80, true)]
    public void TowerHeightsProduceSoftAndHardLandings(int tiles, bool hard)
    {
        var (person, physics) = Create(tiles * Metrics.TileSize);
        float impact = 0;
        person.Landed += speed => impact = speed;
        for (var i = 0; i < 600 && impact == 0; i++) Step(person, physics);
        Assert.True(impact > 0);
        Assert.Equal(hard, impact >= Metrics.HardLandingSpeed);
        Assert.Equal(impact, person.LandingSpeedThisFrame);
        Step(person, physics);
        Assert.Equal(0, person.LandingSpeedThisFrame);
    }

    [Theory]
    [InlineData(4, 6)]
    [InlineData(12, 8)]
    [InlineData(24, 10)]
    [InlineData(48, 12)]
    [InlineData(80, 14)]
    public void AuthoredTowerCanBeClimbedAndEachBoardClearsTheLowerBoards(int tiles, int boardEnd)
    {
        using var database = LevelDatabase2D.OpenRead(Path.Combine(TestAssetPath.StaticRoot, "levels", "cavern", "level.db"));
        var map = database.Load();
        var collision = new CollisionSystem2D();
        var physics = new PhysicsWorld2D(collision) { Gravity = new(0, -Metrics.Gravity), MaxSubstepSeconds = Dt };
        using var level = new SideScrollerLevel2D(Metrics, map);
        level.CreateSimulation(collision, physics, new EntityIdAllocator2D(), 1, 2, 4);
        var groundY = map.Origin.Y + 12 * map.TileSize;
        var person = new Person2D(EntityId2D.Create(), collision, physics, Metrics,
            new(map.Origin.X + 2.5f * map.TileSize - Metrics.PlayerColliderCenterOffsetX,
                groundY + Metrics.PlayerColliderSize.Y / 2), 2, 1, CombatFaction2D.Player, tileMap: map);
        void Tick(PersonCommand2D command = default)
        {
            level.UpdateStreaming(person.Position);
            Step(person, physics, command);
        }

        var boardY = groundY + tiles * map.TileSize;
        for (var i = 0; i < 2200 && person.WorldObject.WorldBounds.Bottom < boardY + 32; i++)
            Tick(new() { ClimbY = 1 });
        Assert.True(person.IsClimbingLadder);
        Assert.True(person.WorldObject.WorldBounds.Bottom >= boardY + 32);

        var edge = map.Origin.X + (boardEnd + 1) * map.TileSize;
        var stoodOnBoard = false;
        for (var i = 0; i < 300 && person.WorldObject.WorldBounds.Left <= edge + 2; i++)
        {
            Tick(new() { MoveX = 1 });
            stoodOnBoard |= person.IsGrounded && MathF.Abs(person.WorldObject.WorldBounds.Bottom - boardY) < 2;
        }
        Assert.True(stoodOnBoard);
        Assert.True(person.WorldObject.WorldBounds.Left > edge);
        // Ignore the small landing onto the board; the next landing must be the original floor.
        float impact = 0;
        person.Landed += speed => impact = speed;
        for (var i = 0; i < 600 && impact == 0; i++) Tick();
        Assert.True(impact > 0);
        Assert.InRange(person.WorldObject.WorldBounds.Bottom, groundY - 1, groundY + 2);
        Assert.Equal(tiles >= 24, impact >= Metrics.HardLandingSpeed);
    }

    [Fact]
    public void DownwardThrowCanLandHardImmediatelyWithoutBeingClamped()
    {
        var (person, physics) = Create(64);
        person.Body.LinearVelocity = new(0, -1800);
        Step(person, physics);
        Assert.True(person.Body.LinearVelocity.Y < -Metrics.MaximumFallSpeed);
        for (var i = 0; i < 20 && person.LandingSpeedThisFrame == 0; i++) Step(person, physics);
        Assert.True(person.LandingSpeedThisFrame > Metrics.MaximumFallSpeed);
        Assert.Equal(1, Metrics.HardLandingIntensity(person.LandingSpeedThisFrame), 4);
    }

    [Fact]
    public void ExtraImpactGrowsWithTimeInTheTailAndSaturates()
    {
        var start = -Metrics.HardLandingSpeed;
        var first = Metrics.HardLandingIntensity(-Metrics.AdvanceVerticalSpeed(start, Metrics.Gravity, .4f));
        var second = Metrics.HardLandingIntensity(-Metrics.AdvanceVerticalSpeed(start, Metrics.Gravity, .8f));
        Assert.InRange(first, .1f, .4f);
        Assert.Equal(first * 2, second, 4);
        Assert.Equal(0, Metrics.HardLandingIntensity(760));
        Assert.Equal(1, Metrics.HardLandingIntensity(Metrics.MaximumFallSpeed), 4);
    }

    private static (Person2D, PhysicsWorld2D) Create(float feetHeight)
    {
        var collision = new CollisionSystem2D();
        var physics = new PhysicsWorld2D(collision) { Gravity = new(0, -Metrics.Gravity), MaxSubstepSeconds = Dt };
        var floor = new SpatialObject2D(AxisAlignedRectangle2D.FromSize(new Vector2(4096, 32)));
        floor.Transform.Position = new(0, -16);
        var ground = physics.AddBody(floor, BodyMotionType2D.Static);
        ground.CollisionLayer = 1; ground.CollisionMask = 2;
        var person = new Person2D(EntityId2D.Create(), collision, physics, Metrics,
            new(0, feetHeight + Metrics.PlayerColliderSize.Y / 2), 2, 1, CombatFaction2D.Player);
        return (person, physics);
    }

    private static void Step(Person2D person, PhysicsWorld2D physics, PersonCommand2D command = default)
    {
        person.BeginFrame(Dt);
        person.ApplyCommand(command, Dt);
        physics.Step(Dt);
        person.UpdateAfterPhysics(Dt);
    }
}
