using App2d.Core;

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
        if (current <= 0 || current > Maximum)
        {
            throw new ArgumentOutOfRangeException(
                nameof(current),
                current,
                $"Health must be between 1 and {Maximum}.");
        }

        Current = current;
    }
}
