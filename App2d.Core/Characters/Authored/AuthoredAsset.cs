using System.Text.Json;
using System.Text.RegularExpressions;

namespace App2d.Core.Characters;

/// <summary>Shared rules for authored character files: stable lowercase IDs, strict parsing and atomic per-file writes.</summary>
public static partial class AuthoredAsset
{
    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();

    public static void RequireId(string? id, string field)
    {
        if (id is null || !IdPattern().IsMatch(id))
            throw new InvalidDataException($"{field} must be a lowercase id of letters, digits and hyphens; found '{id}'.");
    }

    public static T Parse<T>(string json, string kind) where T : class =>
        JsonSerializer.Deserialize<T>(json, AuthoredJson.Options) ?? throw new InvalidDataException($"Empty {kind} file.");

    public static void Write(string path, string json)
    {
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, json); File.Move(temporary, path, true);
    }
}
