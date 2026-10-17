using App2d.Core.Validation;

namespace App2d.Core;

/// <summary>
/// A forward-only creation sequence over a range reserved from a session allocator.
/// Each projectile receives a fresh identity without changing other allocation ranges.
/// </summary>
public sealed class EntityIdSequence2D
{
    private readonly long _rangeStart;
    private readonly int _capacity;

    public EntityIdSequence2D(EntityIdAllocator2D ids, int capacity)
    {
        ArgGuard.ThrowIfNull(ids);
        ArgGuard.Positive(capacity);
        _rangeStart = ids.ReserveRange(capacity);
        _capacity = capacity;
    }

    public int Position { get; private set; }

    public EntityId2D Next()
    {
        StateGuard.ThrowIf(Position == _capacity, "The entity creation sequence is exhausted.");
        return new EntityId2D(_rangeStart + Position++);
    }
}
