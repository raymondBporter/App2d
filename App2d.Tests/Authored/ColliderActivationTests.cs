using App2d.CharacterSeeds;
using App2d.Core;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Collision;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Tests.Authored;

/// <summary>P8b: colliders follow the authoritative fixed-step pose, activation windows are the enabled-state channel, hits deduplicate by attack instance, hit group and target.</summary>
public sealed class ColliderActivationTests
{
    private static readonly AuthoredCatalog Catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);
    private const float Dt = AuthoredArena.StepSeconds;

    private static ResolvedEntity Compile(EntityAsset asset) =>
        ResolvedEntity.Compile(asset, Catalog.Resolve, Catalog.Animations.GetValueOrDefault, Catalog.Props.GetValueOrDefault);

    /// <summary>The spear guard whose thrust is active over a normalized interval of the 0.9 s clip, instead of the strike..recover markers.</summary>
    private static EntityAsset GuardWithWindow(float startSeconds, float finishSeconds, string? id = null)
    {
        var guard = StarterContent.SpearGuardEntity();
        guard.Id = id ?? guard.Id;
        guard.Actions[0].Hits[0].Start = new() { At = startSeconds / .9f };
        guard.Actions[0].Hits[0].Finish = new() { At = finishSeconds / .9f };
        return guard;
    }

    private static AuthoredArena GuardThrust(ResolvedEntity player, ResolvedEntity guard, float distance = 2.05f)
    {
        var arena = new AuthoredArena([player, guard]);
        arena.Teleport(0, new(0, 0));
        arena.Teleport(1, new(distance, 0));
        return arena;
    }

    [Fact]
    public void AWindowCrossedInsideOneStepEnablesItsColliderForThatStepOnly()
    {
        // 1.5 ms of activation inside the 8.3 ms step that starts at 0.4 s: the collider is enabled for the step that crosses it and never afterwards.
        var guard = Compile(GuardWithWindow(.4001f, .4016f));
        var hit = guard.Actions["attack"].Hits[0];
        var animator = new EntityAnimator(guard);
        var system = new CollisionSystem2D();
        var colliders = new ActorColliders(guard, system, EntityId2D.Create(), 4);
        var attack = colliders.Find(hit.Collider.Id)!;
        Assert.True(animator.TryStart("attack"));
        var enabledSteps = new List<double>();
        while (!animator.ActionComplete)
        {
            animator.Step(Dt, Vector2.Zero, 1, "idle", 0, false, []);
            colliders.Sync(animator.Pose, animator.ActiveHits(), alive: true);
            if (attack.Enabled)
            {
                enabledSteps.Add(animator.ActionTime);
            }
        }
        var only = Assert.Single(enabledSteps);
        Assert.True(only >= hit.Finish, "the step that crossed the whole interval ended past its finish and still counted");
        // Half-open: a step ending exactly on the finish is active; the next one, starting there, is not.
        var exact = new EntityAnimator(guard);
        exact.TryStart("attack");
        exact.Step(hit.Start, Vector2.Zero, 1, "idle", 0, false, []);
        Assert.Single(exact.ActiveHits());
        exact.Step(hit.Finish - hit.Start, Vector2.Zero, 1, "idle", 0, false, []);
        Assert.Single(exact.ActiveHits());
        Assert.Equal(hit.Finish, exact.ActionTime, 6);
        exact.Step(Dt, Vector2.Zero, 1, "idle", 0, false, []);
        Assert.Empty(exact.ActiveHits());
        colliders.Sync(exact.Pose, exact.ActiveHits(), alive: true);
        Assert.False(attack.Enabled);
    }

    [Fact]
    public void AShortWindowIsNotLostInTheArenaAndLandsOncePerThrust()
    {
        var reference = GuardThrust(Catalog.Entities["player"], Catalog.Entities["spear-guard"]);
        for (var i = 0; i < 360; i++)
        {
            reference.Step(default);
        }

        Assert.Equal([48L, 289L], reference.Hits.Select(h => h.Tick));
        // The same fight with the thrust active for 1 ms at the strike: each thrust still lands exactly once, on the strike tick.
        var brief = GuardThrust(Catalog.Entities["player"], Compile(GuardWithWindow(.4f, .401f)));
        var enabledTicks = new List<long>();
        for (var i = 0; i < 360; i++)
        {
            brief.Step(default);
            if (brief.Actors[1].Colliders.OfUse(ColliderUses.Attack).Any(c => c.Enabled))
            {
                enabledTicks.Add(brief.Tick);
            }
        }
        var strikes = brief.Events.Where(e => e.Actor == 1 && e.Event.Id == "strike").Select(e => e.Tick).ToList();
        Assert.Equal(2, strikes.Count);
        Assert.Equal(strikes, brief.Hits.Select(h => h.Tick));
        // The 1 ms window straddles the boundary between the strike step and the next, so the collider is enabled in both and the hit lands once.
        Assert.Equal(strikes.SelectMany(t => new[] { t, t + 1 }), enabledTicks);
        Assert.All(brief.Hits, h => Assert.Equal((1, 0, "spear-tip", 1), (h.Attacker, h.Target, h.Window, h.Damage)));
    }

    [Fact]
    public void OneAttackDamagesATargetOncePerWindowWhateverItsHurtColliderCountAndOncePerPulse()
    {
        // The player with three overlapping hurt colliders against the guard: one hit per thrust, not one per collider.
        var player = StarterContent.PlayerEntity();
        player.Version = EntityAsset.CurrentVersion;
        player.Hurt = new();
        player.Colliders.RemoveAll(c => c.Use == ColliderUses.Hurt);
        player.Colliders.AddRange(
        [
            new() { Id = "torso-a", Use = ColliderUses.Hurt, Control = Humanoid.Hips, Frame = CharacterModel.Locomotion, OffsetY = .5f, Shape = Rectangle2D.Builder.FromSize(.6f, 1.2f) },
            new() { Id = "torso-b", Use = ColliderUses.Hurt, Control = Humanoid.Chest, Frame = CharacterModel.Locomotion, Shape = Rectangle2D.Builder.FromSize(.7f, .8f) },
            new() { Id = "head", Use = ColliderUses.Hurt, Control = Humanoid.Head, Frame = CharacterModel.Locomotion, Shape = Circle2D.Builder.Create(.45f) },
        ]);
        var target = Compile(player);
        Assert.True(target.HurtRegistered);
        var arena = GuardThrust(target, Catalog.Entities["spear-guard"]);
        for (var i = 0; i < 360; i++)
        {
            arena.Step(default);
        }

        var strikes = arena.Events.Count(e => e.Actor == 1 && e.Event.Id == "strike");
        Assert.Equal(2, strikes);
        Assert.Equal(strikes, arena.Hits.Count);
        Assert.All(arena.Hits, h => Assert.Equal("spear-tip", h.Window));
        Assert.Equal(target.Damage.Health - strikes, arena.Player.Health);
        // Two pulses on one explicit collider are two hit groups: each lands once per thrust.
        var pulsed = StarterContent.SpearGuardEntity();
        pulsed.Version = EntityAsset.CurrentVersion;
        pulsed.Behaviors!.Strikes.Remove("spear-tip"); // the renamed windows strike with the typed defaults
        pulsed.Colliders.RemoveAll(c => c.Use == ColliderUses.Attack);
        pulsed.Colliders.Add(new() { Id = "point", Use = ColliderUses.Attack, Prop = StarterContent.Spear, Along = -.14f, Shape = Rectangle2D.Builder.FromSize(.42f, .26f) });
        var window = pulsed.Actions[0].Hits[0];
        pulsed.Actions[0].Hits =
        [
            new() { Id = "first", Collider = "point", Start = window.Start, Finish = new() { At = .5f / .9f } },
            new() { Id = "second", Collider = "point", Start = new() { At = .5f / .9f }, Finish = window.Finish },
        ];
        var pulses = GuardThrust(target, Compile(pulsed));
        for (var i = 0; i < 360; i++)
        {
            pulses.Step(default);
        }

        Assert.Equal(2, pulses.Events.Count(e => e.Actor == 1 && e.Event.Id == "strike"));
        Assert.Equal(["first", "second", "first", "second"], pulses.Hits.Select(h => h.Window));
        Assert.Equal(target.Damage.Health - 4, pulses.Player.Health);
    }

    [Fact]
    public void QueriesRespectBothMasksLikePairCollectionAndNeverTheOwner()
    {
        var guard = ColliderAttachmentTests.GuardWithHurtColliders();
        guard.Colliders.First(c => c.Id == "head").Mask = ArenaLayers.Others; // only another of the guard's own faction may reach the head
        var entity = Compile(guard);
        var system = new CollisionSystem2D { CellSize = 2 };
        var player = new ActorColliders(entity, system, EntityId2D.Create(), ArenaLayers.Player, movement: true);
        var other = new ActorColliders(entity, system, EntityId2D.Create(), ArenaLayers.Others, movement: true);
        var rest = PoseEvaluator.Sample(entity.Model, null, 0);
        var pose = new ActorPose(rest, Vector2.Zero, 1);
        player.Sync(pose, null, alive: true);
        other.Sync(pose, null, alive: true);
        var found = new List<AttachedCollider>();
        var scratch = new List<CollisionOverlap2D>();
        // A box over the whole figure: from the player it reaches the other's torso and legs but not the masked head; from the same faction it reaches the head too.
        var reach = player.Find("reach")!;
        var probe = new Core.Spatial.SpatialObject2D(AxisAlignedRectangle2D.Builder.CreateFromSize(1.5f, 2.5f, new(0, 1.1f)));
        ColliderQueries.Overlap(system, probe, player.Owner, ColliderUses.Hurt, found, scratch, ArenaLayers.Others, queryLayer: ArenaLayers.Player);
        Assert.Equal(["legs", "torso"], found.Where(f => f.Owner == other.Owner).Select(f => f.Id).Order());
        ColliderQueries.Overlap(system, probe, player.Owner, ColliderUses.Hurt, found, scratch, ArenaLayers.Others, queryLayer: ArenaLayers.Others);
        Assert.Equal(["head", "legs", "torso"], found.Where(f => f.Owner == other.Owner).Select(f => f.Id).Order());
        // The pair rule says the same about the same two colliders.
        Assert.False(player.Find("reach")!.Collider.CanCollideWith(other.Find("head")!.Collider));
        Assert.True(other.Find("reach")!.Collider.CanCollideWith(other.Find("head")!.Collider));
        Assert.Equal(0, ColliderQueries.Overlap(reach, ColliderUses.Hurt, found, scratch, ArenaLayers.Player));
        // A bounds query declaring its layer is two-sided; an undeclared one keeps the one-sided legacy behaviour.
        var results = new List<Collider2D>();
        Assert.Contains(other.Find("head")!.Collider, system.QueryBounds(probe.WorldBounds, results, ArenaLayers.Others) > 0 ? results : []);
        system.QueryBounds(probe.WorldBounds, results, ArenaLayers.Others, queryLayer: ArenaLayers.Player);
        Assert.DoesNotContain(other.Find("head")!.Collider, results);
    }

    [Fact]
    public void TriggersReportEnterAndExitAgainstMovementCollidersWithoutChangingTheFight()
    {
        var withReach = Compile(ColliderAttachmentTests.GuardWithHurtColliders());
        var arena = GuardThrust(Catalog.Entities["player"], withReach, 3.2f);
        for (var i = 0; i < 360; i++)
        {
            arena.Step(default);
        }

        Assert.NotEmpty(arena.Triggers);
        Assert.All(arena.Triggers, t => Assert.Equal((1, "reach", 0), (t.Actor, t.Collider, t.Other)));
        Assert.True(arena.Triggers[0].Entered);
        for (var i = 1; i < arena.Triggers.Count; i++)
        {
            Assert.NotEqual(arena.Triggers[i - 1].Entered, arena.Triggers[i].Entered);
            Assert.True(arena.Triggers[i].Tick > arena.Triggers[i - 1].Tick);
        }
        // The same fight without the trigger produces the same hits and events: triggers are facts, not behaviour.
        var plain = ColliderAttachmentTests.GuardWithHurtColliders();
        plain.Colliders.RemoveAll(c => c.Use == ColliderUses.Trigger);
        var control = GuardThrust(Catalog.Entities["player"], Compile(plain), 3.2f);
        for (var i = 0; i < 360; i++)
        {
            control.Step(default);
        }

        Assert.Equal(control.Hits, arena.Hits);
        Assert.Equal(control.Events.Select(e => (e.Tick, e.Actor, e.Event.Id)), arena.Events.Select(e => (e.Tick, e.Actor, e.Event.Id)));
        Assert.Empty(control.Triggers);
    }
}
