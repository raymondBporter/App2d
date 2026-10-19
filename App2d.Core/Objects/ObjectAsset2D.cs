using App2d.Core.Characters;
using App2d.Core.Geometry.Operations.Bounds;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Geometry.Shapes;
using App2d.Core.IO;
using App2d.Core.Physics;
using App2d.Core.Physics.Motion;
using App2d.Core.Validation;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.Core.Objects;

/// <summary>A local shape with optional drawing, physics and translation. Definitions are immutable while instantiated.</summary>
public sealed record ObjectAsset2D
{
    public const string FormatId = "app2d-object";
    public string Format { get; init; } = FormatId;
    public int Version { get; init; } = 1;
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public IShape2D Geometry { get; init; } = null!;
    public RenderMaterialDefinition2D? Material { get; init; }
    public PhysicsBodySettings2D? Physics { get; init; }
    public ObjectSurface2D? Surface { get; init; }
    public CurveMotionDefinition2D? Motion { get; init; }
    [JsonIgnore] public string TextureRoot { get; init; } = Environment.CurrentDirectory;

    public void Validate()
    {
        if (Format != FormatId || Version != 1)
            throw new InvalidDataException("Unsupported object format/version.");
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Name))
            throw new InvalidDataException("An object needs an id and name.");
        ArgGuard.ThrowIfNull(Geometry);
        Geometry.Validate();
        if (!ShapeBounds2D.Calculate(Geometry).IsFinite)
            throw new InvalidDataException("An object needs finite geometry.");
        Material?.Validate("material");
        // Shared model references and part paint require a model; object surfaces are inline for now.
        if (Material?.Asset is not null || Material?.Paint is { Count: > 0 })
            throw new InvalidDataException("Object materials currently support inline fill, outline, texture and tint.");
        Physics?.Validate();
        Motion?.Validate();
        if (Physics is not null && Motion is not null && Physics.MotionType != BodyMotionType2D.Kinematic)
            throw new InvalidDataException("A physical object with path motion must be kinematic.");
        if (Surface is not null)
        {
            if (Physics is null) throw new InvalidDataException("Surface behavior requires physics.");
            Surface.Validate();
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this, GeometryJson2D.JsonOptions);
    public static ObjectAsset2D FromJson(string json)
    {
        var asset = JsonSerializer.Deserialize<ObjectAsset2D>(json, GeometryJson2D.JsonOptions)
            ?? throw new InvalidDataException("Empty object asset.");
        asset.Validate();
        return asset;
    }

    public static ObjectAsset2D Load(string path) => FromJson(File.ReadAllText(path)) with { TextureRoot = Path.GetDirectoryName(Path.GetFullPath(path))! };
    public void Save(string path)
    {
        Validate();
        AtomicFile.WriteAllText(path, ToJson());
    }
}

/// <summary>Traversal policy applied by the hosting game; not part of the mathematical shape.</summary>
public sealed record ObjectSurface2D
{
    public Vector2? OneWayNormal { get; init; }
    public float OneWaySlop { get; init; } = 2f;

    public void Validate()
    {
        ArgGuard.NonNegative(OneWaySlop);
        if (OneWayNormal is { } normal)
        {
            ArgGuard.Finite(normal);
            ArgGuard.Positive(normal.Length(), nameof(OneWayNormal));
        }
    }
}

/// <summary>Independent placement identity and world origin; the reusable asset remains in local coordinates.</summary>
public sealed record ObjectPlacement2D(string Id, ObjectAsset2D Asset, Vector2 Position)
{
    public void Validate()
    {
        ArgGuard.ThrowIfNullOrWhiteSpace(Id);
        ArgGuard.ThrowIfNull(Asset);
        ArgGuard.Finite(Position);
        Asset.Validate();
    }
}
