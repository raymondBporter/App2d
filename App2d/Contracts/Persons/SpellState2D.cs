namespace App2d.Contracts.Persons;

/// <summary>Observed spell progress and resource values, including presentation timing.</summary>
public readonly record struct SpellState2D(
    int Energy, int MaximumEnergy, int ShotCost, int HealCost,
    bool IsHealing, float HealProgress, float ChargeProgress,
    float HealSeconds, float ChargeSeconds)
{
    public bool Enabled => MaximumEnergy > 0;
}
