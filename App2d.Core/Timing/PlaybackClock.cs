using App2d.Core.Validation;

namespace App2d.Core.Timing;

/// <summary>What a finite playback clock does on reaching its end. Duration is one trip for PingPong.</summary>
public enum PlaybackEndMode { Hold, Loop, PingPong }

/// <summary>
/// A source-independent playhead driven by supplied delta time. An endless clock has no duration;
/// finite clocks hold their last sample, wrap, or reverse at the end. This owns time, not animation data or events.
/// </summary>
public sealed class PlaybackClock
{
    private double _elapsedSeconds;
    private double _speed = 1;
    private bool _seekedToEnd;

    public PlaybackClock(double? durationSeconds = null, PlaybackEndMode endMode = PlaybackEndMode.Hold) =>
        Configure(durationSeconds, endMode);

    public double? DurationSeconds { get; private set; }
    public PlaybackEndMode EndMode { get; private set; }
    public bool IsPlaying { get; private set; }
    public bool IsComplete { get; private set; }

    /// <summary>Time advanced since the last seek or reset, including completed loops.</summary>
    public double ElapsedSeconds => _elapsedSeconds;

    /// <summary>Time within the current trip; this is the time to sample a clip.</summary>
    public double TimeSeconds
    {
        get
        {
            if (DurationSeconds is not { } duration) return _elapsedSeconds;
            if (EndMode == PlaybackEndMode.Hold) return Math.Min(_elapsedSeconds, duration);
            if (_seekedToEnd) return duration;
            if (EndMode == PlaybackEndMode.Loop) return _elapsedSeconds % duration;
            var trip = Math.Floor(_elapsedSeconds / duration);
            var offset = _elapsedSeconds % duration;
            return trip % 2 == 0 ? offset : duration - offset;
        }
    }

    /// <summary>Completed loops, or completed round trips for PingPong.</summary>
    public long Cycles
    {
        get
        {
            if (DurationSeconds is not { } duration || EndMode == PlaybackEndMode.Hold || _seekedToEnd) return 0;
            var cycles = Math.Floor(_elapsedSeconds / duration / (EndMode == PlaybackEndMode.PingPong ? 2 : 1));
            return cycles >= long.MaxValue ? long.MaxValue : (long)cycles;
        }
    }

    /// <summary>Direction along the trip. A ping pong clock reverses at the far end.</summary>
    public int Direction => EndMode == PlaybackEndMode.PingPong && DurationSeconds is { } duration &&
        Math.Floor(_elapsedSeconds / duration) % 2 != 0 ? -1 : 1;

    public double Speed
    {
        get => _speed;
        set { ArgGuard.ThrowIfNotFiniteOrNegative(value); _speed = value; }
    }

    /// <summary>Changes the timeline while keeping the current sampled time and play state.</summary>
    public void Configure(double? durationSeconds, PlaybackEndMode endMode)
    {
        if (durationSeconds is { } duration) ArgGuard.ThrowIfNotFiniteOrNotPositive(duration);
        if (!Enum.IsDefined(endMode)) throw new ArgumentOutOfRangeException(nameof(endMode));
        if (durationSeconds is null && endMode != PlaybackEndMode.Hold)
            throw new ArgumentException("Loop and ping pong require a finite duration.", nameof(endMode));
        if (DurationSeconds == durationSeconds && EndMode == endMode) return;

        var time = TimeSeconds;
        DurationSeconds = durationSeconds;
        EndMode = endMode;
        _elapsedSeconds = durationSeconds is { } limit ? Math.Min(time, limit) : time;
        _seekedToEnd = durationSeconds is { } bound && time >= bound;
        IsComplete = endMode == PlaybackEndMode.Hold && durationSeconds is { } end && _elapsedSeconds >= end;
        if (IsComplete) IsPlaying = false;
    }

    public void Play() { if (!IsComplete) IsPlaying = true; }
    public void Pause() => IsPlaying = false;
    public void Restart() { Stop(); Play(); }

    /// <summary>Stops playback. Keeping the position allows a later Play to resume from it.</summary>
    public void Stop(bool resetToStart = true)
    {
        IsPlaying = false;
        IsComplete = false;
        if (resetToStart) { _elapsedSeconds = 0; _seekedToEnd = false; }
    }

    /// <summary>Seeks within the first trip and discards completed cycles. The exact end remains sampleable.</summary>
    public void Seek(double seconds)
    {
        ArgGuard.ThrowIfNotFiniteOrNegative(seconds);
        _elapsedSeconds = DurationSeconds is { } duration ? Math.Min(seconds, duration) : seconds;
        _seekedToEnd = DurationSeconds is { } end && seconds >= end;
        IsComplete = false;
    }

    public void Advance(double deltaSeconds)
    {
        ArgGuard.ThrowIfNotFiniteOrNegative(deltaSeconds);
        if (!IsPlaying || deltaSeconds == 0 || Speed == 0) return;
        var amount = deltaSeconds * Speed;
        if (!double.IsFinite(amount) || !double.IsFinite(_elapsedSeconds + amount))
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds), "Playback time is too large.");
        _elapsedSeconds += amount;
        _seekedToEnd = false;
        if (EndMode == PlaybackEndMode.Hold && DurationSeconds is { } duration && _elapsedSeconds >= duration)
        {
            _elapsedSeconds = duration;
            IsPlaying = false;
            IsComplete = true;
        }
    }
}
