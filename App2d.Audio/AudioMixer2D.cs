using App2d.Core;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace App2d.Audio;

/// <summary>
/// A polyphonic mixer for short, memory-resident sound effects. Sounds may be
/// fire-and-forget or adjusted through a lightweight voice handle.
/// </summary>
public sealed class AudioMixer2D : IDisposable
{
    private const int SampleRate = 44_100;
    private const int ChannelCount = 2;
    public const int DefaultMaxConcurrentVoices = 32;

    private readonly WaveOut _output;
    private readonly VoiceMixer _mixer;
    private readonly VolumeSampleProvider _masterVolume;
    private bool _disposed;

    public AudioMixer2D(int maxConcurrentVoices = DefaultMaxConcurrentVoices)
    {
        ArgGuard.ThrowIfNotPositive(maxConcurrentVoices);
        _mixer = new VoiceMixer(maxConcurrentVoices);
        _masterVolume = new VolumeSampleProvider(_mixer);
        _output = new WaveOut();
        _output.Init(_masterVolume);
        _output.Play();
    }

    public int MaxConcurrentVoices => _mixer.MaxConcurrentVoices;
    public int ActiveVoiceCount => _mixer.ActiveVoiceCount;

    public float Volume
    {
        get => _masterVolume.Volume;
        set
        {
            ArgGuard.ThrowIfNotInClosedRange(value, 0f, 1f);
            _masterVolume.Volume = value;
        }
    }

    public AudioClip2D Load(string path)
    {
        ArgGuard.ThrowIfNullOrWhiteSpace(path);
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new AudioClip2D(path, SampleRate);
    }

    public void Play(AudioClip2D clip, float volume = 1f, float playbackRate = 1f)
    {
        ArgGuard.ThrowIfNull(clip);
        ArgGuard.ThrowIfNotInClosedRange(volume, 0f, 1f);
        ArgGuard.ThrowIfNotInClosedRange(playbackRate, 0.5f, 2f);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (volume == 0f)
            return;

        _ = _mixer.Play(clip, volume, playbackRate);
    }

    public AudioVoice2D Begin(
        AudioClip2D clip,
        float volume = 1f,
        float playbackRate = 1f)
    {
        ArgGuard.ThrowIfNull(clip);
        ArgGuard.ThrowIfNotInClosedRange(volume, 0f, 1f);
        ArgGuard.ThrowIfNotInClosedRange(playbackRate, 0.5f, 2f);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var sequence = _mixer.Play(clip, volume, playbackRate);
        return sequence == 0
            ? default
            : new AudioVoice2D(this, sequence);
    }

    internal bool IsVoicePlaying(long sequence) =>
        !_disposed && _mixer.IsPlaying(sequence);

    internal void SetVoiceVolume(
        long sequence,
        float volume,
        float rampSeconds)
    {
        if (!_disposed)
            _mixer.SetVolume(sequence, volume, ToFrameCount(rampSeconds));
    }

    internal void StopVoice(long sequence, float fadeOutSeconds)
    {
        if (!_disposed)
            _mixer.Stop(sequence, ToFrameCount(fadeOutSeconds));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _output.Dispose();
    }

    private static int ToFrameCount(float seconds) =>
        (int)MathF.Ceiling(seconds * SampleRate);

    /// <summary>
    /// Mixes cached clips through a fixed voice pool. Playback only mutates a
    /// preallocated slot, so sound effects do not allocate provider wrappers.
    /// </summary>
    private sealed class VoiceMixer(int maxConcurrentVoices) : ISampleProvider
    {
        private readonly Lock _sync = new();
        private readonly Voice[] _voices = new Voice[maxConcurrentVoices];
        private long _sequence;

        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, ChannelCount);
        public int MaxConcurrentVoices => _voices.Length;

        public int ActiveVoiceCount
        {
            get
            {
                lock (_sync)
                {
                    var count = 0;
                    foreach (var voice in _voices)
                    {
                        if (voice.Clip is not null)
                            count++;
                    }
                    return count;
                }
            }
        }

        public long Play(AudioClip2D clip, float volume, float playbackRate)
        {
            var channels = clip.WaveFormat.Channels;
            if (channels is not 1 and not ChannelCount)
            {
                throw new NotSupportedException($"Sound effects must be mono or stereo, not {channels} channels.");
            }
            if (clip.FrameCount == 0)
                return 0;

            lock (_sync)
            {
                var selectedIndex = 0;
                var oldestSequence = long.MaxValue;
                for (var index = 0; index < _voices.Length; index++)
                {
                    ref var candidate = ref _voices[index];
                    if (candidate.Clip is null)
                    {
                        selectedIndex = index;
                        oldestSequence = long.MinValue;
                        break;
                    }
                    if (candidate.Sequence < oldestSequence)
                    {
                        selectedIndex = index;
                        oldestSequence = candidate.Sequence;
                    }
                }

                var sequence = ++_sequence;
                _voices[selectedIndex] = new Voice(
                    clip,
                    framePosition: 0f,
                    volume,
                    playbackRate,
                    sequence);
                return sequence;
            }
        }

