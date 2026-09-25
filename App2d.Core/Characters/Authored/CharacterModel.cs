using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text.Json;

namespace App2d.Core.Characters;

/// <summary>Rest is in model space; the parent-local rest offset is derived. Scale names the measure this control's animated translation scales with.</summary>
public sealed record ModelControl
{
    public string Id { get; set; } = "";
    public string? Parent { get; set; }
    public PuppetPoint Rest { get; set; }
    public string Scale { get; set; } = CharacterModel.Unit;
}

/// <summary>A two-bone IK chain. Its end target is keyed in Frame: the locomotion frame, or a control that no chain moves.</summary>
public sealed record ModelChain
{
    public string Id { get; set; } = "";
    public string Root { get; set; } = "";
    public string Joint { get; set; } = "";
    public string End { get; set; } = "";
    public int Bend { get; set; } = 1;
    public string Frame { get; set; } = CharacterModel.Locomotion;
    public string Scale { get; set; } = CharacterModel.Unit;
}

/// <summary>A named length: the sum of rest XY distances along a path of controls, such as leg reach.</summary>
public sealed record ModelMeasure
{
    public string Id { get; set; } = "";
    public List<string> Path { get; set; } = [];
}

/// <summary>A reusable character structure. Controls without a parent hang from the locomotion frame.</summary>
public sealed class CharacterModel
{
    public const string FormatId = "app2d-model", Locomotion = "locomotion", Unit = "unit";
    public string Format { get; set; } = FormatId;
    public int Version { get; set; } = 1;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Changes when controls, parents, chains or frames change; proportions and appearance never change it.</summary>
    public int StructureRevision { get; set; } = 1;
    public string Ink { get; set; } = "#222b32";
    public float LineWidth { get; set; } = .045f;
    public List<ModelControl> Controls { get; set; } = [];
    public List<ModelChain> Chains { get; set; } = [];
    public List<ModelMeasure> Measures { get; set; } = [];
    public List<PuppetPart> Parts { get; set; } = [];

    public string ToJson() => JsonSerializer.Serialize(this, AuthoredJson.Options);
    public static CharacterModel FromJson(string json) { var model = AuthoredAsset.Parse<CharacterModel>(json, "model"); model.Validate(); return model; }
    public static CharacterModel Load(string path) => FromJson(File.ReadAllText(path));
    public void Save(string path) { Validate(); AuthoredAsset.Write(path, ToJson()); }

    private static void Require([DoesNotReturnIf(false)] bool condition, string message) { if (!condition) throw new InvalidDataException(message); }

