using App2d.Core.Collision;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Spatial;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Characters.Authored;

public readonly record struct ArenaInput(float Move = 0, bool Run = false, bool Jump = false, bool Attack = false);
public readonly record struct ArenaEvent(long Tick, int Actor, AnimationEvent Event, Vector2 Position);
public readonly record struct ArenaHit(long Tick, int Attacker, int Target, string Window, int Damage);
/// <summary>An accepted contact the target's guard took instead of its hurt geometry: no damage, the guard's block sound.</summary>
public readonly record struct ArenaBlock(long Tick, int Attacker, int Target, string Window, string? Sound);
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
    public const float StepSeconds = 1 / 120f, Gravity = 20, HalfWidth = 12;

    public sealed class Actor
    {
        internal Actor(int index, ResolvedEntity entity, Vector2 position, int facing, CollisionSystem2D collision)
        {
            Index = index;
            Entity = entity;
            Animator = new(entity);
            Position = position;
            Facing = facing;
            Health = entity.Damage.Health;
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

        /// <summary>Whether the guard is up: alive, not staggered and the action has not passed the guard's open event.</summary>
        public bool GuardUp => Alive && !Reaction.Staggered && Entity.GuardBehavior is { } guard && !(Animator.Current?.Events.FirstOrDefault(e => e.Event.Id == guard.OpenEvent) is { } open && Animator.ActionTime >= open.Seconds);

        /// <summary>The pose/collider synchronization point for this actor: after its animator stepped, before any query.</summary>
        internal void Sync() => Colliders.Sync(Pose, Animator.ActiveHits(), Alive, GuardUp);
    }

    private readonly List<AnimationEvent> _scratch = [];
    private readonly List<AttachedCollider> _found = [];
    private readonly List<CollisionOverlap2D> _overlaps = [];
    private readonly List<AcceptedContact> _contacts = [];
    private readonly Dictionary<EntityId2D, Actor> _byId = [];
    private readonly HashSet<(int Actor, string Collider, int Other)> _inside = [];

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
    public List<ArenaBlock> Blocks { get; } = [];
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

            // The shared resolution: registered hurt and guard colliders through the broad phase (the opponents' layer mask does
            // the ally filtering, the owner is skipped, a front guard blocks), the legacy layout scan only for targets whose
            // hurt is not registered yet, each target once per attack instance and hit group.
            CombatContacts.Resolve(attacker.Colliders, attacker.Animator.ActionSequence, attacker.Animator.ActiveHits(), ArenaLayers.OpponentsOf(attacker.Index), attacker.Ledger,
                (attack, collider) => TargetOf(attacker.Position.X, collider), _contacts, _overlaps, (hit, attack, extra) =>
                {
                    LastQueryCandidates += Collision.LastBoundsQueryCandidateCount;
                    var region = attack.Region;
                    foreach (var target in Actors)
                    {
                        if (target != attacker && target.Alive && !target.Colliders.HasHurt && target.Index == 0 != (attacker.Index == 0)
                            && target.Hurt.FirstOrDefault(h => h.Overlaps(region, Vector2.Zero, Vector2.Zero)) is { } legacy)
                        {
                            extra.Add((target.Id, LegacyContact(region.Bounds, legacy.Bounds)));
                        }
                    }
                });
            foreach (var contact in _contacts.OrderBy(c => _byId[c.Target].Index))
            {
                var target = _byId[contact.Target];
                if (contact.Blocked)
                {
                    Blocks.Add(new(Tick, attacker.Index, target.Index, contact.Group, target.Entity.GuardBehavior?.BlockSound));
                    continue;
                }

                Hits.Add(new(Tick, attacker.Index, target.Index, contact.Group, contact.Strike.Damage));
                Damage(target, contact.Strike.Damage);
            }
        }
        StepBolts();
        StepTriggers();
    }

    /// <summary>A live opponent's hurt collider, or its enabled guard when the attacker stands in front of it; nothing for anything else.</summary>
    private (EntityId2D Target, bool Guard)? TargetOf(float attackerX, Collider2D collider)
    {
        if (collider.UserData is not AttachedCollider attached || !_byId.TryGetValue(attached.Owner, out var target) || !target.Alive)
        {
            return null;
        }

        return attached.Use switch
        {
            ColliderUses.Hurt => (target.Id, false),
            ColliderUses.Guard when CombatContacts.FromFront(attackerX, target.Position.X, target.Facing) => (target.Id, true),
            _ => null,
        };
    }

    private static Vector2 LegacyContact(Rect2D attack, Rect2D hurt) => (Vector2.Max(attack.Min, hurt.Min) + Vector2.Min(attack.Max, hurt.Max)) / 2;

    /// <summary>The first opposing actor whose hurt geometry (or front guard) a placed shape meets: registered colliders through the broad phase, or the legacy boxes.</summary>
    private (Actor Target, bool Blocked)? HurtTarget(int owner, SpatialObject2D probe, EntityRegion region)
    {
        var found = CombatContacts.Probe(Collision, probe, ArenaLayers.OpponentsOf(owner), ArenaLayers.Of(owner), c => TargetOf(Actors[owner].Position.X, c), _overlaps, id => _byId[id].Index);
        LastQueryCandidates += Collision.LastBoundsQueryCandidateCount;
        (Actor Target, bool Blocked)? target = found is { } f ? (_byId[f.Target], f.Blocked) : null;
        foreach (var actor in Actors)
        {
            if (actor.Alive && !actor.Colliders.HasHurt && actor.Index == 0 != (owner == 0) && (target is null || actor.Index < target.Value.Target.Index)
                && actor.Hurt.Any(h => h.Overlaps(region, Vector2.Zero, Vector2.Zero)))
            {
                target = (actor, false);
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
            if (target is { Blocked: true } blocked)
            {
                Blocks.Add(new(Tick, bolt.Owner, blocked.Target.Index, "bolt", blocked.Target.Entity.GuardBehavior?.BlockSound));
            }
            else if (target is { } struck)
            {
                Hits.Add(new(Tick, bolt.Owner, struck.Target.Index, "bolt", bolt.Damage));
                Damage(struck.Target, bolt.Damage);
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
                Bolts.Add(new(actor.Index, new(muzzle.X, muzzle.Y), axis * shot.Speed, (IConvexShape2D)shot.Shape.Validated(), shot.Lifetime, animator.Current.ProjectileStrike!.Damage));
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
        target.HurtTime = target.Entity.Damage.HurtSeconds;
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
