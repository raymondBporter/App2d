using App2d.Core;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace App2d.Audio;

/// <summary>
/// A fire-and-forget, polyphonic mixer for short, memory-resident sound effects.
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

        _mixer.Play(clip, volume, playbackRate);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _output.Dispose();
    }

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

        public void Play(AudioClip2D clip, float volume, float playbackRate)
        {
            var channels = clip.WaveFormat.Channels;
            if (channels is not 1 and not ChannelCount)
            {
                throw new NotSupportedException($"Sound effects must be mono or stereo, not {channels} channels.");
            }
            if (volume == 0f || clip.FrameCount == 0)
                return;

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

                _voices[selectedIndex] = new Voice(
                    clip,
                    framePosition: 0f,
                    volume,
                    playbackRate,
                    sequence: ++_sequence);
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

                    voice.FramePosition = MixVoice(
                        clip,
                        voice.FramePosition,
                        requestedFrames,
                        voice.Volume,
                        voice.PlaybackRate,
                        buffer);
                    if (voice.FramePosition >= clip.FrameCount)
                        voice = default;
                }
            }
            return buffer.Length;
        }

        private static float MixVoice(
            AudioClip2D clip,
            float framePosition,
            int requestedFrames,
            float volume,
            float playbackRate,
            Span<float> destination)
        {
            var samples = clip.Samples;
            var channels = clip.WaveFormat.Channels;
            for (var frame = 0; frame < requestedFrames && framePosition < clip.FrameCount; frame++)
            {
                var sourceFrame = (int)framePosition;
                var nextFrame = Math.Min(sourceFrame + 1, clip.FrameCount - 1);
                var fraction = framePosition - sourceFrame;
                var destinationIndex = frame * ChannelCount;
                if (channels == 1)
                {
                    var sample = Lerp(samples[sourceFrame], samples[nextFrame], fraction) * volume;
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
                        fraction) * volume;
                    destination[destinationIndex + 1] += Lerp(
                        samples[sourceIndex + 1],
                        samples[nextIndex + 1],
                        fraction) * volume;
                }
                framePosition += playbackRate;
            }
            return framePosition;
        }

        private static float Lerp(float first, float second, float amount) =>
            first + ((second - first) * amount);

        private struct Voice(
            AudioClip2D clip,
            float framePosition,
            float volume,
            float playbackRate,
            long sequence)
        {
            public AudioClip2D? Clip { get; set; } = clip;
            public float FramePosition { get; set; } = framePosition;
            public float Volume { get; } = volume;
            public float PlaybackRate { get; } = playbackRate;
            public long Sequence { get; } = sequence;
        }
    }
}