    public void Validate()
    {
        var owner = $"Model '{Id}'";
        Require(Format == FormatId && Version == 1, $"{owner}: unsupported format/version.");
        AuthoredAsset.RequireId(Id, "model id");
        Require(!string.IsNullOrWhiteSpace(Name), $"{owner}: a name is required.");
        Require(StructureRevision >= 1, $"{owner}: structureRevision must be at least 1.");
        Require(Controls is not null && Chains is not null && Measures is not null && Parts is not null, $"{owner}: collections cannot be null.");
        Require(Controls.Count <= 256 && Chains.Count <= 64 && Measures.Count <= 64 && Parts.Count <= 512, $"{owner}: capacity exceeded.");
        Limit.Color(Ink, "ink"); new Limit(.001f, 1).Check(LineWidth, "lineWidth");

        var controls = new Dictionary<string, ModelControl>(StringComparer.Ordinal);
        foreach (var control in Controls)
        {
            Require(control is not null, $"{owner}: null control.");
            Require(control.Id != Locomotion && control.Id != Unit, $"{owner}: '{control.Id}' is a reserved name.");
            AuthoredAsset.RequireId(control.Id, $"{owner} control id");
            Require(controls.TryAdd(control.Id, control), $"{owner}: duplicate control '{control.Id}'.");
            control.Rest.Check($"{owner} control '{control.Id}' rest");
        }
        foreach (var control in Controls)
            Require(control.Parent is null || control.Parent != control.Id && controls.ContainsKey(control.Parent), $"{owner}: control '{control.Id}' has unknown parent '{control.Parent}'.");
        foreach (var control in Controls)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var current = control; current.Parent is not null; current = controls[current.Parent])
                Require(seen.Add(current.Id), $"{owner}: parent cycle at '{control.Id}'.");
        }

        // Hand-edited files may hold a null where an ID belongs; treat it as unknown rather than letting a lookup throw.
        bool Known(string? id) => id is not null && controls.ContainsKey(id);
        var scales = new HashSet<string>(StringComparer.Ordinal) { Unit };
        foreach (var measure in Measures)
        {
            Require(measure is not null && measure.Path is not null, $"{owner}: incomplete measure.");
            AuthoredAsset.RequireId(measure.Id, $"{owner} measure id");
            Require(measure.Id != Unit && scales.Add(measure.Id), $"{owner}: duplicate or reserved measure '{measure.Id}'.");
            Require(measure.Path.Count >= 2 && measure.Path.All(Known), $"{owner}: measure '{measure.Id}' needs a path of at least two known controls.");
        }
        foreach (var control in Controls) Require(scales.Contains(control.Scale), $"{owner}: control '{control.Id}' uses unknown scale '{control.Scale}'.");

        var chainIds = new HashSet<string>(StringComparer.Ordinal); var solved = new HashSet<string>(StringComparer.Ordinal);
        foreach (var chain in Chains)
        {
            Require(chain is not null, $"{owner}: null chain.");
            AuthoredAsset.RequireId(chain.Id, $"{owner} chain id");
            Require(chainIds.Add(chain.Id), $"{owner}: duplicate chain '{chain.Id}'.");
            Require(chain.Bend is -1 or 1, $"{owner}: chain '{chain.Id}' bend must be -1 or 1.");
            Require(Known(chain.Root) && Known(chain.Joint) && Known(chain.End), $"{owner}: chain '{chain.Id}' references an unknown control.");
            Require(controls[chain.Joint].Parent == chain.Root && controls[chain.End].Parent == chain.Joint, $"{owner}: chain '{chain.Id}' needs two connected bones (root → joint → end).");
            Require(solved.Add(chain.Joint) && solved.Add(chain.End), $"{owner}: chains cannot share solved controls ('{chain.Id}').");
            Require(scales.Contains(chain.Scale), $"{owner}: chain '{chain.Id}' uses unknown scale '{chain.Scale}'.");
            Require(chain.Frame == Locomotion || Known(chain.Frame), $"{owner}: chain '{chain.Id}' frame '{chain.Frame}' is not a control or '{Locomotion}'.");
        }
        IEnumerable<string> SelfAndAncestors(string id) { for (string? current = id; current is not null; current = controls[current].Parent) yield return current; }
        foreach (var chain in Chains)
        {
            Require(!SelfAndAncestors(chain.Root).Any(solved.Contains), $"{owner}: chain '{chain.Id}' is nested inside another chain; nested IK is not supported.");
            Require(chain.Frame == Locomotion || !SelfAndAncestors(chain.Frame).Any(solved.Contains), $"{owner}: chain '{chain.Id}' frame '{chain.Frame}' must not be moved by IK.");
        }

        var parts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in Parts)
        {
            Require(part is not null, $"{owner}: null part.");
            AuthoredAsset.RequireId(part.Id, $"{owner} part id");
            Require(parts.Add(part.Id), $"{owner}: duplicate part '{part.Id}'.");
            part.Validate(Known);
        }
        CheckGeometry(this, Controls.ToDictionary(c => c.Id, c => c.Rest.XYZ, StringComparer.Ordinal), owner);
    }

    /// <summary>Checks lengths that depend on rest geometry, so a variant's overrides are held to the same rules as its base.</summary>
    public static void CheckGeometry(CharacterModel model, IReadOnlyDictionary<string, Vector3> rest, string owner)
    {
        float Length(string a, string b) => Vector2.Distance(new(rest[a].X, rest[a].Y), new(rest[b].X, rest[b].Y));
        foreach (var chain in model.Chains)
            Require(Length(chain.Root, chain.Joint) >= .01f && Length(chain.Joint, chain.End) >= .01f, $"{owner}: chain '{chain.Id}' needs segment lengths of at least 0.01.");
        foreach (var measure in model.Measures)
            Require(measure.Path.Zip(measure.Path.Skip(1)).Sum(p => Length(p.First, p.Second)) >= .001f, $"{owner}: measure '{measure.Id}' has no length.");
    }
}
