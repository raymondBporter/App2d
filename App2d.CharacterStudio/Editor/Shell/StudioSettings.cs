using App2d.Core.Json;
using System.Text.Json;

namespace App2d.CharacterStudio.Editor;

/// <summary>
/// The author's preferences for the editor window: the UI scale and the panel sizes. They live per user, outside the authored
/// assets, and a missing or unreadable file means the design defaults.
/// </summary>
internal sealed class StudioSettings
{
    public float UiScale { get; set; } = 1;
    public EditorLayout Layout { get; set; } = new();

    public static StudioSettings Load(string path)
    {
        if (!File.Exists(path))
        {
            return new();
        }

        try
        {
            return JsonSerializer.Deserialize<StudioSettings>(File.ReadAllText(path), JsonProfiles.CamelCaseInsensitive) ?? new();
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return new();
        }
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonProfiles.CamelCase));
    }
}
