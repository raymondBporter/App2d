using System.Numerics;
using App2d.Engine;

namespace App2d.Gameplay;

/// <summary>
/// Temporary transform-driven melee sweep. Each weapon class supplies its own
/// shape and timing profile; authored hitbox animation can replace this later.
/// </summary>
internal sealed class MeleeAttack2D(
    SpatialObject2D worldObject,
    MeleeAttackProfile2D profile)
{
    public const float FastDurationSeconds = 0.12f;

    private float _elapsed;
    private float _cooldown;

    public SpatialObject2D WorldObject { get; } = worldObject;
    public int AttackId { get; private set; }
    public bool IsActive { get; private set; }

    public bool TryStart()
    {
        if (_cooldown > 0f)
            return false;

        AttackId++;
        _elapsed = 0f;
        _cooldown = profile.CooldownSeconds;
        IsActive = true;
        return true;
    }

    public void Update(float deltaSeconds, Vector2 ownerPosition, float facing)
    {
        _cooldown = Math.Max(0f, _cooldown - deltaSeconds);
        if (!IsActive)
            return;

        _elapsed += deltaSeconds;
        var progress = Math.Clamp(_elapsed / profile.DurationSeconds, 0f, 1f);
        var rightFacingAngle = Lerp(profile.StartAngle, profile.EndAngle, progress);
        var angle = facing >= 0f
            ? rightFacingAngle
            : MathF.PI - rightFacingAngle;
        var orbit = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * profile.OrbitRadius;
        WorldObject.Transform.Position = ownerPosition +
            new Vector2(facing * profile.ForwardOffset, profile.VerticalOffset) + orbit;
        WorldObject.Transform.Rotation = angle;

        if (_elapsed >= profile.DurationSeconds)
            IsActive = false;
    }

    public void Cancel() => IsActive = false;

    private static float Lerp(float start, float end, float amount) =>
        start + (end - start) * amount;
}

internal readonly record struct MeleeAttackProfile2D(
    float DurationSeconds,
    float CooldownSeconds,
    float StartAngle,
    float EndAngle,
    float ForwardOffset = 10f,
    float VerticalOffset = 2f,
    float OrbitRadius = 0f);
