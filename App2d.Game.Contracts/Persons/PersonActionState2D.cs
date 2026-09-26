using App2d.Gameplay.Simulation;

namespace App2d.Gameplay.Persons;

/// <summary>
/// Authoritative action phase. Duration is gameplay time; a completed shot retains
/// its elapsed age so presentation can finish authored recoil independently.
/// A zero duration denotes no observed action. <see cref="FollowUp"/> marks a sword swing that follows the previous one
/// closely enough that the blade is still out: gameplay decides it, and the drawing plays the matching clip.
/// </summary>
public readonly record struct PersonActionState2D(
    PlayerAttackKind2D Kind, float ElapsedSeconds, float DurationSeconds, bool FollowUp = false)
{
    public bool IsActive => DurationSeconds > 0f && ElapsedSeconds < DurationSeconds;
}
