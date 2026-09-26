using App2d.Core.Characters;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Enemies;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Simulation;
using App2d.Gameplay.World;
using App2d.Levels;
using App2d.Physics;
using App2d.Tiles;
using System.Numerics;
using System.Text.Json;
using Xunit;

namespace App2d.Gameplay.Tests.Enemies;

/// <summary>Authored entities in the real game: spawning, the shared final pose, combat and rollback.</summary>
public sealed class AuthoredEntityEnemyTests
{
    private static readonly string CharactersRoot = Path.GetFullPath(Path.Combine(TestAssetPath.Root, "..", "Characters"));
    private static readonly AuthoredCatalog Authored = AuthoredCatalog.Load(Path.Combine(CharactersRoot, "authored"));

    private static SideScrollerSimulation2D Game()
    {
        var map = new EditableTileMap2D(640, 96, 32, 32, SideScrollerLevel2D.WorldOrigin, ["dark-cave"]);
        for (var x = 0; x < 640; x++) map.SetTileKind(x, 19, TileKind2D.Solid);
        return SideScrollerSimulation2D.Create(new(TraversalMetricsLoader2D.Load(TestAssetPath.Root), map, [],
        [new(1, WorldThingKind2D.PlayerSpawn, null, true, new(-368, 40)),
         new(2, WorldThingKind2D.Shieldback, null, true, new(-290, 42)),
         new(3, WorldThingKind2D.BoilerBrute, null, true, new(-80, 42)),
         new(5, WorldThingKind2D.GreenDinosaur, null, true, new(210, 42))])
        { Characters = new EntityCatalog(CharactersRoot), AuthoredCharacters = Authored, PlayerMaximumHealth = 30 });
    }

    [Fact]
    public void CoveredPlacementsSpawnAuthoredEntitiesAndCarryTheirFinalPose()
    {
        using var game = Game();
        var states = game.Session.CaptureEnemies();
        Assert.Equal(new[] { "spear-guard", "maul", "stalker-pest" }, states.Select(s => s.TypeId));
        var guard = Assert.IsType<AuthoredEntityEnemy2D>(game.Level.EnemySystem.Combatants[0]);
        Assert.IsType<AuthoredEnemy2D>(game.Level.EnemySystem.Combatants[1]);
        Assert.IsType<AuthoredEntityEnemy2D>(game.Level.EnemySystem.Combatants[2]);
        // Presentation receives the very pose object collision reads, never a second clock.
        Assert.Same(guard.Pose, states[0].AuthoredPose);
        Assert.Same(Authored.Entities["spear-guard"], states[0].AuthoredEntity);
    }

    [Fact]
    public void TheGuardThrustsAtThePlayerAndCombatReplaysExactly()
    {
        using var game = Game();
        for (var i = 0; i < 30; i++) game.Session.Advance();
        var checkpoint = game.Session.CaptureCheckpoint();
        string[] Run() => Enumerable.Range(0, 480).Select(i =>
        {
            var tick = game.Session.Tick + 1;
            var frame = game.Session.Advance(new PlayerInput2D(game.Player.Id, tick, tick, new PersonCommand2D { MoveX = i < 60 ? .3f : 0 }));
            var guard = frame.Enemies[0];
            return JsonSerializer.Serialize(new { frame.Tick, Health = game.Player.Health.Current, guard.Position, guard.ActionId, guard.ActionSeconds, Points = guard.AuthoredPose!.Local.Points.Values.ToArray() },
                new JsonSerializerOptions { IncludeFields = true });
        }).ToArray();
        var first = Run(); var damaged = game.Player.Health.Current;
        game.Session.RestoreCheckpoint(checkpoint); var second = Run();
        Assert.Equal(first, second);
        Assert.Contains(first, json => json.Contains("\"ActionId\":\"attack\""));
        Assert.True(damaged < 30, "the spear reached the player");
    }

    [Fact]
    public void PlayerAttacksLandOnPoseDerivedHurtRegionsAboveTheMovementBox()
    {
        using var game = Game();
        var guard = Assert.IsType<AuthoredEntityEnemy2D>(game.Level.EnemySystem.Combatants[0]);
        var head = EntityCollision.Hurt(guard.Entity, guard.Pose).Single(r => r.Id == "head");
        var center = head.Points.Aggregate(Vector2.Zero, (a, b) => a + b) / head.Points.Count * EntityCatalog.WorldUnits;
        Assert.True(center.Y > guard.WorldObject.WorldBounds.Max.Y - 30, "the head region comes from the pose, not the movement box");
        var hit = new App2d.Core.SpatialObject2D(App2d.Core.Geometry.AxisAlignedRectangle2D.FromSize(new(2)));
        hit.Transform.Position = center;
        Assert.True(game.Combat.ResolveAttack(hit, game.Player.Id, 900, CombatFaction2D.Player, SideScrollerLayers2D.Enemy, 1, _ => Vector2.Zero));
        Assert.Equal(guard.Entity.Asset.Health - 1, guard.Health.Current);
        Assert.False(game.Combat.ResolveAttack(hit, game.Player.Id, 900, CombatFaction2D.Player, SideScrollerLayers2D.Enemy, 1, _ => Vector2.Zero));
    }

    [Fact]
    public void TheSpearHitboxFollowsThePropInBothFacings()
    {
        foreach (var side in new[] { -1, 1 })
        {
            var physics = new PhysicsWorld2D { Gravity = Vector2.Zero };
            var guard = new AuthoredEntityEnemy2D(App2d.Core.EntityId2D.Create(), Authored.Entities["spear-guard"], physics, new(0, 42), 1, 4);
            guard.SetSimulationEnabled(true);
            var target = new Vector2(side * 80, 42); var sawHitbox = false;
            for (var i = 0; i < 240; i++)
            {
                guard.Update(1f / 120, target); guard.SyncAfterPhysics();
                foreach (var box in guard.GetActiveAttackHitboxes())
                {
                    sawHitbox = true;
                    var spear = guard.Entity.Equipment[0];
                    var tip = ActorPose.PropPoint(guard.Pose.Socket(spear.Socket), spear.Prop, spear.Prop.Tip) * EntityCatalog.WorldUnits;
                    var bounds = box.WorldBounds;
                    Assert.InRange(tip.X, bounds.Min.X, bounds.Max.X); Assert.InRange(tip.Y, bounds.Min.Y, bounds.Max.Y);
                    Assert.Equal(side, Math.Sign(bounds.Center.X - guard.WorldObject.Transform.Position.X));
                }
            }
            Assert.True(sawHitbox);
        }
    }
}
