using System.Text.Json;
using System.Text.Json.Nodes;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// The compatibility boundary for version-5 model files (P2a of the character-system migration). Version 5 kept a curve's
/// wave as three loose part fields, <c>waveAmplitude</c>, <c>waveCycles</c> and <c>wavePhaseControl</c>; version 6 carries
/// one typed <see cref="Geometry.CurveWave"/> under the part's <c>wave</c> member and no other part has wave fields. This
/// reader rewrites the loose fields into the typed form before the strict parser runs. The file on disk is left alone; it is
/// written at version 6 only when its author saves it. Version 5 applied a wave only for a nonzero amplitude or a phase
/// control, so a part with only <c>waveCycles</c> gets no modifier.
/// Deletion condition: no model file at version 5 remains in the authored tree, the seeds or the tests (the P2c conversion
/// report lists the converted bundles; curve-face is the only shipped wave user).
/// </summary>
internal static class ModelFormat5Reader
{
    public const int Version = 5;
    private const string Amplitude = "waveAmplitude", Cycles = "waveCycles", PhaseControl = "wavePhaseControl", Wave = "wave";

    /// <summary>Returns the model JSON at the current version, or the input unchanged when it is not a version-5 model.</summary>
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

        if (root is null || Find(root, "version") is not (var versionName, JsonValue version) || !version.TryGetValue(out int number) || number != Version)
        {
            return json;
        }

        root[versionName] = CharacterModel.CurrentVersion;
        if (Find(root, "parts") is (_, JsonArray parts))
        {
            foreach (var part in parts.OfType<JsonObject>())
            {
                Convert(part);
            }
        }

        return root.ToJsonString();
    }

    private static void Convert(JsonObject part)
    {
        var amplitude = Take(part, Amplitude);
        var cycles = Take(part, Cycles);
        var phase = Take(part, PhaseControl);
        var active = amplitude is JsonValue height && height.TryGetValue(out float value) && value != 0 || phase is not null;
        if (!active || Find(part, Wave) is not null)
        {
            return;
        }

        var wave = new JsonObject();
        if (amplitude is not null)
        {
            wave["amplitude"] = amplitude;
        }

        if (cycles is not null)
        {
            wave["cycles"] = cycles;
        }

        if (phase is not null)
        {
            wave["phaseControl"] = phase;
        }

        part[Wave] = wave;
    }

    // The strict parser matches member names case-insensitively, so the reader does too.
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

    private static JsonNode? Take(JsonObject owner, string name)
    {
        if (Find(owner, name) is not (var key, var node))
        {
            return null;
        }

        owner.Remove(key);
        return node;
    }
}
