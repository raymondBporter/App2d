using App2d.Core;
using System.Numerics;

namespace App2d.Gameplay.Persons;

/// <summary>
/// An immutable observation of a person. Contains no live simulation objects.
/// This is a client view, not a complete checkpoint for rollback.
/// </summary>
public readonly record struct PersonState2D(
    EntityId2D Id,
    Vector2 Position,
    Vector2 LinearVelocity,
    float Facing,
    int HitPoints,
    int MaximumHitPoints,
    float InvulnerabilitySeconds,
    float LandingSpeedThisFrame,
    int BalanceDirection,
    bool IsGrounded,
    bool IsWallGripping,
    bool IsDashing,
    bool IsClimbingLadder,
    bool IsSustainingJump,
    float JumpPower,
    bool IsChargingPrimary)
{
    public PersonActionState2D Action { get; init; }
    public bool IsAlive => HitPoints > 0;
}
