using App2d.Core.Geometry.Primitives;
using App2d.CharacterSeeds;
using App2d.Core;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Collision;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Tests.Authored;

/// <summary>P9b: one contact resolution over the shared queries, deduplicated by attack instance, hit group and target, with guards blocking.</summary>
public sealed class ContactResolutionTests
{
    private static readonly AuthoredCatalog Catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);

    private static ResolvedEntity Compile(EntityAsset asset) =>
        ResolvedEntity.Compile(asset, Catalog.Resolve, Catalog.Animations.GetValueOrDefault, Catalog.Props.GetValueOrDefault);

    [Fact]
    public void ResolveReportsEachTargetOncePerInstanceAndGroupAndLetsTheGuardTakeAFrontContact()
    {
        var entity = Compile(ColliderAttachmentTests.GuardWithHurtColliders());
        var system = new CollisionSystem2D { CellSize = 2 };
        var attacker = new ActorColliders(entity, system, EntityId2D.Create(), ArenaLayers.Player, movement: true);
        var target = new ActorColliders(entity, system, EntityId2D.Create(), ArenaLayers.Others, movement: true);
        var ally = new ActorColliders(entity, system, EntityId2D.Create(), ArenaLayers.Player, movement: true);
        var animator = new EntityAnimator(entity);
        Assert.True(animator.TryStart("attack"));
        var hit = entity.Actions["attack"].Hits[0];
        while (animator.ActionTime < hit.Start + .02f)
        {
            animator.Step(1 / 120f, Vector2.Zero, 1, "idle", 0, false, []);
        }

        attacker.Sync(animator.Pose, animator.ActiveHits(), alive: true);
        var rest = PoseEvaluator.Rest(entity.Model);
        // The target stands where the spear tip is, its three hurt boxes overlapping it; the ally stands there too, on the attacker's layer.
        var tip = attacker.Find("spear-tip")!.Bounds.Center;
        target.Sync(new ActorPose(rest, new(tip.X, 0), -1), null, alive: true);
        ally.Sync(new ActorPose(rest, new(tip.X, 0), -1), null, alive: true);
        var ledger = new HitLedger();
        var contacts = new List<AcceptedContact>();
        var scratch = new List<CollisionOverlap2D>();
        (EntityId2D, bool)? TargetOf(AttachedCollider attack, Collider2D collider) =>
            collider.UserData is AttachedCollider { Use: ColliderUses.Hurt } a ? (a.Owner, false) : null;
        Assert.Equal(1, CombatContacts.Resolve(attacker, animator.ActionSequence, animator.ActiveHits(), ArenaLayers.Others, ledger, TargetOf, contacts, scratch));
        var contact = Assert.Single(contacts);
        Assert.Equal((target.Owner, "spear-tip", 1, false), (contact.Target, contact.Group, contact.Sequence, contact.Blocked));
        Assert.True(contact.Position.Y > 0.5f && contact.Position.Y < 1.6f, "the contact lies on the target's hurt geometry");
        Assert.True(attacker.Find("spear-tip")!.Bounds.Contains(contact.Position));
        // Repeated overlaps of the same instance and group: nothing more; a new group of the same instance and a new instance count again.
        Assert.Equal(0, CombatContacts.Resolve(attacker, animator.ActionSequence, animator.ActiveHits(), ArenaLayers.Others, ledger, TargetOf, contacts, scratch));
        var pulse = new ResolvedHit(new HitWindow { Id = "spear-butt" }, hit.Start, hit.Finish, hit.Collider, new StrikeDef { Damage = 3 });
        Assert.Equal(1, CombatContacts.Resolve(attacker, animator.ActionSequence, [pulse], ArenaLayers.Others, ledger, TargetOf, contacts, scratch));
        Assert.Equal(3, Assert.Single(contacts).Strike.Damage);
        Assert.Equal(1, CombatContacts.Resolve(attacker, animator.ActionSequence + 1, animator.ActiveHits(), ArenaLayers.Others, ledger, TargetOf, contacts, scratch));
        // The attacker's own hurt is never a target; the ally is excluded by the opponents' layer mask (and reachable only by asking for its layer).
        Assert.Equal(1, CombatContacts.Resolve(attacker, 9, animator.ActiveHits(), ArenaLayers.Player, ledger, TargetOf, contacts, scratch));
        Assert.Equal(ally.Owner, Assert.Single(contacts).Target);
        Assert.DoesNotContain(contacts, c => c.Target == attacker.Owner);
        // A dead target is rejected by the caller's rule; a disabled attack collider reaches nothing.
        Assert.Equal(0, CombatContacts.Resolve(attacker, 10, animator.ActiveHits(), ArenaLayers.Others, ledger, (_, c) => c.UserData is AttachedCollider { Use: ColliderUses.Hurt } ? null : null, contacts, scratch));
        attacker.Sync(animator.Pose, null, alive: true);
        Assert.Equal(0, CombatContacts.Resolve(attacker, 11, animator.ActiveHits(), ArenaLayers.Others, ledger, TargetOf, contacts, scratch));
        // The guard takes a front contact instead of the hurt geometry it covers: a blocked contact, once.
        attacker.Sync(animator.Pose, animator.ActiveHits(), alive: true);
        (EntityId2D, bool)? Guarded(AttachedCollider attack, Collider2D collider) =>
            collider.UserData is AttachedCollider { Use: ColliderUses.Hurt } a && a.Owner == target.Owner ? (a.Owner, a.Id == "torso") : null; // the torso collider stands in for a guard here
        Assert.Equal(1, CombatContacts.Resolve(attacker, 12, animator.ActiveHits(), ArenaLayers.Others, ledger, Guarded, contacts, scratch));
        Assert.True(Assert.Single(contacts).Blocked);
        Assert.Equal(0, CombatContacts.Resolve(attacker, 12, animator.ActiveHits(), ArenaLayers.Others, ledger, Guarded, contacts, scratch));
        // Extra (legacy) targets join the same ledger.
        var extra = EntityId2D.Create();
        Assert.Equal(1, CombatContacts.Resolve(attacker, 13, animator.ActiveHits(), 0u, ledger, TargetOf, contacts, scratch, (h, a, list) => list.Add((extra, a.Bounds.Center))));
        Assert.Equal(extra, Assert.Single(contacts).Target);
        Assert.Equal(0, CombatContacts.Resolve(attacker, 13, animator.ActiveHits(), 0u, ledger, TargetOf, contacts, scratch, (h, a, list) => list.Add((extra, a.Bounds.Center))));
    }

    [Fact]
    public void TheArenaBlocksWithTheGuardAndStrikesSeveralTargetsOnceEach()
    {
        // The shield defender walks into the player's thrust shield first: the shell takes it as a block (its sound, no damage) while the guard is up.
        var arena = new AuthoredArena([Catalog.Entities["player"], Catalog.Entities["shield-defender"]]);
        arena.Teleport(0, new(0, 0));
        arena.Teleport(1, new(2.2f, 0));
        var defender = arena.Actors[1];
        var healthBefore = defender.Health;
        for (var i = 0; i < 60; i++)
        {
            arena.Step(new(Attack: i == 0));
        }

        Assert.NotEmpty(arena.Blocks);
        Assert.All(arena.Blocks, b => Assert.Equal((0, 1, "spear-tip", "shield-block"), (b.Attacker, b.Target, b.Window, b.Sound)));
        Assert.Single(arena.Blocks.Where(b => b.Attacker == 0));
        Assert.DoesNotContain(arena.Hits, h => h.Attacker == 0);
        Assert.Equal(healthBefore, defender.Health);
        Assert.True(defender.GuardUp);
        // Two spear guards standing within one thrust: each takes the strike once, in index order, from the one attack instance.
        var pair = new AuthoredArena([Catalog.Entities["player"], Catalog.Entities["spear-guard"], Catalog.Entities["spear-guard"]]);
        pair.Teleport(0, new(0, 0));
        pair.Teleport(1, new(1.95f, 0));
        pair.Teleport(2, new(2.15f, 0));
        for (var i = 0; i < 80 && pair.Hits.Count(h => h.Attacker == 0) < 2; i++)
        {
            pair.Step(new(Attack: i == 0));
        }

        var struck = pair.Hits.Where(h => h.Attacker == 0).ToList();
        Assert.Equal([1, 2], struck.Select(h => h.Target));
        Assert.Equal(struck[0].Tick, struck[1].Tick);
        Assert.All(pair.Actors.Skip(1), a => Assert.Equal(Catalog.Entities["spear-guard"].Damage.Health - 1, a.Health));
    }
}
