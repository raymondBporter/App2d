using App2d.Levels;
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

public sealed class PersonBalanceTests
{
    private const float Dt = 1f / 120f;
    private readonly TraversalMetrics2D _metrics = TraversalMetricsLoader2D.Load(TestAssetPath.Root);
    private readonly PhysicsWorld2D _physics;
    private readonly Person2D _person;

    public PersonBalanceTests()
    {
        var collision = new CollisionSystem2D();
        _physics = new PhysicsWorld2D(collision) { Gravity = new Vector2(0f, -_metrics.Gravity) };
        _person = new Person2D(collision, _physics, _metrics,
            new Vector2(0f, _metrics.PlayerColliderSize.Y / 2f), 2u, 1u, CombatFaction2D.Player);
    }

    [Theory]
    [InlineData(-1, 0.2f, 0)]
    [InlineData(1, 0.2f, 0)]
    [InlineData(-1, 0.4f, -1)]
    [InlineData(1, 0.4f, 1)]
    [InlineData(-1, 0.8f, -1)]
    [InlineData(1, 0.8f, 1)]
    public void RequiresEnoughOverhangAndKeepsBalancingNearTip(int edge, float overhang, int expected)
    {
        AddLedge(edge, overhang);
        Step();
        Assert.True(_person.IsGrounded);
        Assert.Equal(expected, _person.BalanceDirection);
    }

    [Fact]
    public void AdjacentCollidersAndReturningToSolidGroundDoNotBalance()
    {
        var ledge = AddLedge(1);
        Step();
        Assert.Equal(1, _person.BalanceDirection);
        AddSupport(ledge.WorldObject.WorldBounds.Right, 200f);
        Step();
        Assert.Equal(0, _person.BalanceDirection);
    }

    [Fact]
    public void LowerFloorDoesNotFillInMissingFooting()
    {
        var ledge = AddLedge(1);
        var lowerFloor = AddSupport(ledge.WorldObject.WorldBounds.Right, 200f);
        lowerFloor.WorldObject.Transform.Position -= new Vector2(0f, 32f);
        Step();
        Assert.Equal(1, _person.BalanceDirection);
    }

    [Theory]
    [InlineData(BodyMotionType2D.Static, false)]
    [InlineData(BodyMotionType2D.Kinematic, true)]
    public void SupportsSolidAndOneWayPlatformEdges(BodyMotionType2D motion, bool oneWay)
    {
        AddLedge(-1, motion: motion).IsOneWayPlatform = oneWay;
        Step();
        Assert.Equal(-1, _person.BalanceDirection);
    }

    [Fact]
    public void IgnoredOneWayPlatformDoesNotSupportBalance()
    {
        var platform = AddLedge(1);
        platform.IsOneWayPlatform = true;
        Step();
        Assert.Equal(1, _person.BalanceDirection);
        Step(drop: true);
        Assert.Equal(0, _person.BalanceDirection);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void WalkingJumpingAndDashingInterruptBalance(bool walk, bool jump, bool dash)
    {
        AddLedge(1);
        Step();
        Assert.Equal(1, _person.BalanceDirection);
        Step(move: walk ? -1f : 0f, jump: jump, dash: dash);
        Assert.Equal(0, _person.BalanceDirection);
    }

    [Fact]
    public void LossOfSupportAndResetClearBalance()
    {
        var ledge = AddLedge(1);
        Step();
        Assert.Equal(1, _person.BalanceDirection);
        _physics.RemoveBody(ledge);
        Step();
        Assert.Equal(0, _person.BalanceDirection);
        _person.Reset(new Vector2(0f, 300f));
        Assert.Equal(0, _person.BalanceDirection);
    }

    private void Step(float move = 0f, bool jump = false, bool dash = false, bool drop = false)
    {
        _person.BeginFrame(Dt);
        _person.ApplyCommand(new PersonCommand2D(
            new PersonMovementIntent2D(move, jump, jump, false, drop, dash), false, false), Dt);
        _physics.Step(Dt);
        _person.UpdateAfterPhysics(Dt);
    }

    private PhysicsBody2D AddLedge(int edge, float overhang = 0.4f, BodyMotionType2D motion = BodyMotionType2D.Static)
    {
        var bounds = _person.WorldObject.WorldBounds;
        return edge > 0
            ? AddSupport(-200f, bounds.Right - bounds.Size.X * overhang, motion)
            : AddSupport(bounds.Left + bounds.Size.X * overhang, 200f, motion);
    }

    private PhysicsBody2D AddSupport(float left, float right, BodyMotionType2D motion = BodyMotionType2D.Static)
    {
        var spatial = new SpatialObject2D(AxisAlignedRectangle2D.FromSize(new Vector2(right - left, 32f)));
        spatial.Transform.Position = new Vector2((left + right) / 2f, -16f);
        var body = _physics.AddBody(spatial, motion);
        body.CollisionLayer = 1u;
        body.CollisionMask = 2u;
        return body;
    }
}
