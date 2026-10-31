using App2d.CharacterStudio.Authoring;
using System.Text.Json;

namespace App2d.CharacterStudio.Editing;

/// <summary>Read-only recipes under an authored root's templates folder. Template IDs have their own namespace.</summary>
public sealed class ModelTemplateCatalog
{
    private readonly Dictionary<string, ModelTemplate> _entries = new(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, ModelTemplate> Entries => _entries;
    public List<string> Errors { get; } = [];

    public static ModelTemplateCatalog Load(string authoredRoot)
    {
        var catalog = new ModelTemplateCatalog();
        var directory = Path.Combine(authoredRoot, "templates");
        if (!Directory.Exists(directory))
        {
            return catalog;
        }

        foreach (var path in Directory.EnumerateFiles(directory, "*.json").Order(StringComparer.Ordinal))
        {
            try
            {
                var template = ModelTemplate.FromJson(File.ReadAllText(path));
                if (!catalog._entries.TryAdd(template.Id, template))
                {
                    throw new InvalidDataException($"Duplicate template id '{template.Id}'.");
                }
            }
            catch (Exception error) when (error is InvalidDataException or JsonException or IOException or UnauthorizedAccessException or ArgumentException)
            {
                catalog.Errors.Add($"{path}: {error.Message}");
            }
        }
        return catalog;
    }
}
