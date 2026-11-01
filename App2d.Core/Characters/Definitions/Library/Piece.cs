using System.Text.Json;
using App2d.Core.Json;

namespace App2d.Core.Characters.Authored;

/// <summary>One static drawable, directly attachable to a model. References retain their library identity on disk.</summary>
public sealed record Piece
{
    public string Format { get; set; } = "app2d-piece";
    public int Version { get; set; } = 1;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Geometry { get; set; }
    public GeometryAsset2D? InlineGeometry { get; set; }
    public RenderMaterialDefinition2D Material { get; set; } = new() { Fill = "#b8bdc5" };
    /// <summary>Slash-separated paths such as Clothes/Armor. An asset may belong to several groups.</summary>
    public List<string> Groups { get; set; } = [];
    public string? Attachment { get; set; }
    public PieceGameSettings? Game { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Angle { get; set; }
    public float ScaleX { get; set; } = 1;
    public float ScaleY { get; set; } = 1;
    public float Layer { get; set; }

    public void Validate()
    {
        AuthoredAsset.RequireId(Id, "piece id");
        if (Format != "app2d-piece" || Version != 1) throw new InvalidDataException("Unsupported piece format/version.");
        if (string.IsNullOrWhiteSpace(Name)) throw new InvalidDataException("Piece needs a name.");
        if ((Geometry is null) == (InlineGeometry is null)) throw new InvalidDataException("Choose library geometry or inline geometry for this piece.");
        if (Geometry is not null) AuthoredAsset.RequireId(Geometry, "piece geometry");
        InlineGeometry?.Validate();
        if (Material is null) throw new InvalidDataException("Choose a render material for this piece.");
        Material.Validate("piece material");
        if (Attachment is not null) AuthoredAsset.RequireId(Attachment, "default attachment");
        Game?.Equipment?.Validate();
        if (Groups is null || Groups.Any(g => string.IsNullOrWhiteSpace(g) || g.Split('/').Any(string.IsNullOrWhiteSpace)))
            throw new InvalidDataException("Groups use paths such as Clothes/Armor, with no empty path segments.");
        foreach (var value in new[] { X, Y, Angle, ScaleX, ScaleY, Layer })
            if (!float.IsFinite(value)) throw new InvalidDataException("Piece transform must be finite.");
        new Limit(-100, 100).Check(X, "piece.x");
        new Limit(-100, 100).Check(Y, "piece.y");
        new Limit(-1000, 1000).Check(Angle, "piece.angle");
        new Limit(-16, 16).Check(Layer, "piece.layer");
    }

    /// <summary>Uses the established runtime attachment representation without creating another saved asset.</summary>
    public Part Resolve(Func<string, GeometryAsset2D?> geometryOf, Func<string, RenderMaterialAsset2D?> materialOf)
    {
        Validate();
        var geometry = InlineGeometry ?? geometryOf(Geometry!) ?? throw new InvalidDataException($"Piece '{Id}': no geometry '{Geometry}'.");
        geometry.Validate();
        var material = Material;
        if (material.Asset is { } id)
        {
            var shared = materialOf(id) ?? throw new InvalidDataException($"Piece '{Id}': no render material '{id}'.");
            shared.Validate();
            material = shared.Material.WithOverride(material with { Asset = null });
        }
        var entry = geometry.Entry(material) with { Id = "piece", OffsetX = X, OffsetY = Y, Angle = Angle, ScaleX = ScaleX, ScaleY = ScaleY, Layer = Layer };
        var part = new Part { Id = Id, Name = Name, Attachment = Attachment, Geometry = [entry] };
        part.Validate();
        return part;
    }

    public string ToJson() => JsonSerializer.Serialize(this, AuthoredJson.Options);
    public static Piece FromJson(string json)
    {
        var asset = AuthoredAsset.Parse<Piece>(json, "piece");
        asset.Validate();
        return asset;
    }
}
