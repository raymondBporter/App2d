using App2d.Core;
using System.Numerics;

namespace App2d.Gameplay;

/// <summary>
/// A timed melee overlap placed directly in front of its owner.
/// </summary>
internal sealed class MeleeAttack2D(
    SpatialObject2D worldObject,
    MeleeAttackProfile2D profile)
{
    private float _elapsedSeconds;
    private float _inputBufferSeconds;

    public SpatialObject2D WorldObject { get; } = worldObject;
    public int AttackId { get; private set; }
    public float DurationSeconds => profile.DurationSeconds;
    public bool IsInProgress { get; private set; }
    public bool IsDamageActive { get; private set; }
    public bool IsVisible => IsDamageActive;

    /// <summary>
    /// Starts immediately when available; otherwise buffers the request briefly.
    /// </summary>
    public bool TryStart()
    {
        if (IsInProgress)
        {
            _inputBufferSeconds = profile.InputBufferSeconds;
            return false;
        }

        Start();
        return true;
    }

    /// <summary>
    /// Advances the attack and reports whether a buffered swing began.
    /// </summary>
    public bool Update(float deltaSeconds, Vector2 ownerPosition, float facing)
    {
        ArgGuard.ThrowIfNegativeOrNotFinite(deltaSeconds);
        ArgGuard.ThrowIfNotFinite(ownerPosition);
        ArgGuard.ThrowIfNotFinite(facing);

        _inputBufferSeconds = Math.Max(0f, _inputBufferSeconds - deltaSeconds);
        IsDamageActive = false;

        if (IsInProgress)
        {
            var previousElapsedSeconds = _elapsedSeconds;
            _elapsedSeconds = Math.Min(
                profile.DurationSeconds,
                _elapsedSeconds + deltaSeconds);
            IsDamageActive =
                previousElapsedSeconds < profile.DamageEndSeconds &&
                _elapsedSeconds >= profile.DamageStartSeconds;

            PositionHitbox(ownerPosition, facing);
            if (_elapsedSeconds >= profile.DurationSeconds)
                IsInProgress = false;
        }

        if (IsInProgress || _inputBufferSeconds <= 0f)
            return false;

        Start();
        PositionHitbox(ownerPosition, facing);
        return true;
    }

    public void Cancel()
    {
        _elapsedSeconds = 0f;
        _inputBufferSeconds = 0f;
        IsInProgress = false;
        IsDamageActive = false;
    }

    private void Start()
    {
        AttackId++;
        _elapsedSeconds = 0f;
        _inputBufferSeconds = 0f;
        IsInProgress = true;
        IsDamageActive = false;
    }

    private void PositionHitbox(Vector2 ownerPosition, float facing)
    {
        WorldObject.Transform.Position = ownerPosition +
            new Vector2(facing * profile.ForwardOffset, profile.VerticalOffset);
        WorldObject.Transform.Rotation = 0f;
    }
}

internal readonly record struct MeleeAttackProfile2D
{
    public MeleeAttackProfile2D(
        float durationSeconds,
        float damageStartSeconds,
        float damageEndSeconds,
        float inputBufferSeconds,
        float forwardOffset,
        float verticalOffset = 0f)
    {
        ArgGuard.ThrowIfNotPositive(durationSeconds);
        ArgGuard.ThrowIfNegativeOrNotFinite(damageStartSeconds);
        ArgGuard.ThrowIfGreaterThanOrEqual(
            damageStartSeconds,
            durationSeconds);
        ArgGuard.ThrowIfLessThanOrEqual(
            damageEndSeconds,
            damageStartSeconds);
        if (damageEndSeconds > durationSeconds)
        {
            ArgGuard.ThrowOutOfRange(
                damageEndSeconds,
                "Damage end must not exceed the attack duration.");
        }
        ArgGuard.ThrowIfNegativeOrNotFinite(inputBufferSeconds);
        ArgGuard.ThrowIfNotFinite(forwardOffset);
        ArgGuard.ThrowIfNotFinite(verticalOffset);

        DurationSeconds = durationSeconds;
        DamageStartSeconds = damageStartSeconds;
        DamageEndSeconds = damageEndSeconds;
        InputBufferSeconds = inputBufferSeconds;
        ForwardOffset = forwardOffset;
        VerticalOffset = verticalOffset;
    }

    public float DurationSeconds { get; }
    public float DamageStartSeconds { get; }
    public float DamageEndSeconds { get; }
    public float InputBufferSeconds { get; }
    public float ForwardOffset { get; }
    public float VerticalOffset { get; }
}
