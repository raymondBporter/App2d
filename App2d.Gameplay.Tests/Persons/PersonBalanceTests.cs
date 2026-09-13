using App2d.Collision;
using App2d.Core;
using App2d.Core.Geometry;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Player;
using App2d.Physics;
using App2d.Rendering;
using App2d.Rendering.Textures;
using System.Numerics;
using Xunit;

namespace App2d.Gameplay.Tests.Persons;

public sealed class PersonBalanceTests
{
    private const float Dt = 1f / 120f;
    private readonly TraversalMetrics2D _metrics = TraversalMetrics2D.FromPlayerAsset(TestAssetPath.Root);
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

    [Theory]
    [InlineData(-1, -1, "balance-left-foot")]
    [InlineData(-1, 1, "balance-right-foot")]
    [InlineData(1, -1, "balance-right-foot")]
    [InlineData(1, 1, "balance-left-foot")]
    public void SelectsAndLoopsCorrectPoseForBothEdgesAndFacings(int edge, int facing, string clip)
    {
        _person.Face(facing);
        AddLedge(edge);
        Step();
        var scene = new Scene2D();
        using var textures = new TextureCache2D(TestAssetPath.Root);
        using var presentation = new PersonPresentation2D(scene, textures, _metrics);
        var shader = Assert.IsType<SpriteShader2D>(Assert.Single(scene).Shader);

        Draw(0f);
        Assert.Same(Frame(1), shader.Texture);
        Assert.Equal(facing < 0, shader.FlipX);
        Draw(0.26f);
        Assert.Same(Frame(2), shader.Texture);
        Draw(1.75f);
        Assert.Same(Frame(1), shader.Texture);

        Texture2D Frame(int index) => textures.Load($"characters/player-sword/animations/{clip}/frame-{index:0000}.png");
        void Draw(float dt) => presentation.Update(dt, 0, _person, 0f, false, false);
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

    [Fact]
    public void BalanceYieldsToCombatAndReturnsToIdleOnSolidGround()
    {
        var ledge = AddLedge(1);
        Step();
        var scene = new Scene2D();
        using var textures = new TextureCache2D(TestAssetPath.Root);
        using var presentation = new PersonPresentation2D(scene, textures, _metrics);
        var shader = Assert.IsType<SpriteShader2D>(Assert.Single(scene).Shader);
        Draw();
        AssertClip("balance-left-foot");
        presentation.PlayMeleeAttack(0.35f, false);
        Draw();
        AssertClip("sword-attack");
        presentation.Reset();
        presentation.Update(0f, 0, _person, 0f, true, false);
        AssertClip("shield-block");
        presentation.PlayHit();
        Draw();
        AssertClip("hit-a");
        presentation.Reset();
        Draw();
        AssertClip("balance-left-foot");
        AddSupport(ledge.WorldObject.WorldBounds.Right, 200f);
        Step();
        Draw();
        AssertClip("idle");

        void Draw() => presentation.Update(0f, 0, _person, 0f, false, false);
        void AssertClip(string clip) => Assert.Same(
            textures.Load($"characters/player-sword/animations/{clip}/frame-0001.png"), shader.Texture);
    }

    [Theory]
    [InlineData("gun")]
    [InlineData("unarmed")]
    public void EquipmentWithoutAuthoredBalanceClipsKeepsItsIdle(string equipment)
    {
        AddLedge(1);
        Step();
        var scene = new Scene2D();
        using var textures = new TextureCache2D(TestAssetPath.Root);
        using var presentation = new PersonPresentation2D(scene, textures, _metrics);
        presentation.Equip(equipment);
        presentation.Update(0f, 0, _person, 0f, false, false);
        var shader = Assert.IsType<SpriteShader2D>(Assert.Single(scene).Shader);
        Assert.Same(textures.Load($"characters/player-{equipment}/animations/idle/frame-0001.png"), shader.Texture);
        presentation.Equip("sword");
        presentation.Update(0f, 0, _person, 0f, false, false);
        Assert.Same(textures.Load("characters/player-sword/animations/balance-left-foot/frame-0001.png"), shader.Texture);
    }

    private void Step(float move = 0f, bool jump = false, bool dash = false, bool drop = false)
    {
        _person.BeginFrame(Dt);
        _person.ApplyCommand(new PersonCommand2D(
            new PersonMovementIntent2D(move, jump, jump, false, drop, dash), false, null, false), Dt);
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
