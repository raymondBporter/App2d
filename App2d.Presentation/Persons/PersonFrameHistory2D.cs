namespace App2d.Presentation.Persons;

/// <summary>A short presentation history for sampling animation across clip boundaries without rewinding the actor's position.</summary>
public sealed class PersonFrameHistory2D
{
    private readonly List<(double Time, PersonFrame Frame)> _frames = [];
    public void Clear() => _frames.Clear();

    public void Record(double time, PersonFrame frame)
    {
        // A new authoritative sample can replace frames extrapolated since the previous tick.
        while (_frames.Count > 0 && _frames[^1].Time >= time) _frames.RemoveAt(_frames.Count - 1);
        _frames.Add((time, frame));
        while (_frames.Count > 2 && _frames[1].Time < time - 1) _frames.RemoveAt(0);
    }

    public PersonFrame Sample(double time)
    {
        if (_frames.Count == 0) throw new InvalidOperationException("No animation frames recorded.");
        if (time <= _frames[0].Time) return _frames[0].Frame;
        for (var i = 1; i < _frames.Count; i++)
        {
            var (Time, Frame) = _frames[i];
            if (Time < time) continue;
            if (Time == time) return Frame;
            var a = _frames[i - 1];
            if (a.Frame.Key != Frame.Key || a.Frame.Seconds > Frame.Seconds) return a.Frame;
            var t = (time - a.Time) / (Time - a.Time);
            var overlay = a.Frame.Overlay;
            if (overlay is not null && Frame.Overlay is { } next && overlay.Clip == next.Clip && next.Seconds >= overlay.Seconds)
                overlay = overlay with { Seconds = overlay.Seconds + (next.Seconds - overlay.Seconds) * t };
            return a.Frame with { Seconds = a.Frame.Seconds + (Frame.Seconds - a.Frame.Seconds) * t, Overlay = overlay };
        }
        return _frames[^1].Frame;
    }
}
