using App2d.Core.Characters.Geometry;
using App2d.Core.Geometry.Curves;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.Core.Characters.Authored;

/// <summary>What a part asset's payload is (plan section 3.6). The boundary is declared here; only the static payload is instanced yet.</summary>
public static class PartPayloads
{
    /// <summary>Geometry, images and meshes placed rigidly on the attachment frame, fitted per instance.</summary>
    public const string Static = "static";
    /// <summary>A payload with its own rig, clock and channels. Declared so files can say what they are; instancing arrives with P7a and is refused until then.</summary>
    public const string Animated = "animated";
    public static readonly string[] All = [Static, Animated];
}

/// <summary>
/// A named frame inside a part asset's local space that the payload attaches to. <c>origin</c> is implicit: the frame the
/// instance places on the wearer's socket. Other locations let a payload hang several pieces off one asset and give later
/// passes named points for sockets and colliders.
/// </summary>
public sealed record PartLocation
{
    public const string Origin = "origin";
    public string Id { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
    /// <summary>Rotation in radians in the asset's local axes.</summary>
    public float Angle { get; set; }
    /// <summary>Drawing-layer offset from the asset's origin.</summary>
    public float Layer { get; set; }

    [JsonIgnore] public Vector2 Position => new(X, Y);

    public void Validate(string field)
    {
        AuthoredAsset.RequireId(Id, field + " id");
        if (Id == Origin)
        {
            throw new InvalidDataException($"{field}: '{Origin}' is the implicit location.");
        }

        new Limit(-100, 100).Check(X, field + " x");
        new Limit(-100, 100).Check(Y, field + " y");
        new Limit(-1000, 1000).Check(Angle, field + " angle");
        new Limit(-16, 16).Check(Layer, field + " layer");
    }
}

/// <summary>
/// A reusable library part (character-system migration, P5a): its own asset kind under <c>parts/</c>, owning geometry,
/// images, meshes and shared materials in a local frame whose origin lands on a wearer's socket. A character references it
/// through a <see cref="PartInstance"/> (asset ID, attachment location, instance ID), so one glove asset can sit on both
/// hands as two instances with independent placement, fitting and overrides. The same asset is never copied into a wearer;
/// editing it reaches every dependent preview through the workspace's reference graph.
/// </summary>
public sealed class PartAsset
{
    public const string FormatId = "app2d-part";
    public const int CurrentVersion = 1;
    public string Format { get; set; } = FormatId;
    public int Version { get; set; } = CurrentVersion;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>A library category such as glove, shoe, hair or shirt: browser grouping, not engine behavior.</summary>
    public string Usage { get; set; } = "part";
    /// <summary>One of <see cref="PartPayloads"/>.</summary>
    public string Payload { get; set; } = PartPayloads.Static;
    public string Ink { get; set; } = "#222b32";
    public float LineWidth { get; set; } = .045f;
    /// <summary>Shared surfaces the payload selects through <c>material.asset</c>, like a model's.</summary>
    public Dictionary<string, RenderMaterialDefinition2D> Materials { get; set; } = [];
    public List<PartLocation> Locations { get; set; } = [];
    /// <summary>Shapes, curves and images: ordinary drawing parts whose <c>a</c>/<c>b</c> name the asset's locations (<c>origin</c> or one of <see cref="Locations"/>).</summary>
    public List<PuppetPart> Parts { get; set; } = [];
    /// <summary>Solid meshes in the asset's local space (X along, Y across, Z thickness), lifted by the one planar-to-3D bridge.</summary>
    public List<PropSolid> Solids { get; set; } = [];

    public string ToJson() => JsonSerializer.Serialize(this, AuthoredJson.Options);
    internal static PartAsset FromDraftJson(string json) => AuthoredAsset.Parse<PartAsset>(json, "part");
    public static PartAsset FromJson(string json)
    {
        var part = FromDraftJson(json);
        part.RebuildSolids();
        part.Validate();
        return part;
    }
    public static PartAsset Load(string path) => FromJson(File.ReadAllText(path));
    public void Save(string path)
    {
        Validate();
        AuthoredAsset.Write(path, ToJson());
    }

