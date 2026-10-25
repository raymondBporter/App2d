using App2d.CharacterSeeds.Diagnostics;
using App2d.Contracts.Combat;
using App2d.Contracts.World;
using App2d.Core;
using App2d.Core.Characters.Authored;
using App2d.Core.Collision;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Physics;
using App2d.Core.Shapes;
using App2d.Core.Spatial;
using App2d.Core.Tiles;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Enemies;
using App2d.Gameplay.Persons;
using App2d.Tests.Authored;
using System.Numerics;

namespace App2d.Tests.Gameplay.Enemies;

/// <summary>P8b in the game: an enemy's registered colliders follow the authoritative pose at the sync point after physics, and combat deduplicates by target.</summary>
public sealed class AttachedColliderSyncTests
{
    private const float Scale = GameWorldUnits2D.WorldUnitsPerAuthoredUnit;
    private static readonly AuthoredCatalog Catalog = AuthoredCatalog.Load(Path.GetFullPath(Path.Combine(TestAssetPath.Root, "..", "Characters", "authored")));

    private static ResolvedEntity Compile(EntityAsset asset) =>
        ResolvedEntity.Compile(asset, Catalog.Resolve, Catalog.Animations.GetValueOrDefault, Catalog.Props.GetValueOrDefault);

    private static void AssertSynchronized(AuthoredEntityEnemy2D enemy)
    {
        foreach (var attached in enemy.Colliders.All)
        {
            var expected = attached.Definition.Place(enemy.Pose, Scale);
            var actual = attached.Region.Pose;
            Assert.True(Vector2.Distance(expected.Translation, actual.Translation) < 1e-3f, $"{attached.Id} sits at {actual.Translation}, the pose places it at {expected.Translation}");
            Assert.True(Vector2.Distance(expected.XAxis, actual.XAxis) < 1e-4f, $"{attached.Id} axis");
        }
    }

    [Fact]
    public void PhysicsMovementNeverLeavesStaleColliderFramesPastTheSyncPoint()
    {
        var physics = new PhysicsWorld2D { Gravity = new(0, -1_900f), MaxSubstepSeconds = 1f / 120 };
        var entity = Compile(ColliderAttachmentTests.GuardWithHurtColliders());
        var enemy = new AuthoredEntityEnemy2D(EntityId2D.Create(), entity, physics, new(0, 200), 1, 4);
        enemy.SetSimulationEnabled(true);
        Assert.Equal(entity.Colliders.Count, enemy.Colliders.All.Count);
        Assert.All(enemy.Colliders.All, c => Assert.Equal(4u, c.Collider.CollisionLayer));
        AssertSynchronized(enemy);
        var head = enemy.Colliders.Find("head")!;
        var fell = 0;
        for (var tick = 0; tick < 30; tick++)
        {
            enemy.Update(1f / 120, new(400, 200));
            var before = head.Bounds.Center;
            physics.Step(1f / 120);
            // Physics moved the body; the registered collider still shows the last synchronized frame until the sync point.
            Assert.Equal(before, head.Bounds.Center);
            enemy.SyncAfterPhysics();
            AssertSynchronized(enemy);
            if (head.Bounds.Center.Y < before.Y - 1e-3f)
            {
                fell++;
            }
        }
        Assert.True(fell > 20, "the hurt collider followed the falling body step by step");
        Assert.All(enemy.Colliders.OfUse(ColliderUses.Hurt), c => Assert.True(c.Enabled));
        Assert.All(enemy.Colliders.OfUse(ColliderUses.Attack), c => Assert.False(c.Enabled));
        Assert.Contains(enemy.Colliders.OfUse(ColliderUses.Trigger), c => c.Enabled);
        // Streaming the enemy out disables every collider without removing it; streaming it back re-synchronizes them.
        enemy.SetSimulationEnabled(false);
        Assert.All(enemy.Colliders.All, c => Assert.False(c.Enabled));
        Assert.Equal(entity.Colliders.Count + 1, physics.CollisionSystem.Colliders.Count);
        enemy.SetSimulationEnabled(true);
        Assert.True(head.Enabled);
        AssertSynchronized(enemy);
    }

