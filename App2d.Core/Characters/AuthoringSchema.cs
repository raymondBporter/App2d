using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace App2d.Core.Characters;

/// <summary>Closed vocabularies shared by validators, editors and game consumers. A new value is added here once.</summary>
public static class EntityVocabulary
{
    public static readonly IReadOnlyList<string> Weapons = ["sword", "rapier", "mace", "hammer", "pistol"];
    /// <summary>Per-action weapon override: inherit the look's weapon, hide weapons, or force one.</summary>
    public static readonly IReadOnlyList<string> ActionWeapons = ["inherit", "none", .. Weapons];
    public static readonly IReadOnlyList<string> SoundCues = ["none", "swing", "shot", "hit", "heavy", "bite"];
    public static readonly IReadOnlyList<string> Behaviors = ["player", "melee", "ranged", "passive"];
    public static readonly IReadOnlyList<string> AttackKinds = ["none", "melee", "projectile"];
    public static readonly IReadOnlyList<string> Attachments = ["root", "head", "hand", "muzzle"];
    public static readonly IReadOnlyList<string> Regions = ["body", "head", "legs", "arms"];
    public static readonly IReadOnlyList<string> RegionModes = ["anatomy", "custom", "disabled"];
    /// <summary>Actions every type must define; the game and the studio fall back to these.</summary>
    public static readonly IReadOnlyList<string> RequiredActions = ["idle", "walk", "attack", "hit", "death"];
    /// <summary>Anatomies that have a graphics-free pose evaluator and can therefore become entity types.</summary>
    public static readonly IReadOnlyList<string> EntityAnatomies = ["person", "hound"];

    public static void Require(string? value, IReadOnlyList<string> allowed, string field)
    {
        if (value is null || !allowed.Contains(value, StringComparer.Ordinal))
            throw new InvalidDataException($"{field} must be one of {string.Join(", ", allowed)}; found '{value}'.");
    }
}

/// <summary>An authored value's accepted range. The soft range is the editor's suggested slider span; files may hold any value within the hard range.</summary>
public readonly record struct Limit(float Min, float Max, float SoftMin, float SoftMax)
{
    public Limit(float min, float max) : this(min, max, min, max) { }
    public Limit Soft(float min, float max) => this with { SoftMin = min, SoftMax = max };
    public float Clamp(float value) => Math.Clamp(value, Min, Max);
    public void Check(float value, string field)
    {
        if (!float.IsFinite(value) || value < Min || value > Max)
            throw new InvalidDataException($"{field} must be between {Min} and {Max}; found {value}.");
    }
    public static void Color(string? value, string field)
    {
        if (value is null || value.Length != 7 || value[0] != '#' || !uint.TryParse(value.AsSpan(1), System.Globalization.NumberStyles.HexNumber, null, out _))
            throw new InvalidDataException($"{field} must be a #rrggbb color; found '{value}'.");
    }
}

/// <summary>Serializer settings for authored files: camelCase, tolerant of member case, strict about unknown members, and sparse. Values equal to the type's defaults are omitted so files stay small and diffs stay readable.</summary>
public static class AuthoredJson
{
    public static JsonSerializerOptions Options { get; } = Create(strict: true);
    /// <summary>For files written by other tools, such as head workshop exports, where extra members are tolerated.</summary>
    public static JsonSerializerOptions Tolerant { get; } = Create(strict: false);
    // Identity members are always written even when they equal the defaults, so a file states what it is.
    private static readonly HashSet<string> AlwaysWritten = new(StringComparer.Ordinal) { "format", "version", "id", "name", "library", "clip" };

    private static JsonSerializerOptions Create(bool strict)
    {
        var resolver = new DefaultJsonTypeInfoResolver(); resolver.Modifiers.Add(OmitDefaults);
        return new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, WriteIndented = true,
            UnmappedMemberHandling = strict ? JsonUnmappedMemberHandling.Disallow : JsonUnmappedMemberHandling.Skip,
            TypeInfoResolver = resolver
        };
    }
    private static void OmitDefaults(JsonTypeInfo info)
    {
        if (info.Kind != JsonTypeInfoKind.Object || info.Type.Namespace != typeof(AuthoredJson).Namespace || info.Type.GetConstructor(Type.EmptyTypes) is null) return;
        var defaults = Activator.CreateInstance(info.Type)!;
        foreach (var property in info.Properties)
        {
            if (property.Get is null || AlwaysWritten.Contains(property.Name)) continue;
            var baseline = property.Get(defaults);
            property.ShouldSerialize = (_, value) => !Equals(value, baseline);
        }
    }
}

/// <summary>Named indices of the exported person controls. Verified against each library so a re-exported rig fails loudly instead of mis-attaching hands and heads.</summary>
public static class PersonRig
{
    public const int Head = 0, BodyBottomA = 1, BodyBottomB = 2, BodyTopA = 3, BodyTopB = 4,
        LeftHip = 5, LeftKnee = 6, LeftFoot = 7, RightHip = 8, RightKnee = 9, RightFoot = 10,
        LeftShoulder = 11, LeftElbow = 12, LeftHand = 13, RightShoulder = 14, RightElbow = 15, RightHand = 16,
        HeadUp = 17, RightSwordGrip = 18, RightSwordTip = 19, LeftSwordGrip = 20, LeftSwordTip = 21, RightSwordWidth = 22, LeftSwordWidth = 23;
    public static readonly IReadOnlyList<string> Names =
    [
        "head", "body_bottom_a", "body_bottom_b", "body_top_a", "body_top_b",
        "leg_l_0", "leg_l_1", "leg_l_2", "leg_r_0", "leg_r_1", "leg_r_2",
        "arm_l_0", "arm_l_1", "arm_l_2", "arm_r_0", "arm_r_1", "arm_r_2",
        "head_up", "sword_r_grip", "sword_r_tip", "sword_l_grip", "sword_l_tip", "sword_r_width", "sword_l_width"
    ];
    public static void Verify(PointLibrary library)
    {
        if (library.PointNames.SequenceEqual(Names, StringComparer.Ordinal)) return;
        var mismatch = Enumerable.Range(0, Math.Max(Names.Count, library.PointNames.Count))
            .First(i => i >= Names.Count || i >= library.PointNames.Count || Names[i] != library.PointNames[i]);
        var found = mismatch < library.PointNames.Count ? library.PointNames[mismatch] : "(missing)";
        var expected = mismatch < Names.Count ? Names[mismatch] : "(none)";
        throw new InvalidDataException($"Person library '{library.Id}' control {mismatch} is '{found}', expected '{expected}'. The person rig layout changed; update PersonRig and its consumers.");
    }
}
