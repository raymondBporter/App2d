using App2d.Core.Validation;
namespace App2d.Core;

/// <summary>
/// Deterministic, session-owned identity allocation. Two sessions built from the same
/// definition in the same construction order allocate the same IDs, which keeps tests
/// and diagnostics reproducible.
/// </summary>
public sealed class EntityIdAllocator2D
{
    public EntityIdAllocator2D(long next = 1)
    {
        ArgGuard.ThrowIfNotPositive(next);
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
        ArgGuard.ThrowIfNotPositive(count);
        var start = Next;
        Next = checked(Next + count);
        return start;
    }
}
