using App2d.Core.Validation;
using App2d.Core;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Gameplay.Persons.Actions;

/// <summary>
/// A timed melee overlap placed directly in front of its owner.
/// </summary>
internal sealed partial class MeleeAttack2D(
    EntityId2D sourceId,
    SpatialObject2D worldObject,
    MeleeAttackProfile2D profile)
{
    private float _elapsedSeconds;
    private float _inputBufferSeconds;

    /// <summary>The timing of the current swing.</summary>
    public MeleeAttackProfile2D Profile { get; private set; } = profile;
    /// <summary>A timing for the next swing to start, taken up when it starts (immediately or from the input buffer).</summary>
    public MeleeAttackProfile2D? NextProfile { get; set; }

    /// <summary>The hit box; a profile with its own <see cref="MeleeAttackProfile2D.Shape"/> swaps it in when its swing starts.</summary>
    public SpatialObject2D WorldObject { get; private set; } = worldObject;
    // Identity belongs to this action source, not its owner: punch and kick
    // can have the same attack sequence number without suppressing each other.
    public EntityId2D SourceId { get; } = sourceId.IsValid ? sourceId
        : throw ArgGuard.CreateInvalid("A melee attack requires a valid source ID.", nameof(sourceId));
    public int AttackId { get; private set; }
    public float DurationSeconds => Profile.DurationSeconds;
    public float ElapsedSeconds => _elapsedSeconds;
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
            _inputBufferSeconds = Profile.InputBufferSeconds;
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
        ArgGuard.ThrowIfNotFiniteOrNegative(deltaSeconds);
        ArgGuard.ThrowIfNotFinite(ownerPosition);
        ArgGuard.ThrowIfNotFinite(facing);

        _inputBufferSeconds = Math.Max(0f, _inputBufferSeconds - deltaSeconds);
        IsDamageActive = false;

        if (IsInProgress)
        {
            var previousElapsedSeconds = _elapsedSeconds;
            _elapsedSeconds = Math.Min(
                Profile.DurationSeconds,
                _elapsedSeconds + deltaSeconds);
            IsDamageActive =
                previousElapsedSeconds < Profile.DamageEndSeconds &&
                _elapsedSeconds >= Profile.DamageStartSeconds;

            PositionHitbox(ownerPosition, facing);
            if (_elapsedSeconds >= Profile.DurationSeconds)
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
        if (NextProfile is { } next) { Take(next); NextProfile = null; }
        AttackId++;
        _elapsedSeconds = 0f;
        _inputBufferSeconds = 0f;
        IsInProgress = true;
        IsDamageActive = false;
    }

    private void Take(MeleeAttackProfile2D profile)
    {
        Profile = profile;
        if (profile.Shape is { } shape && !ReferenceEquals(shape, WorldObject.Shape)) WorldObject = new(shape);
    }

    private void PositionHitbox(Vector2 ownerPosition, float facing)
    {
        WorldObject.Transform.Position = ownerPosition +
            new Vector2(facing * Profile.ForwardOffset, Profile.VerticalOffset);
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
        ArgGuard.ThrowIfNotFiniteOrNotPositive(durationSeconds);
        ArgGuard.ThrowIfNotFiniteOrNegative(damageStartSeconds);
        ArgGuard.ThrowIfNotFiniteOrGreaterThanOrEqual(
            damageStartSeconds,
            durationSeconds);
        ArgGuard.ThrowIfNotFiniteOrLessThanOrEqual(
            damageEndSeconds,
            damageStartSeconds);
        ArgGuard.ThrowIfGreaterThan(damageEndSeconds, durationSeconds);
        ArgGuard.ThrowIfNotFiniteOrNegative(inputBufferSeconds);
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
    /// <summary>The swing's own hit box, when it differs from the attack's.</summary>
    public IShape2D? Shape { get; init; }
}
