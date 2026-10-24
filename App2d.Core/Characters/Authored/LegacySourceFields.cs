using System.Text.Json;
using System.Text.Json.Nodes;

namespace App2d.Core.Characters.Authored;

/// <summary>One legacy source field (or field group) the boundary still translates, with the versions that carried it and what replaced it.</summary>
public sealed record LegacySourceField(string Asset, string Versions, string Fields, string Replacement, string Reader);

/// <summary>An authored file still below its kind's current version: an input the boundary's deletion condition counts.</summary>
public sealed record LegacyFile(string Kind, string Path, int Version);

/// <summary>
/// The one named compatibility boundary (plan rule 2; character-system migration, P2c) for legacy source fields in authored
/// files. Every field a versioned reader still translates is listed in <see cref="Entries"/> with the reader that does it;
/// the readers (<see cref="LegacyModelReader"/> for model files below <see cref="CharacterModel.CurrentVersion"/>,
/// <see cref="LegacyVariantReader"/> for variant files below <see cref="ModelVariant.CurrentVersion"/>) rewrite a file's JSON
/// into the current form in memory before the strict parser runs, and nothing writes a file at the current version except
/// its author saving it or an explicit conversion with a report. Callers: <see cref="CharacterModel.FromJson"/> through
/// <see cref="ReadModel"/>, <see cref="ModelVariant.FromJson"/> through <see cref="ReadVariant"/>, and
/// <see cref="VariantConversion"/>, which uses the variant reader's reporting conversion directly. The canonical types carry
/// none of these fields: a part owns its typed payloads (<see cref="Geometry.CurveWave"/>, <see cref="FaceAppearance"/>), a
/// model its constraint list, node kinds and parameters, a variant its typed overrides.
/// </summary>
public static class LegacySourceFields
{
    /// <summary>
    /// Deletion condition for the whole boundary and each reader in it: no model file below version 10 and no variant file
    /// below version 6 remains in the authored tree, the seeds or the tests (<see cref="Remaining"/> lists them; the P11
    /// conversion reports record each bundle converted). Until then the entries stay, in the reader and nowhere else.
    /// </summary>
    public const string DeletionCondition = "No model file below version 10 and no variant file below version 6 remains in the authored tree, the seeds or the tests.";

    public static readonly IReadOnlyList<LegacySourceField> Entries =
    [
        new("model", "5", "part.waveAmplitude, part.waveCycles, part.wavePhaseControl", "part.wave (CurveWave, P2a); a zero amplitude without a phase control becomes no modifier", nameof(LegacyModelReader)),
        new("model", "5-6", "chains; a control's transform without a node kind", "constraints (two-bone-ik entries with the point solver, ahead of typed constraints); node: affine (P3a)", nameof(LegacyModelReader)),
        new("model", "5-7", "build (a code proportion rule)", "refused: parameters with bindings declared in the model (P4a); models/person.json was converted explicitly", nameof(LegacyModelReader)),
        new("model", "5-9", "part.face (expression word), part.faceX", "part.face (FaceAppearance payload, P2b); 'none' or no expression becomes no face member; an explicit null stays an error", nameof(LegacyModelReader)),
        new("variant", "4-5", "parts and attachments dictionaries (one nullable member per property)", "overrides (PropertyOverride entries by PropertyRef, P6a)", nameof(LegacyVariantReader)),
        new("variant", "4-5", "rest (absolute character-frame pins)", "refused by the reader; VariantConversion turns them into parent-local setup overrides with the base (P6b)", nameof(LegacyVariantReader)),
    ];

    /// <summary>A model file's JSON at the current version, or the input unchanged when it already is one.</summary>
    public static string ReadModel(string json) => LegacyModelReader.Read(json);

    /// <summary>A variant file's JSON at the current version, or the input unchanged when it already is one.</summary>
    public static string ReadVariant(string json) => LegacyVariantReader.Read(json);

    /// <summary>The model and variant files under an authored root that still read through the boundary, by kind, path and version.</summary>
    public static IReadOnlyList<LegacyFile> Remaining(string authoredRoot)
    {
        var files = new List<LegacyFile>();
        Scan("models", CharacterModel.FormatId, CharacterModel.CurrentVersion);
        Scan("variants", ModelVariant.FormatId, ModelVariant.CurrentVersion);
        return files;

        void Scan(string folder, string format, int current)
        {
            var directory = Path.Combine(authoredRoot, folder);
            if (!Directory.Exists(directory))
            {
                return;
            }

            foreach (var path in Directory.EnumerateFiles(directory, "*.json").Order(StringComparer.Ordinal))
            {
                JsonObject? root;
                try
                {
                    root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
                }
                catch (JsonException)
                {
                    continue;
                }

                if (root?["format"]?.GetValue<string>() == format && root["version"] is JsonValue value && value.TryGetValue(out int version) && version < current)
                {
                    files.Add(new(folder.TrimEnd('s'), Path.GetRelativePath(authoredRoot, path).Replace('\\', '/'), version));
                }
            }
        }
    }
}
