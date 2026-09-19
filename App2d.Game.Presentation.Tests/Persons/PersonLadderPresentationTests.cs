using App2d.Gameplay.Persons.Actions;
using App2d.Levels;
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

public sealed class PersonLadderPresentationTests
{
    private const float Dt = 1f / 120f;
    private readonly EditableTileMap2D _map = new(16, 64, 32f, 8);
    private readonly PhysicsWorld2D _physics;
    private readonly TraversalMetrics2D _metrics = TraversalMetricsLoader2D.Load(TestAssetPath.Root);
    private readonly Person2D _person;

    public PersonLadderPresentationTests()
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
        _person = new Person2D(EntityId2D.Create(), collision, _physics, _metrics,
            new Vector2(144f - _metrics.PlayerColliderCenterOffsetX, 32f + _metrics.PlayerColliderSize.Y / 2f),
            2u, 1u, CombatFaction2D.Player, tileMap: _map);
        AddSolid(new Vector2(256f, 16f), new Vector2(512f, 32f));
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
        presentation.Equip(Enum.Parse<EquipmentKind2D>(equipment, ignoreCase: true));
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

        void Draw(float dt) => presentation.Update(dt, 0, _person.CaptureState(), 0f, false, false);
    }

    private void Step(float climb = 0f, float move = 0f, bool jump = false,
        bool jumpOff = false, bool dash = false, int frames = 1)
    {
        for (var i = 0; i < frames; i++)
        {
            _person.BeginFrame(Dt);
            _person.ApplyCommand(new PersonCommand2D
                { MoveX = move, ClimbY = climb, JumpHeld = jump || jumpOff, DashHeld = dash }, Dt);
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
