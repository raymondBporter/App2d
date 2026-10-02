using App2d.Core.Validation;
using App2d.Contracts.World;
using System.Numerics;

namespace App2d.Presentation.Audio;

/// <summary>Client-side zone selection with a short dwell time to prevent border chatter.</summary>
public sealed class WorldMusicDirector2D(WorldSoundtrack2D soundtrack, Action<string, string> select)
{
    public const float ZoneDwellSeconds = .75f;
    private MusicSelection2D? _candidate;
    private float _candidateSeconds;
    private string _pieceOverride = "auto", _moodOverride = "auto";
    public WorldZone2D? CurrentZone { get; private set; }
    public MusicSelection2D? CurrentSelection { get; private set; }

    public string PieceOverride
    {
        get => _pieceOverride;
        set
        {
            if (value != "auto" && !soundtrack.Cues.ContainsKey(value))
                throw new ArgumentException("Use auto, or a cue ID from music_status.");
            if (value != "auto" && _moodOverride != "auto") soundtrack.Validate(new(value, _moodOverride));
            _pieceOverride = value;
        }
    }
    public string MoodOverride
    {
        get => _moodOverride;
        set
        {
            if (value != "auto" && soundtrack.Cues.Values.Any(c => !c.Moods.ContainsKey(value)))
                throw new ArgumentException("Use auto, explore, drive or combat.");
            _moodOverride = value;
        }
    }

    public void Update(LevelContent2D content, Vector2 playerPosition, float deltaSeconds)
    {
        ArgGuard.ThrowIfNegativeOrNotFinite(deltaSeconds);
        CurrentZone = WorldZone2D.FindAt(content.Zones.AsSpan(), playerPosition);
        var authored = soundtrack.ForZone(CurrentZone);
        var desired = new MusicSelection2D(
            _pieceOverride == "auto" ? authored.Piece : _pieceOverride,
            _moodOverride == "auto" ? authored.Mood : _moodOverride);
        if (desired != _candidate)
        {
            _candidate = desired;
            _candidateSeconds = 0;
        }
        else
        {
            _candidateSeconds += deltaSeconds;
        }
        if (desired == CurrentSelection || (CurrentSelection is not null && _candidateSeconds < ZoneDwellSeconds)) return;
        soundtrack.Validate(desired);
        select(desired.Piece, desired.Mood);
        CurrentSelection = desired;
    }
}
