namespace App2d.Core;

/// <summary>
/// Runtime identity, independent of object references and storage positions.
/// Zero means no entity. Locally allocated IDs are never reused within a process;
/// they are not authored level IDs or identities shared across network peers.
/// </summary>
public readonly record struct EntityId2D
{
    private static long _lastValue;

    public EntityId2D(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    public long Value { get; }
    public bool IsValid => Value > 0;
    public static EntityId2D None => default;

    internal static long ReserveRange(int count)
    {
        var end = Interlocked.Add(ref _lastValue, count);
        StateGuard.ThrowIf(end <= 0 || end < count, "Runtime entity IDs have been exhausted.");
        return end - count;
    }

    public static EntityId2D Create()
    {
        var value = Interlocked.Increment(ref _lastValue);
        StateGuard.ThrowIf(value <= 0, "Runtime entity IDs have been exhausted.");
        return new EntityId2D(value);
    }
}
