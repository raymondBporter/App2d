using App2d.Core.Characters;

namespace App2d.CharacterStudio.CharacterSeeds;

/// <summary>
/// The declared module extractions (character-system migration, P10b): the Person's upper and lower body and the quadruped's
/// lower body and head as reusable rig modules under <c>parts/</c>, each with its animation target mapping, through
/// <see cref="ModuleConversion"/>. `--extract-modules` runs them on a scratch tree with a report each.
/// </summary>
public static class ModulePlans
{
    public const string PersonUpper = "person-upper", PersonLower = "person-lower", QuadrupedLower = "quadruped-lower", QuadrupedHead = "quadruped-head";
    public const string UpperBody = "upper-body", LowerBody = "lower-body", Head = "head";
    /// <summary>The named point on the quadruped's back a rider or torso mounts on.</summary>
    public const string Withers = "withers";

    public static IReadOnlyList<(string Model, ModuleConversion.Plan Plan)> All =>
    [
        // The Person above the hips: chest (the connection), head and both arms with their chains; the torso shape keeps its
        // lower end on a stand-in for the hips, so the torso measure and the width/torso inputs still land inside.
        ("person", new(PersonUpper, "Person upper body", Humanoid.Chest)
        {
            Usage = UpperBody,
            StandIns = { [Humanoid.Hips] = "waist" },
            Default = "idle",
        }),
        // The Person below the chest: hips (the connection) and both legs; the torso shape stays with the wearer.
        ("person", new(PersonLower, "Person lower body", Humanoid.Hips)
        {
            Usage = LowerBody,
            Exclude = { Humanoid.Chest },
            Leave = { Humanoid.Body },
            Default = "idle",
            KeepTravel = true,
        }),
        // The quadruped without its head: body (the connection), tail and the four planted legs; the gait's travel and
        // contacts are the module's, since a lower body owns the wearer's motion; the withers is where a torso mounts.
        ("quadruped", new(QuadrupedLower, "Quadruped lower body", "body")
        {
            Usage = LowerBody,
            Exclude = { "head" },
            Default = "idle",
            KeepTravel = true,
            Points = { (Withers, "body", .5f, .42f) },
        }),
        ("quadruped", new(QuadrupedHead, "Quadruped head", "head")
        {
            Usage = Head,
            Default = "idle",
        }),
    ];

    /// <summary>Extracts every declared module on a scratch tree; the caller moves what it accepts into the authored tree.</summary>
    public static IReadOnlyList<ModuleConversion.Result> Extract(string root, string output) =>
        [.. All.Select(entry => ModuleConversion.Convert(root, entry.Model, entry.Plan, output))];
}
