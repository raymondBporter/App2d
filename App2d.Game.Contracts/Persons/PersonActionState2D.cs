using App2d.Gameplay.Simulation;

namespace App2d.Gameplay.Persons;

/// <summary>
/// Authoritative action phase. Duration is gameplay time; a completed shot retains
/// its elapsed age so presentation can finish authored recoil independently.
/// A zero duration denotes no observed action. <see cref="Swing"/> names the authored entity action a sword swing plays
/// (the attack, or the combo swing it chained to): gameplay decides it, and the drawing plays that action's clip.
/// </summary>
public readonly record struct PersonActionState2D(
    PlayerAttackKind2D Kind, float ElapsedSeconds, float DurationSeconds, string? Swing = null)
{
    public bool IsActive => DurationSeconds > 0f && ElapsedSeconds < DurationSeconds;
}
