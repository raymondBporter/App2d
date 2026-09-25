using System.Text.Json;

namespace App2d.Core.Characters;

/// <summary>A part value a variant replaces. Null keeps the base value, which then follows base edits.</summary>
public sealed record PartOverride
{
    public float? Width { get; set; }
    public float? Height { get; set; }
    public float? OffsetX { get; set; }
    public float? OffsetY { get; set; }
    public string? Fill { get; set; }
}

/// <summary>Explicit overrides of one base model. Never structural: no controls, parents or chains.</summary>
public sealed class ModelVariant
{
    public const string FormatId = "app2d-variant";
    public string Format { get; set; } = FormatId;
    public int Version { get; set; } = 1;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Base { get; set; } = "";
    /// <summary>Model-space rest positions replacing the base's, by control ID.</summary>
    public Dictionary<string, PuppetPoint> Rest { get; set; } = [];
    public Dictionary<string, PartOverride> Parts { get; set; } = [];

    public string ToJson() => JsonSerializer.Serialize(this, AuthoredJson.Options);
    public static ModelVariant FromJson(string json) { var variant = AuthoredAsset.Parse<ModelVariant>(json, "variant"); variant.Validate(); return variant; }
    public void Save(string path) { Validate(); AuthoredAsset.Write(path, ToJson()); }

    public void Validate()
    {
        var owner = $"Variant '{Id}'";
        if (Format != FormatId || Version != 1) throw new InvalidDataException($"{owner}: unsupported format/version.");
        AuthoredAsset.RequireId(Id, "variant id"); AuthoredAsset.RequireId(Base, $"{owner} base");
        if (string.IsNullOrWhiteSpace(Name)) throw new InvalidDataException($"{owner}: a name is required.");
        if (Rest is null || Parts is null) throw new InvalidDataException($"{owner}: collections cannot be null.");
        foreach (var (id, point) in Rest) point.Check($"{owner} rest.{id}");
        foreach (var (id, part) in Parts)
        {
            if (part is null) throw new InvalidDataException($"{owner} parts.{id}: null override.");
            if (part.Width is { } width) new Limit(.001f, 100).Check(width, $"{owner} parts.{id}.width");
            if (part.Height is { } height) new Limit(.001f, 100).Check(height, $"{owner} parts.{id}.height");
            if (part.OffsetX is { } x) new Limit(-100, 100).Check(x, $"{owner} parts.{id}.offsetX");
            if (part.OffsetY is { } y) new Limit(-100, 100).Check(y, $"{owner} parts.{id}.offsetY");
            if (part.Fill is not null) Limit.Color(part.Fill, $"{owner} parts.{id}.fill");
        }
    }
}
