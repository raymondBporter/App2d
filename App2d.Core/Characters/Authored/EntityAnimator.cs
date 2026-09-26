using System.Collections.Immutable;
using System.Numerics;

namespace App2d.Core.Characters;

/// <summary>A clip marker crossed ("marker") or an action event reached ("event"), dispatched once when time advances across it.</summary>
public readonly record struct AnimationEvent(string Kind, string Id, string? Sound, string? Action, int ActionSequence)
{
    public const string MarkerKind = "marker", EventKind = "event";
}

/// <summary>Everything an animator needs to resume exactly; immutable, for rollback and snapshots.</summary>
public sealed record AnimatorState(string Role, double RoleTime, string? Action, double ActionTime, double PreviousActionTime, int ActionSequence,
    bool Fresh, int Facing, ImmutableDictionary<string, Vector3> Anchors);

/// <summary>
/// Gameplay animation state for one actor: locomotion phase, the playing action, world contact anchors and event
/// dispatch. The controller owns movement; this never moves the actor. Locomotion phase advances by the ground distance
/// actually covered over the clip's resolved stride. Contacts are captured in world space at touchdown and held until
/// release; role changes, action starts and ends, facing changes and <see cref="Reset"/> release them deliberately.
/// Scrubbing is not a thing here: every event is dispatched by <see cref="Step"/> advancing across its time.
/// </summary>
public sealed class EntityAnimator
{
    private readonly Dictionary<string, Vector3> _anchors = new(StringComparer.Ordinal);
    private readonly HashSet<string> _held = new(StringComparer.Ordinal);
    private bool _fresh;

    public EntityAnimator(ResolvedEntity entity)
    {
        Entity = entity; Role = EntityControllers.Idle;
        Pose = new(PoseEvaluator.Sample(entity.Model, entity.Clip(Role), 0, true, new() { InPlace = true }), Vector2.Zero, 1);
    }

    public ResolvedEntity Entity { get; }
    public string Role { get; private set; }
    public double RoleTime { get; private set; }
    public string? Action { get; private set; }
    public double ActionTime { get; private set; }
    /// <summary>The action time before the latest step, for window tests that must not miss a window skipped in one step.</summary>
    public double PreviousActionTime { get; private set; }
    /// <summary>Increments on every action start; hit deduplication keys on it.</summary>
    public int ActionSequence { get; private set; }
    public int Facing { get; private set; } = 1;
    public ActorPose Pose { get; private set; }
    public IReadOnlyDictionary<string, Vector3> Anchors => _anchors;
    public ResolvedAction? Current => Action is null ? null : Entity.Actions[Action];
    public bool ActionComplete => Current is { } action && ActionTime >= action.Clip.Duration;
    public MotionClip? Clip => Current?.Clip ?? Entity.Clip(Role);

    /// <summary>Starts an enabled action. Unsupported or disabled actions are rejected, never replaced by idle.</summary>
    public bool TryStart(string action)
    {
        if (!Entity.Enabled(action)) return false;
        Action = action; ActionTime = PreviousActionTime = 0; ActionSequence++; _fresh = true; _anchors.Clear();
        return true;
    }

    /// <summary>Ends the playing action (finished or interrupted) and returns to locomotion with fresh contacts.</summary>
    public void EndAction()
    {
        if (Action is null) return;
        Action = null; _anchors.Clear(); RoleTime = 0;
    }

    /// <summary>A teleport or respawn: drop the action, the phase and every anchor.</summary>
    public void Reset(string role = EntityControllers.Idle)
    {
        Action = null; ActionTime = PreviousActionTime = RoleTime = 0; Role = role; _anchors.Clear();
    }

