using App2d.Core.Geometry;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Simulation;
using System.Numerics;
using System.Collections.Immutable;
using App2d.Gameplay.Enemies;

namespace App2d.Gameplay.World;

/// <summary>Adapts the level simulation and contact rules to the session.</summary>
public sealed class SideScrollerSessionWorld2D(
    SideScrollerLevel2D level, ContactDamageSystem2D contactDamage) : ISideScrollerSessionWorld2D
{
    public ImmutableArray<int> TerrainColliderIds => level.TerrainColliderIds;
    public WorldSimulationState2D CaptureSimulation() => level.CaptureSimulation();
    public void ValidateSimulation(WorldSimulationState2D state) => level.ValidateSimulation(state);
    public void RestoreSimulation(WorldSimulationState2D state) => level.RestoreSimulation(state);
    public WorldState2D CaptureWorld() => level.CaptureState();
    public ImmutableArray<EnemyState2D> CaptureEnemies() => level.EnemySystem.CaptureStates();
    public ImmutableArray<EnemyEvent2D> DrainEnemyEvents() => level.EnemySystem.DrainEvents();
    public Bounds2D Bounds => level.TileMap.WorldBounds;
    public float GoalX => level.GoalX;
    public void UpdateStreaming(Vector2 focus) => level.UpdateStreaming(focus);
    public void UpdateMovingPlatforms(float dt) => level.UpdateMovingPlatforms(dt);
    public void UpdateEnemies(float dt, Vector2 target) => level.EnemySystem.Update(dt, target);
    public void SyncEnemiesAfterPhysics() => level.EnemySystem.SyncAfterPhysics();
    public WorldThingSpec2D? UpdateSavePoints(float dt, Bounds2D bounds) => level.UpdateSavePoints(dt, bounds);
    public void SetActiveSavePoint(long? id) => level.SetActiveSavePoint(id);

    public void ResolveDamage(Person2D player)
    {
        _ = level.EnemySystem.TryResolvePlayerHits(player);
        if (!player.DownAttackBouncedThisFrame &&
            level.TryGetSpikeSource(player.WorldObject.WorldBounds, out var sourceX))
            _ = player.TryTakeDamageFromX(1, sourceX, 180f, 300f);
        _ = contactDamage.Resolve(player);
    }
}
