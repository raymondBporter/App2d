using App2d.Core.Characters;
using App2d.CharacterSeeds;
using App2d.Core;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Geometry;
using App2d.Core.Collision;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Mathematics;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Physics;
using App2d.Core.Shapes;
using App2d.Core.Spatial;
using System.Numerics;

namespace App2d.Tests.Authored;

/// <summary>P8a: attached colliders resolved from shared shapes and the common placement, registered once through the collision system.</summary>
public sealed class ColliderAttachmentTests
{
    private static readonly AuthoredCatalog Catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);
    private const float Dt = 1 / 120f;

    private static ResolvedEntity Compile(EntityAsset asset) =>
        ResolvedEntity.Compile(asset, Catalog.Resolve, Catalog.Animations.GetValueOrDefault, Catalog.Props.GetValueOrDefault);

    private static string Error(EntityAsset asset) => Assert.Throws<InvalidDataException>(() => Compile(asset)).Message;

    /// <summary>The spear guard with explicit version-3 hurt colliders instead of the legacy layout: a head box in the screen frame and a hips-to-chest capsule that stretches with the span.</summary>
    internal static EntityAsset GuardWithHurtColliders()
    {
        var guard = StarterContent.SpearGuardEntity();
        guard.Version = EntityAsset.CurrentVersion;
        guard.Hurt = new();
        guard.Colliders =
        [
            new() { Id = "head", Use = ColliderUses.Hurt, Control = Humanoid.Head, Frame = CharacterModel.Locomotion, Shape = Rectangle2D.Builder.FromSize(.6f, .6f) },
            new() { Id = "torso", Use = ColliderUses.Hurt, Control = Humanoid.Hips, Toward = Humanoid.Chest, Stretch = true, Shape = Capsule2D.Builder.Create(Vector2.Zero, new(0, .5f), .2f) },
            new() { Id = "legs", Use = ColliderUses.Hurt, Control = Humanoid.LeftHip, Frame = CharacterModel.Locomotion, OffsetY = -.4f, Shape = Rectangle2D.Builder.FromSize(.5f, .9f) },
            new() { Id = "reach", Use = ColliderUses.Trigger, Prop = StarterContent.Spear, Point = PropAsset.TipPoint, Shape = Circle2D.Builder.Create(.5f) },
        ];
        return guard;
    }

    [Fact]
    public void EveryLegacyWindowAndGuardRegistersAsAColliderWithAStableId()
    {
        foreach (var entity in Catalog.Entities.Values)
        {
            var windows = entity.Actions.Values.SelectMany(a => a.Hits).ToList();
            var implied = windows.Count(h => h.Window.Collider is null) + (entity.Asset.Guard is { Collider: null } ? 1 : 0);
            Assert.Equal(implied + entity.Asset.Colliders.Count, entity.Colliders.Count);
            Assert.Equal(entity.Colliders.Count, entity.Colliders.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count());
            foreach (var hit in windows)
            {
                Assert.Equal(ColliderUses.Attack, hit.Collider.Use);
                Assert.Contains(hit.Collider, entity.Colliders);
                Assert.True(hit.Collider.Sensor);
                Assert.Same(hit.Collider.Shape, hit.Shape);
            }
            Assert.Equal(entity.Asset.Guard is not null, entity.GuardCollider is not null);
            Assert.Equal(entity.Asset.Colliders.Any(c => c.Use == ColliderUses.Hurt), entity.HurtRegistered);
            Assert.Equal(entity.Asset.Hurt.Layout is null, entity.HurtRegistered || entity.Hurt.Count == 0);
        }
        // P8c converted the player, the spear guard and the shield defender; P11a the other eight: every shipped entity registers its hurt.
        Assert.All(Catalog.Entities.Values, e => Assert.True(e.HurtRegistered, e.Id));
        // A window id shared by several actions was qualified by the action when the P11a conversion declared the hero's colliders; a unique one keeps its own name.
        var hero = Catalog.Entities["hero"];
        Assert.Equal(["attack-blade", "body", "follow-up-blade", "forehand-blade", "head", "legs"], hero.Colliders.Select(c => c.Id).Order(StringComparer.Ordinal));
        Assert.All(hero.Colliders, c => Assert.Equal("collider", c.Source));
        Assert.Equal(["spear-tip", "body", "head", "legs", "reach"], Catalog.Entities["spear-guard"].Colliders.Select(c => c.Id));
        Assert.All(Catalog.Entities["spear-guard"].Colliders, c => Assert.Equal("collider", c.Source));
        var defender = Catalog.Entities["shield-defender"];
        Assert.Equal(ColliderUses.Guard, defender.GuardCollider!.Id);
        Assert.Equal(("collider", ColliderBindings.Prop, "scavenged-shell", PropAsset.TipPoint), (defender.GuardCollider.Source, defender.GuardCollider.Binding, defender.GuardCollider.Equipment!.Prop.Id, defender.GuardCollider.Definition.Point));
        // An unconverted guard would register at the prop's tip too, as the legacy guard window did.
        var brute = EntityAsset.FromJson(Catalog.Entities["maul-brute"].Asset.ToJson());
        brute.Guard = new() { Prop = "hammer" };
        Assert.Equal(("guard", PropAsset.TipPoint), (Compile(brute).GuardCollider!.Source, Compile(brute).GuardCollider!.Definition.Point));
    }

    [Theory, InlineData(1), InlineData(-1)]
    public void RegisteredCollidersAgreeWithThePlacementTipAndArtUnderMirroredAndTiltedWeapons(int facing)
    {
        // The tilted hammer at the strike (orient tracks), the thrust spear, the actor-fixed blade and the socket-anchored horns.
        foreach (var (id, action) in new[] { ("maul-brute", "attack"), ("spear-guard", "attack"), ("hero", "attack"), ("baby-triceratops", "attack"), ("stalker-pest", "attack") })
        {
            var entity = Catalog.Entities[id];
            var swing = entity.Actions[action];
            var hit = swing.Hits[0];
            var system = new CollisionSystem2D();
            var colliders = new ActorColliders(entity, system, EntityId2D.Create(), 4, scale: 1, movement: true);
            foreach (var t in new[] { hit.Start, (hit.Start + hit.Finish) / 2, hit.Finish - 1e-3f })
            {
                var pose = new ActorPose(PoseEvaluator.Sample(entity.Model, swing.Clip, t), new(3, .5f), facing);
                colliders.Sync(pose, [hit], alive: true);
                var registered = colliders.Find(hit.Collider.Id)!;
                Assert.True(registered.Enabled);
                // The registered collider (through the affine transform) is the placement value, and that is the legacy region.
                var placed = hit.Collider.Place(pose);
                var view = registered.Region;
                TestModels.Near(new(placed.Translation, 0), new(view.Pose.Translation, 0), 1e-5f, $"{id} translation at {t}");
                TestModels.Near(new(placed.XAxis, 0), new(view.Pose.XAxis, 0), 1e-5f, $"{id} axis at {t}");
                Assert.Equal(placed.IsMirrored, view.Pose.IsMirrored);
                Assert.Equal(facing < 0, placed.IsMirrored);
                var legacy = EntityCollision.Attack(entity, pose, hit);
                Assert.Equal(legacy.Pose, placed);
                Assert.True(Vector2.Distance(legacy.Bounds.Min, view.Bounds.Min) < 1e-5f && Vector2.Distance(legacy.Bounds.Max, view.Bounds.Max) < 1e-5f);
                // A prop-anchored window keeps the weapon's tip inside its registered collider: tip, collider and art agree.
                if (hit.Collider.Binding == ColliderBindings.Prop)
                {
                    var equipment = hit.Collider.Equipment!;
                    var tip = PropPlacement3D.PropPoint(pose.Socket(equipment.Socket), equipment.Prop, equipment.Prop.Tip);
                    Assert.True(registered.Object.ContainsWorldPoint(tip.XY), $"{id}: the tip {tip.XY} lies in the registered collider {view.Bounds} at {t}, facing {facing}");
                }
            }
        }
    }

    [Fact]
    public void CollidersAreRegisteredOnceMovedEveryStepAndRemovedAtDetach()
    {
        var entity = Compile(GuardWithHurtColliders());
        var system = new CollisionSystem2D();
        var id = EntityId2D.Create();
        var colliders = new ActorColliders(entity, system, id, 4, movement: true);
        Assert.Equal(entity.Colliders.Count + 1, system.Colliders.Count);
        Assert.Equal([ColliderUses.Movement, "head", "torso", "legs", "reach", "spear-tip"], colliders.All.Select(c => c.Id));
        Assert.All(colliders.All, c => Assert.Equal(id, c.Owner));
        Assert.All(colliders.All.Where(c => c.Use != ColliderUses.Movement), c => Assert.True(c.Collider.IsSensor));
        Assert.False(colliders.Movement!.Collider.IsSensor);
        Assert.Equal(4u, colliders.Find("head")!.Collider.CollisionLayer);
        var instances = colliders.All.Select(c => c.Collider).ToArray();
        var ids = instances.Select(c => c.Id).ToArray();
        var animator = new EntityAnimator(entity);
        var position = new Vector2(1, 0);
        var moved = 0;
        var before = colliders.Find("head")!.Bounds;
        for (var step = 0; step < 240; step++)
        {
            position.X += .9f * Dt;
            if (step == 10)
            {
                Assert.True(animator.TryStart("attack"));
            }

            animator.Step(Dt, position, 1, "walk", .9f * Dt, false, []);
            colliders.Sync(animator.Pose, animator.ActiveHits(), alive: true);
            Assert.Equal(instances, colliders.All.Select(c => c.Collider));
            Assert.Equal(ids, colliders.All.Select(c => c.Collider.Id));
            Assert.Equal(entity.Colliders.Count + 1, system.Colliders.Count);
            if (colliders.Find("head")!.Bounds != before)
            {
                moved++;
                before = colliders.Find("head")!.Bounds;
            }
        }
        Assert.True(moved > 200, "the head collider followed the walking pose every step");
        Assert.True(colliders.Find("torso")!.Region.Pose.Scale is > .8f and < 1.25f, "the stretched capsule scales with the hips-to-chest span");
        colliders.Detach();
        Assert.Empty(system.Colliders);
        Assert.False(colliders.Attached);
        Assert.Throws<InvalidOperationException>(() => colliders.Sync(animator.Pose, null, true));
    }

    [Fact]
    public void HurtGeometryOutsideTheMovementBodyIsFoundByTheBroadPhaseWithSelfAndAllyFiltering()
    {
        var entity = Compile(GuardWithHurtColliders());
        var system = new CollisionSystem2D { CellSize = 2 };
        var owner = EntityId2D.Create();
        var ally = EntityId2D.Create();
        var enemy = EntityId2D.Create();
        var own = new ActorColliders(entity, system, owner, 4, movement: true);
        var friend = new ActorColliders(entity, system, ally, 4, movement: true);
        var foe = new ActorColliders(entity, system, enemy, 2, movement: true);
        var rest = PoseEvaluator.Sample(entity.Model, null, 0);
        own.Sync(new ActorPose(rest, new(0, 0), 1), null, alive: true);
        friend.Sync(new ActorPose(rest, new(0, 0), 1), null, alive: true);
        foe.Sync(new ActorPose(rest, new(0, 0), -1), null, alive: true);
        // The head box sits above the movement box's top: the ordinary bounds query finds it there, by the owner's layer.
        var head = own.Find("head")!;
        Assert.True(head.Bounds.Max.Y > own.Movement!.Bounds.Max.Y - 1e-3f, "the head collider reaches past the movement body");
        var probeShape = new SpatialObject2D(AxisAlignedRectangle2D.Builder.CreateFromSize(.1f, .1f));
        probeShape.Transform.Position = new(head.Bounds.Center.X, head.Bounds.Max.Y - .05f);
        var results = new List<Collider2D>();
        Assert.Contains(head.Collider, system.QueryBounds(probeShape.WorldBounds, results, 4) > 0 ? results : []);
        Assert.Equal(0, system.QueryBounds(probeShape.WorldBounds, results, 1));
        // A query from the owner's own attack skips the owner (self), the layer mask skips the ally, the use skips the movement and trigger colliders.
        var found = new List<AttachedCollider>();
        var scratch = new List<CollisionOverlap2D>();
        ColliderQueries.Overlap(system, probeShape, owner, ColliderUses.Hurt, found, scratch, layerMask: 2);
        Assert.Equal([enemy], found.Select(f => f.Owner).Distinct());
        Assert.All(found, f => Assert.Equal(ColliderUses.Hurt, f.Use));
        ColliderQueries.Overlap(system, probeShape, owner, ColliderUses.Hurt, found, scratch, layerMask: 4);
        Assert.Equal([ally], found.Select(f => f.Owner).Distinct());
        ColliderQueries.Overlap(system, probeShape, EntityId2D.None, ColliderUses.Hurt, found, scratch);
        Assert.Equal(3, found.Select(f => f.Owner).Distinct().Count());
        // A contact position comes from the hurt colliders as registered; a dead or inactive actor exposes none.
        Assert.NotNull(own.HurtContact(probeShape.WorldBounds));
        own.Sync(new ActorPose(rest, new(0, 0), 1), null, alive: false);
        Assert.Null(own.HurtContact(probeShape.WorldBounds));
        ColliderQueries.Overlap(system, probeShape, enemy, ColliderUses.Hurt, found, scratch, layerMask: 4 | 2);
        Assert.Equal([ally], found.Select(f => f.Owner).Distinct()); // the dead owner exposes no hurt geometry; the living ally still does
        own.SetActive(false);
        own.Sync(new ActorPose(rest, new(0, 0), 1), null, alive: true);
        Assert.All(own.All, c => Assert.False(c.Enabled));
    }

    [Fact]
    public void SensorsCauseNoPushbackAndTheMovementBodyStaysStable()
    {
        var entity = Compile(GuardWithHurtColliders());
        var physics = new PhysicsWorld2D { Gravity = Vector2.Zero };
        var box = new SpatialObject2D(AxisAlignedRectangle2D.Builder.CreateFromSize(20, 60));
        box.Transform.Position = new(0, 30);
        var body = physics.AddBody(box, BodyMotionType2D.Dynamic);
        body.CollisionLayer = 2;
        body.CollisionMask = uint.MaxValue;
        body.Restitution = 0;
        var colliders = new ActorColliders(entity, physics.CollisionSystem, EntityId2D.Create(), 4, scale: 40);
        var animator = new EntityAnimator(entity);
        animator.Step(0, Vector2.Zero, 1, "idle", 0, false, []);
        colliders.Sync(animator.Pose, animator.ActiveHits(), alive: true);
        Assert.Contains(colliders.All, c => c.Enabled && c.Bounds.Intersects(box.WorldBounds));
        var pairs = 0;
        for (var i = 0; i < 20; i++)
        {
            physics.Step(Dt);
            pairs += physics.LastCandidatePairCount;
        }
        Assert.Equal(new Vector2(0, 30), box.Transform.Position);
        Assert.Equal(Vector2.Zero, body.LinearVelocity);
        Assert.Empty(physics.LastContacts);
        Assert.Equal(0, pairs); // sensors without a body never reach the physics pair list
        Assert.Equal(entity.Colliders.Count + 1, physics.CollisionSystem.Colliders.Count);
    }

    [Fact]
    public void TheArenaRegistersOneColliderSetPerActorAndQueriesThroughTheBroadPhase()
    {
        // guard-thrust/right: the converted player (movement, spear-tip, body, head, legs) and spear guard (the same plus the reach trigger), nothing re-registered while they fight.
        var arena = new AuthoredArena([Catalog.Entities["player"], Catalog.Entities["spear-guard"]]);
        arena.Teleport(0, new(0, 0));
        arena.Teleport(1, new(2.05f, 0));
        var registered = arena.Collision.Colliders.ToArray();
        Assert.Equal(11, registered.Length);
        var candidates = new List<int>();
        var idle = new List<int>();
        var enabledAttacks = 0;
        for (var step = 0; step < 360; step++)
        {
            arena.Step(default);
            Assert.Equal(registered, arena.Collision.Colliders);
            if (arena.Actors[1].Attacks.Any())
            {
                enabledAttacks++;
                candidates.Add(arena.LastQueryCandidates);
            }
            else
            {
                idle.Add(arena.LastQueryCandidates); // only the reach trigger's query runs while no window is active
            }
            Assert.All(arena.Actors, a => Assert.Equal(a.Attacks.Count(), a.Colliders.OfUse(ColliderUses.Attack).Count(c => c.Enabled)));
        }
        Assert.Equal([48L, 289L], arena.Hits.Select(h => h.Tick));
        Assert.True(enabledAttacks > 0);
        // Measured (P8c): 9 broad-phase candidates per active attack query (every collider of the pair whose bounds meet the
        // spear tip's, before the use and owner filters), 2..6 for the idle reach-trigger query, 5 trigger enter/exit facts.
        Assert.Equal((9, 9), (candidates.Min(), candidates.Max()));
        Assert.InRange(idle.Max(), 1, 11);
        Assert.Equal(5, arena.Triggers.Count);
    }

    [Fact]
    public void ExplicitCollidersValidateTheirBindingsAndTheFormatVersion()
    {
        var guard = GuardWithHurtColliders();
        var compiled = Compile(guard);
        Assert.True(compiled.HurtRegistered);
        Assert.Equal(["head", "torso", "legs", "reach", "spear-tip"], compiled.Colliders.Select(c => c.Id));
        Assert.Equal(("collider", ColliderBindings.Control, Humanoid.Hips), (compiled.Collider("torso")!.Source, compiled.Collider("torso")!.Binding, compiled.Collider("torso")!.Socket!.Control));
        Assert.True(compiled.Collider("torso")!.RestSpan > .3f);
        Assert.Equal(ColliderBindings.Prop, compiled.Collider("reach")!.Binding);
        // Round trip at version 3; the same declarations at version 2 are refused.
        var restored = EntityAsset.FromJson(guard.ToJson());
        Assert.Equal(4, restored.Colliders.Count);
        Assert.True(restored.UsesColliders);
        var downgraded = EntityAsset.FromJson(guard.ToJson());
        downgraded.Version = EntityAsset.LegacyVersion;
        Assert.Contains("need entity version 3", Assert.Throws<InvalidDataException>(downgraded.Validate).Message);
        var legacy = StarterContent.SpearGuardEntity();
        legacy.Behaviors = null;
        legacy.Version = EntityAsset.LegacyVersion;
        legacy.Validate();
        Assert.False(legacy.UsesColliders);
        // A window names an attack collider; the legacy inline geometry is then unused.
        var named = GuardWithHurtColliders();
        named.Colliders.Add(new() { Id = "point", Use = ColliderUses.Attack, Prop = StarterContent.Spear, Along = -.14f, Shape = Rectangle2D.Builder.FromSize(.42f, .26f) });
        named.Actions[0].Hits[0].Collider = "point";
        named.Actions[0].Hits[0].Prop = null;
        var pointed = Compile(named);
        Assert.Same(pointed.Collider("point"), pointed.Actions["attack"].Hits[0].Collider);
        Assert.Equal(5, pointed.Colliders.Count);
        // Refusals by name.
        string Refused(Action<EntityAsset> change)
        {
            var broken = GuardWithHurtColliders();
            change(broken);
            try
            {
                broken.Validate();
            }
            catch (InvalidDataException ex)
            {
                return ex.Message;
            }

            return Error(broken);
        }
        Assert.Contains("duplicate collider 'head'", Refused(e => e.Colliders.Add(new() { Id = "head", Use = ColliderUses.Hurt, Control = Humanoid.Head })));
        Assert.Contains("has no control 'tail'", Refused(e => e.Colliders[0].Control = "tail"));
        Assert.Contains("has no control 'tail'", Refused(e => e.Colliders[1].Toward = "tail"));
        Assert.Contains("has no socket 'ear'", Refused(e => { e.Colliders[0].Control = null; e.Colliders[0].Frame = null; e.Colliders[0].Socket = "ear"; }));
        Assert.Contains("prop 'hammer' is not equipped", Refused(e => e.Colliders[3].Prop = "hammer"));
        Assert.Contains("has no 'muzzle' point", Refused(e => e.Colliders[3].Point = PropAsset.MuzzlePoint));
        Assert.Contains("stretch needs a toward control", Refused(e => { e.Colliders[0].Stretch = true; }));
        Assert.Contains("not several", Refused(e => e.Colliders[3].Control = Humanoid.Head));
        Assert.Contains("either the legacy layout", Refused(e => e.Hurt.Layout = "standard"));
        Assert.Contains("no collider 'blade'", Refused(e => e.Actions[0].Hits[0].Collider = "blade"));
        Assert.Contains("is a hurt collider, not attack", Refused(e => e.Actions[0].Hits[0].Collider = "head"));
        Assert.Contains("use", Refused(e => e.Colliders[0].Use = "poke"));
        Assert.Contains("would register as collider 'spear-tip'", Refused(e => e.Colliders.Add(new() { Id = "spear-tip", Use = ColliderUses.Trigger, Control = Humanoid.Head })));
    }
}
