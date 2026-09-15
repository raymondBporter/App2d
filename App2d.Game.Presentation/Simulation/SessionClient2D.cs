using App2d.Core;
using App2d.Gameplay.Persons;
using App2d.Gameplay.World;

namespace App2d.Gameplay.Simulation;

/// <summary>Value-only endpoint with separate sent-input and received-state clocks.</summary>
public sealed class SessionClient2D
{
    private long _nextInputSequence;
    public long InputTick { get; private set; }
    public long Tick => Snapshot.Tick;
    public long LastInputSequence => Snapshot.LastInputSequence;
    public SessionSnapshot2D Snapshot { get; private set; }
    public PlayerState2D State => Snapshot.Player;

    public SessionClient2D(SessionSnapshot2D snapshot)
    {
        ArgGuard.ThrowIfNull(snapshot);
        ArgumentOutOfRangeException.ThrowIfNegative(snapshot.Tick);
        ArgumentOutOfRangeException.ThrowIfNegative(snapshot.LastInputSequence);
        if (!snapshot.Player.Person.Id.IsValid || snapshot.World is null || snapshot.Enemies.IsDefault)
            throw new ArgumentException("A complete initial observation is required.", nameof(snapshot));
        Snapshot = snapshot;
        InputTick = snapshot.Tick;
        _nextInputSequence = snapshot.LastInputSequence;
    }

    public SessionClient2D(PlayerState2D initialState)
        : this(new SessionSnapshot2D(0, 0, initialState, WorldState2D.Empty, [])) { }

    public PlayerInput2D CreateInput(PersonCommand2D command)
    {
        var tick = checked(InputTick + 1);
        var sequence = checked(_nextInputSequence + 1);
        InputTick = tick;
        _nextInputSequence = sequence;
        return new(State.Person.Id, tick, sequence, command);
    }

    /// <summary>Requires contiguous frames so events cannot be silently lost; duplicates have no effects.</summary>
    public bool Apply(SessionFrame2D frame)
    {
        ArgGuard.ThrowIfNull(frame);
        if (frame.Player.Person.Id != State.Person.Id)
            throw new ArgumentException("The frame belongs to another player.", nameof(frame));
        if (frame.Tick <= Tick) return false;
        StateGuard.ThrowIf(frame.Events.IsDefault || frame.Enemies.IsDefault,
            "A complete frame is required.");
        StateGuard.ThrowIf(frame.Tick != Tick + 1, "Local session frames must be delivered in order without gaps.");
        StateGuard.ThrowIf(frame.LastInputSequence < LastInputSequence,
            "Input acknowledgements cannot move backwards.");
        var world = StateGuard.RequireNotNull(frame.World, "A complete frame is required.");
        Snapshot = new(frame.Tick, frame.LastInputSequence, frame.Player, world, frame.Enemies);
        InputTick = Math.Max(InputTick, frame.Tick);
        _nextInputSequence = Math.Max(_nextInputSequence, frame.LastInputSequence);
        return true;
    }
}
