using System.Text.Json;
using System.Text.Json.Nodes;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// The compatibility boundary for clip files below the current version (character-system migration, P7c). Version 3 could
/// carry <c>faces</c> tracks, the fixed-vocabulary expression channel of the retired procedural face; version 4 has none: a
/// face is an animated part instance and a clip selects its expression through <c>plays</c>. A version-3 file without face
/// tracks reads unchanged at version 4; one with them is refused by name, since the instance that now shows the face is a
/// decision the bundle's conversion records (<see cref="FaceConversion.ConvertPayload"/> rewrote the Person's 28 clips).
/// Deletion condition: no clip file below version 4 remains in the authored tree, the seeds or the tests. The authored tree
/// has none since P7c (`--convert-clips` rewrote the 63 face-less version-3 files at version 4, changing only the version and
/// the empty face list); the conversions and their tests still feed version-3 inputs through here until P11 removes the readers.
/// </summary>
internal static class LegacyClipReader
{
    public const int FaceTracksVersion = 3;

    public static string Read(string json)
    {
        JsonObject? root;
        try
        {
            root = JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return json;
        }

        if (root is null || Find(root, "version") is not (var versionName, JsonValue version) || !version.TryGetValue(out int number) || number != FaceTracksVersion)
        {
            return json;
        }

        if (Find(root, "faces") is (var key, var faces))
        {
            if (faces is JsonArray { Count: > 0 } tracks)
            {
                var parts = string.Join(", ", tracks.OfType<JsonObject>().Select(t => Find(t, "part")?.Node?.ToString() ?? "?"));
                throw new InvalidDataException($"Clip file at version {number} carries face tracks on {parts}, the procedural face channel retired in P7c: convert the bundle (FaceConversion.ConvertPayload, `--convert-face-payload`) so the clip selects the face instance's expression through plays.");
            }

            root.Remove(key);
        }

        root[versionName] = MotionClip.CurrentVersion;
        return root.ToJsonString();
    }

    private static (string Name, JsonNode? Node)? Find(JsonObject owner, string name)
    {
        foreach (var (key, node) in owner)
        {
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            {
                return (key, node);
            }
        }

        return null;
    }
}