    /// <summary>
    /// Advances one fixed step and evaluates the final pose at <paramref name="position"/> (the feet origin). The
    /// controller picks the locomotion role; <paramref name="groundDistance"/> is how far the actor actually moved along
    /// the ground this step. <paramref name="hold"/> freezes locomotion, the explicit fallback for states without a role.
    /// </summary>
    public void Step(float dt, Vector2 position, int facing, string role, float groundDistance, bool hold, List<AnimationEvent> events, string? expression = null)
    {
        if (facing is not (1 or -1)) throw new ArgumentOutOfRangeException(nameof(facing));
        if (facing != Facing) { Facing = facing; _anchors.Clear(); }
        if (Current is { } action)
        {
            PreviousActionTime = ActionTime;
            var from = ActionTime; var to = Math.Min(action.Clip.Duration, ActionTime + dt);
            Cross(action.Clip, from, to, false, _fresh, events, action.Id);
            foreach (var cue in action.Events)
                if (Crosses(cue.Seconds, from, to, _fresh)) events.Add(new(AnimationEvent.EventKind, cue.Event.Id, cue.Event.Sound, action.Id, ActionSequence));
            ActionTime = to; _fresh = false;
        }
        else
        {
            if (role != Role)
            {
                if (Entity.Clip(role) is null) throw new InvalidOperationException($"Entity '{Entity.Id}' has no '{role}' role; the controller must choose an assigned role or hold.");
                Role = role; RoleTime = 0; _anchors.Clear();
            }
            var clip = Entity.Clip(Role)!;
            if (!hold)
            {
                var stride = MathF.Abs(PoseEvaluator.CycleTravel(Entity.Model, clip).X);
                var advance = clip.Loop && stride > 1e-4f ? groundDistance / stride * clip.Duration : dt;
                var from = RoleTime; var to = RoleTime + advance;
                Cross(clip, from, to, clip.Loop, from == 0, events, null);
                RoleTime = clip.Loop ? to : Math.Min(to, clip.Duration);
            }
        }
        Evaluate(position, expression);
    }

    private void Evaluate(Vector2 position, string? expression)
    {
        var placed = new ActorPose(new EvaluatedPose(), position, Facing);
        _held.Clear();
        Vector3 Hold(string chain, Vector3 authored)
        {
            _held.Add(chain);
            if (_anchors.TryGetValue(chain, out var anchor)) return placed.ToLocal(anchor);
            _anchors[chain] = placed.Place(authored); return authored;
        }
        var clip = Clip; var action = Current;
        var local = PoseEvaluator.Sample(Entity.Model, clip, action is null ? RoleTime : ActionTime, action is null && clip?.Loop == true,
            new(expression) { InPlace = true, Contact = Hold });
        foreach (var chain in _anchors.Keys.Where(c => !_held.Contains(c)).ToList()) _anchors.Remove(chain);
        Pose = placed with { Local = local };
    }

    /// <summary>Hit windows active at any moment of the latest step, including one that opened and closed inside it.</summary>
    public IEnumerable<ResolvedHit> ActiveHits()
    {
        if (Current is not { } action) yield break;
        foreach (var hit in action.Hits)
            if (PreviousActionTime < hit.Finish && ActionTime >= hit.Start) yield return hit;
    }

    private static bool Crosses(float time, double from, double to, bool inclusiveStart) => (time > from || inclusiveStart && time == from) && time <= to;

    /// <summary>Markers in (from, to], or [from, to] on a fresh start; looping clips dispatch every cycle crossed. No advance, no events.</summary>
    private void Cross(MotionClip clip, double from, double to, bool loop, bool inclusiveStart, List<AnimationEvent> events, string? action)
    {
        if (clip.Markers.Count == 0 || to <= from) return;
        if (!loop) { foreach (var marker in clip.Markers) if (Crosses(marker.Time, from, to, inclusiveStart)) Add(marker.Id); return; }
        var duration = clip.Duration;
        for (var cycle = Math.Floor(from / duration); cycle * duration <= to; cycle++)
            foreach (var marker in clip.Markers)
            {
                var time = cycle * duration + marker.Time;
                if ((time > from || inclusiveStart && time == from) && time <= to) Add(marker.Id);
            }
        void Add(string id) => events.Add(new(AnimationEvent.MarkerKind, id, null, action, ActionSequence));
    }

    public AnimatorState Capture() => new(Role, RoleTime, Action, ActionTime, PreviousActionTime, ActionSequence, _fresh, Facing, _anchors.ToImmutableDictionary());

    public void Restore(AnimatorState state, Vector2 position, string? expression = null)
    {
        Role = state.Role; RoleTime = state.RoleTime; Action = state.Action; ActionTime = state.ActionTime; PreviousActionTime = state.PreviousActionTime;
        ActionSequence = state.ActionSequence; _fresh = state.Fresh; Facing = state.Facing;
        _anchors.Clear(); foreach (var (chain, anchor) in state.Anchors) _anchors[chain] = anchor;
        Evaluate(position, expression);
    }
}

/// <summary>Per-action hit deduplication: one attack instance damages each target at most once per window.</summary>
public sealed class HitLedger
{
    private readonly HashSet<(int Sequence, string Window, int Target)> _hits = [];
    public bool TryHit(int sequence, string window, int target)
    {
        _hits.RemoveWhere(h => h.Sequence != sequence);
        return _hits.Add((sequence, window, target));
    }
    public ImmutableArray<(int Sequence, string Window, int Target)> Capture() => [.. _hits];
    public void Restore(IEnumerable<(int Sequence, string Window, int Target)> hits) { _hits.Clear(); _hits.UnionWith(hits); }
}
