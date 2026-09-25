using System.Numerics;

namespace App2d.Core.Characters;

/// <summary>
/// Person build values: multipliers on the template's proportions. A variant stores them under <c>build</c>; the
/// <see cref="Rule"/> turns them into rest geometry when the variant resolves. An authoring shortcut, not a parameter system.
/// </summary>
public sealed record PersonBuild
{
    public const string RuleId = "person";
    public static readonly Limit Range = new(.5f, 2);
    public float Legs { get; init; } = 1;
    public float Torso { get; init; } = 1;
    public float Arms { get; init; } = 1;
    public float Width { get; init; } = 1;
    public float Head { get; init; } = 1;

    public static PersonBuild TallThin { get; } = new() { Legs = 1.2f, Torso = 1.12f, Arms = 1.15f, Width = .8f, Head = .92f };
    public static PersonBuild ShortBroad { get; } = new() { Legs = .82f, Torso = .9f, Arms = .88f, Width = 1.3f, Head = 1.1f };
    public static IBuildRule Rule { get; } = new PersonBuildRule();

    public Dictionary<string, float> Values() => new(StringComparer.Ordinal) { ["legs"] = Legs, ["torso"] = Torso, ["arms"] = Arms, ["width"] = Width, ["head"] = Head };
    public static PersonBuild From(IReadOnlyDictionary<string, float> values)
    {
        float Get(string id) => values.TryGetValue(id, out var value) ? value : 1;
        return new() { Legs = Get("legs"), Torso = Get("torso"), Arms = Get("arms"), Width = Get("width"), Head = Get("head") };
    }

    /// <summary>A variant of <paramref name="person"/> holding these build values; values equal to the default are not stored.</summary>
    public ModelVariant Apply(CharacterModel person, string id, string name, string? bodyFill = null)
    {
        Range.Check(Legs, "legs"); Range.Check(Torso, "torso"); Range.Check(Arms, "arms"); Range.Check(Width, "width"); Range.Check(Head, "head");
        if (person.Build != RuleId) throw new InvalidDataException($"Person build values apply to models using the '{RuleId}' build rule, not '{person.Id}'.");
        var variant = new ModelVariant { Id = id, Name = name, Base = person.Id };
        foreach (var (value, amount) in Values()) if (amount != 1) variant.Build[value] = amount;
        if (bodyFill is not null) variant.Parts["body"] = new() { Fill = bodyFill };
        variant.Validate(); return variant;
    }

    private sealed class PersonBuildRule : IBuildRule
    {
        private static readonly string[] Controls =
            ["hips", "chest", "head", .. new[] { "left", "right" }.SelectMany(s => new[] { "foot", "hip", "knee", "shoulder", "elbow", "hand" }.Select(c => s + "-" + c))];
        public string Id => RuleId;
        public IReadOnlyList<BuildValue> Values { get; } =
        [
            new("legs", "Leg length", Range.Soft(.7f, 1.4f)), new("torso", "Torso length", Range.Soft(.7f, 1.4f)),
            new("arms", "Arm length", Range.Soft(.7f, 1.4f)), new("width", "Width", Range.Soft(.6f, 1.6f)), new("head", "Head size", Range.Soft(.7f, 1.4f)),
        ];
        public IReadOnlyList<BuildPreset> Presets { get; } =
        [
            new("standard", "Standard", new PersonBuild().Values()),
            new("tall-thin", "Tall / thin", TallThin.Values()),
            new("short-broad", "Short / broad", ShortBroad.Values()),
        ];

        public void Check(CharacterModel model)
        {
            var missing = Controls.Where(c => !model.Controls.Any(m => m.Id == c)).Concat(new[] { "body", "head" }.Where(p => !model.Parts.Any(m => m.Id == p))).ToArray();
            if (missing.Length > 0) throw new InvalidDataException($"Model '{model.Id}': the '{RuleId}' build rule needs {string.Join(", ", missing)}.");
        }

        public void Apply(CharacterModel model, IReadOnlyDictionary<string, float> values, Dictionary<string, Vector3> rest, List<PuppetPart> parts)
        {
            var b = From(values);
            if (b == new PersonBuild()) return;
            var built = new Dictionary<string, Vector3>(StringComparer.Ordinal);
            // Feet stay on the ground; everything above them scales from it. Width spreads the hips and shoulders, and their depth.
            var ground = rest["left-foot"].Y;
            float Lift(float y) => ground + (y - ground) * b.Legs;
            Vector3 Across(Vector3 v) => new(v.X * b.Width, v.Y, v.Z * b.Width);
            built["hips"] = rest["hips"] with { Y = Lift(rest["hips"].Y) };
            built["chest"] = built["hips"] + (rest["chest"] - rest["hips"]) * b.Torso;
            built["head"] = built["chest"] + (rest["head"] - rest["chest"]) * b.Torso;
            foreach (var side in new[] { "left", "right" })
            {
                string Side(string control) => side + "-" + control;
                built[Side("foot")] = Across(rest[Side("foot")]);
                built[Side("hip")] = Across(rest[Side("hip")]) with { Y = Lift(rest[Side("hip")].Y) };
                built[Side("knee")] = built[Side("foot")] + (rest[Side("knee")] - rest[Side("foot")]) * b.Legs;
                var shoulder = rest[Side("shoulder")] - rest["chest"];
                built[Side("shoulder")] = built["chest"] + new Vector3(shoulder.X * b.Width, shoulder.Y * b.Torso, shoulder.Z * b.Width);
                built[Side("elbow")] = built[Side("shoulder")] + (rest[Side("elbow")] - rest[Side("shoulder")]) * b.Arms;
                built[Side("hand")] = built[Side("elbow")] + (rest[Side("hand")] - rest[Side("elbow")]) * b.Arms;
            }
            foreach (var (control, point) in built) rest[control] = point;
            var body = parts.FindIndex(p => p.Id == "body"); var head = parts.FindIndex(p => p.Id == "head");
            parts[body] = parts[body] with { Width = parts[body].Width * b.Width, Height = parts[body].Height * b.Torso, OffsetY = parts[body].OffsetY * b.Torso };
            parts[head] = parts[head] with { Width = parts[head].Width * b.Head, Height = parts[head].Height * b.Head };
        }
    }
}
