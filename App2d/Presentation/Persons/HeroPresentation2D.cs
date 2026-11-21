using App2d.Contracts.Combat;
using App2d.Contracts.Persons;
using App2d.Contracts.Persons.Actions;
using App2d.Contracts.Player;
using App2d.Core;
using App2d.Core.Animation;
using App2d.Core.Characters;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Rendering;
using App2d.Core.Rendering.Characters;
using App2d.Presentation.Player;
using App2d.Presentation.World.Presentation;

namespace App2d.Presentation.Persons;

/// <summary>
/// Draws the player as its character. It watches the player's state; <see cref="PersonAnimationDirector"/> picks the clip,
/// <see cref="PlantedLimbs"/> keeps planted feet planted while the body moves, and <see cref="PlayerWardrobe2D"/> decides what
/// is worn and where the sword and pistol are. The figure is scaled so its resting height matches the player's collider.
/// </summary>
public sealed class HeroPresentation2D : IDisposable
{
    private readonly Scene2D _scene;
    private readonly WorldObject2D _visual;
    private readonly CharacterShader _shader;
    private readonly PlantedLimbs _planted = new();
    private readonly PersonFrameHistory2D _history = new();
    private readonly PlayerContactHold2D _hitstop = new(CombatHitstop2D.Curve);
    private readonly PersonFace2D _face = new();
    private readonly BladeSwoosh _swoosh = new();
    private readonly Dictionary<SkeletonClip2D, bool> _backhands = [];
    /// <summary>The swoosh sits just behind the blade so the sword always draws over it.</summary>
    private const float SwooshLayerOffset = .01f;
    /// <summary>Where the blade starts, along the sword from the grip.</summary>
    private const float GuardAlongBlade = .07f;
    private readonly float _halfHeight, _worldUnitsPerModelUnit;
    private PersonState2D _state;
    private double _clock, _stateClock = double.NaN;
    private string _drawnKey = "";
    private bool _enabled = true;
    public bool Enabled
    {
        get => _enabled; set
        {
            _enabled = value;
            _visual.IsVisible = value && Visible();
        }
    }

    public HeroPresentation2D(Scene2D scene, PersonMoves moves, TraversalMetrics2D traversal)
    {
        _scene = scene;
        _halfHeight = traversal.PlayerColliderSize.Y / 2;
        _worldUnitsPerModelUnit = traversal.PlayerColliderSize.Y / moves.Hero.Height;
        Director = new(moves, _worldUnitsPerModelUnit)
        {
            HardLandingSpeed = traversal.HardLandingSpeed
        };
        _shader = new()
        {
            Swoosh = _swoosh
        };
        _visual = new(AxisAlignedRectangle2D.Builder.CreateFromSize(10, 10, new(0, 2)), _shader)
        {
            Scale = _worldUnitsPerModelUnit,
            ZIndex = 1
        };
        scene.Add(_visual);
        Wardrobe = new(moves);
        Wardrobe.Changed += RefreshWardrobe;
    }

    public PersonAnimationDirector Director { get; }
    public CharacterPose? Pose { get; private set; }
    internal PlayerWardrobe2D Wardrobe { get; }

    private void RefreshWardrobe() => Update();

    public void Equip(EquipmentKind2D equipment) => Director.Equipment = equipment;
    public void PresentContact(CombatDamage2D damage, EntityId2D playerId, long tick) => _hitstop.Present(damage, playerId, tick / 120.0);
    public void ResetContact()
    {
        _hitstop.Reset();
        _history.Clear();
    }
    public void PlayHit()
    {
        _hitstop.Reset();
        Director.PlayHit();
        _face.Hit();
    }
    public void PlayLanding() => _face.Land();
    public void PlayCelebrate()
    {
        Director.PlayCelebrate();
        _face.Celebrate();
    }
    public void PlayDeath()
    {
        _hitstop.Reset();
        Director.PlayDeath();
    }
    public void Reset()
    {
        ResetContact();
        Director.Reset();
        _planted.Clear();
        _face.Reset();
        _swoosh.Reset();
        _drawnKey = "";
        _stateClock = double.NaN;
    }

    public void ApplyState(PersonState2D state, long tick, PersonFrame? animationSample = null)
    {
        // Planted feet stand on the ground, so they ride a moving platform instead of staying where it was.
        var elapsed = double.IsNaN(_stateClock) ? 0 : (float)Math.Clamp(tick / 120.0 - _stateClock, 0, .1);
        if (state.IsGrounded && state.GroundVelocity != Vector2f.Zero)
        {
            _planted.Shift(state.GroundVelocity * (float)elapsed / _worldUnitsPerModelUnit);
        }

        _state = state;
        _clock = _stateClock = tick / 120.0;
        Director.ApplyState(state, Feet(state) / _worldUnitsPerModelUnit, _clock);
        Update(animationSample);
    }