        public bool IsPlaying(long sequence)
        {
            if (sequence == 0)
                return false;

            lock (_sync)
            {
                foreach (var voice in _voices)
                {
                    if (voice.Sequence == sequence && voice.Clip is not null)
                        return true;
                }
                return false;
            }
        }

        public void SetVolume(long sequence, float volume, int rampFrames)
        {
            lock (_sync)
            {
                for (var index = 0; index < _voices.Length; index++)
                {
                    ref var voice = ref _voices[index];
                    if (voice.Sequence != sequence || voice.Clip is null)
                        continue;

                    voice.SetVolume(volume, rampFrames);
                    return;
                }
            }
        }

        public void Stop(long sequence, int fadeFrames)
        {
            lock (_sync)
            {
                for (var index = 0; index < _voices.Length; index++)
                {
                    ref var voice = ref _voices[index];
                    if (voice.Sequence != sequence || voice.Clip is null)
                        continue;

                    if (fadeFrames == 0)
                        voice = default;
                    else
                        voice.BeginStop(fadeFrames);
                    return;
                }
            }
        }

        public int Read(Span<float> buffer)
        {
            buffer.Clear();
            var requestedFrames = buffer.Length / ChannelCount;
            lock (_sync)
            {
                for (var voiceIndex = 0; voiceIndex < _voices.Length; voiceIndex++)
                {
                    ref var voice = ref _voices[voiceIndex];
                    var clip = voice.Clip;
                    if (clip is null)
                        continue;

                    MixVoice(ref voice, requestedFrames, buffer);
                    if (voice.Clip is not null &&
                        voice.FramePosition >= clip.FrameCount)
                    {
                        voice = default;
                    }
                }
            }
            return buffer.Length;
        }

        private static void MixVoice(
            ref Voice voice,
            int requestedFrames,
            Span<float> destination)
        {
            var clip = voice.Clip!;
            var samples = clip.Samples;
            var channels = clip.WaveFormat.Channels;
            for (var frame = 0;
                 frame < requestedFrames && voice.FramePosition < clip.FrameCount;
                 frame++)
            {
                var sourceFrame = (int)voice.FramePosition;
                var nextFrame = Math.Min(sourceFrame + 1, clip.FrameCount - 1);
                var fraction = voice.FramePosition - sourceFrame;
                var destinationIndex = frame * ChannelCount;
                if (channels == 1)
                {
                    var sample = Lerp(samples[sourceFrame], samples[nextFrame], fraction) * voice.Volume;
                    destination[destinationIndex] += sample;
                    destination[destinationIndex + 1] += sample;
                }
                else
                {
                    var sourceIndex = sourceFrame * ChannelCount;
                    var nextIndex = nextFrame * ChannelCount;
                    destination[destinationIndex] += Lerp(
                        samples[sourceIndex],
                        samples[nextIndex],
                        fraction) * voice.Volume;
                    destination[destinationIndex + 1] += Lerp(
                        samples[sourceIndex + 1],
                        samples[nextIndex + 1],
                        fraction) * voice.Volume;
                }
                voice.FramePosition += voice.PlaybackRate;
                if (voice.AdvanceVolume())
                {
                    voice = default;
                    return;
                }
            }
        }

        private static float Lerp(float first, float second, float amount) =>
            first + ((second - first) * amount);

        private struct Voice
        {
            private float _targetVolume;
            private float _volumeStep;
            private int _volumeRampFramesRemaining;
            private bool _stopWhenSilent;

            public Voice(
                AudioClip2D clip,
                float framePosition,
                float volume,
                float playbackRate,
                long sequence)
            {
                Clip = clip;
                FramePosition = framePosition;
                Volume = volume;
                PlaybackRate = playbackRate;
                Sequence = sequence;
                _targetVolume = volume;
            }

            public AudioClip2D? Clip { get; set; }
            public float FramePosition { get; set; }
            public float Volume { get; private set; }
            public float PlaybackRate { get; }
            public long Sequence { get; }

            public void SetVolume(float volume, int rampFrames)
            {
                if (_stopWhenSilent)
                    return;

                SetVolumeCore(volume, rampFrames);
            }

            public void BeginStop(int fadeFrames)
            {
                _stopWhenSilent = true;
                SetVolumeCore(0f, fadeFrames);
            }

            public bool AdvanceVolume()
            {
                if (_volumeRampFramesRemaining > 0)
                {
                    Volume += _volumeStep;
                    _volumeRampFramesRemaining--;
                    if (_volumeRampFramesRemaining == 0)
                        Volume = _targetVolume;
                }

                return _stopWhenSilent &&
                    _volumeRampFramesRemaining == 0 &&
                    Volume == 0f;
            }

            private void SetVolumeCore(float volume, int rampFrames)
            {
                _targetVolume = volume;
                if (rampFrames == 0)
                {
                    Volume = volume;
                    _volumeStep = 0f;
                    _volumeRampFramesRemaining = 0;
                    return;
                }

                _volumeStep = (volume - Volume) / rampFrames;
                _volumeRampFramesRemaining = rampFrames;
            }
        }
    }
}
