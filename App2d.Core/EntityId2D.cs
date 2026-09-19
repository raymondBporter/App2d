namespace App2d.Core;

/// <summary>
/// Runtime identity, independent of object references and storage positions. Zero means
/// no entity. Simulation code receives IDs from a session's <see cref="EntityIdAllocator2D"/>;
/// <see cref="Create"/> is a process-local convenience for tests and diagnostics only.
/// </summary>
public readonly record struct EntityId2D
{
    private static long _lastValue = 1L << 40;

    public EntityId2D(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    public long Value { get; }
    public bool IsValid => Value > 0;
    public static EntityId2D None => default;

    /// <summary>Process-local ID for tests and diagnostics. Never shared across sessions or peers.</summary>
    public static EntityId2D Create()
    {
        var value = Interlocked.Increment(ref _lastValue);
        StateGuard.ThrowIf(value <= 0, "Runtime entity IDs have been exhausted.");
        return new EntityId2D(value);
    }
}
