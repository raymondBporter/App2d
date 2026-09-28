using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace App2d.Core.Characters.Authored;

/// <summary>Local prop art: a stroke through its points, or a filled polygon. X runs along the socket axis, Y across it, Z is depth.</summary>
public sealed record PropShape
{
    public string Kind { get; set; } = "stroke";
    public List<PuppetPoint> Points { get; set; } = [];
    public float Width { get; set; } = .05f;
    public string Fill { get; set; } = "#c8b18a";
}

/// <summary>A closed, consistently wound triangle mesh in prop space. X along the weapon, Y across its broad face, Z thickness.</summary>
public sealed record PropSolid
{
    public List<PuppetPoint> Vertices { get; set; } = [];
    public List<int> Triangles { get; set; } = [];
    public string Fill { get; set; } = "#c8b18a";
    public bool Outlined { get; set; } = true;
    /// <summary>Optional editable source for an extruded cutout. Indexed geometry remains the runtime representation.</summary>
    public List<PuppetPoint>? Outline { get; set; }
    public float Thickness { get; set; }
}

/// <summary>
/// A held object: local art plus the named points gameplay needs. The grip lands on the equipment socket; the tip and
/// muzzle place hit regions and projectiles from the same transform the art is drawn with. The second grip is recorded
/// for a later two-hand solve and not yet used.
/// </summary>
public sealed class PropAsset
{
    public const string FormatId = "app2d-prop", GripPoint = "grip", TipPoint = "tip", SecondGripPoint = "second-grip", MuzzlePoint = "muzzle";
    public static readonly IReadOnlyList<string> PointNames = [GripPoint, TipPoint, SecondGripPoint, MuzzlePoint];
    public string Format { get; set; } = FormatId;
    public int Version { get; set; } = 1;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Usage { get; set; } = "prop";
    public string? Attachment { get; set; }
    public string? BackView { get; set; }
    public string Ink { get; set; } = "#222b32";
    public float LineWidth { get; set; } = .035f;
    public float Scale { get; set; } = 1;
    public PuppetPoint Grip { get; set; }
    public PuppetPoint Tip { get; set; } = new(1, 0);
    public PuppetPoint? SecondGrip { get; set; }
    public PuppetPoint? Muzzle { get; set; }
    public List<PropShape> Shapes { get; set; } = [];
    public List<PropSolid> Solids { get; set; } = [];

    /// <summary>A named local point, or null when the prop does not define it.</summary>
    public PuppetPoint? Point(string name) => name switch
    {
        GripPoint => Grip,
        TipPoint => Tip,
        SecondGripPoint => SecondGrip,
        MuzzlePoint => Muzzle,
        _ => null,
    };

    public string ToJson() => JsonSerializer.Serialize(this, AuthoredJson.Options);
    public static PropAsset FromJson(string json) { var prop = AuthoredAsset.Parse<PropAsset>(json, "prop"); prop.Validate(); return prop; }
    public void Save(string path) { Validate(); AuthoredAsset.Write(path, ToJson()); }

    private static void Require([DoesNotReturnIf(false)] bool condition, string message) { if (!condition) throw new InvalidDataException(message); }

    public void Validate()
    {
        var owner = $"Prop '{Id}'";
        Require(Format == FormatId && Version == 1, $"{owner}: unsupported format/version.");
        AuthoredAsset.RequireId(Id, "prop id");
        Require(!string.IsNullOrWhiteSpace(Name), $"{owner}: a name is required.");
        EntityVocabulary.Require(Usage, ["prop", "hair", "clothing"], owner + " usage");
        if (Attachment is not null) AuthoredAsset.RequireId(Attachment, owner + " attachment");
        if (BackView is not null) { AuthoredAsset.RequireId(BackView, owner + " backView"); Require(BackView != Id, owner + ": back view cannot reference itself."); }
        Limit.Color(Ink, $"{owner} ink"); new Limit(.001f, 1).Check(LineWidth, $"{owner} lineWidth");
        new Limit(.001f, 100).Check(Scale, $"{owner} scale");
        Grip.Check($"{owner} grip"); Tip.Check($"{owner} tip"); SecondGrip?.Check($"{owner} secondGrip"); Muzzle?.Check($"{owner} muzzle");
        Require(Solids?.Count <= 128, $"{owner}: solids must be a list of at most 128.");
        Require(Solids.Sum(s => s?.Vertices?.Count ?? 0) <= 32768 && Solids.Sum(s => s?.Triangles?.Count ?? 0) <= 196608, $"{owner}: mesh is too large.");
        foreach (var solid in Solids)
        {
            Require(solid?.Vertices is not null && solid.Triangles is not null, $"{owner}: incomplete solid.");
            Require(solid.Vertices.Count >= 3 && solid.Triangles.Count >= 3 && solid.Triangles.Count % 3 == 0, $"{owner}: a solid needs indexed triangles.");
            foreach (var vertex in solid.Vertices) vertex.Check(owner + " mesh vertex");
            Require(solid.Triangles.All(i => i >= 0 && i < solid.Vertices.Count), $"{owner}: triangle index outside vertices.");
            for (var i = 0; i < solid.Triangles.Count; i += 3)
            {
                var a = solid.Vertices[solid.Triangles[i]].XYZ; var b = solid.Vertices[solid.Triangles[i + 1]].XYZ; var c = solid.Vertices[solid.Triangles[i + 2]].XYZ;
                Require(System.Numerics.Vector3.Cross(b - a, c - a).LengthSquared() > 1e-16f, $"{owner}: degenerate triangle.");
            }
            Limit.Color(solid.Fill, owner + " mesh fill");
            if (solid.Outline is { } outline)
            {
                Require(outline.Count is >= 3 and <= 256, owner + ": outline needs 3 to 256 points.");
                new Limit(.0001f, 32).Check(solid.Thickness, owner + " thickness");
                foreach (var p in outline) p.Check(owner + " outline");
            }
        }
        Require(Shapes?.Count <= 128, $"{owner}: shapes must be a list of at most 128.");
        for (var i = 0; i < Shapes.Count; i++)
        {
            var shape = Shapes[i]; var field = $"{owner} shapes[{i}]";
            Require(shape?.Points is not null, $"{field}: incomplete shape.");
            EntityVocabulary.Require(shape.Kind, ["stroke", "polygon"], field + " kind");
            Require(shape.Kind == "stroke" ? shape.Points.Count >= 2 : shape.Points.Count >= 3, $"{field}: a stroke needs two points and a polygon three.");
            foreach (var point in shape.Points) point.Check(field + " point");
            new Limit(.001f, 10).Check(shape.Width, field + " width"); Limit.Color(shape.Fill, field + " fill");
        }
    }
}
