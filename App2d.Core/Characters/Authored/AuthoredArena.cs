using App2d.Core.Collision;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Spatial;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Characters.Authored;

public readonly record struct ArenaInput(float Move = 0, bool Run = false, bool Jump = false, bool Attack = false);
public readonly record struct ArenaEvent(long Tick, int Actor, AnimationEvent Event, Vector2 Position);
public readonly record struct ArenaHit(long Tick, int Attacker, int Target, string Window, int Damage);
/// <summary>A trigger collider's contact with another actor's movement collider: once when they start to overlap and once when they stop.</summary>
public readonly record struct ArenaTrigger(long Tick, int Actor, string Collider, int Other, bool Entered);
/// <summary>A projectile in flight, in model units.</summary>
public readonly record struct ArenaBolt(int Owner, Vector2 Position, Vector2 Velocity, IConvexShape2D Shape, float Lifetime, int Damage);

/// <summary>The arena's gameplay layers: the controlled actor and everyone else, so an attack's layer mask does the ally filtering.</summary>
public static class ArenaLayers
{
    public const uint Player = 1u << 1, Others = 1u << 2;
    public static uint Of(int actor) => actor == 0 ? Player : Others;
    public static uint OpponentsOf(int actor) => actor == 0 ? Others : Player;
}

/// <summary>
/// The flat-ground entity arena over authored entities. Actor 0 is controlled; the others walk toward it and attack in
/// range. Deterministic fixed steps. Controllers own movement; each actor's <see cref="EntityAnimator"/> supplies the one
/// final pose that drawing, sockets, props and collision all read. Every actor's attached colliders (movement, hurt,
/// attack, guard, trigger) are registered once in the arena's <see cref="CollisionSystem2D"/> and synchronized from that
/// pose after every controller has stepped and before any combat query (<see cref="Step"/>). Actions an entity has not
/// enabled are rejected.
/// </summary>
public sealed class AuthoredArena
{
    public const float StepSeconds = 1 / 120f, Gravity = 20, HalfWidth = 12, HurtSeconds = .45f;

    public sealed class Actor
    {
        internal Actor(int index, ResolvedEntity entity, Vector2 position, int facing, CollisionSystem2D collision)
        {
            Index = index;
            Entity = entity;
            Animator = new(entity);
            Position = position;
            Facing = facing;
            Health = entity.Asset.Health;
            Id = EntityId2D.Create();
            Colliders = new(entity, collision, Id, ArenaLayers.Of(index), movement: true);
            Animator.Step(0, position, facing, EntityControllers.Idle, 0, false, []);
            Sync();
        }
        public int Index { get; }
        public EntityId2D Id { get; }
        public ResolvedEntity Entity { get; }
        public EntityAnimator Animator { get; }
        /// <summary>The actor's registered colliders: the one set collision overlays, combat queries and triggers read.</summary>
        public ActorColliders Colliders { get; }
        public HitLedger Ledger { get; } = new();
        public Vector2 Position { get; internal set; }
        public Vector2 Velocity { get; internal set; }
        public int Facing { get; internal set; }
        public int Health { get; internal set; }
        public bool Grounded { get; internal set; } = true;
        public bool Launched { get; internal set; }
        public float Cooldown { get; internal set; }
        public float HurtTime { get; internal set; }
        public EntityReaction Reaction { get; } = new();
        public bool Alive => Health > 0;
        public ActorPose Pose => Animator.Pose;
        /// <summary>The gameplay face input: a reaction wins over the clip's face channel while it lasts.</summary>
        public string? Expression => !Alive ? "knocked-out" : HurtTime > 0 ? "hurt" : null;
        /// <summary>The movement collider as registered.</summary>
        public EntityRegion Movement => Colliders.Movement!.Region;
        /// <summary>The hurt regions at the current pose: the registered hurt colliders, or the legacy layout's boxes for an unconverted entity.</summary>
        public List<EntityRegion> Hurt => Colliders.HasHurt ? Colliders.Hurt.Select(h => h.Region).ToList() : EntityCollision.Hurt(Entity, Pose);
        /// <summary>The attack colliders enabled by the windows active in the latest step, as registered.</summary>
        public IEnumerable<(ResolvedHit Hit, EntityRegion Region)> Attacks => Animator.ActiveHits().Select(h => (h, Colliders.Find(h.Collider.Id)!.Region));

        /// <summary>The pose/collider synchronization point for this actor: after its animator stepped, before any query.</summary>
        internal void Sync() => Colliders.Sync(Pose, Animator.ActiveHits(), Alive);
    }

