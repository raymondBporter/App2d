namespace App2d.Core.Characters;

/// <summary>Preview playback policy; the game can sample clips directly at authoritative action times.</summary>
public sealed class PointPlayback(PointLibrary library, string initialClip)
{
    private string[]? _sequence;
    private int _sequenceIndex;
    private bool _repeatSequence;
    public PointLibrary Library { get; } = library;
    public string ClipId { get; private set; } = library.Clips.ContainsKey(initialClip) ? initialClip : throw new ArgumentException("Unknown clip.");
    public PointClip Clip => Library.Clips[ClipId];
    public double Time { get; private set; }
    public bool Playing { get; set; } = true;
    public bool Finished => Time == Clip.Duration;
    public bool Repeat { get; set; }
    public string? Queued { get; private set; }
    public int SequenceIndex => _sequenceIndex;
    public int SequenceLength => _sequence?.Length ?? 0;

    public void Select(string id, bool atEnd = false)
    {
        if (!Library.Clips.ContainsKey(id)) throw new ArgumentException("Unknown clip.", nameof(id));
        _sequence = null;
        if (atEnd && !Finished) { Queued = id; Playing = true; return; }
        Queued = null; ClipId = id; Time = 0; Playing = true;
    }

    public void Seek(double time)
    {
        if (!double.IsFinite(time)) throw new ArgumentOutOfRangeException(nameof(time));
        Time = Math.Clamp(time, 0, Clip.Duration); Playing = false; Queued = null; _sequence = null;
    }

    public void Step(int direction)
    {
        var time = direction > 0 ? Clip.Times.FirstOrDefault(t => t > Time + 1e-9, Clip.Duration)
            : Clip.Times.LastOrDefault(t => t < Time - 1e-9, 0);
        Seek(time);
    }

    public void StartSequence(IEnumerable<string> ids, bool repeat)
    {
        var sequence = ids.ToArray();
        if (sequence.Length == 0 || sequence.Any(id => !Library.Clips.ContainsKey(id))) throw new ArgumentException("Invalid sequence.");
        Select(sequence[0]); _sequence = sequence; _sequenceIndex = 0; _repeatSequence = repeat;
    }

    public void Advance(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (!Playing) return;
        Time += seconds;
        if (_sequence is not null && _repeatSequence)
        {
            var duration = _sequence.Sum(id => Library.Clips[id].Duration);
            if (Time >= duration) Time %= duration;
        }
        while (Time >= Clip.Duration)
        {
            var carry = Time - Clip.Duration;
            if (Queued is { } queued) { ClipId = queued; Queued = null; Time = carry; }
            else if (_sequence is not null && (_sequenceIndex + 1 < _sequence.Length || _repeatSequence))
            { _sequenceIndex = (_sequenceIndex + 1) % _sequence.Length; ClipId = _sequence[_sequenceIndex]; Time = carry; }
            else if (_sequence is null && (Clip.Loop || Repeat)) { Time %= Clip.Duration; break; }
            else { Time = Clip.Duration; Playing = false; break; }
        }
    }
}
