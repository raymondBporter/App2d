using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.Core.Characters;

/// <summary>Rig-independent head workshop model. Local coordinates use X right, Y down.</summary>
public sealed record HeadShape
{
    public int Version { get; init; } = 1;
    public float Width { get; init; } = 1;
    public float Height { get; init; } = 1;
    public float Muzzle { get; init; } = .65f;
    public float Depth { get; init; } = .48f;
    public float Drop { get; init; } = .05f;
    public float Brow { get; init; }
    public float Jaw { get; init; } = .35f;
    public float Roundness { get; init; } = .9f;
    public float FaceX { get; init; }
    public float FaceY { get; init; } = -.04f;
    public float FaceSize { get; init; } = .9f;
    public float FaceAngle { get; init; }
    public string Face { get; init; } = "happy";
    public string Color { get; init; } = "#c8d4aa";
    public HeadOffsets Offsets { get; init; }
    public static IReadOnlyList<string> PointNames { get; } = Array.AsReadOnly(new[] { "Back", "Crown back", "Crown", "Forehead", "Bridge", "Upper nose", "Nose tip", "Lower nose", "Chin", "Nape" });

    public Vector2[] Points()
    {
        Vector2[] points = [new(-.6f * Width, 0), new(-.5f * Width, -.5f * Height), new(.05f * Width, -.6f * Height),
            new(.42f * Width, -.32f * Height + Brow * .3f), new(.5f * Width, Drop - Depth / 2),
            new(.5f * Width + Muzzle, Drop - Depth / 2), new(.56f * Width + Muzzle, Drop),
            new(.5f * Width + Muzzle, Drop + Depth / 2), new(.18f * Width + Muzzle * .28f, MathF.Max(Height * .3f, Drop + Depth / 2) + Jaw * .35f), new(-.45f * Width, Height * .4f)];
        for (var i = 0; i < points.Length; i++) points[i] += new Vector2(Offsets[i].X, Offsets[i].Y);
        return points;
    }
    public Vector2[] Contour()
    {
        var points = Points(); var result = new List<Vector2>(); var f = Roundness * .49f;
        for (var i = 0; i < points.Length; i++)
        {
            var p = points[i]; var entry = Vector2.Lerp(p, points[(i + 9) % 10], f); var exit = Vector2.Lerp(p, points[(i + 1) % 10], f);
            for (var k = 0; k <= 8; k++)
            {
                var t = k / 8f; var value = entry * ((1 - t) * (1 - t)) + p * (2 * t * (1 - t)) + exit * (t * t);
                if (result.Count == 0 || Vector2.DistanceSquared(result[^1], value) > 1e-12f) result.Add(value);
            }
        }
        return result.ToArray();
    }
    public bool IsSimple()
    {
        var points = Points();
        static float Cross(Vector2 a, Vector2 b, Vector2 c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        for (var i = 0; i < points.Length; i++) for (var j = i + 2; j < points.Length; j++)
        {
            if (i == 0 && j == points.Length - 1) continue;
            var a = points[i]; var b = points[(i + 1) % points.Length]; var c = points[j]; var d = points[(j + 1) % points.Length];
            if (Cross(a, b, c) * Cross(a, b, d) < -1e-9f && Cross(c, d, a) * Cross(c, d, b) < -1e-9f) return false;
        }
        return true;
    }
    public static class Limits
    {
        public static readonly Limit Width = new(.45f, 1.8f), Height = new(.45f, 1.8f), Muzzle = new(0, 1.8f), Depth = new(.12f, 1.2f),
            Drop = new(-.65f, .7f), Brow = new(-.6f, .6f), Jaw = new(0, 1), Roundness = new(0, 1),
            FaceX = new(-1.2f, 2.6f), FaceY = new(-1.2f, 1.4f), FaceSize = new(.25f, 2), FaceAngle = new(-90, 90), Offset = new(-1, 1);
    }
    public void Validate()
    {
        if (Version != 1) throw new InvalidDataException($"Expected a version 1 head configuration; found version {Version}.");
        Limits.Width.Check(Width, "head.width"); Limits.Height.Check(Height, "head.height"); Limits.Muzzle.Check(Muzzle, "head.muzzle"); Limits.Depth.Check(Depth, "head.depth");
        Limits.Drop.Check(Drop, "head.drop"); Limits.Brow.Check(Brow, "head.brow"); Limits.Jaw.Check(Jaw, "head.jaw"); Limits.Roundness.Check(Roundness, "head.roundness");
        Limits.FaceX.Check(FaceX, "head.faceX"); Limits.FaceY.Check(FaceY, "head.faceY"); Limits.FaceSize.Check(FaceSize, "head.faceSize"); Limits.FaceAngle.Check(FaceAngle, "head.faceAngle");
        for (var i = 0; i < 10; i++) { Limits.Offset.Check(Offsets[i].X, $"head.offsets[{i}].x"); Limits.Offset.Check(Offsets[i].Y, $"head.offsets[{i}].y"); }
        if (Face != "none" && !FaceExpressions.Contains(Face)) throw new InvalidDataException($"head.face '{Face}' is not a known expression.");
        Limit.Color(Color, "head.color");
        if (!IsSimple()) throw new InvalidDataException("That edit folds the head outline across itself.");
    }
}

public readonly record struct HeadOffset(float X, float Y);

// Value semantics keep appearance undo snapshots independent without mutable array copies.
[JsonConverter(typeof(HeadOffsetsConverter))]
public readonly record struct HeadOffsets(HeadOffset P0, HeadOffset P1, HeadOffset P2, HeadOffset P3, HeadOffset P4, HeadOffset P5, HeadOffset P6, HeadOffset P7, HeadOffset P8, HeadOffset P9)
{
    public HeadOffset this[int i] => i switch { 0 => P0, 1 => P1, 2 => P2, 3 => P3, 4 => P4, 5 => P5, 6 => P6, 7 => P7, 8 => P8, 9 => P9, _ => throw new ArgumentOutOfRangeException(nameof(i)) };
    public HeadOffsets With(int i, HeadOffset value) => i switch { 0 => this with { P0 = value }, 1 => this with { P1 = value }, 2 => this with { P2 = value }, 3 => this with { P3 = value }, 4 => this with { P4 = value }, 5 => this with { P5 = value }, 6 => this with { P6 = value }, 7 => this with { P7 = value }, 8 => this with { P8 = value }, 9 => this with { P9 = value }, _ => throw new ArgumentOutOfRangeException(nameof(i)) };
}

public sealed class HeadOffsetsConverter : JsonConverter<HeadOffsets>
{
    public override HeadOffsets Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader); var array = doc.RootElement;
        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() != 10) throw new JsonException("A head requires ten point offsets.");
        var offsets = new HeadOffsets();
        for (var i = 0; i < 10; i++)
        {
            if (array[i].ValueKind != JsonValueKind.Array || array[i].GetArrayLength() != 2 || !array[i][0].TryGetSingle(out var x) || !array[i][1].TryGetSingle(out var y)) throw new JsonException("Invalid head point offset.");
            offsets = offsets.With(i, new(x, y));
        }
        return offsets;
    }
    public override void Write(Utf8JsonWriter writer, HeadOffsets value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        for (var i = 0; i < 10; i++) { writer.WriteStartArray(); writer.WriteNumberValue(value[i].X); writer.WriteNumberValue(value[i].Y); writer.WriteEndArray(); }
        writer.WriteEndArray();
    }
}
