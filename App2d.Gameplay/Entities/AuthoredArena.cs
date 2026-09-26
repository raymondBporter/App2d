using System.Numerics;
using App2d.Core.Characters;

namespace App2d.Gameplay.Entities;

public readonly record struct ArenaInput(float Move = 0, bool Run = false, bool Jump = false, bool Attack = false);
public readonly record struct ArenaEvent(long Tick, int Actor, AnimationEvent Event, Vector2 Position);
public readonly record struct ArenaHit(long Tick, int Attacker, int Target, string Window, int Damage);

/// <summary>
/// The flat-ground entity arena over authored entities. Actor 0 is controlled; the others walk toward it and attack in
/// range. Deterministic fixed steps. Controllers own movement; each actor's <see cref="EntityAnimator"/> supplies the one
/// final pose that drawing, sockets, props and collision all read. Actions an entity has not enabled are rejected.
/// </summary>
public sealed class AuthoredArena
{
    public const float StepSeconds = 1 / 120f, Gravity = 20, HalfWidth = 12, HurtSeconds = .45f;

    public sealed class Actor
    {
        internal Actor(int index, ResolvedEntity entity, Vector2 position, int facing)
        {
            Index = index; Entity = entity; Animator = new(entity); Position = position; Facing = facing; Health = entity.Asset.Health;
            Animator.Step(0, position, facing, EntityControllers.Idle, 0, false, []);
        }
        public int Index { get; }
        public ResolvedEntity Entity { get; }
        public EntityAnimator Animator { get; }
        public HitLedger Ledger { get; } = new();
        public Vector2 Position { get; internal set; }
        public Vector2 Velocity { get; internal set; }
        public int Facing { get; internal set; }
        public int Health { get; internal set; }
        public bool Grounded { get; internal set; } = true;
        public bool Launched { get; internal set; }
        public float Cooldown { get; internal set; }
        public float HurtTime { get; internal set; }
        public bool Alive => Health > 0;
        public ActorPose Pose => Animator.Pose;
        /// <summary>The gameplay face input: a reaction wins over the clip's face channel while it lasts.</summary>
        public string? Expression => !Alive ? "knocked-out" : HurtTime > 0 ? "hurt" : null;
        public EntityRegion Movement => EntityCollision.Movement(Entity, Position, Facing);
        public List<EntityRegion> Hurt => EntityCollision.Hurt(Entity, Pose);
        public IEnumerable<(ResolvedHit Hit, EntityRegion Region)> Attacks => Animator.ActiveHits().Select(h => (h, EntityCollision.Attack(Entity, Pose, h)));
    }

    private readonly List<AnimationEvent> _scratch = [];

    public AuthoredArena(IEnumerable<ResolvedEntity> entities)
    {
        var list = entities.ToList();
        if (list.Count == 0) throw new ArgumentException("The arena needs at least one entity.", nameof(entities));
        for (var i = 0; i < list.Count; i++) Actors.Add(new(i, list[i], new(i == 0 ? -6 : -2 + 3.2f * i, 0), i == 0 ? 1 : -1));
    }

    public List<Actor> Actors { get; } = [];
    public Actor Player => Actors[0];
    public long Tick { get; private set; }
    public List<ArenaEvent> Events { get; } = [];
    public List<ArenaHit> Hits { get; } = [];

    /// <summary>Moves an actor instantly. Its action, phase and contacts reset rather than stretching a foot across the jump.</summary>
    public void Teleport(int index, Vector2 position)
    {
        var actor = Actors[index];
        actor.Position = position; actor.Velocity = Vector2.Zero; actor.Grounded = position.Y <= 0; actor.Launched = false;
        actor.Animator.Reset(); actor.Animator.Step(0, position, actor.Facing, EntityControllers.Idle, 0, false, []);
    }

    public void Step(ArenaInput input)
    {
        Tick++;
        foreach (var actor in Actors) Control(actor, actor.Index == 0 ? input : Think(actor));
        foreach (var attacker in Actors)
        {
            if (!attacker.Alive) continue;
            foreach (var (hit, region) in attacker.Attacks)
                foreach (var target in Actors)
                {
                    if (target == attacker || !target.Alive || (target.Index == 0) == (attacker.Index == 0)) continue;
                    if (!target.Hurt.Any(h => h.Overlaps(region, Vector2.Zero, Vector2.Zero))) continue;
                    if (!attacker.Ledger.TryHit(attacker.Animator.ActionSequence, hit.Window.Id, target.Index)) continue;
                    Hits.Add(new(Tick, attacker.Index, target.Index, hit.Window.Id, hit.Window.Damage));
                    Damage(target, hit.Window.Damage);
                }
        }
    }

