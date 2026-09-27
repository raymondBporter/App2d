using App2d.Core;
using App2d.Core.Geometry;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Enemies;
using App2d.Physics;
using System.Numerics;
using Xunit;

namespace App2d.Gameplay.Tests.Combat;

public sealed class CombatDamageEventTests
{
    [Fact]
    public void SwordContactCarriesItsWielderWithoutChangingWeaponHitDeduplication()
    {
        var physics = new PhysicsWorld2D();
        var metrics = App2d.Levels.TraversalMetricsLoader2D.Load(TestAssetPath.Root);
        var target = new App2d.Gameplay.Persons.Person2D(EntityId2D.Create(), physics.CollisionSystem, physics,
            metrics, Vector2.Zero, 2, 1, CombatFaction2D.Enemy);
        var registry = new CombatantRegistry2D(); registry.Register(target);
        var combat = new CombatSystem2D(physics.CollisionSystem, registry);
        var facts = new List<CombatDamage2D>(); combat.DamageResolved += facts.Add;
        var weapon = EntityId2D.Create(); var player = EntityId2D.Create();
        Assert.True(combat.ResolveAttack(target.WorldObject, weapon, 1, CombatFaction2D.Player,
            2, 1, _ => Vector2.Zero, impactKind: CombatImpactKind2D.Sword, attackerId: player));
        var contact = Assert.Single(facts).Contact!.Value;
        Assert.Equal(player, contact.AttackerId); Assert.Equal(weapon, contact.SourceId);
        Assert.False(combat.ResolveAttack(target.WorldObject, weapon, 1, CombatFaction2D.Player,
            2, 1, _ => Vector2.Zero, impactKind: CombatImpactKind2D.Sword, attackerId: player));
        Assert.Single(facts);
    }

    [Fact]
    public void InvulnerableContactStillAllowsBounceButEmitsNoDamageFeedback()
    {
        var physics = new PhysicsWorld2D();
        var metrics = App2d.Levels.TraversalMetricsLoader2D.Load(TestAssetPath.Root);
        var target = new App2d.Gameplay.Persons.Person2D(EntityId2D.Create(), physics.CollisionSystem, physics,
            metrics, Vector2.Zero, 2, 1, CombatFaction2D.Enemy);
        Assert.True(target.TakeDamage(1, Vector2.Zero));
        var health = target.Health.Current;
        var registry = new CombatantRegistry2D(); registry.Register(target);
        var combat = new CombatSystem2D(physics.CollisionSystem, registry);
        var facts = new List<CombatDamage2D>(); combat.DamageResolved += facts.Add;
        Assert.True(combat.ResolveAttack(target.WorldObject, EntityId2D.Create(), 1, CombatFaction2D.Player,
            2, 1, _ => new(0, -200), impactKind: CombatImpactKind2D.Sword));
        Assert.Equal(health, target.Health.Current);
        Assert.Empty(facts);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(10, true)]
    public void DamageFactIdentifiesTheTargetAndImpactPosition(int damage, bool killed)
    {
        var physics = new PhysicsWorld2D();
        var shape = new SpatialObject2D(AxisAlignedRectangle2D.FromSize(new Vector2(30f)));
        shape.Transform.Position = new(600f, 200f);
        var body = physics.AddBody(shape, BodyMotionType2D.Dynamic);
        body.CollisionLayer = 2;
        var enemy = new PatrolEnemy2D(EntityId2D.Create(), shape, body, 500f, 700f, 10f, 10);
        var registry = new CombatantRegistry2D();
        registry.Register(enemy);
        var events = new List<CombatDamage2D>();
        var combat = new CombatSystem2D(physics.CollisionSystem, registry);

        combat.DamageResolved += events.Add;

        Assert.True(combat.TryDamageFirst(shape, CombatFaction2D.Player, 2, damage, _ => Vector2.Zero));
        Assert.Equal(new CombatDamage2D(enemy.Id, CombatFaction2D.Enemy, shape.Transform.Position, killed), Assert.Single(events));
        Assert.Equal(killed ? 1 : 0, combat.DefeatedEnemies);
    }

}