    public void Advance(float dt)
    {
        _clock += dt;
        Director.Advance(dt);
        _face.Update(_state, dt);
        Update();
    }

    private Vector2f Feet(PersonState2D state) => state.Position - new Vector2f(0, _halfHeight);

    private bool Visible() => _state.InvulnerabilitySeconds <= 0 || ((int)(_clock * 20) & 1) == 0 || !_state.IsAlive;

    private void Update(PersonFrame? animationSample = null)
    {
        var s = _state;
        _face.Update(s, 0);
        var frame = animationSample ?? Director.Frame();
        if (animationSample is null)
        {
            _history.Record(_clock, frame);
            frame = _hitstop.Sample(_history, _clock, frame);
        }
        // A new clip starts with nothing planted; feet are planted again from its first pose.
        if (frame.Key != _drawnKey)
        {
            _drawnKey = frame.Key;
            _planted.Clear();
        }
        var hero = Director.Moves.Hero;
        var facing = s.Facing < 0 ? -1 : 1;
        // The face shows what the player's state asks for and blinks on this presentation's own clock; the clip decides
        // whether the player is seen from behind, which hides it.
        var pose = _planted.Sample(hero.Skeleton, frame.Clip, Math.Max(0, frame.Seconds), frame.Repeat, Feet(s) / _worldUnitsPerModelUnit, facing, frame.Overlay, hold: frame.Planted)
            .Showing(_face.Expression, _clock);
        Pose = pose;
        var seconds = (float)Math.Min(frame.PropSeconds, frame.PropClip.Duration);
        _shader.Pose = pose;
        _shader.Drawn = Wardrobe.Dress(pose, frame.PropClip, seconds, frame.Gear);
        RecordSwoosh(frame, seconds, pose);
        _visual.Position = Feet(s);
        _visual.IsVisible = _enabled && Visible();
    }

    /// <summary>The held sword's blade, guard to tip, in the character's own space: along the sword's own axis from the hand.</summary>
    private static (Vector3f Guard, Vector3f Tip) Blade(CharacterPose pose)
    {
        var hand = pose.MountFrame(PersonSkeleton.SwordHand);
        return (hand.TransformPoint(new(GuardAlongBlade, 0, 0)), hand.TransformPoint(new(PersonWardrobe.SwordTip.X, 0, 0)));
    }

    /// <summary>
    /// Whether a clip's first swoosh is a backhand: the blade turning round the body the opposite way to a forehand, which
    /// crosses from the far side toward the viewer. Read from the clip, finely sampled over the swoosh; false when it has none.
    /// </summary>
    private bool Backhand(SkeletonClip2D clip)
    {
        if (_backhands.TryGetValue(clip, out var known)) return known;
        var yaw = 0f;
        if (clip.Marker(ClipMarkers.Swoosh) is { } start)
        {
            var end = clip.Markers.Where(m => m.Name == ClipMarkers.SwooshEnd && m.Time > start).Select(m => (float?)m.Time).FirstOrDefault() ?? clip.Duration;
            Vector3f? last = null;
            for (var time = start; time <= end + 1e-4f; time += 1 / 240f)
            {
                var (guard, tip) = Blade(CharacterPose.Sample(Director.Moves.Skeleton, clip, time));
                var direction = Vector3f.Normalize(tip - guard);
                if (last is { } before) yaw += Vector3f.Cross(before, direction).Y;
                last = direction;
            }
        }
        return _backhands[clip] = yaw < 0;
    }

    /// <summary>
    /// Feeds the swoosh the held blade while the clip's swoosh markers say it is swinging. A backhand takes the follow-up
    /// mark and turns the other way round.
    /// </summary>
    private void RecordSwoosh(PersonFrame frame, float seconds, CharacterPose pose)
    {
        var clip = frame.PropClip;
        if (frame.Gear != PersonGear.Sword || !clip.SwordInHand(seconds))
        {
            _swoosh.Record((float)_clock, default, default, false);
            return;
        }
        var backhand = Backhand(clip);
        _swoosh.Sweep = backhand ? 1 : -1;
        _swoosh.Style = backhand ? SwooshStyle.FollowUp : SwooshStyle.Primary;
        var (guard, tip) = Blade(pose);
        _swoosh.Record((float)_clock, guard, tip, clip.Swooshing(seconds), pose.Posed.MountLayer(PersonSkeleton.SwordHand) + SwooshLayerOffset);
    }

    public void Dispose() => _scene.Remove(_visual);
}