    private readonly List<AnimationEvent> _scratch = [];
    private readonly List<AttachedCollider> _found = [];
    private readonly List<CollisionOverlap2D> _overlaps = [];
    private readonly Dictionary<EntityId2D, Actor> _byId = [];
    private readonly HashSet<(int Actor, string Collider, int Other)> _inside = [];
    private readonly SortedSet<int> _targets = [];

    public AuthoredArena(IEnumerable<ResolvedEntity> entities)
    {
        var list = entities.ToList();
        ArgGuard.ThrowIf(list.Count == 0, "The arena needs at least one entity.", nameof(entities));
        for (var i = 0; i < list.Count; i++)
        {
            var actor = new Actor(i, list[i], new(i == 0 ? -6 : -2 + 3.2f * i, 0), i == 0 ? 1 : -1, Collision);
            Actors.Add(actor);
            _byId[actor.Id] = actor;
        }
    }

    public List<Actor> Actors { get; } = [];
    public Actor Player => Actors[0];
    public long Tick { get; private set; }
    /// <summary>The one collision system every actor's colliders are registered in, in model units.</summary>
    public CollisionSystem2D Collision { get; } = new() { CellSize = 2 };
    public List<ArenaEvent> Events { get; } = [];
    public List<ArenaHit> Hits { get; } = [];
    public List<ArenaTrigger> Triggers { get; } = [];
    public List<ArenaBolt> Bolts { get; } = [];
    /// <summary>Broad-phase candidates the latest step's attack, bolt and trigger queries visited, before the use and owner filters.</summary>
    public int LastQueryCandidates { get; private set; }

    /// <summary>Moves an actor instantly. Its action, phase and contacts reset rather than stretching a foot across the jump.</summary>
    public void Teleport(int index, Vector2 position)
    {
        var actor = Actors[index];
        actor.Position = position;
        actor.Velocity = Vector2.Zero;
        actor.Grounded = position.Y <= 0;
        actor.Launched = false;
        actor.Animator.Reset();
        actor.Animator.Step(0, position, actor.Facing, EntityControllers.Idle, 0, false, []);
        actor.Sync();
    }

    /// <summary>
    /// One fixed step in the preserved order: every controller steps its animator, every actor's colliders are synchronized
    /// from that final pose, then attacks, bolts and triggers query the registered colliders.
    /// </summary>
    public void Step(ArenaInput input)
    {
        Tick++;
        foreach (var actor in Actors)
        {
            Control(actor, actor.Index == 0 ? input : Think(actor));
        }

        foreach (var actor in Actors)
        {
            actor.Sync();
        }

        LastQueryCandidates = 0;
        foreach (var attacker in Actors)
        {
            if (!attacker.Alive)
            {
                continue;
            }

            foreach (var hit in attacker.Animator.ActiveHits())
            {
                var collider = attacker.Colliders.Find(hit.Collider.Id)!;
                // Registered hurt geometry through the broad phase (the window's layer mask excludes allies, the owner itself is
                // skipped), then the legacy layout scan for targets whose hurt is not registered yet; each target once.
                ColliderQueries.Overlap(collider, ColliderUses.Hurt, _found, _overlaps, ArenaLayers.OpponentsOf(attacker.Index));
                LastQueryCandidates += Collision.LastBoundsQueryCandidateCount;
                _targets.Clear();
                foreach (var found in _found)
                {
                    if (_byId.TryGetValue(found.Owner, out var hurt) && hurt.Alive)
                    {
                        _targets.Add(hurt.Index);
                    }
                }
                var region = collider.Region;
                foreach (var target in Actors)
                {
                    if (target != attacker && target.Alive && !target.Colliders.HasHurt && target.Index == 0 != (attacker.Index == 0)
                        && target.Hurt.Any(h => h.Overlaps(region, Vector2.Zero, Vector2.Zero)))
                    {
                        _targets.Add(target.Index);
                    }
                }
                foreach (var index in _targets)
                {
                    if (!attacker.Ledger.TryHit(attacker.Animator.ActionSequence, hit.Window.Id, index))
                    {
                        continue;
                    }

                    Hits.Add(new(Tick, attacker.Index, index, hit.Window.Id, hit.Window.Damage));
                    Damage(Actors[index], hit.Window.Damage);
                }
            }
        }
        StepBolts();
        StepTriggers();
    }

