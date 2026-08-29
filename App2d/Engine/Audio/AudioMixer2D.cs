using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace App2d.Engine.Audio;

/// <summary>
/// A fire-and-forget, polyphonic mixer for short, memory-resident sound effects.
/// </summary>
public sealed class AudioMixer2D : IDisposable
{
    private const int SampleRate = 44_100;
    private const int ChannelCount = 2;

    private readonly WaveOut _output;
    private readonly MixingSampleProvider _mixer;
    private readonly VolumeSampleProvider _masterVolume;
    private bool _disposed;

    public AudioMixer2D()
    {
        _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, ChannelCount))
        {
            ReadFully = true
        };
        _masterVolume = new VolumeSampleProvider(_mixer);
        _output = new WaveOut();
        _output.Init(_masterVolume);
        _output.Play();
    }

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

    public void Play(AudioClip2D clip, float volume = 1f)
    {
        ArgGuard.ThrowIfNull(clip);
        ArgGuard.ThrowIfNotInClosedRange(volume, 0f, 1f);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var provider = clip.CreateSampleProvider();
        ISampleProvider stereoProvider = provider.WaveFormat.Channels switch
        {
            ChannelCount => provider,
            1 => new MonoToStereoSampleProvider(provider),
            _ => throw new NotSupportedException($"Sound effects must be mono or stereo, not {provider.WaveFormat.Channels} channels.")
        };

        _mixer.AddMixerInput(new VolumeSampleProvider(stereoProvider)
        {
            Volume = volume
        });
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _output.Dispose();
    }
}
