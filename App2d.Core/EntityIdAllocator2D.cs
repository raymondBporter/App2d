namespace App2d.Core;

/// <summary>
/// Deterministic, session-owned identity allocation. Two sessions built from the same
/// definition in the same construction order allocate the same IDs, so a server and a
/// predicting client agree on identities without exchanging them.
/// </summary>
public sealed class EntityIdAllocator2D
{
    public EntityIdAllocator2D(long next = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(next);
        Next = next;
    }

    /// <summary>The value the next allocation returns.</summary>
    public long Next { get; private set; }

    public EntityId2D Allocate()
    {
        var id = new EntityId2D(Next);
        Next = checked(Next + 1);
        return id;
    }

    /// <summary>Reserves <paramref name="count"/> consecutive values and returns the first.</summary>
    public long ReserveRange(long count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        var start = Next;
        Next = checked(Next + count);
        return start;
    }
}