    /// <summary>The first opposing actor whose hurt geometry a placed shape meets: registered colliders through the broad phase, or the legacy boxes.</summary>
    private Actor? HurtTarget(int owner, SpatialObject2D probe, EntityRegion region)
    {
        ColliderQueries.Overlap(Collision, probe, EntityId2D.None, ColliderUses.Hurt, _found, _overlaps, ArenaLayers.OpponentsOf(owner));
        LastQueryCandidates += Collision.LastBoundsQueryCandidateCount;
        Actor? target = null;
        foreach (var found in _found)
        {
            if (_byId.TryGetValue(found.Owner, out var hurt) && hurt.Alive && (target is null || hurt.Index < target.Index))
            {
                target = hurt;
            }
        }
        foreach (var actor in Actors)
        {
            if (actor.Alive && !actor.Colliders.HasHurt && actor.Index == 0 != (owner == 0) && (target is null || actor.Index < target.Index)
                && actor.Hurt.Any(h => h.Overlaps(region, Vector2.Zero, Vector2.Zero)))
            {
                target = actor;
            }
        }
        return target;
    }

    /// <summary>Bolts fly straight until they meet an opposing actor's hurt geometry, the ground or the end of their lifetime.</summary>
    private void StepBolts()
    {
        for (var i = Bolts.Count - 1; i >= 0; i--)
        {
            var bolt = Bolts[i];
            bolt = bolt with
            {
                Position = bolt.Position + bolt.Velocity * StepSeconds,
                Lifetime = bolt.Lifetime - StepSeconds
            };
            var box = new EntityRegion("bolt", bolt.Shape, Similarity2D.FromTranslation(bolt.Position));
            var probe = new SpatialObject2D(bolt.Shape);
            probe.Transform.Position = bolt.Position;
            var target = HurtTarget(bolt.Owner, probe, box);
            if (target is not null)
            {
                Hits.Add(new(Tick, bolt.Owner, target.Index, "bolt", bolt.Damage));
                Damage(target, bolt.Damage);
            }
            if (target is not null || bolt.Lifetime <= 0 || bolt.Position.Y < 0 || MathF.Abs(bolt.Position.X) > HalfWidth + 2)
            {
                Bolts.RemoveAt(i);
            }
            else
            {
                Bolts[i] = bolt;
            }
        }
    }

    /// <summary>Trigger colliders report when they start and stop overlapping another actor's movement collider; nothing acts on them here.</summary>
    private void StepTriggers()
    {
        foreach (var actor in Actors)
        {
            foreach (var trigger in actor.Colliders.OfUse(ColliderUses.Trigger))
            {
                if (trigger.Enabled)
                {
                    ColliderQueries.Overlap(trigger, ColliderUses.Movement, _found, _overlaps, ArenaLayers.OpponentsOf(actor.Index));
                    LastQueryCandidates += Collision.LastBoundsQueryCandidateCount;
                }
                else
                {
                    _found.Clear();
                }

                foreach (var other in Actors)
                {
                    if (other == actor)
                    {
                        continue;
                    }

                    var key = (actor.Index, trigger.Id, other.Index);
                    var overlapping = _found.Any(f => f.Owner == other.Id);
                    if (overlapping && _inside.Add(key))
                    {
                        Triggers.Add(new(Tick, actor.Index, trigger.Id, other.Index, true));
                    }
                    else if (!overlapping && _inside.Remove(key))
                    {
                        Triggers.Add(new(Tick, actor.Index, trigger.Id, other.Index, false));
                    }
                }
            }
        }
    }

    private ArenaInput Think(Actor actor)
    {
        if (!actor.Alive || !Player.Alive)
        {
            return default;
        }

        var dx = Player.Position.X - actor.Position.X;
        var config = actor.Entity.Asset.Controller;
        var direction = MathF.Sign(dx);
        if (MathF.Abs(dx) <= config.Range)
        {
            return new(direction * 1e-4f, Attack: true); // face the target, stand and attack
        }

        return MathF.Abs(dx) < 14 ? new(direction) : default;
    }

