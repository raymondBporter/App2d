using App2d.Contracts.Enemies;
using App2d.Contracts.World;
using App2d.Core.Geometry;
using App2d.Gameplay.Persons;
using App2d.Gameplay.World;
using System.Collections.Immutable;
using System.Numerics;

namespace App2d.Gameplay.Simulation;

/// <summary>
/// Simulation operations required by the current side-scroller session.
/// The production level and test worlds implement the same renderer-free boundary.
/// </summary>
public interface ISideScrollerSessionWorld2D
{
    LevelContent2D CaptureContent() => LevelContent2D.Empty;
    WorldState2D CaptureWorld() => WorldState2D.Empty;
    ImmutableArray<EnemyState2D> CaptureEnemies() => [];
    ImmutableArray<EnemyEvent2D> DrainEnemyEvents() => [];
    Rect2D Bounds { get; }
    float GoalX { get; }
    void UpdateStreaming(Vector2 focus);
    void UpdateMovingPlatforms(float deltaSeconds);
    void UpdateEnemies(float deltaSeconds, Vector2 targetPosition);
    void SyncEnemiesAfterPhysics();
    void ResolveDamage(Person2D player);
    WorldThingSpec2D? UpdateSavePoints(float deltaSeconds, Rect2D playerBounds);
    void SetActiveSavePoint(long? id);
}
