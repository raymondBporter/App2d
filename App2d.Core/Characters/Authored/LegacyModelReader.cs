using System.Text.Json;
using System.Text.Json.Nodes;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// The compatibility boundary for model files below the current version. It rewrites the JSON into the current form before
/// the strict parser runs; the file on disk is left alone and is written at the current version only when its author saves it.
/// <list type="bullet">
/// <item>Version 5 (P2a) kept a curve's wave as three loose part fields, <c>waveAmplitude</c>, <c>waveCycles</c> and
/// <c>wavePhaseControl</c>; they become one typed <see cref="Geometry.CurveWave"/> under the part's <c>wave</c> member.
/// Version 5 applied a wave only for a nonzero amplitude or a phase control, so a part with only <c>waveCycles</c> gets
/// no modifier.</item>
/// <item>Versions 5 and 6 (P3a) carried point IK in a separate <c>chains</c> list and selected a control's setup semantics
/// from whether <c>transform</c> was present. The chains join the front of <c>constraints</c> as <c>two-bone-ik</c> entries
/// with their point solver, which keeps the former evaluation order (chains first, then typed constraints), and a control
/// with a <c>transform</c> is declared <c>node: affine</c>; every other control is a point control, the default.</item>
/// </list>
/// Deletion condition: no model file below version 7 remains in the authored tree, the seeds or the tests (the P11
/// conversion reports list the converted bundles; six of the seven shipped models are still at version 5).
/// </summary>
internal static class LegacyModelReader
{
    public const int WaveFieldsVersion = 5, ChainsVersion = 6;
    private const string Amplitude = "waveAmplitude", Cycles = "waveCycles", PhaseControl = "wavePhaseControl", Wave = "wave";

    /// <summary>Returns the model JSON at the current version, or the input unchanged when it is not an older model.</summary>
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

        if (root is null || Find(root, "version") is not (var versionName, JsonValue version) || !version.TryGetValue(out int number) || number is < WaveFieldsVersion or > ChainsVersion)
        {
            return json;
        }

        root[versionName] = CharacterModel.CurrentVersion;
        if (number == WaveFieldsVersion && Find(root, "parts") is (_, JsonArray parts))
        {
            foreach (var part in parts.OfType<JsonObject>())
            {
                ConvertWave(part);
            }
        }

        ConvertChains(root);
        if (Find(root, "controls") is (_, JsonArray controls))
        {
            foreach (var control in controls.OfType<JsonObject>())
            {
                if (Find(control, "transform") is not null && Find(control, "node") is null)
                {
                    control["node"] = RigNodeKinds.Affine;
                }
            }
        }

        return root.ToJsonString();
    }

    private static void ConvertChains(JsonObject root)
    {
        if (Take(root, "chains") is not JsonArray chains)
        {
            return;
        }

        var found = Find(root, "constraints");
        var constraints = found is (_, JsonArray existing) ? existing : null;
        var merged = new JsonArray();
        foreach (var chain in chains.OfType<JsonObject>().ToArray())
        {
            chains.Remove(chain);
            if (Find(chain, "kind") is null)
            {
                chain.Insert(0, "kind", "two-bone-ik");
            }

            merged.Add(chain);
        }
        if (constraints is not null)
        {
            foreach (var constraint in constraints.ToArray())
            {
                constraints.Remove(constraint);
                merged.Add(constraint);
            }

            root[found!.Value.Name] = merged;
        }
        else
        {
            root["constraints"] = merged;
        }
    }

    private static void ConvertWave(JsonObject part)
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
