using System.Text.Json;
using App2d.Core.Json;

namespace App2d.Core.Characters.Authored;

/// <summary>A named, independently saved render surface.</summary>
public sealed record RenderMaterialAsset2D
{
    public string Format { get; set; } = "app2d-render-material";
    public int Version { get; set; } = 1;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public RenderMaterialDefinition2D Material { get; set; } = new() { Fill = "#b8bdc5" };

    public void Validate()
    {
        AuthoredAsset.RequireId(Id, "render material id");
        if (Format != "app2d-render-material" || Version != 1) throw new InvalidDataException("Unsupported render material format/version.");
        if (string.IsNullOrWhiteSpace(Name)) throw new InvalidDataException("Render material needs a name.");
        if (Material is null || Material.Asset is not null) throw new InvalidDataException("A library render material owns its surface directly.");
        Material.Validate("render material");
    }

    public string ToJson() => JsonSerializer.Serialize(this, AuthoredJson.Options);
    public static RenderMaterialAsset2D FromJson(string json)
    {
        var asset = AuthoredAsset.Parse<RenderMaterialAsset2D>(json, "render material");
        asset.Validate();
        return asset;
    }
}
