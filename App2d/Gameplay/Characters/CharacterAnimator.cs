using App2d.Core.Animation;
using App2d.Core.Characters;

namespace App2d.Gameplay.Characters;

/// <summary>Something an animator passed while stepping: a clip's marker, or one of an action's cues.</summary>
public readonly record struct CharacterEvent(string Name, bool IsCue, string? Sound, string? Action, int Sequence);

/// <summary>
/// The animation of one character in play: which role it is in and how far through, the action it is taking, and its pose.
/// It never moves the character; whoever does tells it each step where the character is and how far it went. A walk advances
/// by the ground actually covered over the clip's stride, so feet keep pace with the body. A masked action plays over the
/// role underneath, which keeps its own time and its planted feet. Every marker and cue is reported by the step that passes it.
/// </summary>
public sealed class CharacterAnimator
{
    public const string Idle = "idle", Walk = "walk", Run = "run", Hit = "hit", Death = "death", Attack = "attack", Fire = "fire";

    private readonly PlantedLimbs _planted = new();
    private bool _fresh, _reversed;

    public CharacterAnimator(Combatant character)
    {
        Character = character;
        Pose = CharacterPose.Sample(character.Skeleton, Clip(Idle), 0, true);
    }

    public Combatant Character { get; }
    public string Role { get; private set; } = Idle;
    public double RoleTime { get; private set; }
    public CharacterAction? Action { get; private set; }
    public double ActionTime { get; private set; }
    /// <summary>The action's time before the latest step, so a hit shorter than the step is not missed.</summary>
    public double PreviousActionTime { get; private set; }
    /// <summary>Goes up each time an action starts; a hit is counted once per target for each value.</summary>
    public int ActionSequence { get; private set; }
    public int Facing { get; private set; } = 1;
    /// <summary>Time since the character was made. Nothing restarts it, so a blink never jumps when the role changes.</summary>
    public double Clock { get; private set; }
    public CharacterPose Pose { get; private set; }
    public bool ActionComplete => Action is { } action && ActionTime >= action.Clip.Duration;
    /// <summary>Whether the role is playing: no action, or a masked one over it.</summary>
    public bool Locomoting => Action is null || Action.Mask is not null;

    private SkeletonClip2D Clip(string role) => Character.Clip(role) ?? throw new InvalidOperationException($"'{Character.Name}' has no '{role}' clip.");

    /// <summary>Starts an action the character has. One it does not have is refused.</summary>
    public bool TryStart(string action)
    {
        if (Character.Action(action) is not { } found) return false;
        Action = found;
        ActionTime = PreviousActionTime = 0;
        ActionSequence++;
        _fresh = true;
        if (found.Mask is null) _planted.Clear();
        return true;
    }

    /// <summary>Ends the action, finished or not, and returns to the role with nothing planted.</summary>
    public void EndAction()
    {
        if (Action is not { } action) return;
        Action = null;
        if (action.Mask is null)
        {
            _planted.Clear();
            RoleTime = 0;
        }
    }

    /// <summary>Plays a role from its start, even the one already playing, and ends any action. A clip that does not loop then holds its end.</summary>
    public void Play(string role, bool reversed = false)
    {
        Clip(role);
        Action = null;
        Role = role;
        RoleTime = 0;
        _planted.Clear();
        _reversed = reversed;
    }

    /// <summary>The ground under the character moved; planted limbs ride with it.</summary>
    public void Carry(Vector2f delta) => _planted.Shift(delta);

    /// <summary>The character left the ground; planted limbs let go.</summary>
    public void Lift() => _planted.Clear();