    /// <summary>Rebuilds the indexed meshes of extruded outlines, which are the editable source.</summary>
    public void RebuildSolids()
    {
        foreach (var solid in Solids ?? [])
        {
            if (solid?.Outline is not { } outline)
            {
                continue;
            }

            if (outline.Count is < 3 or > 256)
            {
                throw new InvalidDataException($"Part '{Id}': outline needs 3 to 256 points.");
            }

            var mesh = PropGeometry.Extrude(outline, solid.Thickness, solid.RenderMaterial.Fill ?? throw new InvalidDataException($"Part '{Id}': a solid needs a fill."));
            solid.Vertices = mesh.Vertices;
            solid.Triangles = mesh.Triangles;
        }
    }

    public bool IsLocation(string? id) => id == PartLocation.Origin || id is not null && Locations.Any(l => l.Id == id);

    /// <summary>A location's frame in the asset's local space: identity for the origin.</summary>
    public LayeredFrame2D LocationFrame(string id)
    {
        if (id == PartLocation.Origin)
        {
            return LayeredFrame2D.Identity;
        }

        var location = Locations.FirstOrDefault(l => l.Id == id) ?? throw new KeyNotFoundException($"Part '{Id}' has no location '{id}'.");
        return LayeredFrame2D.Local(Vector2.One, location.Angle, location.Position, location.Layer);
    }

