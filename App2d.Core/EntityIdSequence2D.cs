namespace App2d.Core;

/// <summary>
/// A creation sequence over a range reserved from a session allocator. Rollback restores
/// its position so replay reproduces the same IDs without touching the allocator.
/// </summary>
public sealed class EntityIdSequence2D
{
    private readonly long _rangeStart;
    private readonly int _capacity;

    public EntityIdSequence2D(EntityIdAllocator2D ids, int capacity)
    {
        ArgGuard.ThrowIfNull(ids);
        ArgGuard.ThrowIfNotPositive(capacity);
        _rangeStart = ids.ReserveRange(capacity);
        _capacity = capacity;
    }

    public int Position { get; private set; }

    public EntityId2D Next()
    {
        StateGuard.ThrowIf(Position == _capacity, "The entity creation sequence is exhausted.");
        return new EntityId2D(_rangeStart + Position++);
    }

    public void Restore(int position)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(position, _capacity);
        Position = position;
    }
}
