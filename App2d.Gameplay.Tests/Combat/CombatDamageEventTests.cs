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
        var enemy = new PatrolEnemy2D(shape, body, 500f, 700f, 10f, 10);
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
