using App2d.Contracts.Combat;
using App2d.Contracts.Persons;
using App2d.Contracts.Persons.Actions;
using App2d.Contracts.Player;
using App2d.Core;
using App2d.Core.Characters.Authored;
using App2d.Core.Rendering;
using App2d.Core.Rendering.Characters;
using App2d.Core.Shapes;
using App2d.Presentation.World.Presentation;
using System.Numerics;

namespace App2d.Presentation.Persons;

/// <summary>
/// Draws the player as the authored Person with the player move set. Observes the existing traversal/controller state;
/// <see cref="PersonAnimationDirector"/> picks the clip, a <see cref="ContactHold"/> keeps planted feet planted while the
/// body moves, and <see cref="PersonLoadout"/> decides where the sword and pistol sit. The figure is scaled so its
/// rest height matches the traversal collider.
/// </summary>
public sealed class AuthoredPersonPresentation2D : IDisposable
{
    private readonly Scene2D _scene;
    private readonly WorldObject2D _visual;
    private readonly AuthoredCharacterShader _shader;
    private readonly ContactHold _hold = new();
    private readonly PersonFrameHistory2D _history = new();
    private readonly PlayerContactHold2D _hitstop = new(CombatHitstop2D.Curve);
    private readonly PersonFace2D _face = new();
    private readonly BladeSwoosh _swoosh = new();
    private readonly Dictionary<MotionClip, bool> _backhands = [];
    /// <summary>The swoosh sits just behind the blade so the sword always draws over it.</summary>
    private static readonly Vector3 SwooshDepth = new(0, 0, .01f);
    private readonly float _halfHeight, _worldUnitsPerModelUnit;
    private PersonState2D _state;
    private double _clock, _stateClock = double.NaN;
    private string _drawnKey = "";
    private bool _enabled = true;
    public bool Enabled { get => _enabled; set { _enabled = value; _visual.IsVisible = value && Visible(); } }

    public AuthoredPersonPresentation2D(Scene2D scene, PersonMoves moves, TraversalMetrics2D traversal)
    {
        _scene = scene; _halfHeight = traversal.PlayerColliderSize.Y / 2;
        _worldUnitsPerModelUnit = traversal.PlayerColliderSize.Y / RestHeight(moves.Model);
        Director = new(moves, _worldUnitsPerModelUnit) { HardLandingSpeed = traversal.HardLandingSpeed };
        _shader = new(moves.Model) { Swoosh = _swoosh };
        _visual = new(AxisAlignedRectangle2D.FromSize(new Vector2(10, 10), new(0, 2)), _shader) { ZIndex = 1 };
        _visual.Transform.Scale = new(_worldUnitsPerModelUnit);
        scene.Add(_visual);
    }

    public PersonAnimationDirector Director { get; }
    public ActorPose? Pose { get; private set; }

    /// <summary>The top of the rest pose's drawn shapes above the feet.</summary>
    public static float RestHeight(ResolvedModel model) => model.DrawnHeight();

    public void Equip(EquipmentKind2D equipment) => Director.Equipment = equipment;
    public void PresentContact(CombatDamage2D damage, EntityId2D playerId, long tick) => _hitstop.Present(damage, playerId, tick / 120.0);
    public void ResetContact() { _hitstop.Reset(); _history.Clear(); }
    public void PlayHit() { _hitstop.Reset(); Director.PlayHit(); _face.Hit(); }
    public void PlayLanding() => _face.Land();
    public void PlayCelebrate() { Director.PlayCelebrate(); _face.Celebrate(); }
    public void PlayDeath() { _hitstop.Reset(); Director.PlayDeath(); }
    public void Reset() { ResetContact(); Director.Reset(); _hold.Clear(); _face.Reset(); _swoosh.Reset(); _drawnKey = ""; _stateClock = double.NaN; }

    public void ApplyState(PersonState2D state, long tick, float moveX, bool shield, bool melee, PersonFrame? animationSample = null)
    {
        // Planted feet stand on the ground, so they ride a moving platform instead of staying where it was.
        var elapsed = double.IsNaN(_stateClock) ? 0 : (float)Math.Clamp(tick / 120.0 - _stateClock, 0, .1);
        if (state.IsGrounded && state.GroundVelocity != Vector2.Zero)
            _hold.Shift(new Vector3(state.GroundVelocity * (float)elapsed / _worldUnitsPerModelUnit, 0));
        _state = state; _clock = _stateClock = tick / 120.0;
        Director.ApplyState(state, Feet(state) / _worldUnitsPerModelUnit, _clock);
        Update(animationSample);
    }

    public void Advance(float dt) { _clock += dt; Director.Advance(dt); _face.Update(_state, dt); Update(); }

    private Vector2 Feet(PersonState2D state) => state.Position - new Vector2(0, _halfHeight);

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
        // A new clip starts with fresh contacts; they are captured again from its first pose.
        if (frame.Key != _drawnKey) { _drawnKey = frame.Key; _hold.Clear(); }
        var facing = s.Facing < 0 ? -1 : 1;
        var pose = _hold.Evaluate(Director.Moves.Model, frame.Clip, Math.Max(0, frame.Seconds), frame.Repeat, Feet(s) / _worldUnitsPerModelUnit, facing, new() { Overlay = frame.Overlay }, frame.Planted);
        Pose = pose;
        var model = Director.Moves.Model; var sockets = model.Base.Sockets;
        _shader.Pose = pose.Local; _shader.Facing = facing; _shader.Face = _face.Pose;
        var seconds = (float)Math.Min(frame.PropSeconds, frame.PropClip.Duration);
        _shader.Props = [.. PersonLoadout.Dressed(frame.PropClip, seconds, frame.Gear, Director.Moves.Hero).Select(w => (Director.Moves.Props[w.Prop], sockets.First(k => k.Id == w.Socket)))];
        RecordSwoosh(frame, seconds, pose.Local);
        _visual.Transform.Position = Feet(s);
        _visual.IsVisible = _enabled && Visible();
    }

    /// <summary>
    /// Feeds the swoosh the held blade while the clip's swoosh markers say it is swinging. A backhand takes the follow-up
    /// mark and turns the other way round.
    /// </summary>
    private void RecordSwoosh(PersonFrame frame, float seconds, EvaluatedPose pose)
    {
        var moves = Director.Moves;
        var clip = frame.PropClip;
        if (frame.Gear != PersonGear.Sword || !PersonLoadout.SwordInHand(clip, seconds)) { _swoosh.Record((float)_clock, default, default, false); return; }
        var sword = moves.Props[PersonLoadout.Sword]; var socket = moves.Model.Base.Sockets.First(k => k.Id == PersonLoadout.SwordSocket);
        if (!_backhands.TryGetValue(clip, out var backhand)) _backhands[clip] = backhand = PersonLoadout.Backhand(moves.Model, clip, sword, socket);
        _swoosh.Sweep = backhand ? 1 : -1; _swoosh.Style = backhand ? SwooshStyle.FollowUp : SwooshStyle.Primary;
        var (guard, tip) = PersonLoadout.Blade(pose, sword, socket);
        _swoosh.Record((float)_clock, guard + SwooshDepth, tip + SwooshDepth, PersonLoadout.Swooshing(clip, seconds));
    }

    public void Dispose() => _scene.Remove(_visual);
}
