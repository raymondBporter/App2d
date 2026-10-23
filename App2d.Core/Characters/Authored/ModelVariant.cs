using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.Core.Characters.Authored;

/// <summary>A part value a variant replaces. Null keeps the base value, which then follows base edits.</summary>
public sealed record PartOverride
{
    public float? ScaleX { get; set; }
    public float? ScaleY { get; set; }
    public float? OffsetX { get; set; }
    public float? OffsetY { get; set; }
    public RenderMaterialDefinition2D? Material { get; set; }
    /// <summary>Default expression; "none" removes the face.</summary>
    public string? Face { get; set; }
    public float? FaceX { get; set; }
    public bool? Hidden { get; set; }
    [JsonIgnore]
    public bool IsEmpty => ScaleX is null && ScaleY is null && OffsetX is null && OffsetY is null &&
        Material?.Asset is null && Material?.Paint is null && Material?.Fill is null && Material?.StrokeWidth is null && Material?.Outline?.Width is null && Material?.Outline?.Color is null &&
        Material?.Texture is null && Material?.Tint is null &&
        Face is null && FaceX is null && Hidden is null;

    /// <summary>Range and vocabulary checks shared by variants and look presets.</summary>
    public static void Check(PartOverride? part, string field)
    {
        if (part is null)
        {
            throw new InvalidDataException($"{field}: null override.");
        }

        if (part.ScaleX is { } scaleX && !float.IsFinite(scaleX))
        {
            throw new InvalidDataException(field + ".scaleX must be finite.");
        }

        if (part.ScaleY is { } scaleY && !float.IsFinite(scaleY))
        {
            throw new InvalidDataException(field + ".scaleY must be finite.");
        }

        if (part.OffsetX is { } x)
        {
            new Limit(-100, 100).Check(x, field + ".offsetX");
        }

        if (part.OffsetY is { } y)
        {
            new Limit(-100, 100).Check(y, field + ".offsetY");
        }

        part.Material?.Validate(field + ".material");
        if (part.Face is not null && part.Face != "none" && !FaceExpressions.Contains(part.Face))
        {
            throw new InvalidDataException($"{field}.face: unknown expression '{part.Face}'.");
        }

        if (part.FaceX is { } faceX)
        {
            new Limit(-1, 1).Check(faceX, field + ".faceX");
        }
    }

    /// <summary>This override with <paramref name="over"/>'s set fields written on top.</summary>
    public PartOverride Merge(PartOverride over) => new()
    {
        ScaleX = over.ScaleX ?? ScaleX,
        ScaleY = over.ScaleY ?? ScaleY,
        OffsetX = over.OffsetX ?? OffsetX,
        OffsetY = over.OffsetY ?? OffsetY,
        Material = Material is null ? over.Material : Material.WithOverride(over.Material),
        Face = over.Face ?? Face,
        FaceX = over.FaceX ?? FaceX,
        Hidden = over.Hidden ?? Hidden,
    };
}

/// <summary>Explicit overrides of one base model. Never structural: no controls, parents or chains.</summary>
public sealed class ModelVariant
{
    public const string FormatId = "app2d-variant";
    /// <summary>Version 5 adds <see cref="Attachments"/> overrides (P5a); a version-4 file has none and reads unchanged.</summary>
    public const int CurrentVersion = 5;
    public const int LegacyVersion = 4;
    public string Format { get; set; } = FormatId;
    public int Version { get; set; } = CurrentVersion;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Base { get; set; } = "";
    /// <summary>
    /// Values for the base's declared parameters by ID, applied before the explicit overrides below; a missing value keeps
    /// the declared default. The file member is still <c>build</c>: its meaning (values of the base's exposed proportion
    /// parameters) has not changed since the code build rule became declared bindings in P4a.
    /// </summary>
    [JsonPropertyName("build")]
    public Dictionary<string, float> Parameters { get; set; } = [];
    /// <summary>Model-space rest positions replacing the built base's, by control ID.</summary>
    public Dictionary<string, PuppetPoint> Rest { get; set; } = [];
    public Dictionary<string, PartOverride> Parts { get; set; } = [];
    /// <summary>Overrides of the base's attachment instances by instance ID: swap the asset, hide, re-place or refit it on this wearer alone.</summary>
    public Dictionary<string, AttachmentOverride> Attachments { get; set; } = [];

    public string ToJson() => JsonSerializer.Serialize(this, AuthoredJson.Options);
    public static ModelVariant FromJson(string json)
    {
        var variant = AuthoredAsset.Parse<ModelVariant>(ReadLegacy(json), "variant");
        variant.Validate();
        return variant;
    }

    /// <summary>A version-4 file carries no attachment overrides; it is read at the current version without any other change. The file on disk is not rewritten.</summary>
    internal static string ReadLegacy(string json)
    {
        System.Text.Json.Nodes.JsonObject? root;
        try
        {
            root = System.Text.Json.Nodes.JsonNode.Parse(json) as System.Text.Json.Nodes.JsonObject;
        }
        catch (JsonException)
        {
            return json;
        }

        var key = root?.FirstOrDefault(p => string.Equals(p.Key, "version", StringComparison.OrdinalIgnoreCase)).Key;
        if (root is null || key is null || root[key] is not System.Text.Json.Nodes.JsonValue value || !value.TryGetValue(out int version) || version != LegacyVersion)
        {
            return json;
        }

        root[key] = CurrentVersion;
        return root.ToJsonString();
    }
    public void Save(string path)
    {
        Validate();
        AuthoredAsset.Write(path, ToJson());
    }

    public void Validate()
    {
        var owner = $"Variant '{Id}'";
        if (Format != FormatId || Version != CurrentVersion)
        {
            throw new InvalidDataException($"{owner}: unsupported format/version.");
        }

        AuthoredAsset.RequireId(Id, "variant id");
        AuthoredAsset.RequireId(Base, $"{owner} base");
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidDataException($"{owner}: a name is required.");
        }

        if (Parameters is null || Rest is null || Parts is null || Attachments is null)
        {
            throw new InvalidDataException($"{owner}: collections cannot be null.");
        }

        foreach (var (id, change) in Attachments)
        {
            AuthoredAsset.RequireId(id, $"{owner} attachments key");
            if (change is null)
            {
                throw new InvalidDataException($"{owner} attachments.{id}: null override.");
            }

            change.Validate($"{owner} attachments.{id}");
        }

        foreach (var (id, value) in Parameters)
        {
            new Limit(.01f, 100).Check(value, $"{owner} build.{id}");
        }

        foreach (var (id, point) in Rest)
        {
            point.Check($"{owner} rest.{id}");
        }

        foreach (var (id, part) in Parts)
        {
            PartOverride.Check(part, $"{owner} parts.{id}");
        }
    }
}
