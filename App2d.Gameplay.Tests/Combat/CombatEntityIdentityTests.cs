using App2d.Levels;
using App2d.Collision;
using App2d.Core;
using App2d.Core.Geometry;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Enemies;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Player;
using App2d.Physics;
using System.Numerics;
using Xunit;

namespace App2d.Gameplay.Tests.Combat;

public sealed class CombatEntityIdentityTests
{
    private const uint EnemyLayer = 2;
    private readonly PhysicsWorld2D _physics = new();
    private readonly CombatantRegistry2D _registry = new();

    [Fact]
    public void RuntimeIdsAreValuesAndDefaultMeansNoEntity()
    {
        var ids = Enumerable.Range(0, 100).Select(_ => EntityId2D.Create()).ToArray();

        Assert.Equal(ids.Length, ids.Distinct().Count());
        Assert.All(ids, id => Assert.True(id.IsValid));
        Assert.Equal(ids[0], new EntityId2D(ids[0].Value));
        Assert.False(default(EntityId2D).IsValid);
        Assert.Equal(default, EntityId2D.None);
        Assert.Throws<ArgumentOutOfRangeException>(() => new EntityId2D(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EntityId2D(-1));
    }

    [Fact]
    public void RegistrationResolvesValueIdentityAndRejectsDuplicates()
    {
        var enemy = CreateEnemy();
        _registry.Register(enemy);

        Assert.Equal(enemy.Id, enemy.Body.EntityId);
        Assert.Equal(enemy.Id, enemy.Body.Collider.EntityId);
        Assert.Same(enemy, _registry.Find(new EntityId2D(enemy.Id.Value)));
        Assert.Throws<InvalidOperationException>(() => _registry.Register(enemy));
        Assert.Null(_registry.Find(EntityId2D.None));
        Assert.Null(new CombatantRegistry2D().Find(enemy.Id));
    }

    [Fact]
    public void RemovedIdentityDoesNotResolveToAnotherEntity()
    {
        var first = CreateEnemy();
        _registry.Register(first);
        Assert.True(_registry.Unregister(first.Id));
        Assert.False(_registry.Unregister(first.Id));

        var second = CreateEnemy();
        _registry.Register(second);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Null(_registry.Find(first.Id));
        Assert.Same(second, _registry.Find(second.Id));
    }

    [Fact]
    public void CombatUsesRegistryAndIgnoresAnUnregisteredCollider()
    {
        var enemy = CreateEnemy();
        var combat = CreateCombat();

        Assert.False(DamageFirst(combat, enemy.WorldObject));
        _registry.Register(enemy);
        Assert.True(DamageFirst(combat, enemy.WorldObject));
        Assert.Equal(9, enemy.Health.Current);

        _registry.Unregister(enemy.Id);
        Assert.False(DamageFirst(combat, enemy.WorldObject));
        Assert.Equal(9, enemy.Health.Current);
    }

    [Fact]
    public void HitDeduplicationUsesSourceValueAndSequenceIndependently()
    {
        var enemy = CreateEnemy();
        _registry.Register(enemy);
        var combat = CreateCombat();
        var punch = EntityId2D.Create();
        var kick = EntityId2D.Create();

        Assert.True(Attack(punch, 1));
        Assert.False(Attack(new EntityId2D(punch.Value), 1));
        Assert.True(Attack(kick, 1));
        Assert.True(Attack(punch, 2));
        Assert.Equal(7, enemy.Health.Current);
        Assert.Throws<ArgumentException>(() => Attack(EntityId2D.None, 1));

        bool Attack(EntityId2D source, int sequence) => combat.ResolveAttack(
            enemy.WorldObject, source, sequence, CombatFaction2D.Player,
            EnemyLayer, 1, _ => Vector2.Zero);
    }

    [Fact]
    public void EachTargetTracksTheSameAttackIndependently()
    {
        var first = CreateEnemy();
        var second = CreateEnemy();
        _registry.Register(first);
        _registry.Register(second);
        var source = EntityId2D.Create();
        var combat = CreateCombat();

        Assert.True(combat.ResolveAttack(first.WorldObject, source, 1,
            CombatFaction2D.Player, EnemyLayer, 1, _ => Vector2.Zero));
        Assert.Equal(9, first.Health.Current);
        Assert.Equal(9, second.Health.Current);
        Assert.False(combat.ResolveAttack(first.WorldObject, source, 1,
            CombatFaction2D.Player, EnemyLayer, 1, _ => Vector2.Zero));
    }

    [Fact]
    public void ContactDamageResolvesEntityAndRespawnKeepsPlayerIdentity()
    {
        var enemy = CreateEnemy();
        var player = new Person2D(EntityId2D.Create(), _physics.CollisionSystem, _physics,
            TraversalMetricsLoader2D.Load(TestAssetPath.Root),
            Vector2.Zero, 4, 1, CombatFaction2D.Player);
        _registry.Register(player);
        var id = player.Id;
        var contacts = new ContactDamageSystem2D(_physics.CollisionSystem, EnemyLayer, _registry);

        contacts.Resolve(player);
        Assert.Equal(player.Health.Maximum, player.Health.Current);
        _registry.Register(enemy);
        contacts.Resolve(player);
        Assert.Equal(player.Health.Maximum - 1, player.Health.Current);

        player.Reset(new Vector2(300f, 120f));
        Assert.Equal(id, player.Id);
        Assert.Equal(id, player.Body.Collider.EntityId);
        Assert.Same(player, _registry.Find(id));
        Assert.Equal(player.Health.Maximum, player.Health.Current);
    }

    private PatrolEnemy2D CreateEnemy()
    {
        var shape = new SpatialObject2D(AxisAlignedRectangle2D.FromSize(new Vector2(30f)));
        var body = _physics.AddBody(shape, BodyMotionType2D.Dynamic);
        body.CollisionLayer = EnemyLayer;
        return new PatrolEnemy2D(EntityId2D.Create(), shape, body, -100f, 100f, 10f, 10);
    }

    private CombatSystem2D CreateCombat() =>
        new(_physics.CollisionSystem, _registry);

    private static bool DamageFirst(CombatSystem2D combat, SpatialObject2D hitbox) =>
        combat.TryDamageFirst(hitbox, CombatFaction2D.Player, EnemyLayer, 1, _ => Vector2.Zero);

}
