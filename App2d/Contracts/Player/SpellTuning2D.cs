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
        Ensure.Positive(MaximumEnergy);
        Ensure.InRange(StartingEnergy, 0, MaximumEnergy);
        Ensure.NonNegative(EnergyPerSecond);
        Ensure.InRange(ShotCost, 1, MaximumEnergy);
        Ensure.InRange(HealCost, 1, MaximumEnergy);
        Ensure.Positive(ShotChargeSeconds);
        Ensure.Positive(ShotRecoverySeconds);
        Ensure.Positive(HealSeconds);
        Ensure.InRange(HealFraction, 0f, 1f, bounds: RangeBounds.OpenClosed);
        return this;
    }
}
