using App2d.Core.Geometry;
using App2d.Gameplay.Persons;
using App2d.Gameplay.World;
using System.Numerics;
using System.Collections.Immutable;
using App2d.Gameplay.Enemies;

namespace App2d.Gameplay.Simulation;

/// <summary>
/// Simulation operations required by the current side-scroller session.
/// The production level and test worlds implement the same renderer-free boundary.
/// </summary>
public interface ISideScrollerSessionWorld2D
{
    ImmutableArray<int> TerrainColliderIds => [];
    WorldSimulationState2D CaptureSimulation() => throw new NotSupportedException("This world does not support rollback.");
    void ValidateSimulation(WorldSimulationState2D state) => throw new NotSupportedException("This world does not support rollback.");
    void RestoreSimulation(WorldSimulationState2D state) => throw new NotSupportedException("This world does not support rollback.");
    LevelContent2D CaptureContent() => LevelContent2D.Empty;
    WorldState2D CaptureWorld() => WorldState2D.Empty;
    ImmutableArray<EnemyState2D> CaptureEnemies() => [];
    ImmutableArray<EnemyEvent2D> DrainEnemyEvents() => [];
    Bounds2D Bounds { get; }
    float GoalX { get; }
    void UpdateStreaming(Vector2 focus);
    void UpdateMovingPlatforms(float deltaSeconds);
    void UpdateEnemies(float deltaSeconds, Vector2 targetPosition);
    void SyncEnemiesAfterPhysics();
    void ResolveDamage(Person2D player);
    WorldThingSpec2D? UpdateSavePoints(float deltaSeconds, Bounds2D playerBounds);
    void SetActiveSavePoint(long? id);
}
