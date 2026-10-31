namespace App2d.Core.Characters.Authored;

/// <summary>
/// Where a converted asset came from. The source file is never changed; this record is enough to repeat the conversion.
/// Kind <c>puppet</c>: File is the .puppet.json path and Motion the motion's name. Kind <c>library</c>: File is the imported
/// library's id, Motion its clip, Rest the library clip whose first frame stands for the model's rest pose, and Points
/// which source points each control follows (several are averaged).
/// </summary>
public sealed record AssetSource
{
    public const string Puppet = "puppet";
    public const string Library = "library";
    public string Kind { get; set; } = "";
    public string File { get; set; } = "";
    public string? Motion { get; set; }
    public string? Rest { get; set; }
    public Dictionary<string, List<string>>? Points { get; set; }

    public void Validate(string owner)
    {
        EntityVocabulary.Require(Kind, [Puppet, Library], owner + " source kind");
        if (string.IsNullOrWhiteSpace(File))
        {
            throw new InvalidDataException($"{owner} source: a file is required.");
        }

        if (Points?.Any(p => p.Value is null || p.Value.Count == 0 || p.Value.Any(string.IsNullOrWhiteSpace)) == true)
        {
            throw new InvalidDataException($"{owner} source: every mapped control needs at least one source point.");
        }
    }
}
