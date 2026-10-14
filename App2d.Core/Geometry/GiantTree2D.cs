using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Collections.Immutable;
using System.Numerics;

namespace App2d.Core.Geometry;

/// <summary>Dimensions in world units. Branch lengths are measured beyond the trunk's edge.</summary>
public sealed record GiantTreeSettings2D
{
    public float Height { get; init; } = 1_600f;
    public float TrunkWidth { get; init; } = 192f;
    public Interval<float> BranchLength { get; init; } = new(128f, 384f);
    public Interval<float> BranchGap { get; init; } = new(64f, 96f);
    public float BranchThickness { get; init; } = 24f;
    public Interval<float> FoliageRadius { get; init; } = new(56f, 112f);

    public void Validate()
    {
        ArgGuard.ThrowIfNotFiniteOrNotPositive(Height);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(TrunkWidth);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(BranchThickness);
        ValidateRange(BranchLength, nameof(BranchLength));
        ValidateRange(BranchGap, nameof(BranchGap));
        ValidateRange(FoliageRadius, nameof(FoliageRadius));
        ArgGuard.ThrowIf(BranchThickness >= BranchGap.Min, "Branches must be thinner than their minimum spacing.", nameof(BranchThickness));
        ArgGuard.ThrowIf(Height * 0.9 <= BranchGap.Max, "The tree must be taller than its first branch.", nameof(Height));
        ArgGuard.ThrowIf((double)Height / BranchGap.Min > 256, "Use wider branch spacing; a tree supports at most 256 branch levels.", nameof(BranchGap));
    }

    private static void ValidateRange(Interval<float> range, string name)
    {
        ArgGuard.ThrowIfNotFiniteOrNotPositive(range.Min, name);
        ArgGuard.ThrowIfNotFiniteOrLessThan(range.Max, range.Min, name);
        ArgGuard.ThrowIfNotFinite(range.Length, name);
    }
}

/// <summary>A horizontal, flat-topped limb. Side is -1 for left or +1 for right.</summary>
public readonly record struct GiantTreeBranch2D(Rect2D Bounds, int Side);

/// <summary>
/// Immutable seeded level geometry. Simulation and rendering share these exact branch tops;
/// decorative randomness never changes the platforms. The trunk is background scenery.
/// </summary>
public sealed class GiantTree2D
{
    public GiantTree2D(Vector2 root, GiantTreeSettings2D settings, int seed)
    {
        ArgGuard.ThrowIfNotFinite(root);
        ArgGuard.ThrowIfNull(settings);
        settings.Validate();
        Root = root;
        Settings = settings;
        Seed = seed;
        TrunkBounds = new(root - new Vector2(settings.TrunkWidth * 0.75f, 0),
            root + new Vector2(settings.TrunkWidth * 0.75f, settings.Height));
        var crownRadius = Math.Max(settings.TrunkWidth, settings.FoliageRadius.Max * 1.7f);
        var reach = Math.Max(settings.TrunkWidth * 0.5f + settings.BranchLength.Max + settings.FoliageRadius.Max * 1.2f,
            crownRadius * 1.8f) + 12f;
        Bounds = new(root - new Vector2(reach, settings.FoliageRadius.Max * 0.6f + 12f),
            root + new Vector2(reach, settings.Height + crownRadius * 1.1f + 12f));
        ArgGuard.ThrowIf(!Bounds.IsFinite || !TrunkBounds.IsFinite, "Tree geometry must fit in finite world coordinates.", nameof(root));

        var random = new Random(seed);
        var branches = ImmutableArray.CreateBuilder<GiantTreeBranch2D>();
        var side = random.Next(2) == 0 ? -1 : 1;
        var height = Sample(settings.BranchGap, random);
        while (height < settings.Height * 0.9f)
        {
            AddBranch(side, height);
            if (random.NextSingle() < 0.3f) AddBranch(-side, height);
            if (random.NextSingle() < 0.65f) side = -side;
            height += Sample(settings.BranchGap, random);
        }
        Branches = branches.ToImmutable();

        void AddBranch(int direction, float y)
        {
            var startX = root.X + direction * settings.TrunkWidth * 0.15f;
            var endX = root.X + direction * (settings.TrunkWidth * 0.5f + Sample(settings.BranchLength, random));
            branches.Add(new(new(new(Math.Min(startX, endX), root.Y + y - settings.BranchThickness),
                new(Math.Max(startX, endX), root.Y + y)), direction));
        }
    }

    public Vector2 Root { get; }
    public int Seed { get; }
    public GiantTreeSettings2D Settings { get; }
    public Rect2D TrunkBounds { get; }
    /// <summary>Includes the canopy and a margin for leaf motion, even when the root is off-screen.</summary>
    public Rect2D Bounds { get; }
    public ImmutableArray<GiantTreeBranch2D> Branches { get; }

    private static float Sample(Interval<float> range, Random random) => random.NextFloat(range.Min, range.Max);
}
