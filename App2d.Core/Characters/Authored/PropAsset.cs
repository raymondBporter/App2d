using App2d.Core.Meshes;
using App2d.Core.Geometry;
using App2d.Core.Curves;
using App2d.Core.Shapes;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Numerics;

namespace App2d.Core.Characters.Authored;

/// <summary>Local prop art: a stroke through its points, or a filled polygon. X runs along the socket axis, Y across it, Z is depth.</summary>
public sealed record PropShape
{
    public string Kind { get; set; } = "stroke";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GeometryDefinition2D? Geometry { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<float>? Depths { get; set; }
    public List<PuppetPoint> Points { get; set; } = [];
    public float Width { get; set; } = .05f;
    public string Fill { get; set; } = "#c8b18a";

    [JsonIgnore] public bool IsFilled => Geometry is ShapeDefinition2D || Geometry is null && Kind == "polygon";

    public GeometryDefinition2D Definition() => Geometry ?? (Kind == "polygon"
        ? new SimplePolygonShapeDefinition2D { Vertices = [.. Points.Select(point => Point2D.From(point.XY))] }
        : Points.Count == 2
            ? new LineCurveDefinition2D { Start = Point2D.From(Points[0].XY), End = Point2D.From(Points[1].XY) }
            : new PolylineCurveDefinition2D { Points = [.. Points.Select(point => Point2D.From(point.XY))] });

    public void RestorePoints()
    {
        if (Geometry is null) return;
        Vector2[] xy;
        if (Geometry is ShapeDefinition2D definition)
        {
            Kind = "polygon";
            var shape = definition.Build();
            var count = WorldShape2D.OutlineVertexCount(shape, 48);
            if (count == 0) throw new InvalidDataException($"A {definition.Kind} cannot be used as prop art.");
            xy = new Vector2[count];
            WorldShape2D.WriteOutline(shape, xy, 48);
        }
        else
        {
            Kind = "stroke";
            xy = Geometry switch
            {
                LineCurveDefinition2D line => [line.Start.Vector, line.End.Vector],
                PolylineCurveDefinition2D polyline => [.. polyline.Points.Select(point => point.Vector)],
                CurveDefinition2D curve => Sample(curve),
                _ => throw new InvalidDataException("A prop needs shape or curve geometry.")
            };
        }
        if (Depths is { } depths && depths.Count != xy.Length)
            throw new InvalidDataException("Prop depth count must match geometry points.");
        Points = [.. xy.Select((point, i) => new PuppetPoint(point.X, point.Y, Depths is { } z ? z[i] : 0))];
    }

    private static Vector2[] Sample(CurveDefinition2D curve)
    {
        var built = curve.Build();
        return [.. Enumerable.Range(0, 33).Select(i => built.Evaluate(i / 32f))];
    }
}

/// <summary>A closed, consistently wound triangle mesh in prop space. X along the weapon, Y across its broad face, Z thickness.</summary>
public sealed record PropSolid
{
    public List<PuppetPoint> Vertices { get; set; } = [];
    public List<int> Triangles { get; set; } = [];
    public string Fill { get; set; } = "#c8b18a";
    public bool Outlined { get; set; } = true;
    /// <summary>Editable source for an extruded cutout. The indexed runtime mesh is rebuilt from it when loaded.</summary>
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

    public string ToJson()
    {
        foreach (var shape in Shapes)
            if (shape.Geometry is not null && shape.Points.Count == 0) shape.RestorePoints();
        var root = JsonNode.Parse(JsonSerializer.Serialize(this, AuthoredJson.Options))!.AsObject();
        var entries = root["shapes"]!.AsArray();
        for (var i = 0; i < Shapes.Count; i++)
        {
            var entry = entries[i]!.AsObject();
            entry.Remove("kind"); entry.Remove("points"); entry.Remove("geometry"); entry.Remove("depths");
            entry["geometry"] = JsonNode.Parse(Shapes[i].Definition().ToGeometryJson());
            entry["depths"] = JsonSerializer.SerializeToNode(Shapes[i].Points.Select(point => point.Z).ToArray(), AuthoredJson.Options);
        }
        return root.ToJsonString(AuthoredJson.Options);
    }
    internal string ToSnapshotJson() => JsonSerializer.Serialize(this, AuthoredJson.SnapshotOptions);
    public static PropAsset FromJson(string json)
    {
        var prop = AuthoredAsset.Parse<PropAsset>(json, "prop");
        foreach (var shape in prop.Shapes) shape.RestorePoints();
        if (prop.Solids is not null)
        {
            foreach (var solid in prop.Solids)
            {
                if (solid?.Outline is not { } outline) continue;
                if (outline.Count is < 3 or > 256) throw new InvalidDataException($"Prop '{prop.Id}': outline needs 3 to 256 points.");
                var mesh = PropGeometry.Extrude(outline, solid.Thickness, solid.Fill);
                solid.Vertices = mesh.Vertices;
                solid.Triangles = mesh.Triangles;
            }
        }

        prop.Validate();
        return prop;
    }
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
                Require(new Triangle3D(a, b, c).DoubleArea > 1e-8d, $"{owner}: degenerate triangle.");
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
            if (shape.Geometry is not null && shape.Points.Count == 0) shape.RestorePoints();
            if (shape.Geometry is null) EntityVocabulary.Require(shape.Kind, ["stroke", "polygon"], field + " kind");
            if (shape.Geometry is ShapeDefinition2D typedShape) _ = typedShape.Build();
            if (shape.Geometry is CurveDefinition2D typedCurve) _ = typedCurve.Build();
            Require(shape.IsFilled ? shape.Points.Count >= 3 : shape.Points.Count >= 2, $"{field}: a stroke needs two points and a polygon three.");
            foreach (var point in shape.Points) point.Check(field + " point");
            new Limit(.001f, 10).Check(shape.Width, field + " width"); Limit.Color(shape.Fill, field + " fill");
        }
    }
}
