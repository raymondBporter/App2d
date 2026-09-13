using App2d.Core;
using App2d.Core.Geometry;
using App2d.Gameplay.Audio;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Enemies;
using App2d.Physics;
using System.Numerics;
using Xunit;

namespace App2d.Gameplay.Tests.Audio;

public sealed class CombatSoundPositionTests
{
    [Theory]
    [InlineData(1, SoundEffect2D.EnemyHurt)]
    [InlineData(10, SoundEffect2D.EnemyDeath)]
    public void EnemyDamageSoundComesFromTheTarget(int damage, SoundEffect2D expected)
    {
        var physics = new PhysicsWorld2D();
        var shape = new SpatialObject2D(AxisAlignedRectangle2D.FromSize(new Vector2(30f)));
        shape.Transform.Position = new(600f, 200f);
        var body = physics.AddBody(shape, BodyMotionType2D.Dynamic);
        body.CollisionLayer = 2;
        var enemy = new PatrolEnemy2D(shape, body, 500f, 700f, 10f, 10);
        var registry = new CombatantRegistry2D();
        registry.Register(enemy);
        var sounds = new RecordingSink();
        var combat = new CombatSystem2D(physics.CollisionSystem, sounds, registry);

        Assert.True(combat.TryDamageFirst(shape, CombatFaction2D.Player, 2, damage, _ => Vector2.Zero));
        Assert.Equal((expected, shape.Transform.Position), Assert.Single(sounds.Played));
    }

    private sealed class RecordingSink : ISoundEffectSink2D
    {
        public List<(SoundEffect2D Effect, Vector2 Position)> Played { get; } = [];
        public void Play(SoundEffect2D effect) => Assert.Fail("Combat sounds must have a world position.");
        public void PlayAt(SoundEffect2D effect, Vector2 position) => Played.Add((effect, position));
    }
}
