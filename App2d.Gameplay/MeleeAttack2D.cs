using System.Numerics;
using App2d.Engine;

namespace App2d.Gameplay;

/// <summary>
/// A one-frame melee overlap placed directly in front of its owner.
/// </summary>
internal sealed class MeleeAttack2D(
    SpatialObject2D worldObject,
    MeleeAttackProfile2D profile)
{
    public const float FastDurationSeconds = 0.35f;
    private const float DebugVisibilitySeconds = 0.05f;

    private float _cooldown;
    private float _debugVisibility;
    private bool _hitFramePositioned;

    public SpatialObject2D WorldObject { get; } = worldObject;
    public int AttackId { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsVisible => _debugVisibility > 0f;

    public bool TryStart()
    {
        if (_cooldown > 0f)
            return false;

        AttackId++;
        _cooldown = profile.CooldownSeconds;
        _debugVisibility = DebugVisibilitySeconds;
        _hitFramePositioned = false;
        IsActive = true;
        return true;
    }

    public void Update(float deltaSeconds, Vector2 ownerPosition, float facing)
    {
        _cooldown = Math.Max(0f, _cooldown - deltaSeconds);
        _debugVisibility = Math.Max(0f, _debugVisibility - deltaSeconds);
        if (IsVisible || IsActive)
        {
            WorldObject.Transform.Position = ownerPosition +
                new Vector2(facing * profile.ForwardOffset, profile.VerticalOffset);
            WorldObject.Transform.Rotation = 0f;
        }

        if (!IsActive)
            return;

        if (_hitFramePositioned)
        {
            IsActive = false;
            return;
        }

        _hitFramePositioned = true;
    }

    public void Cancel()
    {
        IsActive = false;
        _debugVisibility = 0f;
    }
}

internal readonly record struct MeleeAttackProfile2D(
    float CooldownSeconds,
    float ForwardOffset,
    float VerticalOffset = 0f);
