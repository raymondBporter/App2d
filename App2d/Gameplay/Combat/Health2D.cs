using App2d.Core.Validation;

namespace App2d.Gameplay.Combat;

public sealed class Health2D
{
    public Health2D(int maximum)
    {
        ArgGuard.ThrowIfNotPositive(maximum);

        Maximum = maximum;
        Current = maximum;
    }

    public int Maximum { get; }
    public int Current { get; private set; }
    public bool IsAlive => Current > 0;

    public int Heal(int amount)
    {
        ArgGuard.ThrowIfNotPositive(amount);
        if (!IsAlive) return 0;
        var restored = Math.Min(amount, Maximum - Current);
        Current += restored;
        return restored;
    }

    public bool Damage(int amount)
    {
        ArgGuard.ThrowIfNotPositive(amount);
        if (!IsAlive)
            return false;

        Current = Math.Max(0, Current - amount);
        return true;
    }

    public void Reset() => Current = Maximum;

    public void Reset(int current)
    {
        ArgGuard.ThrowIfNotInClosedRange(current, 1, Maximum);

        Current = current;
    }
}
