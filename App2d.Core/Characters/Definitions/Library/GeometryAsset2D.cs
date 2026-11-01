using App2d.Core.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Mathematics;
using App2d.Core.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.Core.Characters.Authored;

/// <summary>Reusable geometry in local coordinates. Placement, rig bindings and materials belong to its users.</summary>
public sealed record GeometryAsset2D
{
    public string Format { get; set; } = "app2d-geometry";
    public int Version { get; set; } = 1;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public IGeometryValue2D? Geometry { get; set; }
    public PartMesh? Mesh { get; set; }
    [JsonConverter(typeof(Core.Geometry.Serialization.SpatialCurveJsonConverter))]
    public ICurve<Vector3f, float>? SpatialCurve { get; set; }
    [JsonIgnore] public string Kind => Mesh is not null ? "mesh" : SpatialCurve is not null ? "spatial-curve" : Geometry?.GetKind() ?? "empty";

    /// <summary>A fresh placed entry; resolving or rebuilding it cannot mutate the library mesh.</summary>
    public PartGeometry Entry(RenderMaterialDefinition2D material) => new()
    {
        Id = Id, Geometry = Geometry, SpatialCurve = SpatialCurve,
        Mesh = Mesh is null ? null : Mesh with { Vertices = [.. Mesh.Vertices], Triangles = [.. Mesh.Triangles], Outline = Mesh.Outline is null ? null : [.. Mesh.Outline] },
        A = PartLocation.Origin, Frame = PartLocation.Origin, Material = material,
    };

    public void Validate()
    {
        AuthoredAsset.RequireId(Id, "geometry id");
        if (Format != "app2d-geometry" || Version != 1) throw new InvalidDataException("Unsupported geometry format/version.");
        if (string.IsNullOrWhiteSpace(Name)) throw new InvalidDataException("Geometry needs a name.");
        if (Mesh is not null)
        {
            if (Geometry is not null || SpatialCurve is not null) throw new InvalidDataException("Choose one shape, curve or mesh.");
            Mesh.Validate();
            return;
        }
        Entry(RenderMaterialDefinition2D.Ink).Validate(id => id == PartLocation.Origin);
    }

    public string ToJson() => JsonSerializer.Serialize(this, AuthoredJson.Options);
    public static GeometryAsset2D FromJson(string json)
    {
        var asset = AuthoredAsset.Parse<GeometryAsset2D>(json, "geometry");
        asset.Validate();
        return asset;
    }
}
