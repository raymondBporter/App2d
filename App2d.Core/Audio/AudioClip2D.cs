using NAudio.Wave;

namespace App2d.Audio;

/// <summary>A decoded sound effect that can be played repeatedly without disk I/O.</summary>
public sealed class AudioClip2D
{

    internal AudioClip2D(string path, int expectedSampleRate)
    {
        using var reader = new AudioFileReader(path);
        if (reader.WaveFormat.SampleRate != expectedSampleRate)
        {
            throw new NotSupportedException($"'{path}' uses {reader.WaveFormat.SampleRate} Hz; sound effects must use {expectedSampleRate} Hz.");
        }

        WaveFormat = reader.WaveFormat;
        if (reader.Length % sizeof(float) != 0)
        {
            throw new InvalidDataException($"Decoded audio length is not float-aligned: '{path}'.");
        }

        var expectedSampleCount = checked((int)(reader.Length / sizeof(float)));
        var samples = GC.AllocateUninitializedArray<float>(expectedSampleCount);
        var sampleCount = 0;
        while (sampleCount < samples.Length)
        {
            var read = reader.Read(samples.AsSpan(sampleCount));
            if (read == 0)
                break;
            sampleCount += read;
        }
        if (sampleCount != samples.Length)
            Array.Resize(ref samples, sampleCount);
        Samples = samples;
    }

    internal WaveFormat WaveFormat { get; }
    internal float[] Samples { get; }
    internal int FrameCount => Samples.Length / WaveFormat.Channels;
}