    private void Control(Actor actor, ArenaInput input)
    {
        var animator = actor.Animator;
        var config = actor.Entity.Asset.Controller;
        var spec = actor.Entity.Controller;
        actor.Cooldown = MathF.Max(0, actor.Cooldown - StepSeconds);
        actor.HurtTime = MathF.Max(0, actor.HurtTime - StepSeconds);
        var events = _scratch;
        events.Clear();
        var reaction = actor.Reaction.Role(actor.Entity, actor.Alive);
        if (!actor.Alive)
        {
            animator.Step(StepSeconds, actor.Position, actor.Facing, reaction ?? animator.Role, 0, reaction is null, events, actor.Expression);
            return;
        }
        if (actor.Reaction.Tick(StepSeconds))
        {
            actor.Cooldown = MathF.Max(actor.Cooldown, actor.Entity.Asset.Controller.Cooldown);
        }
        // Staggered: the controller neither steers nor starts actions.
        if (actor.Reaction.Staggered)
        {
            input = default;
        }

        var move = spec.Moves || input.Attack ? Math.Clamp(input.Move, -1, 1) : 0;
        // Turn before acting, so an attack starts toward where the controller is steering.
        if (animator.Action is null && MathF.Abs(move) > 0)
        {
            actor.Facing = move > 0 ? 1 : -1;
        }

        if (!spec.Moves)
        {
            move = 0;
        }

        if (animator.Action is null && actor.Grounded)
        {
            // Rejected requests (a guard asked to jump) simply do nothing: there is no accidental idle substitute.
            if (input.Attack && actor.Cooldown <= 0 && animator.TryStart(EntityControllers.Attack))
            {
                actor.Cooldown = animator.Current!.Clip.Duration + config.Cooldown;
            }
            else if (input.Jump && spec.Jumps)
            {
                animator.TryStart(EntityControllers.Jump);
            }
        }
        var action = animator.Action;
        var speed = input.Run && config.RunSpeed > 0 ? config.RunSpeed : config.WalkSpeed;
        // A whole-body attack stands still; a masked one is layered over locomotion, so the controller keeps moving.
        var standing = action == EntityControllers.Attack && animator.Current!.Mask is null || action == EntityControllers.Jump && !actor.Launched;
        var vx = standing ? 0 : MathF.Abs(move) < .01f ? 0 : move * speed;
        var velocity = new Vector2(vx, actor.Grounded ? 0 : actor.Velocity.Y - Gravity * StepSeconds);
        var before = actor.Position;
        var position = before + velocity * StepSeconds;
        position.X = Math.Clamp(position.X, -HalfWidth, HalfWidth);
        var landed = false;
        if (position.Y <= 0 && !actor.Grounded)
        {
            position.Y = 0;
            velocity.Y = 0;
            actor.Grounded = true;
            landed = true;
        }
        actor.Position = position;
        actor.Velocity = velocity;
        if (landed && action == EntityControllers.Jump)
        {
            animator.EndAction();
            actor.Launched = false;
        }

        var role = MathF.Abs(velocity.X) < 1e-3f ? EntityControllers.Idle
            : input.Run && actor.Entity.Clip(EntityControllers.Run) is not null ? EntityControllers.Run : EntityControllers.Walk;
        var hold = false;
        if (!actor.Grounded && animator.Locomoting)
        {
            // Airborne without an action: the fall role when assigned, otherwise the explicit fallback of holding the pose.
            if (actor.Entity.Clip(EntityControllers.Fall) is not null)
            {
                role = EntityControllers.Fall;
            }
            else
            {
                role = animator.Role;
                hold = true;
            }
        }
        if (actor.Reaction.Role(actor.Entity, true) is { } hit)
        {
            role = hit;
            hold = false;
        }
        var ground = actor.Grounded ? MathF.Abs(position.X - before.X) : 0;
        animator.Step(StepSeconds, position, actor.Facing, role, ground, hold, events, actor.Expression);

        foreach (var e in events)
        {
            Events.Add(new(Tick, actor.Index, e, position));
            if (e is { Kind: AnimationEvent.EventKind, Id: EntityControllers.Launch } && animator.Action == EntityControllers.Jump)
            {
                actor.Grounded = false;
                actor.Launched = true;
                actor.Velocity = new(actor.Velocity.X, config.JumpSpeed);
            }
            if (e is { Kind: AnimationEvent.EventKind, Id: EntityControllers.Fire } && animator.Current?.Projectile is { } shot)
            {
                var (muzzle, axis) = EntityCollision.Muzzle(actor.Entity, actor.Pose);
                Bolts.Add(new(actor.Index, new(muzzle.X, muzzle.Y), axis * shot.Speed, (IConvexShape2D)shot.Shape.Validated(), shot.Lifetime, shot.Damage));
            }
        }
        if (animator.ActionComplete && animator.Action != EntityControllers.Jump)
        {
            animator.EndAction();
        }
    }

    private static void Damage(Actor target, int damage)
    {
        target.Health = Math.Max(0, target.Health - damage);
        target.HurtTime = HurtSeconds;
        // A hit interrupts the target's action and staggers it; contacts are released and re-captured from the next pose.
        if (target.Alive)
        {
            target.Reaction.Hit(target.Animator);
        }
        else
        {
            target.Reaction.Die(target.Animator);
        }

        target.Launched = false;
    }
}
