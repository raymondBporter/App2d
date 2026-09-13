using App2d.Collision;
using App2d.Core;
using App2d.Core.Geometry;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Player;
using App2d.Physics;
using App2d.Rendering;
using App2d.Rendering.Textures;
using App2d.Tiles;
using System.Numerics;
using Xunit;

namespace App2d.Gameplay.Tests.Persons;

public sealed class PersonLadderTests
{
    private const float Dt = 1f / 120f;
    private readonly EditableTileMap2D _map = new(16, 64, 32f, 8);
    private readonly PhysicsWorld2D _physics;
    private readonly TraversalMetrics2D _metrics = TraversalMetrics2D.FromPlayerAsset(TestAssetPath.Root);
    private readonly Person2D _person;

    public PersonLadderTests()
    {
        var collision = new CollisionSystem2D();
        _physics = new PhysicsWorld2D(collision)
        {
            Gravity = new Vector2(0f, -_metrics.Gravity),
            MaxSubstepSeconds = Dt,
            PositionIterations = 3,
            VelocityIterations = 2
        };
        for (var y = 1; y <= 20; y++)
            _map.SetTileKind(4, y, TileKind2D.Ladder);
        _person = new Person2D(collision, _physics, _metrics,
            new Vector2(144f - _metrics.PlayerColliderCenterOffsetX, 32f + _metrics.PlayerColliderSize.Y / 2f),
            2u, 1u, CombatFaction2D.Player, tileMap: _map);
        AddSolid(new Vector2(256f, 16f), new Vector2(512f, 32f));
    }

    [Fact]
    public void ClimbPauseAndDescendAcrossChunkBoundaries()
    {
        var initialY = _person.Position.Y;
        Step(climb: 1f, frames: 180);
        Assert.True(_person.IsClimbingLadder);
        Assert.Equal(initialY + _metrics.LadderClimbSpeed * 1.5f, _person.Position.Y, 2);
        var hangY = _person.Position.Y;
        Step(frames: 120);
        Assert.Equal(hangY, _person.Position.Y, 3);
        Assert.Equal(0f, _person.Body.GravityScale);
        Step(climb: -1f, frames: 60);
        Assert.Equal(hangY - _metrics.LadderClimbSpeed * 0.5f, _person.Position.Y, 2);
    }

    [Fact]
    public void JumpKeyUsedForUpGrabsLadderWithoutJumping()
    {
        Step(climb: 1f, jump: true);
        Assert.True(_person.IsClimbingLadder);
        Assert.False(_person.IsSustainingJump);
        Assert.Equal(_metrics.LadderClimbSpeed, _person.Body.LinearVelocity.Y);
    }