    [Fact]
    public void CombatFindsRegisteredHurtThroughTheBroadPhaseAndDamagesATargetOnce()
    {
        var physics = new PhysicsWorld2D { Gravity = Vector2.Zero };
        var entity = Compile(ColliderAttachmentTests.GuardWithHurtColliders());
        var enemy = new AuthoredEntityEnemy2D(EntityId2D.Create(), entity, physics, new(0, 40), 1, 4);
        enemy.SetSimulationEnabled(true);
        enemy.Update(0, new(400, 40));
        enemy.SyncAfterPhysics();
        var player = new Person2D(EntityId2D.Create(), physics.CollisionSystem, physics, TraversalMetricsLoader2D.Load(TestAssetPath.Root), new(-200, 26), 2, 1, CombatFaction2D.Player, 30);
        var registry = new CombatantRegistry2D();
        registry.Register(enemy);
        registry.Register(player);
        var combat = new CombatSystem2D(physics.CollisionSystem, registry);
        var facts = new List<CombatDamage2D>();
        combat.DamageResolved += facts.Add;
        // A box over the head and the torso at once, above the movement body's top: found by the broad phase, damaged once.
        var head = enemy.Colliders.Find("head")!;
        var torso = enemy.Colliders.Find("torso")!;
        var hit = new SpatialObject2D(AxisAlignedRectangle2D.Builder.CreateFromSize(60, 60));
        hit.Transform.Position = new(head.Bounds.Center.X, (head.Bounds.Center.Y + torso.Bounds.Max.Y) / 2);
        Assert.True(hit.WorldBounds.Intersects(head.Bounds) && hit.WorldBounds.Intersects(torso.Bounds));
        Assert.True(enemy.HurtRegistered);
        Assert.True(enemy.OverlapsHurt(hit.WorldBounds));
        var health = enemy.Health.Current;
        var contact = enemy.Colliders.HurtContact(hit.WorldBounds); // read before the damage tick moves the pose
        Assert.True(combat.ResolveAttack(hit, player.Id, 7, CombatFaction2D.Player, 4, 1, _ => new(100, 0)));
        Assert.Equal(health - 1, enemy.Health.Current);
        Assert.Equal(contact, Assert.Single(facts).Contact!.Value.Position);
        Assert.False(combat.ResolveAttack(hit, player.Id, 7, CombatFaction2D.Player, 4, 1, _ => new(100, 0)));
        Assert.Single(facts);
        // The damage tick re-synchronized the colliders onto the reaction pose; a lethal hit disables the hurt geometry.
        AssertSynchronized(enemy);
        Assert.True(combat.ResolveAttack(hit, player.Id, 8, CombatFaction2D.Player, 4, enemy.Health.Current, _ => new(100, 0)));
        Assert.False(enemy.IsAlive);
        Assert.All(enemy.Colliders.OfUse(ColliderUses.Hurt), c => Assert.False(c.Enabled));
        Assert.False(enemy.OverlapsHurt(hit.WorldBounds));
        Assert.False(combat.ResolveAttack(hit, player.Id, 9, CombatFaction2D.Player, 4, 1, _ => new(100, 0)));
        // A probe far from the body but on the head finds nothing of the terrain body and still reaches the (living) head.
        var alive = new AuthoredEntityEnemy2D(EntityId2D.Create(), entity, physics, new(300, 40), 1, 4);
        alive.SetSimulationEnabled(true);
        alive.Update(0, new(-400, 40));
        alive.SyncAfterPhysics();
        registry.Register(alive);
        var top = alive.Colliders.Find("head")!.Bounds;
        var probe = new SpatialObject2D(AxisAlignedRectangle2D.Builder.CreateFromSize(4, 4));
        probe.Transform.Position = new(top.Center.X, top.Max.Y - 2);
        Assert.False(probe.WorldBounds.Intersects(alive.WorldObject.WorldBounds), "the probe is above the movement body");
        var overlaps = new List<CollisionOverlap2D>();
        Assert.True(physics.CollisionSystem.Overlap(probe, overlaps, 4) > 0);
        Assert.True(combat.ResolveAttack(probe, player.Id, 10, CombatFaction2D.Player, 4, 1, _ => Vector2.Zero));
    }
}
