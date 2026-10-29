using App2d.CharacterSeeds.Diagnostics;
using App2d.Contracts.Combat;
using App2d.Contracts.Enemies;
using App2d.Core;
using App2d.Core.Characters.Authored;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Physics;
using App2d.Core.Shapes;
using App2d.Core.Spatial;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Enemies;
using App2d.Gameplay.Persons;
using System.Numerics;

namespace App2d.Tests.Gameplay.Combat;

/// <summary>P9b in the game: hit groups in the dedupe key, accepted contacts as the only facts, blocks and invulnerability producing no damage.</summary>
public sealed class CombatContactTests
{
    private static readonly AuthoredCatalog Catalog = AuthoredCatalog.Load(Path.GetFullPath(Path.Combine(TestAssetPath.Root, "..", "Characters", "authored")));

    private static (AuthoredEntityEnemy2D Enemy, Person2D Player, CombatSystem2D Combat, PhysicsWorld2D Physics) Create(string entityId, Vector2 at)
    {
        var physics = new PhysicsWorld2D { Gravity = Vector2.Zero };
        var enemy = new AuthoredEntityEnemy2D(EntityId2D.Create(), Catalog.Entities[entityId], physics, at, 1, 4);
        enemy.SetSimulationEnabled(true);
        enemy.Update(0, new(-400, at.Y));
        enemy.SyncAfterPhysics();
        var player = new Person2D(EntityId2D.Create(), physics.CollisionSystem, physics, TraversalMetricsLoader2D.Load(TestAssetPath.Root), new(-90, 26), 2, 1, CombatFaction2D.Player, 30);
        var registry = new CombatantRegistry2D();
        registry.Register(enemy);
        registry.Register(player);
        return (enemy, player, new CombatSystem2D(physics.CollisionSystem, registry), physics);
    }

    private static SpatialObject2D Box(Vector2 position, float size = 6)
    {
        var box = new SpatialObject2D(AxisAlignedRectangle2D.Builder.CreateFromSize(new Vector2(size)));
        box.Transform.Position = position;
        return box;
    }

    [Fact]
    public void HitGroupsOfOneAttackInstanceEachLandOnceAndTheContactComesFromTheRegisteredHurtCollider()
    {
        var (enemy, player, combat, _) = Create("spear-guard", new(0, 40));
        var facts = new List<CombatDamage2D>();
        combat.DamageResolved += facts.Add;
        var head = enemy.Colliders.Find("head")!;
        var headBounds = head.Bounds; // before the reaction pose moves it
        var box = Box(headBounds.Center);
        Assert.True(combat.ResolveAttack(box, player.Id, 5, CombatFaction2D.Player, 4, 1, _ => Vector2.Zero, hitGroup: "blade"));
        Assert.False(combat.ResolveAttack(box, player.Id, 5, CombatFaction2D.Player, 4, 1, _ => Vector2.Zero, hitGroup: "blade")); // repeated overlap of the same group
        Assert.True(combat.ResolveAttack(box, player.Id, 5, CombatFaction2D.Player, 4, 1, _ => Vector2.Zero, hitGroup: "pommel")); // an intended second pulse
        Assert.False(combat.ResolveAttack(box, player.Id, 5, CombatFaction2D.Player, 4, 1, _ => Vector2.Zero, hitGroup: "pommel"));
        Assert.True(combat.ResolveAttack(box, player.Id, 6, CombatFaction2D.Player, 4, 1, _ => Vector2.Zero, hitGroup: "blade")); // the next instance: the third hit kills the guard (health 3)
        Assert.Equal(3, facts.Count);
        Assert.Equal(Catalog.Entities["spear-guard"].Damage.Health - 3, enemy.Health.Current);
        Assert.False(enemy.IsAlive);
        Assert.True(facts[2].WasKilled);
        // The contact position is the overlap of the attack with the head collider, not the body's centre.
        var expected = (Vector2.Max(box.WorldBounds.Min, headBounds.Min) + Vector2.Min(box.WorldBounds.Max, headBounds.Max)) / 2;
        Assert.True(Vector2.Distance(expected, facts[0].Contact!.Value.Position) < 1e-3f, $"contact {facts[0].Contact!.Value.Position} expected {expected}");
        Assert.True(headBounds.Max.Y > enemy.WorldObject.WorldBounds.Max.Y - 1e-3f, "the head sits above the terrain body");
        // A box above the terrain body that meets only the dead enemy's hurt geometry produces nothing: a rejected contact.
        Assert.False(combat.ResolveAttack(box, player.Id, 8, CombatFaction2D.Player, 4, 1, _ => Vector2.Zero, hitGroup: "blade"));
        Assert.Equal(3, facts.Count);
    }

    [Fact]
    public void ABlockedStrikeProducesTheBlockCueAndNoDamageFactAndAnInvulnerablePlayerTakesNothing()
    {
        var (defender, player, combat, _) = Create("shield-defender", new(0, 40));
        var facts = new List<CombatDamage2D>();
        combat.DamageResolved += facts.Add;
        var guard = defender.Colliders.Find("guard")!;
        Assert.True(guard.Enabled, "the shield is up while idle");
        var box = Box(guard.Bounds.Center, 8);
        var health = defender.Health.Current;
        Assert.True(combat.ResolveAttack(box, player.Id, 1, CombatFaction2D.Player, 4, 2, _ => new(-100, 0), attackerId: player.Id, hitGroup: "blade"));
        Assert.Empty(facts);
        Assert.Equal(health, defender.Health.Current);
        Assert.Single(defender.DrainEvents().OfType<EntityCue2D>(), e => e.Cue == Catalog.Entities["shield-defender"].GuardBehavior!.BlockSound);
        Assert.False(combat.ResolveAttack(box, player.Id, 1, CombatFaction2D.Player, 4, 2, _ => new(-100, 0), attackerId: player.Id, hitGroup: "blade"));
        Assert.Empty(defender.DrainEvents()); // a repeated overlap of the blocked group produces nothing more
        // The enemy's own strike on an invulnerable player is a rejected contact: no damage, no cue.
        player.TakeDamage(1, Vector2.Zero);
        Assert.True(player.InvulnerabilitySeconds > 0);
        var before = player.Health.Current;
        var attacker = defender;
        attacker.DrainEvents();
        for (var i = 0; i < 90; i++)
        {
            attacker.Update(1f / 120, player.Position);
            attacker.SyncAfterPhysics();
            attacker.TryResolvePlayerHit(player);
        }
        Assert.Equal(before, player.Health.Current);
        Assert.DoesNotContain(attacker.DrainEvents().OfType<EntityCue2D>(), e => e.Cue == "hit");
    }
}
