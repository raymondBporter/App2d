using App2d.Core.Validation;
using NAudio.Wave;

namespace App2d.Audio;

/// <summary>Streaming music bus, separate from the SFX voice pool. No audio device required.</summary>
public sealed class MusicMixer2D : ISampleProvider, IDisposable
{
    // Control requests never wait for disk reads or decoding. Only Read/Dispose
    // share the render lock; the brief mailbox lock protects a coherent request.
    private readonly Lock _controlSync = new();
    private readonly Lock _renderSync = new();
    private readonly Dictionary<string, MusicCue2D> _cues;
    private readonly Dictionary<string, MusicStream2D> _streams = new(StringComparer.Ordinal);
    private readonly float[] _first = new float[MusicStream2D.BlockFrames * 2];
    private readonly float[] _second = new float[MusicStream2D.BlockFrames * 2];
    private string? _requestedPiece, _requestedMood, _currentPiece;
    private MusicStream2D? _current, _outgoing;
    private int _fadePosition;
    private const int FadeFrames = MusicCue2D.SampleRate * 2;
    private float _volume = .45f, _appliedVolume = float.NaN, _gain, _volumeStep;
    private int _volumeRamp;
    private bool _disposed;

    public MusicMixer2D(IReadOnlyDictionary<string, MusicCue2D> cues) : this(cues, cue => new MusicStream2D(cue)) { }

    internal MusicMixer2D(IReadOnlyDictionary<string, MusicCue2D> cues, Func<MusicCue2D, MusicStream2D> open)
    {
        _cues = new(cues, StringComparer.Ordinal);
        try { foreach (var (id, cue) in cues) _streams.Add(id, open(cue)); }
        catch { Dispose(); throw; }
    }

    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(MusicCue2D.SampleRate, 2);
    public float Volume
    {
        get { lock (_controlSync) return _volume; }
        set { ArgGuard.ThrowIfNotInClosedRange(value, 0f, 1f); lock (_controlSync) _volume = value; }
    }

    public void Select(string piece, string mood)
    {
        lock (_controlSync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_cues.TryGetValue(piece, out var cue) || !cue.Moods.ContainsKey(mood))
                throw new ArgumentException($"Unknown music selection: {piece} / {mood}");
            _requestedPiece = piece; _requestedMood = mood;
        }
    }

    private void SetVolumeRamp(float volume)
    {
        _appliedVolume = volume;
        _volumeRamp = MusicCue2D.SampleRate / 20;
        _volumeStep = (volume - _gain) / _volumeRamp;
    }

    public int Read(Span<float> buffer)
    {
        if (buffer.Length % 2 != 0) throw new ArgumentException("Music buffers must contain stereo frames.");
        lock (_renderSync)
        {
            buffer.Clear();
            string? requestedPiece, requestedMood;
            float volume;
            lock (_controlSync)
            {
                if (_disposed || _requestedPiece is null) return buffer.Length;
                requestedPiece = _requestedPiece; requestedMood = _requestedMood; volume = _volume;
            }
            if (volume != _appliedVolume) SetVolumeRamp(volume);
            var written = 0;
            while (written < buffer.Length)
            {
                // Complete the current crossfade before accepting another piece.
                // Rapid boundary crossings coalesce into the most recent request.
                if (requestedPiece != _currentPiece && _outgoing is null)
                {
                    _outgoing = _current; _current = _streams[requestedPiece];
                    _current.Restart(requestedMood!); _currentPiece = requestedPiece; _fadePosition = 0;
                }
                if (requestedPiece == _currentPiece) _current!.SetMood(requestedMood!);
                var frames = Math.Min(MusicStream2D.BlockFrames, (buffer.Length - written) / 2);
                if (_fadePosition < FadeFrames) frames = Math.Min(frames, FadeFrames - _fadePosition);
                var samples = frames * 2;
                _current!.Read(_first.AsSpan(0, samples));
                _outgoing?.Read(_second.AsSpan(0, samples));
                for (var frame = 0; frame < frames; frame++)
                {
                    var mix = Math.Min(1f, (float)_fadePosition / FadeFrames);
                    // Linear crossfade keeps the combined gains <= 1, preserving headroom.
                    for (var channel = 0; channel < 2; channel++)
                    {
                        var index = frame * 2 + channel;
                        buffer[written + index] = (_first[index] * mix +
                            (_outgoing is null ? 0f : _second[index] * (1 - mix))) * _gain;
                    }
                    if (_fadePosition < FadeFrames) _fadePosition++;
                    if (_volumeRamp > 0) { _gain += _volumeStep; if (--_volumeRamp == 0) _gain = _appliedVolume; }
                }
                if (_fadePosition == FadeFrames) _outgoing = null;
                written += samples;
            }
            return buffer.Length;
        }
    }

    public void Dispose()
    {
        lock (_controlSync)
        {
            if (_disposed) return;
            _disposed = true;
        }
        lock (_renderSync)
        {
            foreach (var stream in _streams.Values) stream.Dispose();
        }
    }
}
