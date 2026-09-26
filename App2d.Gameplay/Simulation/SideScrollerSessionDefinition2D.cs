using App2d.Core;
using App2d.Gameplay.Player;
using App2d.Gameplay.World;
using App2d.Tiles;

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
/// Everything needed to construct a session. A server and a predicting client that share
/// a definition and construct from it in the same order get identical actors, identities,
/// and physics; nothing here is a live simulation object.
/// </summary>
public sealed record SideScrollerSessionDefinition2D(
    TraversalMetrics2D Traversal,
    IChunkedTileMap2D TileMap,
    IReadOnlyList<MovingPlatformSpec2D> MovingPlatforms,
    IReadOnlyList<WorldThingSpec2D> WorldThings)
{
    /// <summary>Authored entities; placements they cover spawn from these, otherwise the built-in sprite enemies.</summary>
    public App2d.Core.Characters.AuthoredCatalog? AuthoredCharacters { get; init; }
    public int PlayerMaximumHealth { get; init; } = 5;
    /// <summary>Resume point; ignored when it names a missing checkpoint or invalid health.</summary>
    public SavedProgress2D? SavedProgress { get; init; }
    public int PositionIterations { get; init; } = 3;
    public int VelocityIterations { get; init; } = 2;

    public SideScrollerSessionDefinition2D Validate()
    {
        ArgGuard.ThrowIfNull(Traversal);
        ArgGuard.ThrowIfNull(TileMap);
        ArgGuard.ThrowIfNull(MovingPlatforms);
        ArgGuard.ThrowIfNull(WorldThings);
        ArgGuard.ThrowIfNotPositive(PlayerMaximumHealth);
        ArgGuard.ThrowIfNotPositive(PositionIterations);
        ArgGuard.ThrowIfNotPositive(VelocityIterations);
        return this;
    }
}
