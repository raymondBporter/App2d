using App2d.Core.Characters;

namespace App2d.Gameplay.Persons;

/// <summary>Presentation-only reactions, independent of body animation resets.</summary>
public sealed class PersonFace2D
{
    private double _clock;
    private float _hit, _landing, _celebrate, _fall;
    private FacePose _pose = FaceExpressions.Get("relaxed");
    public string Expression { get; private set; } = "relaxed";
    public FacePose Pose { get; private set; } = FaceExpressions.Get("relaxed");
    public void Hit() => _hit = .38f;
    public void Land() => _landing = .3f;
    public void Celebrate() => _celebrate = 1.8f;
    public void Reset()
    {
        _clock = 0; _hit = _landing = _celebrate = _fall = 0;
        Expression = "relaxed"; Pose = _pose = FaceExpressions.Get(Expression);
    }
    public void Update(PersonState2D state, float dt)
    {
        _clock += dt;
        _hit = MathF.Max(0, _hit - dt); _landing = MathF.Max(0, _landing - dt); _celebrate = MathF.Max(0, _celebrate - dt);
        _fall = state.IsAlive && !state.IsGrounded && !state.IsWallGripping && !state.IsClimbingLadder && state.LinearVelocity.Y < 0 ? _fall + dt : 0;
        Expression = !state.IsAlive ? "knocked-out"
            : _hit > 0 ? "hurt"
            : _celebrate > 0 ? "delighted"
            : state.IsChargingPrimary ? "determined"
            : state.Action.IsActive || state.IsDashing ? "focused"
            : state.IsWallGripping ? "strained"
            : _fall > .65f ? "panic" : _fall > .25f ? "surprised"
            : _landing > 0 ? "strained"
            : state.HitPoints <= state.MaximumHitPoints * .3f ? "worried"
            : state.IsClimbingLadder || MathF.Abs(state.LinearVelocity.X) > 5 ? "focused"
            : "relaxed";
        var target = FaceExpressions.Get(Expression);
        _pose = !state.IsAlive || _hit > 0 ? target : FacePose.Blend(_pose, target, 1 - MathF.Exp(-18 * dt));
        Pose = Expression is "relaxed" or "focused" or "worried" ? FaceExpressions.Blink(_pose, _clock) : _pose;
    }
}