    private static void Require([DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }

    public void Validate()
    {
        var owner = $"Part '{Id}'";
        Require(Format == FormatId && Version == CurrentVersion, $"{owner}: unsupported format/version.");
        // An outline is the editable source of a solid; its mesh is derived here as on load, so an authored draft validates too.
        RebuildSolids();
        AuthoredAsset.RequireId(Id, "part id");
        Require(!string.IsNullOrWhiteSpace(Name), $"{owner}: a name is required.");
        AuthoredAsset.RequireId(Usage, owner + " usage");
        Require(PartPayloads.All.Contains(Payload, StringComparer.Ordinal), $"{owner}: unknown payload '{Payload}'.");
        // The animated payload boundary is declared; its rig, clock and channel instancing is P7a.
        Require(Payload == PartPayloads.Static, $"{owner}: an animated payload cannot be instanced yet; nested part rigs, clocks and channels arrive with P7a. Declare the payload static.");
        Limit.Color(Ink, owner + " ink");
        new Limit(.001f, 1).Check(LineWidth, owner + " lineWidth");
        Require(Materials is not null && Locations is not null && Parts is not null && Solids is not null, $"{owner}: collections cannot be null.");
        foreach (var (id, material) in Materials)
        {
            AuthoredAsset.RequireId(id, $"{owner} material id");
            Require(material is not null, $"{owner}: null material '{id}'.");
            material.Resolve(Materials);
        }
        var locations = new HashSet<string>(StringComparer.Ordinal) { PartLocation.Origin };
        foreach (var location in Locations)
        {
            Require(location is not null, $"{owner}: null location.");
            location.Validate($"{owner} location");
            Require(locations.Add(location.Id), $"{owner}: duplicate location '{location.Id}'.");
        }
        Require(Parts.Count + Solids.Count > 0, $"{owner}: a part needs at least one shape, curve, image or solid.");
        Require(Parts.Count <= 256 && Solids.Count <= 128, $"{owner}: capacity exceeded.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in Parts)
        {
            Require(part is not null, $"{owner}: null drawing part.");
            AuthoredAsset.RequireId(part.Id, $"{owner} drawing part id");
            Require(ids.Add(part.Id), $"{owner}: duplicate drawing part '{part.Id}'.");
            Require(part.Frame is null, $"{owner} part '{part.Id}': a library part's pieces attach to its locations, not to a wearer's bone.");
            Require(part.Face is null, $"{owner} part '{part.Id}': faces belong to a face part asset (P7b), not a static library part.");
            try
            {
                part.Validate(locations.Contains);
            }
            catch (InvalidDataException ex)
            {
                throw new InvalidDataException($"{owner} part '{part.Id}': {ex.Message}", ex);
            }
            part.RenderMaterial.Resolve(Materials);
        }
        for (var i = 0; i < Solids.Count; i++)
        {
            var solid = Solids[i];
            var field = $"{owner} solid {i + 1}";
            Require(solid?.Vertices is not null && solid.Triangles is not null, $"{field}: incomplete solid.");
            Require(solid.Material is not null, $"{field}: a material is required.");
            solid.Material.Validate(field + " material");
            Require(solid.Material.Fill is not null, $"{field}: a solid needs a fill.");
            if (solid.Outline is { } outline)
            {
                Require(outline.Count is >= 3 and <= 256, $"{field}: outline needs 3 to 256 points.");
                new Limit(.0001f, 32).Check(solid.Thickness, field + " thickness");
                foreach (var point in outline)
                {
                    point.Check(field + " outline");
                }
            }
            Require(solid.Vertices.Count >= 3 && solid.Triangles.Count >= 3 && solid.Triangles.Count % 3 == 0, $"{field}: a solid needs indexed triangles (an outline is extruded on load).");
            Require(solid.Triangles.All(t => t >= 0 && t < solid.Vertices.Count), $"{field}: triangle index outside vertices.");
        }
    }

    /// <summary>A static part holding one prop's solids, for converting hair, clothing and footwear props into library parts.</summary>
    public static PartAsset FromProp(PropAsset prop, string id, string name, string usage)
    {
        var part = new PartAsset
        {
            Id = id,
            Name = name,
            Usage = usage,
            Ink = prop.Ink,
            LineWidth = prop.LineWidth,
        };
        foreach (var solid in prop.Solids)
        {
            // The prop's grip is the point its socket carried; the part's origin takes that role.
            part.Solids.Add(solid with
            {
                Outline = solid.Outline?.ConvertAll(p => new PuppetPoint((p.X - prop.Grip.X) * prop.Scale, (p.Y - prop.Grip.Y) * prop.Scale, (p.Z - prop.Grip.Z) * prop.Scale)),
                Vertices = solid.Vertices.ConvertAll(p => new PuppetPoint((p.X - prop.Grip.X) * prop.Scale, (p.Y - prop.Grip.Y) * prop.Scale, (p.Z - prop.Grip.Z) * prop.Scale)),
                Triangles = [.. solid.Triangles],
                Thickness = solid.Thickness * prop.Scale,
            });
        }
        part.RebuildSolids();
        part.Validate();
        return part;
    }
}

/// <summary>
/// One occurrence of a library part on a character: the instance ID (stable, addressed by overrides), the asset ID
/// (which library resource) and the socket ID (the wearer's named attachment location), plus this instance's placement on
/// the socket, mirroring, leaf fitting and local overrides. Two instances of one asset are independent; neither edits it.
/// </summary>
public sealed record PartInstance
{
    public string Id { get; set; } = "";
    public string Asset { get; set; } = "";
    public string Socket { get; set; } = "";
    /// <summary>Offset of the asset's origin in the socket's axes.</summary>
    public float X { get; set; }
    public float Y { get; set; }
    /// <summary>Rotation in radians in the socket's axes.</summary>
    public float Angle { get; set; }
    /// <summary>Uniform size, part of the similarity.</summary>
    public float Scale { get; set; } = 1;
    /// <summary>Reflects the asset across the socket's Y axis: the same glove on the other hand.</summary>
    public bool Mirror { get; set; }
    /// <summary>Drawing-layer offset from the socket.</summary>
    public float Layer { get; set; }
    /// <summary>Leaf geometry fit (plan section 3.4): stretches this instance's artwork; nothing inherits it.</summary>
    public float FitX { get; set; } = 1;
    public float FitY { get; set; } = 1;
    /// <summary>Inline material override over every piece of the payload (a fill, a shared material of the asset, a tint); null inherits.</summary>
    public RenderMaterialDefinition2D? Material { get; set; }
    public bool Hidden { get; set; }

    /// <summary>
    /// The instance's placement on its socket: fit and size, then rotation, then offset, then the layer; a mirrored instance
    /// is reflected in the socket's axes afterwards, so the mirror image lands where the socket's other side would.
    /// </summary>
    [JsonIgnore]
    public LayeredFrame2D Local
    {
        get
        {
            var local = LayeredFrame2D.Local(new(FitX * Scale, FitY * Scale), Angle, new(X, Y), Layer);
            return Mirror ? local.Then(LayeredFrame2D.Local(new(-1, 1), 0, Vector2.Zero, 0)) : local;
        }
    }

    public void Validate(string field)
    {
        AuthoredAsset.RequireId(Id, field + " id");
        AuthoredAsset.RequireId(Asset, field + " asset");
        AuthoredAsset.RequireId(Socket, field + " socket");
        new Limit(-100, 100).Check(X, field + " x");
        new Limit(-100, 100).Check(Y, field + " y");
        new Limit(-1000, 1000).Check(Angle, field + " angle");
        new Limit(.001f, 100).Check(Scale, field + " scale");
        new Limit(-16, 16).Check(Layer, field + " layer");
        new Limit(-100, 100).Check(FitX, field + " fitX");
        new Limit(-100, 100).Check(FitY, field + " fitY");
        if (FitX == 0 || FitY == 0)
        {
            throw new InvalidDataException($"{field}: a zero fit collapses the artwork.");
        }

        Material?.Validate(field + " material");
    }
}

/// <summary>
/// An attachment instance resolved for one wearer: the merged instance, its asset, the socket it hangs from, and the payload
/// with materials resolved against the asset's shared materials and the instance's override. The placement is derived per
/// pose from the socket's evaluated placement; nothing here is a copy of the asset.
/// </summary>
public sealed record ResolvedAttachment(PartInstance Instance, PartAsset Asset, ModelSocket Socket, IReadOnlyList<PuppetPart> Parts, IReadOnlyList<PropSolid> Solids)
{
    public string Id => Instance.Id;

    /// <summary>The instance's placement in the frame its socket placement is given in (actor-local or world).</summary>
    public LayeredFrame2D Placement(LayeredFrame2D socketPlacement) => Instance.Local.Then(socketPlacement);

    /// <summary>A payload location's placement for a given instance placement: what the payload's pieces attach to.</summary>
    public LayeredFrame2D LocationPlacement(string location, LayeredFrame2D placement) => Asset.LocationFrame(location).Then(placement);

    /// <summary>The payload's drawing inputs for an instance placement: each location's origin and frame, as the shared part drawing expects.</summary>
    public (Func<string, Vector3> World, Func<string, LayeredFrame2D> Frame) Lookups(LayeredFrame2D placement) =>
        (location => LocationPlacement(location, placement).Origin, location => LocationPlacement(location, placement));

    /// <summary>Resolves an instance for a wearer: the asset must exist and the socket must be the wearer's.</summary>
    public static ResolvedAttachment Resolve(PartInstance instance, Func<string, PartAsset?> partOf, IReadOnlyDictionary<string, ModelSocket> sockets, string owner)
    {
        var asset = partOf(instance.Asset) ?? throw new InvalidDataException($"{owner} attachment '{instance.Id}': no part asset '{instance.Asset}'.");
        if (!sockets.TryGetValue(instance.Socket, out var socket))
        {
            throw new InvalidDataException($"{owner} attachment '{instance.Id}': the wearer has no socket '{instance.Socket}'.");
        }

        var parts = asset.Parts.ConvertAll(p => p with { Material = p.RenderMaterial.WithOverride(instance.Material).Resolve(asset.Materials) });
        var solids = asset.Solids.ConvertAll(s => s with { Material = s.RenderMaterial.WithOverride(instance.Material).Resolve(asset.Materials) });
        return new(instance, asset, socket, parts, solids);
    }
}
