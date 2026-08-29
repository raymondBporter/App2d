using NAudio.Wave;

namespace App2d.Engine.Audio;

/// <summary>A decoded sound effect that can be played repeatedly without disk I/O.</summary>
public sealed class AudioClip2D
{
    private readonly float[] _samples;

    internal AudioClip2D(string path, int expectedSampleRate)
    {
        using var reader = new AudioFileReader(path);
        if (reader.WaveFormat.SampleRate != expectedSampleRate)
        {
            throw new NotSupportedException(
                $"'{path}' uses {reader.WaveFormat.SampleRate} Hz; " +
                $"sound effects must use {expectedSampleRate} Hz.");
        }

        WaveFormat = reader.WaveFormat;
        var samples = new List<float>();
        var buffer = new float[16_384];
        int count;
        while ((count = reader.Read(buffer)) > 0)
        {
            for (var i = 0; i < count; i++)
                samples.Add(buffer[i]);
        }
        _samples = [.. samples];
    }

    internal WaveFormat WaveFormat { get; }

    internal ISampleProvider CreateSampleProvider() => new CachedSampleProvider(_samples, WaveFormat);

    private sealed class CachedSampleProvider(float[] samples, WaveFormat waveFormat)
        : ISampleProvider
    {
        private int _position;

        public WaveFormat WaveFormat { get; } = waveFormat;

        public int Read(Span<float> buffer)
        {
            var available = samples.Length - _position;
            var count = Math.Min(available, buffer.Length);
            samples.AsSpan(_position, count).CopyTo(buffer);
            _position += count;
            return count;
        }
    }
}
