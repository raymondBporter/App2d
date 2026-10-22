using System.Collections.Immutable;

namespace App2d.Core.Audio;

/// <summary>How one cue plays: the clips it alternates between, its level, whether pitch varies per play, and how it carries with distance.</summary>
public sealed record SoundCue2D(ImmutableArray<string> Stems, float Volume)
{
    /// <summary>Slight random pitch on each play; off for sustained cues that would warble.</summary>
    public bool PitchJitter { get; init; } = true;

    /// <summary>Attenuation when played at a position; null takes the spatial sink's default.</summary>
    public SoundFalloff2D? Falloff { get; init; }

    public static SoundCue2D Of(float volume, params string[] stems) => new([.. stems], volume);
}
