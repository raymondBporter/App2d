using System.Numerics;

namespace App2d.Core.Characters;

/// <summary>
/// Person build values: multipliers on the template's proportions, applied by the explicit geometry below and written
/// as ordinary variant overrides. An authoring shortcut, not a parameter system or a live parent.
/// </summary>
public sealed record PersonBuild
{
    public static readonly Limit Range = new(.5f, 2);
    public float Legs { get; init; } = 1;
    public float Torso { get; init; } = 1;
    public float Arms { get; init; } = 1;
    public float Width { get; init; } = 1;
    public float Head { get; init; } = 1;

    public static PersonBuild TallThin { get; } = new() { Legs = 1.2f, Torso = 1.12f, Arms = 1.15f, Width = .8f, Head = .92f };
    public static PersonBuild ShortBroad { get; } = new() { Legs = .82f, Torso = .9f, Arms = .88f, Width = 1.3f, Head = 1.1f };

    public ModelVariant Apply(CharacterModel person, string id, string name, string? bodyFill = null)
    {
        Range.Check(Legs, "legs"); Range.Check(Torso, "torso"); Range.Check(Arms, "arms"); Range.Check(Width, "width"); Range.Check(Head, "head");
        if (person.Id != PersonTemplate.Id) throw new InvalidDataException($"Person build values apply to the '{PersonTemplate.Id}' template, not '{person.Id}'.");
        var rest = person.Controls.ToDictionary(c => c.Id, c => c.Rest.XYZ, StringComparer.Ordinal);
        var built = new Dictionary<string, Vector3>(StringComparer.Ordinal);
        // Feet stay on the ground; everything above them scales from it. Width spreads the hips and shoulders, and their depth.
        var ground = rest["left-foot"].Y;
        float Lift(float y) => ground + (y - ground) * Legs;
        Vector3 Across(Vector3 v) => new(v.X * Width, v.Y, v.Z * Width);
        built["hips"] = rest["hips"] with { Y = Lift(rest["hips"].Y) };
        built["chest"] = built["hips"] + (rest["chest"] - rest["hips"]) * Torso;
        built["head"] = built["chest"] + (rest["head"] - rest["chest"]) * Torso;
        foreach (var side in new[] { "left", "right" })
        {
            string Side(string control) => side + "-" + control;
            built[Side("foot")] = Across(rest[Side("foot")]);
            built[Side("hip")] = Across(rest[Side("hip")]) with { Y = Lift(rest[Side("hip")].Y) };
            built[Side("knee")] = built[Side("foot")] + (rest[Side("knee")] - rest[Side("foot")]) * Legs;
            var shoulder = rest[Side("shoulder")] - rest["chest"];
            built[Side("shoulder")] = built["chest"] + new Vector3(shoulder.X * Width, shoulder.Y * Torso, shoulder.Z * Width);
            built[Side("elbow")] = built[Side("shoulder")] + (rest[Side("elbow")] - rest[Side("shoulder")]) * Arms;
            built[Side("hand")] = built[Side("elbow")] + (rest[Side("hand")] - rest[Side("elbow")]) * Arms;
        }
        var variant = new ModelVariant { Id = id, Name = name, Base = person.Id };
        foreach (var (control, point) in built) if (point != rest[control]) variant.Rest[control] = PuppetPoint.From(point);
        var body = person.Parts.Single(p => p.Id == "body"); var head = person.Parts.Single(p => p.Id == "head");
        variant.Parts["body"] = new() { Width = body.Width * Width, Height = body.Height * Torso, OffsetY = body.OffsetY * Torso, Fill = bodyFill };
        variant.Parts["head"] = new() { Width = head.Width * Head, Height = head.Height * Head };
        variant.Validate(); return variant;
    }
}
