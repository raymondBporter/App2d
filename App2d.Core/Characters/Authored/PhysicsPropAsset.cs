using App2d.Core.Geometry.Shapes;
using App2d.Core.Physics;
using App2d.Core.Rendering;
using App2d.Core.Shapes;
using App2d.Core.Validation;
using System.Text.Json;

namespace App2d.Core.Characters.Authored;

/// <summary>A passive, indestructible physics prop. Shape coordinates and body settings use game world units.</summary>
public sealed class PhysicsPropAsset
{
    public const string FormatId = "app2d-physics-prop";
    public const int CurrentVersion = 1;
    public string Format { get; set; } = FormatId;
    public int Version { get; set; } = CurrentVersion;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public IShape2D Shape { get; set; } = null!;
    public string Fill { get; set; } = "#ffffff";
    public PhysicsBodySettings2D Body { get; set; } = new();
    public float HitVelocityScale { get; set; } = 1f;
    public float HitAngularKick { get; set; }

    public void Validate()
    {
        var owner = $"Physics prop '{Id}'";
        if (Format != FormatId || Version != CurrentVersion)
            throw new InvalidDataException($"{owner}: unsupported format/version.");
        AuthoredAsset.RequireId(Id, "physics prop id");
        if (string.IsNullOrWhiteSpace(Name) || Shape is null || Body is null)
            throw new InvalidDataException($"{owner}: name, shape and body settings are required.");
        if (Shape is HalfSpace2D)
            throw new InvalidDataException($"{owner}: a physics prop needs a finite drawable shape.");
        Shape.Validate();
        Body.Validate();
        if (!ColorExtensions.TryParseRgb(Fill.AsSpan(), out _))
            throw new InvalidDataException($"{owner} fill must be a #RRGGBB color.");
        ArgGuard.NonNegative(HitVelocityScale, nameof(HitVelocityScale));
        ArgGuard.NonNegative(HitAngularKick, nameof(HitAngularKick));
    }

    public string ToJson() => JsonSerializer.Serialize(this, AuthoredJson.Options);
    public static PhysicsPropAsset FromJson(string json)
    {
        var asset = AuthoredAsset.Parse<PhysicsPropAsset>(json, "physics prop");
        asset.Validate();
        return asset;
    }

    public void Save(string path)
    {
        Validate();
        AuthoredAsset.Write(path, ToJson());
    }
}
