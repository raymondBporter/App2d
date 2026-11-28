using App2d.Contracts.Enemies;
using App2d.Contracts.World;
using App2d.Core.Geometry.Primitives;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Persons.Actions;
using App2d.Gameplay.Simulation;
using System.Collections.Immutable;

namespace App2d.Gameplay.World;

/// <summary>Adapts the level simulation and contact rules to the session.</summary>
public sealed class SideScrollerSessionWorld2D(
    SideScrollerLevel2D level, ContactDamageSystem2D contactDamage) : ISideScrollerSessionWorld2D
{
    public LevelContent2D CaptureContent() => level.CaptureContent();
    public WorldState2D CaptureWorld() => level.CaptureWorld();
    public ImmutableArray<EnemyState2D> CaptureEnemies() => level.EnemySystem.CaptureStates();
    public ImmutableArray<EnemyEvent2D> DrainEnemyEvents() => level.EnemySystem.DrainEvents();
    public Rect2f Bounds => level.TileMap.WorldBounds;
    public float GoalX => level.GoalX;
    public bool CanReachGoal(Rect2f playerBounds) => level.CanReachGoal(playerBounds);
    public void UpdateStreaming(Vector2f focus) => level.UpdateStreaming(focus);
    public void UpdateWorldMotion(float dt) => level.UpdateWorldMotion(dt);
    public void UpdateEnemies(float dt, Vector2f target) => level.EnemySystem.Update(dt, target);
    public void SyncEnemiesAfterPhysics() => level.EnemySystem.SyncAfterPhysics();
    public WorldThingSpec2D? UpdateSavePoints(float dt, Rect2f bounds) => level.UpdateSavePoints(dt, bounds);
    public void SetActiveSavePoint(long? id) => level.SetActiveSavePoint(id);

    public void ResolveDamage(Person2D player)
    {
        if (player.IsAlive && player.Actions is PersonArsenal2D arsenal)
        {
            foreach (var hitbox in arsenal.GetActiveSwordHitboxes())
            {
                level.CutGrass(hitbox.WorldBounds);
            }
        }

        _ = level.EnemySystem.TryResolvePlayerHits(player);
        if (!player.DownAttackBouncedThisFrame &&
            level.TryGetSpikeSource(player.WorldObject.WorldBounds, out var sourceX))
        {
            _ = player.TryTakeDamageFromX(1, sourceX, 180f, 300f);
        }

        _ = contactDamage.Resolve(player);
    }
}
