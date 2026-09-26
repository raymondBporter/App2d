using App2d.Core;
using App2d.Core.Characters;
using App2d.Core.Geometry;
using App2d.Gameplay.Persons.Actions;
using App2d.Gameplay.Player;
using App2d.Rendering;
using App2d.Rendering.Characters;
using System.Numerics;

namespace App2d.Gameplay.Persons;

/// <summary>
/// Draws the player as the authored Person with the player move set. Observes the existing traversal/controller state;
/// <see cref="PersonAnimationDirector"/> picks the clip, a <see cref="ContactHold"/> keeps planted feet planted while the
/// body moves, and <see cref="PersonLoadout"/> decides where the sheath, sword and pistol sit. The figure is scaled so its
/// rest height matches the traversal collider.
/// </summary>
public sealed class AuthoredPersonPresentation2D : IDisposable
{
    private readonly Scene2D _scene;
    private readonly WorldObject2D _visual;
    private readonly AuthoredCharacterShader _shader;
    private readonly PersonAnimationDirector _director;
    private readonly ContactHold _hold = new();
    private readonly PersonFace2D _face = new();
    private readonly float _halfHeight, _pixelsPerUnit;
    private PersonState2D _state;
    private double _clock;
    private string _drawnKey = "";

    public AuthoredPersonPresentation2D(Scene2D scene, PersonMoves moves, TraversalMetrics2D traversal)
    {
        _scene = scene; _halfHeight = traversal.PlayerColliderSize.Y / 2;
        _pixelsPerUnit = traversal.PlayerColliderSize.Y / RestHeight(moves.Model);
        _director = new(moves, _pixelsPerUnit);
        _shader = new(moves.Model);
        _visual = new(AxisAlignedRectangle2D.FromSize(new(10, 10), new(0, 2)), _shader) { ZIndex = 1 };
        _visual.Transform.Scale = new(_pixelsPerUnit);
        scene.Add(_visual);
    }

    public PersonAnimationDirector Director => _director;
    public ActorPose? Pose { get; private set; }

    /// <summary>The top of the rest pose's drawn shapes above the feet.</summary>
    public static float RestHeight(ResolvedModel model)
    {
        var rest = PoseEvaluator.Rest(model);
        return model.Parts.Where(p => !p.Hidden).SelectMany(p => PartGeometry.Contour(p, rest.World)).Max(p => p.Y);
    }

    public void Equip(EquipmentKind2D equipment) => _director.Equipment = equipment;
    public void PlayHit() { _director.PlayHit(); _face.Hit(); }
    public void PlayLanding() => _face.Land();
    public void PlayCelebrate() { _director.PlayCelebrate(); _face.Celebrate(); }
    public void PlayDeath() => _director.PlayDeath();
    public void Reset() { _director.Reset(); _hold.Clear(); _face.Reset(); _drawnKey = ""; }

    public void ApplyState(PersonState2D state, long tick, float moveX, bool shield, bool melee)
    {
        _state = state; _clock = tick / 120.0;
        _director.ApplyState(state, Feet(state) / _pixelsPerUnit, _clock);
        Update();
    }

    public void Advance(float dt) { _clock += dt; _director.Advance(dt); _face.Update(_state, dt); Update(); }

    private Vector2 Feet(PersonState2D state) => state.Position - new Vector2(0, _halfHeight);

    private void Update()
    {
        var s = _state;
        _face.Update(s, 0);
        var frame = _director.Frame();
        // A new clip starts with fresh contacts; they are captured again from its first pose.
        if (frame.Key != _drawnKey) { _drawnKey = frame.Key; _hold.Clear(); }
        var facing = s.Facing < 0 ? -1 : 1;
        var pose = _hold.Evaluate(_director.Moves.Model, frame.Clip, Math.Max(0, frame.Seconds), frame.Repeat, Feet(s) / _pixelsPerUnit, facing, new() { Overlay = frame.Overlay }, frame.Planted);
        Pose = pose;
        var model = _director.Moves.Model; var sockets = model.Base.Sockets;
        _shader.Pose = pose.Local; _shader.Facing = facing; _shader.Face = _face.Pose;
        _shader.Props = [.. PersonLoadout.Worn(frame.Clip, (float)Math.Min(frame.Seconds, frame.Clip.Duration), frame.Gear)
            .Select(w => (_director.Moves.Props[w.Prop], sockets.First(k => k.Id == w.Socket)))];
        _visual.Transform.Position = Feet(s);
        _visual.IsVisible = s.InvulnerabilitySeconds <= 0 || ((int)(_clock * 20) & 1) == 0 || !s.IsAlive;
    }

    public void Dispose() => _scene.Remove(_visual);
}
