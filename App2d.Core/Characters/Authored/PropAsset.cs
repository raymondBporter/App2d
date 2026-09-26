using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace App2d.Core.Characters;

/// <summary>Local prop art: a stroke through its points, or a filled polygon. X runs along the socket axis, Y across it, Z is depth.</summary>
public sealed record PropShape
{
    public string Kind { get; set; } = "stroke";
    public List<PuppetPoint> Points { get; set; } = [];
    public float Width { get; set; } = .05f;
    public string Fill { get; set; } = "#c8b18a";
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
    public string Ink { get; set; } = "#222b32";
    public float LineWidth { get; set; } = .035f;
    public PuppetPoint Grip { get; set; }
    public PuppetPoint Tip { get; set; } = new(1, 0);
    public PuppetPoint? SecondGrip { get; set; }
    public PuppetPoint? Muzzle { get; set; }
    public List<PropShape> Shapes { get; set; } = [];

    /// <summary>A named local point, or null when the prop does not define it.</summary>
    public PuppetPoint? Point(string name) => name switch
    {
        GripPoint => Grip, TipPoint => Tip, SecondGripPoint => SecondGrip, MuzzlePoint => Muzzle, _ => null,
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
        Limit.Color(Ink, $"{owner} ink"); new Limit(.001f, 1).Check(LineWidth, $"{owner} lineWidth");
        Grip.Check($"{owner} grip"); Tip.Check($"{owner} tip"); SecondGrip?.Check($"{owner} secondGrip"); Muzzle?.Check($"{owner} muzzle");
        Require(Shapes is not null && Shapes.Count <= 128, $"{owner}: shapes must be a list of at most 128.");
        for (var i = 0; i < Shapes.Count; i++)
        {
            var shape = Shapes[i]; var field = $"{owner} shapes[{i}]";
            Require(shape is not null && shape.Points is not null, $"{field}: incomplete shape.");
            EntityVocabulary.Require(shape.Kind, ["stroke", "polygon"], field + " kind");
            Require(shape.Kind == "stroke" ? shape.Points.Count >= 2 : shape.Points.Count >= 3, $"{field}: a stroke needs two points and a polygon three.");
            foreach (var point in shape.Points) point.Check(field + " point");
            new Limit(.001f, 10).Check(shape.Width, field + " width"); Limit.Color(shape.Fill, field + " fill");
        }
    }
}
