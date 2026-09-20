using App2d.Core.Characters;
using System.Numerics;

namespace App2d.Gameplay.Entities;

public readonly record struct EntityInput(float Move, bool Jump, bool Attack, bool Shoot);
public readonly record struct EntityCue(long Tick, int Actor, int ActionSequence, string Sound, Vector2 Position);

/// <summary>Small fixed-step combat arena for authored types. No renderer, editor or audio dependency.</summary>
public sealed class EntityPlaytest
{
    public const float StepSeconds = 1f / 120;
    public sealed class Actor
    {
        internal readonly HashSet<int> HitTargets = [];
        internal float Cooldown;
        internal bool ShotFired, CueFired;
        public EntityTypeDefinition Type { get; }
        public PointLibrary Library { get; }
        public EntityPose Pose { get; }
        public Vector2 Position { get; internal set; }
        public Vector2 Velocity { get; internal set; }
        public int Health { get; internal set; }
        public bool FacingLeft { get; internal set; }
        public string ActionId { get; internal set; } = "idle";
        public double ActionTime { get; internal set; }
        public int ActionSequence { get; internal set; }
        public EntityAction Action => Type.Actions[ActionId];
        public bool Alive => Health > 0;
        public bool Busy => ActionId is "attack" or "shoot" or "hit";
        internal Actor(EntityTypeDefinition type, PointLibrary library, float x)
        { Type = type.Copy(); Library = library; Pose = new(library); Position = new(x, 0); Health = type.Health; }
    }
    public sealed class Bolt
    {
        public int Owner { get; init; }
        public Vector2 Position { get; internal set; }
        public Vector2 Velocity { get; init; }
        public int Damage { get; init; }
        public Vector2 Size { get; init; } = new(.12f);
        public string ImpactCue { get; init; } = "hit";
        internal float Life = 3;
    }
    private readonly List<Actor> _actors = [];
    private readonly List<Bolt> _bolts = [];
    private readonly List<EntityCue> _cues = [];
    public IReadOnlyList<Actor> Actors => _actors;
    public IReadOnlyList<Bolt> Bolts => _bolts;
    public IReadOnlyList<EntityCue> Cues => _cues;
    public long Tick { get; private set; }
    public bool EnemiesEnabled { get; set; } = true;
    public EntityPlaytest(IEnumerable<(EntityTypeDefinition Type, PointLibrary Library)> definitions)
    {
        var i = 0;
        foreach (var (type, library) in definitions)
        {
            type.Validate(library); _actors.Add(new(type, library, i == 0 ? -7 : -3 + (i - 1) * 3.4f)); i++;
        }
        if (_actors.Count < 1) throw new ArgumentException("A playtest requires a controlled actor.");
        foreach (var actor in _actors) actor.Pose.Evaluate(actor.Type, actor.Action, 0, false);
    }
    private static void Start(Actor actor, string id)
    {
        if (!actor.Type.Actions.ContainsKey(id)) id = "idle";
        actor.ActionId = id; actor.ActionTime = 0; actor.ActionSequence++; actor.HitTargets.Clear(); actor.ShotFired = actor.CueFired = false;
    }
    public void Step(EntityInput input)
    {
        if (!float.IsFinite(input.Move)) throw new ArgumentException("Invalid movement input.");
        Tick++; _cues.Clear();
        var player = _actors[0];
        for (var i = 0; i < _actors.Count; i++)
        {
            var actor = _actors[i]; actor.Cooldown = Math.Max(0, actor.Cooldown - StepSeconds);
            var command = i == 0 ? input : default;
            if (i > 0 && EnemiesEnabled && player.Alive && actor.Type.Behavior != "passive")
            {
                var dx = player.Position.X - actor.Position.X; var distance = MathF.Abs(dx);
                var range = actor.Type.PreferredRange;
                var move = distance > range ? MathF.Sign(dx) : actor.Type.Behavior == "ranged" && distance < range * .65f ? -MathF.Sign(dx) : 0;
                command = new(move, false, distance <= range + .15f, false);
                if (!actor.Busy) actor.FacingLeft = dx < 0;
            }
            if (actor.Alive)
            {
                if (!actor.Busy)
                {
                    actor.Velocity = new(Math.Clamp(command.Move, -1, 1) * actor.Type.MoveSpeed, actor.Velocity.Y);
                    if (i == 0 && command.Move != 0) actor.FacingLeft = command.Move < 0;
                    if (command.Jump && actor.Position.Y <= 0) actor.Velocity = new(actor.Velocity.X, actor.Type.JumpSpeed);
                    if (actor.Cooldown <= 0 && (command.Attack || command.Shoot))
                    {
                        Start(actor, command.Shoot && actor.Type.Actions.ContainsKey("shoot") ? "shoot" : "attack"); actor.Cooldown = actor.Action.Duration + actor.Type.Cooldown;
                    }
                    else
                    {
                        var desired = actor.Position.Y > .01f ? (actor.Velocity.Y > 0 ? "jump" : "fall") : MathF.Abs(actor.Velocity.X) > .01f ? "walk" : "idle";
                        if (!actor.Type.Actions.ContainsKey(desired)) desired = "idle";
                        if (actor.ActionId != desired) Start(actor, desired);
                    }
                }
                else actor.Velocity = new(0, actor.Velocity.Y);
                actor.Velocity -= new Vector2(0, 20 * StepSeconds); actor.Position += actor.Velocity * StepSeconds;
                var bodyOffset = actor.Type.Movement.OffsetX * (actor.FacingLeft ? -1 : 1);
                actor.Position = new(Math.Clamp(actor.Position.X, -12 + actor.Type.Movement.Width / 2 - bodyOffset, 12 - actor.Type.Movement.Width / 2 - bodyOffset), Math.Max(0, actor.Position.Y));
                if (actor.Position.Y == 0) actor.Velocity = new(actor.Velocity.X, 0);
            }
            var previous = actor.ActionTime; actor.ActionTime += StepSeconds;
            var action = actor.Action;
            actor.Pose.Evaluate(actor.Type, action, actor.ActionTime, actor.FacingLeft);
            if (!actor.CueFired && previous <= action.CueTime * action.Duration && actor.ActionTime >= action.CueTime * action.Duration)
            { actor.CueFired = true; _cues.Add(new(Tick, i, actor.ActionSequence, action.Cue, actor.Position)); }
            if (actor.Alive && action.AttackKind == "projectile" && !actor.ShotFired && actor.ActionTime >= action.ActiveStart * action.Duration)
            {
                actor.ShotFired = true;
                var spawn = actor.Pose.Hit.Points.Aggregate(Vector2.Zero, (sum, p) => sum + p) / actor.Pose.Hit.Points.Count;
                _bolts.Add(new() { Owner = i, Position = actor.Position + spawn, Velocity = actor.Pose.Aim * action.ProjectileSpeed, Damage = action.Damage, Size = new(action.HitWidth, action.HitHeight), ImpactCue = action.ImpactCue });
            }
            if (actor.Alive && action.AttackKind == "melee" && previous < action.ActiveEnd * action.Duration && actor.ActionTime >= action.ActiveStart * action.Duration)
            {
                for (var j = 0; j < _actors.Count; j++)
                {
                    if (j == i || (i != 0 && j != 0) || actor.HitTargets.Contains(j)) continue;
                    var target = _actors[j]; if (!target.Alive) continue;
                    if (target.Pose.Hurt.Any(region => actor.Pose.Hit.Overlaps(region, actor.Position, target.Position)))
                    { actor.HitTargets.Add(j); Damage(j, action.Damage, action.ImpactCue); }
                }
            }
            if (actor.Alive && actor.Busy && actor.ActionTime >= action.Duration) Start(actor, "idle");
            else if (action.Loop && actor.ActionTime >= action.Duration) { actor.ActionTime %= action.Duration; actor.CueFired = false; }
        }
        foreach (var bolt in _bolts)
        {
            var old = bolt.Position; bolt.Position += bolt.Velocity * StepSeconds; bolt.Life -= StepSeconds;
            var shape = EntityRegion.Box("bolt sweep", (old + bolt.Position) / 2, Vector2.Abs(bolt.Position - old) + bolt.Size);
            for (var j = 0; j < _actors.Count; j++)
            {
                if (j == bolt.Owner || (bolt.Owner != 0 && j != 0) || !_actors[j].Alive) continue;
                if (_actors[j].Pose.Hurt.Any(region => shape.Overlaps(region, Vector2.Zero, _actors[j].Position))) { Damage(j, bolt.Damage, bolt.ImpactCue); bolt.Life = 0; break; }
            }
            if (MathF.Abs(bolt.Position.X) > 12 || bolt.Position.Y < 0) bolt.Life = 0;
        }
        _bolts.RemoveAll(b => b.Life <= 0);
        foreach (var actor in _actors) actor.Pose.Evaluate(actor.Type, actor.Action, actor.ActionTime, actor.FacingLeft);
    }
    private void Damage(int index, int amount, string sound)
    {
        if (amount <= 0) return;
        var actor = _actors[index]; actor.Health = Math.Max(0, actor.Health - amount); Start(actor, actor.Alive ? "hit" : "death");
        actor.Velocity = Vector2.Zero; _cues.Add(new(Tick, index, actor.ActionSequence, sound, actor.Position));
    }
}