    /// <summary>
    /// Advances one step and poses the character at <paramref name="position"/>. <paramref name="groundDistance"/> is how far
    /// it moved along the ground this step. <paramref name="hold"/> freezes the role where it is. <paramref name="expression"/>
    /// asks the face for a look; without one the clip's own is used.
    /// </summary>
    public void Step(float dt, Vector2f position, int facing, string role, float groundDistance, bool hold, List<CharacterEvent> events, string? expression = null)
    {
        Facing = facing;
        Clock += dt;
        if (Action is { } action)
        {
            PreviousActionTime = ActionTime;
            var from = ActionTime;
            var to = Math.Min(action.Clip.Duration, ActionTime + dt);
            Cross(action.Clip, from, to, false, _fresh, events, action.Name);
            foreach (var cue in action.Cues)
            {
                if (Crosses(cue.Seconds, from, to, _fresh)) events.Add(new(cue.Name, true, cue.Sound, action.Name, ActionSequence));
            }
            ActionTime = to;
            _fresh = false;
        }
        if (Locomoting)
        {
            if (role != Role)
            {
                Clip(role);
                Role = role;
                RoleTime = 0;
                _planted.Clear();
                _reversed = false;
            }
            var clip = Clip(Role);
            if (!hold)
            {
                var stride = MathF.Abs(clip.Stride(Character.Skeleton).X);
                var advance = clip.Loop && stride > 1e-4f ? groundDistance / stride * clip.Duration : dt;
                var from = RoleTime;
                var to = RoleTime + advance;
                Cross(clip, from, to, clip.Loop, from == 0, events, null);
                RoleTime = clip.Loop ? to : Math.Min(to, clip.Duration);
            }
        }

        var skeleton = Character.Skeleton;
        var pose = Action is { Mask: null } whole
            ? _planted.Sample(skeleton, whole.Clip, ActionTime, false, position, Facing)
            : _planted.Sample(skeleton, Clip(Role), RoleTime, Clip(Role).Loop, position, Facing,
                Action is { Mask: { } mask } masked ? new(masked.Clip, ActionTime, mask, masked.Weight(ActionTime)) : null, _reversed);
        Pose = Character.Face is { } face && expression is not null && face.Face.Has(expression) ? pose.Showing(expression, Clock) : pose.Showing(pose.Expression, Clock);
    }

    /// <summary>The hits open at any moment of the latest step.</summary>
    public IEnumerable<HitWindow> ActiveHits()
    {
        if (Action is not { } action) yield break;
        foreach (var hit in action.Hits)
        {
            if (hit.Active(PreviousActionTime, ActionTime)) yield return hit;
        }
    }

    private static bool Crosses(float time, double from, double to, bool inclusiveStart) => (time > from || inclusiveStart && time == from) && time <= to;

    /// <summary>Reports the markers passed in (from, to], and one at the very start of a fresh play. A looping clip reports every cycle passed.</summary>
    private void Cross(SkeletonClip2D clip, double from, double to, bool loop, bool inclusiveStart, List<CharacterEvent> events, string? action)
    {
        if (clip.Markers.Length == 0 || to <= from) return;
        var duration = loop ? clip.Duration : 0;
        for (var cycle = loop ? Math.Floor(from / duration) : 0; cycle * duration <= to; cycle++)
        {
            foreach (var marker in clip.Markers)
            {
                var time = cycle * duration + marker.Time;
                if ((time > from || inclusiveStart && time == from) && time <= to) events.Add(new(marker.Name, false, null, action, ActionSequence));
            }
            if (!loop) break;
        }
    }
}

/// <summary>
/// How a character reacts to being hit. Whoever deals damage says when; the character's "hit" and "death" clips say what it
/// looks like. A hit ends the action and staggers the character for the length of the hit clip, or a given time without
/// one; while staggered it should not be steered, so a knockback carries. Death plays once and holds its last frame.
/// </summary>
public sealed class CharacterReaction(float staggerSeconds = .3f)
{
    public float Stagger { get; private set; }
    public bool Staggered => Stagger > 0;

    public void Hit(CharacterAnimator animator, int incomingDirection = 0)
    {
        animator.EndAction();
        var clip = animator.Character.Clip(CharacterAnimator.Hit);
        Stagger = clip?.Duration ?? staggerSeconds;
        if (clip is not null) animator.Play(CharacterAnimator.Hit, incomingDirection * animator.Facing > 0);
    }

    public void Die(CharacterAnimator animator, int incomingDirection = 0)
    {
        animator.EndAction();
        Stagger = 0;
        if (animator.Character.Clip(CharacterAnimator.Death) is not null) animator.Play(CharacterAnimator.Death, incomingDirection * animator.Facing > 0);
    }

    /// <summary>Counts the stagger down. True on the step it ends.</summary>
    public bool Tick(float dt)
    {
        if (!Staggered) return false;
        Stagger = MathF.Max(0, Stagger - dt);
        return !Staggered;
    }

    /// <summary>The role to play instead of moving about: death when dead, hit while staggered, if the character has the clip.</summary>
    public string? Role(Character character, bool alive)
    {
        var role = !alive ? CharacterAnimator.Death : Staggered ? CharacterAnimator.Hit : null;
        return role is not null && character.Clip(role) is not null ? role : null;
    }
}
