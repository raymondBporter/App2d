using App2d.Core;
using App2d.Gameplay.Persons;

namespace App2d.Gameplay.Simulation;

/// <summary>
/// Value-only endpoint for one player with separate sent-input and received-state clocks.
/// Frames must arrive contiguous and in order; a transport that cannot guarantee that
/// re-attaches from a snapshot instead of forcing frames through here.
/// </summary>
public sealed class SessionClient2D
{
    private long _nextInputSequence;
    public EntityId2D PlayerId { get; }
    public long InputTick { get; private set; }
    public long Tick => Snapshot.Tick;
    public long LastInputSequence => State.LastInputSequence;
    public SessionSnapshot2D Snapshot { get; private set; }
    public PlayerState2D State { get; private set; }

    public SessionClient2D(SessionSnapshot2D snapshot, EntityId2D playerId)
    {
        ArgGuard.ThrowIfNull(snapshot);
        ArgumentOutOfRangeException.ThrowIfNegative(snapshot.Tick);
        if (!playerId.IsValid)
            throw new ArgumentException("A player identity is required.", nameof(playerId));
        if (snapshot.Players.IsDefault || snapshot.Content is null || snapshot.World is null || snapshot.Enemies.IsDefault)
            throw new ArgumentException("A complete initial observation is required.", nameof(snapshot));
        State = snapshot.FindPlayer(playerId)
            ?? throw new ArgumentException("The snapshot does not observe this player.", nameof(playerId));
        ArgumentOutOfRangeException.ThrowIfNegative(State.LastInputSequence);
        PlayerId = playerId;
        Snapshot = snapshot;
        InputTick = snapshot.Tick;
        _nextInputSequence = State.LastInputSequence;
    }

    public PlayerInput2D CreateInput(PersonCommand2D command)
    {
        var tick = checked(InputTick + 1);
        var sequence = checked(_nextInputSequence + 1);
        InputTick = tick;
        _nextInputSequence = sequence;
        return new(PlayerId, tick, sequence, command);
    }

    /// <summary>Checks and applies a frame without throwing; nothing changes when it is rejected.</summary>
    public bool TryApply(SessionFrame2D frame, out FrameRejection2D rejection)
    {
        ArgGuard.ThrowIfNull(frame);
        var player = frame.FindPlayer(PlayerId);
        rejection = frame.Tick <= Tick ? FrameRejection2D.Stale
            : frame.Players.IsDefault || frame.Events.IsDefault || frame.Enemies.IsDefault ||
              frame.World is null || frame.Content is null ? FrameRejection2D.Incomplete
            : frame.Tick != Tick + 1 ? FrameRejection2D.Gap
            : player is null ? FrameRejection2D.MissingPlayer
            : player.Value.LastInputSequence < LastInputSequence ? FrameRejection2D.AcknowledgementMovedBackwards
            : FrameRejection2D.None;
        if (rejection != FrameRejection2D.None)
            return false;

        Snapshot = new(frame.Tick, frame.Players, frame.Content, frame.World, frame.Enemies);
        State = player!.Value;
        InputTick = Math.Max(InputTick, frame.Tick);
        _nextInputSequence = Math.Max(_nextInputSequence, State.LastInputSequence);
        return true;
    }

    /// <summary>Local delivery: stale frames return false, anything else rejected is a programming error.</summary>
    public bool Apply(SessionFrame2D frame)
    {
        if (TryApply(frame, out var rejection)) return true;
        if (rejection == FrameRejection2D.Stale) return false;
        throw new InvalidOperationException($"The frame was rejected: {rejection}.");
    }
}