    private ArenaInput Think(Actor actor)
    {
        if (!actor.Alive || !Player.Alive) return default;
        var dx = Player.Position.X - actor.Position.X; var config = actor.Entity.Asset.Controller;
        var direction = MathF.Sign(dx);
        if (MathF.Abs(dx) <= config.Range) return new(direction * 1e-4f, Attack: true); // face the target, stand and attack
        return MathF.Abs(dx) < 14 ? new(direction) : default;
    }

    private void Control(Actor actor, ArenaInput input)
    {
        var animator = actor.Animator; var config = actor.Entity.Asset.Controller; var spec = actor.Entity.Controller;
        actor.Cooldown = MathF.Max(0, actor.Cooldown - StepSeconds); actor.HurtTime = MathF.Max(0, actor.HurtTime - StepSeconds);
        var events = _scratch; events.Clear();
        if (!actor.Alive) { animator.Step(StepSeconds, actor.Position, actor.Facing, animator.Role, 0, true, events, actor.Expression); return; }

        var move = spec.Moves || input.Attack ? Math.Clamp(input.Move, -1, 1) : 0;
        // Turn before acting, so an attack starts toward where the controller is steering.
        if (animator.Action is null && MathF.Abs(move) > 0) actor.Facing = move > 0 ? 1 : -1;
        if (!spec.Moves) move = 0;
        if (animator.Action is null && actor.Grounded)
        {
            // Rejected requests (a guard asked to jump) simply do nothing: there is no accidental idle substitute.
            if (input.Attack && actor.Cooldown <= 0 && animator.TryStart(EntityControllers.Attack))
                actor.Cooldown = animator.Current!.Clip.Duration + config.Cooldown;
            else if (input.Jump && spec.Jumps) animator.TryStart(EntityControllers.Jump);
        }
        var action = animator.Action;
        var speed = input.Run && config.RunSpeed > 0 ? config.RunSpeed : config.WalkSpeed;
        var vx = action == EntityControllers.Attack || action == EntityControllers.Jump && !actor.Launched ? 0 : MathF.Abs(move) < .01f ? 0 : move * speed;
        var velocity = new Vector2(vx, actor.Grounded ? 0 : actor.Velocity.Y - Gravity * StepSeconds);
        var before = actor.Position;
        var position = before + velocity * StepSeconds;
        position.X = Math.Clamp(position.X, -HalfWidth, HalfWidth);
        var landed = false;
        if (position.Y <= 0 && !actor.Grounded) { position.Y = 0; velocity.Y = 0; actor.Grounded = true; landed = true; }
        actor.Position = position; actor.Velocity = velocity;
        if (landed && action == EntityControllers.Jump) { animator.EndAction(); actor.Launched = false; }

        var role = MathF.Abs(velocity.X) < 1e-3f ? EntityControllers.Idle
            : input.Run && actor.Entity.Clip(EntityControllers.Run) is not null ? EntityControllers.Run : EntityControllers.Walk;
        var hold = false;
        if (!actor.Grounded && animator.Action is null)
        {
            // Airborne without an action: the fall role when assigned, otherwise the explicit fallback of holding the pose.
            if (actor.Entity.Clip(EntityControllers.Fall) is not null) role = EntityControllers.Fall; else { role = animator.Role; hold = true; }
        }
        var ground = actor.Grounded ? MathF.Abs(position.X - before.X) : 0;
        animator.Step(StepSeconds, position, actor.Facing, role, ground, hold, events, actor.Expression);

        foreach (var e in events)
        {
            Events.Add(new(Tick, actor.Index, e, position));
            if (e is { Kind: AnimationEvent.EventKind, Id: EntityControllers.Launch } && animator.Action == EntityControllers.Jump)
            { actor.Grounded = false; actor.Launched = true; actor.Velocity = new(actor.Velocity.X, config.JumpSpeed); }
        }
        if (animator.ActionComplete && animator.Action != EntityControllers.Jump) animator.EndAction();
    }

    private static void Damage(Actor target, int damage)
    {
        target.Health = Math.Max(0, target.Health - damage); target.HurtTime = HurtSeconds;
        // A hit interrupts the target's action; its contacts are released and re-captured from the next pose.
        if (target.Animator.Action == EntityControllers.Attack) target.Animator.EndAction();
    }
}
