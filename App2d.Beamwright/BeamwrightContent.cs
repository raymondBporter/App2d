using App2d.Core.Json;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Mathematics;
using App2d.Core.Shapes;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.Beamwright;

/// <summary>Snapshots of the original campaign and part definitions; no JavaScript at runtime.</summary>
public sealed class BeamwrightContent
{
    public static BeamwrightContent Default { get; } = ReadResource<BeamwrightContent>("campaign.json");
    public string Version { get; init; } = "";
    public PartDefinition[] Pieces { get; init; } = [];
    public BenchLevel[] Levels { get; init; } = [];

    public PartDefinition Part(string type) => Pieces.First(p => p.Type == type);

    public static T ReadResource<T>(string name)
    {
        using var stream = typeof(BeamwrightContent).Assembly.GetManifestResourceStream($"App2d.Beamwright.Content.{name}")
            ?? throw new InvalidOperationException($"Missing Beamwright content: {name}");
        return JsonSerializer.Deserialize<T>(stream, JsonProfiles.CaseInsensitive) ?? throw new InvalidDataException(name);
    }

    public static BenchLevel FreeBench => new()
    {
        Name = "Free Bench",
        W = 11,
        H = 7,
        Sandbox = true,
        Tip = "Unlimited parts. Build a path into every photocell, or just experiment with light.",
        Sources =
        [
            new() { X = 0, Y = 3 },
            new() { X = 0, Y = 6, Color = "B" }
        ],
        Targets =
        [
            new() { X = 10, Y = 0, Need = 30 },
            new() { X = 10, Y = 6, Need = 30 },
            new() { X = 5, Y = 0, Need = 5, Color = "R", Radius = .36f }
        ],
        Mirage =
        [
            new() { X = 6, Y = 5, W = 4, H = 2, K = .5f }
        ]
    };
}

public sealed record PartPlacement
{
    public string Type { get; init; } = "mirror";
    public int X { get; init; }
    public int Y { get; init; }
    [JsonPropertyName("a")] public float Angle { get; init; }
    [JsonIgnore] public Vector2i Cell => new(X, Y);
    [JsonIgnore] public Vector2 Center => Cell.ToVector2() + new Vector2(.5f);
}

public sealed class PartDefinition
{
    public string Type { get; init; } = "";
    public string Name { get; init; } = "";
    public string Shape { get; init; } = "";
    public string Act { get; init; } = "";
    public string Spec { get; init; } = "";
    public float Half { get; init; }
    [JsonPropertyName("f")] public float FocalLength { get; init; }
    [JsonPropertyName("r")] public float Radius { get; init; }
    [JsonPropertyName("n")] public float RefractiveIndex { get; init; }
    public Dictionary<string, float>? Tab { get; init; }
    public bool Mirror { get; init; }
    public float Partial { get; init; }
    public int[] Ports { get; init; } = [];
    public float[][] Pts { get; init; } = [];
    public int Sym { get; init; }
    public float DefaultAngle { get; init; }
    private IGeometryValue2D? _geometry;

    [JsonIgnore] public IGeometryValue2D Geometry => _geometry ??= CreateGeometry();

    private IGeometryValue2D CreateGeometry()
    {
        if (Shape == "seg") return LineSegmentCurve2D.Create(new(-Half, 0), new(Half, 0));
        if (Shape == "disc") return Circle2D.Builder.Create(Radius);
        var points = Pts.Select(p => new Vector2(p[0], p[1])).ToArray();
        if (Type is "block" or "fiber") return Rectangle2D.Builder.Create(points[0], points[2]);
        return SimplePolygon2D.Builder.Create(points);
    }
}

public sealed class BenchLevel
{
    public string Name { get; init; } = "Bench";
    public string Tip { get; init; } = "";
    public int W { get; init; } = 9;
    public int H { get; init; } = 7;
    public int Chapter { get; init; } = 1;
    public int Par { get; init; }
    public bool Sandbox { get; init; }
    public LightSource[] Sources { get; init; } = [];
    public Photocell[] Targets { get; init; } = [];
    public int[][] Walls { get; init; } = [];
    public PartPlacement[] Fixed { get; init; } = [];
    public Aperture[] Gates { get; init; } = [];
    public MirageZone[] Mirage { get; init; } = [];
    public Dictionary<string, int> Inv { get; init; } = [];
    public PartPlacement[] Solution { get; init; } = [];
}

public sealed class LightSource
{
    public int X { get; init; }
    public int Y { get; init; }
    [JsonPropertyName("a")] public float Angle { get; init; }
    public string Color { get; init; } = "W";
    public int Rays { get; init; } = 5;
    public float? Width { get; init; }
    [JsonIgnore] public Vector2i Cell => new(X, Y);
    [JsonIgnore] public Vector2 Center => Cell.ToVector2() + new Vector2(.5f);
}

public sealed class Photocell
{
    public int X { get; init; }
    public int Y { get; init; }
    public float Ox { get; init; }
    public float Oy { get; init; }
    public string Color { get; init; } = "any";
    public float Need { get; init; }
    [JsonPropertyName("r")] public float Radius { get; init; } = .3f;
    [JsonPropertyName("a")] public float Angle { get; init; }
    public bool Profile { get; init; }
    public int Bins { get; init; } = 5;
    public float BinNeed { get; init; } = 5;
    public float Tolerance { get; init; } = 89;
    public float Half { get; init; } = .4f;
    [JsonIgnore] public Vector2i Cell => new(X, Y);
    [JsonIgnore] public Vector2 Offset => new(Ox, Oy);
    [JsonIgnore] public Vector2 Center => Cell.ToVector2() + new Vector2(.5f) + Offset;
}

public sealed class Aperture
{
    public int X { get; init; }
    public int Y { get; init; }
    public float Gap { get; init; }
}

public sealed class MirageZone
{
    public int X { get; init; }
    public int Y { get; init; }
    public int W { get; init; }
    public int H { get; init; }
    public float K { get; init; } = .3f;
    [JsonIgnore] public Vector2i Cell => new(X, Y);
    [JsonIgnore] public Rect2D AirBounds => new(Cell.ToVector2(), Cell.ToVector2() + new Vector2(W, H - .12f));
}
