using App2d.Contracts.Player;
using App2d.Contracts.World;
using App2d.Core.Characters.Authored;
using App2d.Core.Objects;
using App2d.Core.Tiles;
using App2d.Core.Validation;
using App2d.Gameplay.World;
using App2d.Gameplay.World.Vegetation;
using System.Collections.Immutable;
namespace App2d.Gameplay.Simulation;

/// <summary>Collision layers shared by every session participant.</summary>
public static class SideScrollerLayers2D
{
    public const uint World = 1u << 0;
    public const uint Player = 1u << 1;
    public const uint Enemy = 1u << 2;
}

/// <summary>Where a returning player resumes: the authored checkpoint and health they saved with.</summary>
public readonly record struct SavedProgress2D(long SavePointId, int HitPoints);

/// <summary>
/// Everything needed to construct a session. Two sessions built from the same definition
/// in the same order get identical actors, identities, and physics; nothing here is a live
/// simulation object.
/// </summary>
public sealed record SideScrollerSessionDefinition2D(TraversalMetrics2D Traversal, IChunkedTileMap2D TileMap, IReadOnlyList<WorldThingSpec2D> WorldThings)
{
    /// <summary>Authored entities; placements they cover spawn from these, otherwise the built-in sprite enemies.</summary>
    public AuthoredCatalog? AuthoredCharacters { get; init; }
    public IReadOnlyList<ObjectPlacement2D> Objects { get; init; } = [];
    /// <summary>Rectangular regions shared by simulation and presentation consumers.</summary>
    public ImmutableArray<WorldZone2D> Zones { get; init; } = [];
    public ImmutableArray<GiantTree2D> GiantTrees { get; init; } = [];
    public int PlayerMaximumHealth { get; init; } = 5;
    public SpellTuning2D Spells { get; init; } = new();

    /// <summary>Resume point; ignored when it names a missing checkpoint or invalid health.</summary>
    public SavedProgress2D? SavedProgress { get; init; }
    public int PositionIterations { get; init; } = 3;
    public int VelocityIterations { get; init; } = 2;

    public SideScrollerSessionDefinition2D Validate()
    {
        Ensure.ThrowIfNull(Traversal);
        Ensure.ThrowIfNull(TileMap);
        Ensure.ThrowIfNull(WorldThings);
        Ensure.ThrowIfNull(Objects);
        foreach (var placement in Objects) placement.Validate();
        Ensure.ThrowIf(Objects.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != Objects.Count, "Object placements need unique IDs.", nameof(Objects));
        Ensure.ThrowIf(GiantTrees.IsDefault || GiantTrees.Any(tree => tree is null), "Trees must be initialized and non-null.", nameof(GiantTrees));
        if (Zones.IsDefault || Zones.Any(z => z is null) || Zones.Select(z => z.Id).Distinct(StringComparer.Ordinal).Count() != Zones.Length)
        {
            throw new ArgumentException("Zones must be initialized and have unique IDs.", nameof(Zones));
        }

        Ensure.Positive(PlayerMaximumHealth);
        Ensure.ThrowIfNull(Spells);
        Spells.Validate();
        Ensure.Positive(PositionIterations);
        Ensure.Positive(VelocityIterations);
        return this;
    }
}
