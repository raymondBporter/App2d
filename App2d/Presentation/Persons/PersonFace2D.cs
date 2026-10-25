using App2d.Contracts.Persons;

namespace App2d.Presentation.Persons;

/// <summary>
/// Presentation-only reactions, independent of body animation resets: the semantic expression the Person asks its face for
/// (P7c). The face itself is the animated library part instanced on the model; its asset binds each word to a pose, blinks on
/// its own layer and hides for a back view through the clip's selection channel, so nothing here draws or blends features.
/// </summary>
public sealed class PersonFace2D
{
    private float _hit, _landing, _celebrate, _fall;
    public string Expression { get; private set; } = "relaxed";
    public void Hit() => _hit = .38f;
    public void Land() => _landing = .3f;
    public void Celebrate() => _celebrate = 1.8f;
    public void Reset()
    {
        _hit = _landing = _celebrate = _fall = 0;
        Expression = "relaxed";
    }
    public void Update(PersonState2D state, float dt)
    {
        _hit = MathF.Max(0, _hit - dt);
        _landing = MathF.Max(0, _landing - dt);
        _celebrate = MathF.Max(0, _celebrate - dt);
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
    }
}