    [Fact]
    public void JumpOffWorksWhileHoldingUpAndDoesNotImmediatelyRegrab()
    {
        Step(climb: 1f, frames: 60);
        Step(climb: 1f, jumpOff: true);
        Assert.False(_person.IsClimbingLadder);
        Assert.True(_person.Body.LinearVelocity.Y > _metrics.LadderClimbSpeed);
        Step(climb: 1f, frames: 5);
        Assert.False(_person.IsClimbingLadder);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MovingSidewaysOrDashingLeavesLadder(bool dash)
    {
        Step(climb: 1f, frames: 60);
        Step(move: 1f, dash: dash);
        Assert.False(_person.IsClimbingLadder);
        Assert.True(_person.Body.LinearVelocity.X > 0f);
        Assert.Equal(dash, _person.IsDashing);
        if (!dash)
        {
            Assert.Equal(1f, _person.Body.GravityScale);
            Step(move: 1f, frames: 30);
            Assert.True(_person.Body.LinearVelocity.Y < 0f);
        }
    }

    [Fact]
    public void TopStopsAtLastRungAndAllowsDescendingAgain()
    {
        Step(climb: 1f, frames: 600);
        Assert.True(_person.IsClimbingLadder);
        Assert.Equal(21 * 32f, _person.WorldObject.WorldBounds.Center.Y, 2);
        Assert.Equal(21 * 32f - _metrics.PlayerColliderSize.Y / 2f,
            _person.WorldObject.WorldBounds.Bottom, 2);
        var topPosition = _person.Position;
        Step(climb: 1f, frames: 120);
        Assert.Equal(topPosition, _person.Position);
        Assert.Equal(0f, _person.Body.LinearVelocity.Y);
        Step(frames: 60);
        Assert.Equal(topPosition, _person.Position);
        Step(climb: -1f, frames: 30);
        Assert.True(_person.IsClimbingLadder);
        Assert.True(_person.WorldObject.WorldBounds.Center.Y < 21 * 32f);
    }

    [Theory]
    [InlineData(false, 0f)]
    [InlineData(false, 1f)]
    [InlineData(true, 0f)]
    [InlineData(true, 1f)]
    public void FreshJumpAtTopReleasesLadderEvenWithUpInput(bool dedicatedJump, float move)
    {
        Step(climb: 1f, frames: 600);
        Step(); // Release W/Up before pressing it again to jump.
        var topY = _person.Position.Y;
        var jumps = 0;
        _person.JumpStarted += () => jumps++;

        Step(climb: 1f, move: move, jump: true, jumpOff: dedicatedJump);

        Assert.False(_person.IsClimbingLadder);
        Assert.Equal(1, jumps);
        Assert.True(_person.Body.LinearVelocity.Y > _metrics.LadderClimbSpeed);
        Assert.True(_person.Position.Y > topY);
        Step(climb: 1f, frames: 5);
        Assert.False(_person.IsClimbingLadder);
    }

    [Fact]
    public void PressingUpAgainBelowTopContinuesClimbing()
    {
        Step(climb: 1f, frames: 60);
        Step();
        Step(climb: 1f, jump: true);
        Assert.True(_person.IsClimbingLadder);
        Assert.Equal(_metrics.LadderClimbSpeed, _person.Body.LinearVelocity.Y);
    }

    [Fact]
    public void CeilingBlocksClimbing()
    {
        AddSolid(new Vector2(144f, 300f), new Vector2(96f, 32f));
        Step(climb: 1f, frames: 240);
        Assert.True(_person.WorldObject.WorldBounds.Top <= 284.1f);
    }

    [Fact]
    public void WalkingPastLadderDoesNotAttach()
    {
        Step(move: 1f, frames: 30);
        Assert.False(_person.IsClimbingLadder);
        Assert.True(_person.Position.X > 160f);
    }

    [Fact]
    public void DescendingThroughOneWaySupportDoesNotGetStuck()
    {
        var platform = AddSolid(new Vector2(144f, 250f), new Vector2(96f, 8f));
        platform.IsOneWayPlatform = true;
        Step(climb: 1f, frames: 200);
        Step(climb: -1f, frames: 160);
        Assert.True(_person.IsClimbingLadder);
        Assert.True(_person.WorldObject.WorldBounds.Top < 246f);
    }

    [Fact]
    public void DescendingPastBottomLetsGo()
    {
        Step(climb: 1f, frames: 180);
        for (var y = 1; y < 8; y++)
            _map.SetTileKind(4, y, TileKind2D.Empty);
        Step(climb: -1f, frames: 90);
        Assert.False(_person.IsClimbingLadder);
        Assert.Equal(1f, _person.Body.GravityScale);
    }

    [Fact]
    public void ErasingLadderRestoresGravityImmediately()
    {
        Step(climb: 1f, frames: 60);
        for (var y = 1; y <= 20; y++)
            _map.SetTileKind(4, y, TileKind2D.Empty);
        Step(frames: 30);
        Assert.False(_person.IsClimbingLadder);
        Assert.Equal(1f, _person.Body.GravityScale);
        Assert.True(_person.Body.LinearVelocity.Y < 0f);
    }

    [Fact]
    public void DamageReleasesLadderAndPreservesKnockback()
    {
        Step(climb: 1f, frames: 60);
        Assert.True(_person.TakeDamage(1, new Vector2(220f, 170f)));
        Step(climb: 1f);
        Assert.False(_person.IsClimbingLadder);
        Assert.True(_person.Body.LinearVelocity.X > 0f);
    }

    [Fact]
    public void ResetAndDeathClearClimbingState()
    {
        Step(climb: 1f, frames: 30);
        var position = _person.Position;
        _person.Reset(position);
        Assert.False(_person.IsClimbingLadder);
        Assert.Equal(1f, _person.Body.GravityScale);
        Step(climb: 1f, frames: 60);
        Assert.True(_person.TakeDamage(5, Vector2.Zero));
        Assert.False(_person.IsClimbingLadder);
        Assert.Equal(1f, _person.Body.GravityScale);
    }

    [Theory]
    [InlineData("sword", "player-sword")]
    [InlineData("gun", "player-gun")]
    [InlineData("unarmed", "player-unarmed")]
    public void ClimbingAnimationAdvancesPausesAndResumesForEachEquipment(string equipment, string character)
    {
        var scene = new Scene2D();
        using var textures = new TextureCache2D(TestAssetPath.Root);
        using var presentation = new PersonPresentation2D(scene, textures, _metrics);
        presentation.Equip(equipment);
        var shader = Assert.IsType<SpriteShader2D>(Assert.Single(scene).Shader);
        var frames = Enumerable.Range(1, 4).Select(index => textures.Load(
            $"characters/{character}/animations/climb/frame-{index:0000}.png")).ToArray();

        Step(climb: 1f, frames: 60);
        Draw(0f);
        Assert.Same(frames[0], shader.Texture);
        Draw(0.13f);
        Assert.Same(frames[1], shader.Texture);

        Step();
        Draw(0.25f);
        Assert.Same(frames[1], shader.Texture);

        Step(climb: -1f);
        Draw(0.13f);
        Assert.Same(frames[2], shader.Texture);
        Draw(0.13f);
        Assert.Same(frames[3], shader.Texture);
        Draw(0.13f);
        Assert.Same(frames[0], shader.Texture);

        Step(jumpOff: true);
        Draw(0f);
        Assert.DoesNotContain(shader.Texture, frames);

        void Draw(float dt) => presentation.Update(dt, 0, _person, 0f, false, false);
    }

    private void Step(float climb = 0f, float move = 0f, bool jump = false,
        bool jumpOff = false, bool dash = false, int frames = 1)
    {
        for (var i = 0; i < frames; i++)
        {
            _person.BeginFrame(Dt);
            _person.ApplyCommand(new PersonCommand2D(
                new PersonMovementIntent2D(move, jump && i == 0, jump || jumpOff,
                    false, false, dash && i == 0, climb, jumpOff && i == 0),
                false, null, false), Dt);
            _physics.Step(Dt);
            _person.UpdateAfterPhysics(Dt);
        }
    }

    private PhysicsBody2D AddSolid(Vector2 center, Vector2 size)
    {
        var spatial = new SpatialObject2D(AxisAlignedRectangle2D.FromSize(size));
        spatial.Transform.Position = center;
        var body = _physics.AddBody(spatial, BodyMotionType2D.Static);
        body.CollisionLayer = 1u;
        body.CollisionMask = 2u;
        return body;
    }
}
