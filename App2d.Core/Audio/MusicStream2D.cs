using NVorbis;
using System.Collections.Immutable;

namespace App2d.Core.Audio;

/// <summary>One cursor for every layer, including muted layers. Owned by the audio mixer thread.</summary>
internal sealed class MusicStream2D : IDisposable
{
    internal const int BlockFrames = 1024;
    private readonly MusicCue2D _cue;
    private readonly IMusicStem2D[] _stems;
    private readonly float[][] _buffers;
    private readonly float[] _gains;
    private readonly float[] _steps;
    private ImmutableArray<float> _target;
    private ImmutableArray<float> _pending;
    private long _pendingAt = -1;
    private long _absoluteFrame;
    private int _rampRemaining;
    public string? RequestedMood { get; private set; }
    public long FramePosition { get; private set; }

    public MusicStream2D(MusicCue2D cue) : this(cue, Open(cue)) { }

    internal MusicStream2D(MusicCue2D cue, IMusicStem2D[] stems)
    {
        _cue = cue; _stems = stems;
        _buffers = [.. stems.Select(_ => new float[BlockFrames * 2])];
        _gains = new float[stems.Length]; _steps = new float[stems.Length];
    }

    private static IMusicStem2D[] Open(MusicCue2D cue)
    {
        var opened = new List<IMusicStem2D>();
        try
        {
            foreach (var path in cue.StemPaths) opened.Add(new OggMusicStem2D(path, cue.LoopFrames));
            return [.. opened];
        }
        catch { foreach (var stem in opened) stem.Dispose(); throw; }
    }

    public void Restart(string mood)
    {
        var gains = _cue.Moods[mood];
        foreach (var stem in _stems) stem.Rewind();
        FramePosition = 0; _absoluteFrame = 0; _pendingAt = -1; _rampRemaining = 0;
        gains.CopyTo(_gains); _target = gains; RequestedMood = mood;
    }

    public void SetMood(string mood)
    {
        if (mood == RequestedMood) return;
        _pending = _cue.Moods[mood];
        _pendingAt = (_absoluteFrame / _cue.BarFrames + 1) * _cue.BarFrames;
        RequestedMood = mood;
    }

    public void Read(Span<float> destination)
    {
        if (destination.Length % 2 != 0) throw new ArgumentException("Music buffers must contain stereo frames.");
        destination.Clear();
        var written = 0;
        while (written < destination.Length)
        {
            var frames = (int)Math.Min(Math.Min(BlockFrames, (destination.Length - written) / 2), _cue.LoopFrames - FramePosition);
            var samples = frames * 2;
            for (var i = 0; i < _stems.Length; i++)
            {
                var read = 0;
                while (read < samples)
                {
                    var count = _stems[i].Read(_buffers[i], read, samples - read);
                    if (count <= 0 || count > samples - read || count % 2 != 0)
                        throw new InvalidDataException($"Music stem {i} ended before its declared loop boundary.");
                    read += count;
                }
            }
            for (var frame = 0; frame < frames; frame++)
            {
                if (_pendingAt == _absoluteFrame)
                {
                    _target = _pending; _rampRemaining = _cue.BarFrames; _pendingAt = -1;
                    for (var i = 0; i < _stems.Length; i++) _steps[i] = (_target[i] - _gains[i]) / _rampRemaining;
                }
                for (var i = 0; i < _stems.Length; i++)
                {
                    destination[written + frame * 2] += _buffers[i][frame * 2] * _gains[i];
                    destination[written + frame * 2 + 1] += _buffers[i][frame * 2 + 1] * _gains[i];
                    if (_rampRemaining > 0) _gains[i] += _steps[i];
                }
                if (_rampRemaining > 0 && --_rampRemaining == 0) _target.CopyTo(_gains);
                _absoluteFrame++;
            }
            written += samples; FramePosition += frames;
            if (FramePosition == _cue.LoopFrames)
            {
                foreach (var stem in _stems) stem.Rewind();
                FramePosition = 0;
            }
        }
    }

    public void Dispose() { foreach (var stem in _stems) stem.Dispose(); }
}

internal interface IMusicStem2D : IDisposable
{
    int Read(float[] buffer, int offset, int count);
    void Rewind();
}

internal sealed class OggMusicStem2D : IMusicStem2D
{
    private readonly VorbisReader _reader;
    public OggMusicStem2D(string path, long frames)
    {
        _reader = new VorbisReader(path) { ClipSamples = false };
        if (_reader.SampleRate != MusicCue2D.SampleRate || _reader.Channels != 2 || _reader.TotalSamples != frames)
        {
            _reader.Dispose();
            throw new InvalidDataException($"Music stem format or frame count disagrees with its manifest: {path}");
        }
    }
    public int Read(float[] buffer, int offset, int count) => _reader.ReadSamples(buffer, offset, count);
    public void Rewind() => _reader.SamplePosition = 0;
    public void Dispose() => _reader.Dispose();
}
