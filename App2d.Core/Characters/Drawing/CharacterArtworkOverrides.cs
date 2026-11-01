namespace App2d.Core.Characters.Drawing;

/// <summary>Per-draw artwork replacements. Null suppresses a piece; the shared model and pose stay unchanged.</summary>
public sealed class CharacterArtworkOverrides
{
    public Dictionary<string, PartGeometry?> Parts { get; } = new(StringComparer.Ordinal);
    public HashSet<string> HiddenSockets { get; } = new(StringComparer.Ordinal);
    public PartGeometry? Apply(PartGeometry part) => Parts.TryGetValue(part.Id, out var replacement) ? replacement : part;
}
