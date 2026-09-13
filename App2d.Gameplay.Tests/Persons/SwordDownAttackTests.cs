using App2d.Collision;
using App2d.Core;
using App2d.Core.Geometry;
using App2d.Gameplay.Audio;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Enemies;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Persons.Actions;
using App2d.Gameplay.Player;
using App2d.Gameplay.World;
using App2d.Physics;
using App2d.Rendering;
using App2d.Rendering.Textures;
using App2d.Tiles;
using System.Numerics;
using Xunit;

namespace App2d.Gameplay.Tests.Persons;

public sealed class SwordDownAttackTests
{
    private static PersonCommand2D Attack => new(default, true, null, false, DownHeld: true);

    [Theory]
    [InlineData(-1f)]
    [InlineData(1f)]
    public void DownStabHitsBelowImmediatelyOnceAndPlaysAuthoredFrames(float facing)
    {
        using var game = new Fixture();
        game.Person.Face(facing);
        var below = game.AddEnemy(new Vector2(0f, -55f));
        var beside = game.AddEnemy(new Vector2(70f, 0f));
        game.Step(Attack, 0f);
        Assert.Equal(1, game.DownAttacks);
        Assert.Equal(0, game.NormalAttacks);
        Assert.Equal(0.25f, game.AttackDuration);
        game.AssertFrame("sword-down-attack", 1);
        Assert.Equal(facing < 0f, game.Shader.FlipX);

        var hitbox = Assert.Single(game.Arsenal.GetActiveAttackHitboxes());
        Assert.Equal(game.Person.Position + new Vector2(0f, -32f), hitbox.Transform.Position);
        Assert.Equal(3, below.Health.Current);
        Assert.True(below.Body.LinearVelocity.Y < 0f);
        Assert.Equal(5, beside.Health.Current);
        Assert.True(game.Person.DownAttackBouncedThisFrame);
        Assert.Equal(520f, game.Person.Body.LinearVelocity.Y);

        // Clear invulnerability to prove attack identity prevents a second hit.
        // Keep the player in range instead of letting the bounce move them away.
        game.Person.Body.LinearVelocity = Vector2.Zero;
        below.BeginFrame(1f);
        game.Step(default, 0.05f);
        Assert.Equal(3, below.Health.Current);
        Assert.False(game.Person.DownAttackBouncedThisFrame);
        Assert.Equal(Vector2.Zero, game.Person.Body.LinearVelocity);
        game.AssertFrame("sword-down-attack", 2);
        game.Step(default, 0.04f);
        game.Step(default, 0f);
        game.AssertFrame("sword-down-attack", 3);
        Assert.Empty(game.Arsenal.GetActiveAttackHitboxes());
        Assert.True(game.Arsenal.IsMeleeAttackActive);
        game.Step(default, 0.17f);
        game.Step(default, 0f);
        Assert.False(game.Arsenal.IsMeleeAttackActive);
        game.AssertFrame("fall", 1);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void GroundedOrWithoutDownUsesNormalSword(bool grounded, bool downHeld)
    {
        using var game = new Fixture(grounded);
        game.Step(Attack with { DownHeld = downHeld }, 0f);
        Assert.Equal(1, game.NormalAttacks);
        Assert.Equal(0, game.DownAttacks);
        game.AssertFrame("sword-attack", 1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OppositeAttackCannotOverlapOrReplaceCurrentSwing(bool startDown)
    {
        using var game = new Fixture();
        game.Step(Attack with { DownHeld = startDown }, 0.05f);
        game.Step(Attack with { DownHeld = !startDown }, 0.05f);
        Assert.Equal(startDown ? 1 : 0, game.DownAttacks);
        Assert.Equal(startDown ? 0 : 1, game.NormalAttacks);
        game.Step(default, 0.5f);
        game.Step(Attack with { DownHeld = !startDown }, 0f);
        Assert.Equal(1, game.DownAttacks);
        Assert.Equal(1, game.NormalAttacks);
    }

    [Theory]
    [InlineData("damage")]
    [InlineData("switch")]
    [InlineData("reset")]
    [InlineData("disable")]
    public void InterruptionClearsDownwardHitbox(string cause)
    {
        using var game = new Fixture();
        game.Step(Attack, 0f);
        Assert.Single(game.Arsenal.GetActiveAttackHitboxes());
        switch (cause)
        {
            case "damage": Assert.True(game.Person.TakeDamage(1, Vector2.Zero)); break;
            case "switch": game.Arsenal.SelectNext(); break;
            case "reset": game.Person.Reset(Vector2.Zero); break;
            case "disable": game.Person.SetSimulationEnabled(false); break;
        }
        Assert.Empty(game.Arsenal.GetActiveAttackHitboxes());
        Assert.False(game.Arsenal.IsMeleeAttackActive);
    }

    [Fact]
    public void AttackDoesNotChangeAirMovementOrGravity()
    {
        using var attacking = new Fixture();
        using var baseline = new Fixture();
        attacking.Physics.Gravity = baseline.Physics.Gravity = new Vector2(0f, -900f);
        var movement = new PersonMovementIntent2D(1f, false, false, false, false, false);
        for (var i = 0; i < 60; i++)
        {
            attacking.Step(Attack with { Movement = movement, UsePrimaryAction = i == 0 }, 1f / 120f);
            baseline.Step(new(movement, false, null, false), 1f / 120f);
            Assert.Equal(baseline.Person.Position, attacking.Person.Position);
            Assert.Equal(baseline.Person.Body.LinearVelocity, attacking.Person.Body.LinearVelocity);
        }
    }

    [Theory]
    [InlineData("enemy")]
    [InlineData("spikes")]
    [InlineData("both")]
    public void HitReversesFallOncePreservesSidewaysVelocityAndCanBounceAgain(string target)
    {
        using var game = new Fixture();
        game.Physics.Gravity = new Vector2(0f, -1900f);
        if (target != "spikes") game.AddEnemy(new Vector2(0f, -55f));
        if (target != "enemy") game.AddSpikes();
        game.Person.Body.LinearVelocity = new Vector2(120f, -900f);
        game.Step(Attack, 0f);
        Assert.True(game.Person.DownAttackBouncedThisFrame);
        Assert.Equal(new Vector2(120f, 520f), game.Person.Body.LinearVelocity);
        Assert.Equal(5, game.Person.Health.Current);
        Assert.False(game.Person.IsGrounded);
        Assert.False(game.Level.TryGetSpikeSource(game.Person.WorldObject.WorldBounds, out _));

        game.Step(default, 0.01f);
        Assert.False(game.Person.DownAttackBouncedThisFrame);
        Assert.InRange(game.Person.Body.LinearVelocity.Y, 490f, 510f);
        Assert.True(game.Person.Position.Y > 0f);

        game.Step(default, 0.25f);
        game.Person.WorldObject.Transform.Position = Vector2.Zero;
        game.Person.Body.LinearVelocity = new Vector2(-90f, -600f);
        game.Step(Attack, 0f);
        Assert.True(game.Person.DownAttackBouncedThisFrame);
        Assert.Equal(new Vector2(-90f, 520f), game.Person.Body.LinearVelocity);
    }

    [Theory]
    [InlineData("miss")]
    [InlineData("normal")]
    [InlineData("recovery")]
    [InlineData("cancelled")]
    [InlineData("dead-enemy")]
    public void OnlyAnActiveDownwardHitCanBounce(string scenario)
    {
        using var game = new Fixture();
        game.Person.Body.LinearVelocity = new Vector2(0f, -200f);
        if (scenario == "dead-enemy")
            game.AddEnemy(new Vector2(0f, -55f)).TakeDamage(5, Vector2.Zero);
        if (scenario == "normal") game.AddSpikes();
        game.Step(Attack with { DownHeld = scenario != "normal" }, 0f);
        if (scenario == "recovery")
        {
            game.Step(default, 0.09f);
            game.Step(default, 0f);
            game.AddSpikes();
        }
        if (scenario == "cancelled")
        {
            game.Arsenal.InterruptPrimary();
            game.AddSpikes();
        }
        game.Step(default, 0f);
        Assert.False(game.Person.DownAttackBouncedThisFrame);
        Assert.True(game.Person.Body.LinearVelocity.Y < 0f);
    }

    [Fact]
    public void BounceWinsOverEnemyBodyContactOnImpactFrameOnly()
    {
        using var game = new Fixture();
        game.AddContactEnemy(new Vector2(0f, -40f));
        game.Step(Attack, 0f);
        Assert.True(game.Person.DownAttackBouncedThisFrame);
        game.ContactDamage.Resolve(game.Person);
        Assert.Equal(5, game.Person.Health.Current);
        Assert.Equal(520f, game.Person.Body.LinearVelocity.Y);

        // Staying inside an enemy after the impact frame is still dangerous.
        game.Step(default, 0f);
        game.ContactDamage.Resolve(game.Person);
        Assert.Equal(4, game.Person.Health.Current);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly CollisionSystem2D _collision = new();
        private readonly CombatantRegistry2D _combatants = new();
        private readonly TextureCache2D _textures = new(TestAssetPath.Root);
        private readonly TraversalMetrics2D _metrics = TraversalMetrics2D.FromPlayerAsset(TestAssetPath.Root);
        private readonly PersonPresentation2D _presentation;
        public PhysicsWorld2D Physics { get; }
        public Person2D Person { get; }
        public PersonArsenal2D Arsenal { get; }
        public SpriteShader2D Shader { get; }
        public EditableTileMap2D TileMap { get; }
        public SideScrollerLevel2D Level { get; }
        public ContactDamageSystem2D ContactDamage { get; }
        public int DownAttacks { get; private set; }
        public int NormalAttacks { get; private set; }
        public float AttackDuration { get; private set; }

        public Fixture(bool grounded = false)
        {
            Physics = new(_collision) { Gravity = Vector2.Zero };
            TileMap = new(SideScrollerLevel2D.WorldWidthTiles, SideScrollerLevel2D.WorldHeightTiles,
                32f, SideScrollerLevel2D.ChunkSizeTiles, SideScrollerLevel2D.WorldOrigin);
            Level = new(_metrics, TileMap, _ => 0);
            ContactDamage = new(_collision, 4, _combatants);
            Person = new(_collision, Physics, _metrics, Vector2.Zero, 2, 1, CombatFaction2D.Player);
            if (grounded)
            {
                var floor = Physics.AddBody(new SpatialObject2D(
                    AxisAlignedRectangle2D.FromSize(new Vector2(500f, 20f))), BodyMotionType2D.Static);
                floor.WorldObject.Transform.Position = new(0f, Person.WorldObject.WorldBounds.Bottom - 10f);
                floor.CollisionLayer = 1;
                floor.CollisionMask = 2;
            }
            var scene = new Scene2D();
            _presentation = new(scene, _textures, _metrics);
            Shader = Assert.IsType<SpriteShader2D>(Assert.Single(scene).Shader);
            var sounds = new SilentSounds();
            Arsenal = new(scene, Person.Body, _textures, _collision, 1, 4, CombatFaction2D.Player,
                new CombatSystem2D(_collision, sounds, _combatants), sounds,
                overlapsSpikes: bounds => Level.TryGetSpikeSource(bounds, out _));
            Person.AttachActions(Arsenal);
            Arsenal.DownAttackStarted += duration =>
            {
                DownAttacks++;
                AttackDuration = duration;
                _presentation.PlayDownAttack(duration);
            };
            Arsenal.MeleeAttackStarted += duration =>
            {
                NormalAttacks++;
                _presentation.PlayMeleeAttack(duration, Person.IsWallGripping);
            };
        }

        public Person2D AddEnemy(Vector2 position)
        {
            var enemy = new Person2D(_collision, Physics, _metrics, position, 4, 0, CombatFaction2D.Enemy);
            // Keep overlap targets fixed while still recording damage knockback.
            enemy.Body.MotionType = BodyMotionType2D.Static;
            _combatants.Register(enemy);
            return enemy;
        }

        public void AddSpikes() => TileMap.SetTileKind(16, 18, TileKind2D.Spikes);

        public void AddContactEnemy(Vector2 position)
        {
            var spatial = new SpatialObject2D(AxisAlignedRectangle2D.FromSize(new Vector2(24f, 40f)));
            spatial.Transform.Position = position;
            var body = Physics.AddBody(spatial, BodyMotionType2D.Static);
            body.CollisionLayer = 4;
            body.CollisionMask = 0;
            _combatants.Register(new PatrolEnemy2D(spatial, body, -100f, 100f, 1f, 5));
        }

        public void Step(PersonCommand2D command, float dt)
        {
            Person.BeginFrame(dt);
            Person.ApplyCommand(command, dt);
            Physics.Step(dt);
            Person.UpdateAfterPhysics(dt);
            _presentation.Update(dt, 0, Person, command.Movement.MoveX, false, Arsenal.IsMeleeAttackActive);
        }

        public void AssertFrame(string clip, int frame) => Assert.Same(
            _textures.Load($"characters/player-sword/animations/{clip}/frame-{frame:0000}.png"), Shader.Texture);

        public void Dispose()
        {
            _presentation.Dispose();
            _textures.Dispose();
        }
    }

    private sealed class SilentSounds : ISoundEffectSink2D
    {
        public void Play(SoundEffect2D effect) { }
    }
}
