namespace App2d.Core;

/// <summary>A local creation sequence with a private ID range; rollback cannot affect other sessions.</summary>
public sealed class EntityIdSequence2D
{
    private readonly long _rangeStart = EntityId2D.ReserveRange(int.MaxValue);
    public int Position { get; private set; }

    public EntityId2D Next()
    {
        StateGuard.ThrowIf(Position == int.MaxValue, "The entity creation sequence is exhausted.");
        return new EntityId2D(_rangeStart + ++Position);
    }

    public void Restore(int position)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        Position = position;
    }
}
