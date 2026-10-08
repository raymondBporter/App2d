using App2d.Core.Validation;

namespace App2d.Contracts.Player;

/// <summary>Shared by the session and its clients; casting art follows progress, not fixed clip lengths.</summary>
public sealed record SpellTuning2D
{
    public int MaximumEnergy { get; init; } = 90;
    public int StartingEnergy { get; init; } = 90;
    public float EnergyPerSecond { get; init; } = 10f;
    public int ShotCost { get; init; } = 30;
    public int HealCost { get; init; } = 30;
    public float ShotChargeSeconds { get; init; } = .25f;
    public float ShotRecoverySeconds { get; init; } = .2f;
    public float HealSeconds { get; init; } = 1f;
    public float HealFraction { get; init; } = .2f;

    public SpellTuning2D Validate()
    {
        ArgGuard.ThrowIfNotPositive(MaximumEnergy);
        ArgGuard.ThrowIfNotInClosedRange(StartingEnergy, 0, MaximumEnergy);
        ArgGuard.ThrowIfNotFiniteOrNegative(EnergyPerSecond);
        ArgGuard.ThrowIfNotInClosedRange(ShotCost, 1, MaximumEnergy);
        ArgGuard.ThrowIfNotInClosedRange(HealCost, 1, MaximumEnergy);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(ShotChargeSeconds);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(ShotRecoverySeconds);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(HealSeconds);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(HealFraction);
        ArgGuard.ThrowIf(HealFraction > 1, "Healing fraction cannot exceed one.");
        return this;
    }
}
