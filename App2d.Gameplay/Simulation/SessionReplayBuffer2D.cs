using App2d.Core;
using System.Collections.Immutable;

namespace App2d.Gameplay.Simulation;

/// <summary>
/// Bounded local input/checkpoint history. The caller owns presenting or discarding replayed facts.
/// Advance through this buffer; clear/recreate it after authoring changes or out-of-band stepping.
/// </summary>
public sealed class SessionReplayBuffer2D
{
    private readonly SideScrollerSession2D _session;
    private readonly int _capacity;
    private readonly List<(PlayerInput2D Input, SessionCheckpoint2D After)> _history = [];
    private SessionCheckpoint2D _oldest;
    private long _timelineRevision;

    public SessionReplayBuffer2D(SideScrollerSession2D session, int capacity = 240)
    {
        _session = ArgGuard.RequireNotNull(session);
        ArgGuard.ThrowIfNotPositive(capacity);
        _capacity = capacity;
        StateGuard.ThrowIf(session.IsPaused, "Start input history after resuming the session.");
        _oldest = session.CaptureCheckpoint();
        _timelineRevision = session.TimelineRevision;
    }

    public long OldestTick => _oldest.Tick;
    public long LatestTick => Latest.Tick;
    public int Count => _history.Count;
    private SessionCheckpoint2D Latest => _history.Count == 0 ? _oldest : _history[^1].After;

    public SessionFrame2D Advance(PlayerInput2D input)
    {
        RequireAligned();
        _session.ValidateCheckpoint(Latest);
        var frame = _session.Advance(input);
        _history.Add((input, _session.CaptureCheckpoint()));
        _timelineRevision = _session.TimelineRevision;
        if (_history.Count > _capacity)
        {
            _oldest = _history[0].After;
            _history.RemoveAt(0);
        }
        return frame;
    }

    public SessionCheckpoint2D GetCheckpoint(long tick)
    {
        if (tick == _oldest.Tick) return _oldest;
        foreach (var entry in _history) if (entry.After.Tick == tick) return entry.After;
        throw new ArgumentOutOfRangeException(nameof(tick), "The tick is outside retained history.");
    }

    public ImmutableArray<SessionFrame2D> ReplayFrom(long tick)
    {
        RequireAligned();
        var checkpoint = GetCheckpoint(tick); // Fail before restore if history has expired.
        _session.RestoreCheckpoint(checkpoint);
        var frames = ImmutableArray.CreateBuilder<SessionFrame2D>();
        for (var i = 0; i < _history.Count; i++)
        {
            var entry = _history[i];
            if (entry.Input.Tick <= tick) continue;
            frames.Add(_session.Advance(entry.Input));
            _history[i] = (entry.Input, _session.CaptureCheckpoint());
        }
        _timelineRevision = _session.TimelineRevision;
        return frames.ToImmutable();
    }

    private void RequireAligned() => StateGuard.ThrowIf(_session.Tick != LatestTick ||
        _session.TimelineRevision != _timelineRevision,
        "The session was advanced, restored, or paused outside this history. Start a new history at the current state.");
}
