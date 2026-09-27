using NAudio.Wave;

namespace App2d.Audio;

/// <summary>Owns the device for the streaming music bus; SFX cannot evict its voices.</summary>
public sealed class MusicPlayer2D : IDisposable
{
    private readonly MusicMixer2D _mixer;
    private readonly WaveOut _output;
    private bool _disposed;

    public MusicPlayer2D(IReadOnlyDictionary<string, MusicCue2D> cues)
    {
        _mixer = new(cues);
        _output = new WaveOut();
        try { _output.Init(_mixer); _output.Play(); }
        catch { _output.Dispose(); _mixer.Dispose(); throw; }
    }

    public float Volume { get => _mixer.Volume; set => _mixer.Volume = value; }
    public void Select(string piece, string mood) => _mixer.Select(piece, mood);
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _output.Dispose(); _mixer.Dispose();
    }
}
