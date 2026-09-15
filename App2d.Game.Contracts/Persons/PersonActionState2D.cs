using App2d.Gameplay.Simulation;

namespace App2d.Gameplay.Persons;

/// <summary>
/// Authoritative action phase. Duration is gameplay time; a completed shot retains
/// its elapsed age so presentation can finish authored recoil independently.
/// A zero duration denotes no observed action.
/// </summary>
public readonly record struct PersonActionState2D(
    PlayerAttackKind2D Kind, float ElapsedSeconds, float DurationSeconds)
{
    public bool IsActive => DurationSeconds > 0f && ElapsedSeconds < DurationSeconds;
}
