using App2d.Core;
using App2d.Core.Characters;
using App2d.Core.Geometry;
using App2d.Gameplay.Persons.Actions;
using App2d.Gameplay.Player;
using App2d.Gameplay.Simulation;
using App2d.Rendering;
using App2d.Rendering.Characters;
using System.Numerics;

namespace App2d.Gameplay.Persons;

/// <summary>Observes the existing traversal/controller; all poses come from the authored player type.</summary>
public sealed class PointPersonPresentation2D : IDisposable
{
    private readonly Scene2D _scene;
    private readonly WorldObject2D _visual;
    private readonly PointCharacterShader _shader;
    private readonly float _halfHeight;
    private PersonState2D _state;
    private EquipmentKind2D _equipment;
    private double _clock;
    private float _sinceState, _reaction;
    private string? _reactionId;
    private string _lastLocomotion = "idle";
    private double _locomotionStart, _landUntil;
    private readonly PersonFace2D _face = new();
    public PointPersonPresentation2D(Scene2D scene, EntityCatalog catalog, TraversalMetrics2D traversal)
    {
        _scene = scene; _halfHeight = traversal.PlayerColliderSize.Y / 2;
        _shader = new(catalog, catalog.Types["player"]);
        _visual = new(AxisAlignedRectangle2D.FromSize(new(10, 10), new(0, 2)), _shader) { ZIndex = 1 };
        _visual.Transform.Scale = new(EntityCatalog.WorldUnits);
        scene.Add(_visual);
    }
    public void Equip(EquipmentKind2D equipment) => _equipment = equipment;
    public void PlayHit() { _reactionId = "hit"; _reaction = 0; _face.Hit(); }
    public void PlayLanding() => _face.Land();
    public void PlayCelebrate() => _face.Celebrate();
    public void PlayDeath() { _reactionId = "death"; _reaction = 0; }
    public void Reset() { _reactionId = null; _reaction = 0; _sinceState = 0; _landUntil = 0; _lastLocomotion = "idle"; _locomotionStart = _clock; _face.Reset(); }
    public void ApplyState(PersonState2D state, long tick, float moveX, bool shield, bool melee)
    {
        _state = state; _clock = tick / 120.0; _sinceState = 0;
        if (state.LandingSpeedThisFrame > 0 && _shader.Type.Actions.TryGetValue("land", out var land)) _landUntil = _clock + land.Duration;
        Update();
    }
    public void Advance(float dt) { _clock += dt; _sinceState += dt; _reaction += dt; _face.Update(_state, dt); Update(); }
    private void Update()
    {
        var s = _state; var type = _shader.Type;
        _face.Update(s, 0); _shader.Face = _face.Pose;
        string action;
        double time = _clock;
        var locomotion = false;
        if (!s.IsAlive) { action = "death"; time = _reaction; }
        else if (_reactionId == "hit" && _reaction < type.Actions["hit"].Duration) { action = "hit"; time = _reaction; }
        else if (s.Action.IsActive)
        {
            action = s.Action.Kind switch
            {
                PlayerAttackKind2D.Shot => s.IsWallGripping ? "wall_shot" : "shoot",
                PlayerAttackKind2D.Downward => "down_attack",
                _ => s.IsWallGripping ? "wall_attack" : "attack"
            };
            var duration = type.Actions.GetValueOrDefault(action)?.Duration ?? type.Actions["attack"].Duration;
            time = (s.Action.ElapsedSeconds + _sinceState) / s.Action.DurationSeconds * duration;
            if (s.Action.Kind == PlayerAttackKind2D.Shot)
                time = (type.Actions[action].Contact + (1 - type.Actions[action].Contact) * Math.Clamp((s.Action.ElapsedSeconds + _sinceState) / s.Action.DurationSeconds, 0, 1)) * duration;
        }
        else if (_equipment == EquipmentKind2D.Gun && (s.IsChargingPrimary || s.IsGrounded && Math.Abs(s.LinearVelocity.X) < 5))
        { action = s.IsWallGripping ? "wall_shot" : "shoot"; time = type.Actions[action].Contact * type.Actions[action].Duration; }
        else
        {
            locomotion = true;
            if (s.IsClimbingLadder) action = "climb";
            else if (s.IsWallGripping) action = "wall_grip";
            else if (!s.IsGrounded) action = s.LinearVelocity.Y > 0 ? "jump" : "fall";
            else if (Math.Abs(s.LinearVelocity.X) > 5) action = "walk";
            else if (_clock < _landUntil) action = "land";
            else if (s.BalanceDirection != 0) action = s.BalanceDirection == Math.Sign(s.Facing) ? "balance_forward" : "balance_backward";
            else action = "idle";
        }
        if (_lastLocomotion != action) { _lastLocomotion = action; _locomotionStart = _clock; }
        if (locomotion) time = _clock - _locomotionStart;
        _shader.Action = type.Actions.ContainsKey(action) ? action : "idle";
        _shader.Seconds = time; _shader.FacingLeft = s.Facing < 0;
        _shader.Weapon = _equipment switch { EquipmentKind2D.Gun => "pistol", EquipmentKind2D.Unarmed => "none", _ => type.Appearance.Weapon };
        _visual.Transform.Position = s.Position - new Vector2(0, _halfHeight);
        _visual.IsVisible = s.InvulnerabilitySeconds <= 0 || ((int)(_clock * 20) & 1) == 0 || !s.IsAlive;
    }
    public void Dispose() => _scene.Remove(_visual);
}
