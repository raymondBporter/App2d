using App2d.Contracts.Enemies;
using App2d.Contracts.World;
using App2d.Core.Geometry.Primitives;
using App2d.Gameplay.Persons;
using App2d.Gameplay.World;
using System.Collections.Immutable;

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
    Rect2f Bounds { get; }
    float GoalX { get; }
    bool CanReachGoal(Rect2f playerBounds) => playerBounds.Center.X >= GoalX;
    void UpdateStreaming(Vector2f focus);
    void UpdateWorldMotion(float deltaSeconds);
    void UpdateEnemies(float deltaSeconds, Vector2f targetPosition);
    void SyncEnemiesAfterPhysics();
    void ResolveDamage(Person2D player);
    WorldThingSpec2D? UpdateSavePoints(float deltaSeconds, Rect2f playerBounds);
    void SetActiveSavePoint(long? id);
}
