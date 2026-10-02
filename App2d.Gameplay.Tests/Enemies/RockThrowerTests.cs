using App2d.Contracts.Enemies;
using App2d.Contracts.World;
using App2d.Core;
using App2d.Core.Characters.Authored;
using App2d.Core.Physics;
using App2d.Core.Shapes;
using App2d.Contracts.Combat;
using App2d.Gameplay.Enemies;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Simulation;
using App2d.Gameplay.World;
using App2d.Levels;
using App2d.Tiles;
using System.Numerics;
using System.Text.Json;
using Xunit;

namespace App2d.Gameplay.Tests.Enemies;

public sealed class RockThrowerTests
{
    private static readonly AuthoredCatalog Catalog = AuthoredCatalog.Load(Path.GetFullPath(Path.Combine(TestAssetPath.Root, "..", "Characters", "authored")));
    private static (AuthoredEntityEnemy2D Enemy, PhysicsWorld2D Physics, Person2D Player) Create(int facing = 1)
    {
        var physics = new PhysicsWorld2D { Gravity = Vector2.Zero };
        var enemy = new AuthoredEntityEnemy2D(EntityId2D.Create(), Catalog.Entities["rock-thrower"], physics, new(0, 40), 1, 4);
        enemy.SetSimulationEnabled(true);
        var player = new Person2D(EntityId2D.Create(), physics.CollisionSystem, physics, TraversalMetricsLoader2D.Load(TestAssetPath.Root), new(facing * 200, 26), 2, 1, CombatFaction2D.Player, 30);
        return (enemy, physics, player);
    }

    private static void Tick(AuthoredEntityEnemy2D enemy, Vector2 target, int ticks)
    {
        for (var i = 0; i < ticks; i++) { enemy.Update(1f / 120, target); enemy.SyncAfterPhysics(); }
    }

    [Theory, InlineData(-1), InlineData(1)]
    public void RockReleasesAtTheMarkerAndTargetsTheCommittedPosition(int facing)
    {
        var (enemy, _, player) = Create(facing);
        Tick(enemy, player.Position, 1);
        Tick(enemy, new(-facing * 200, 26), 130);
        Assert.Empty(enemy.CaptureState().Bolts);
        Assert.Equal(facing, enemy.Pose.Facing);
        Tick(enemy, new(-facing * 200, 26), 8);
        var rock = Assert.Single(enemy.CaptureState().Bolts);
        Assert.Equal(600, rock.Gravity);
        var landing = rock.Position + rock.Velocity * .85f + new Vector2(0, -rock.Gravity * .85f * .85f / 2);
        Assert.InRange(Vector2.Distance(landing, player.Position), 0, .01f);
        Assert.Contains(enemy.DrainEvents().OfType<EntityCue2D>(), e => e.Cue == "rock-throw");
    }

    [Theory, InlineData(false), InlineData(true)]
    public void DamageBeforeReleaseCancelsTheRock(bool kill)
    {
        var (enemy, _, player) = Create();
        Tick(enemy, player.Position, 60);
        Assert.True(enemy.TakeDamage(kill ? 6 : 1, new(-100, 0)));
        Tick(enemy, player.Position, 100);
        Assert.Empty(enemy.CaptureState().Bolts);
        Assert.DoesNotContain(enemy.DrainEvents().OfType<EntityCue2D>(), e => e.Cue == "rock-throw");
    }

    [Theory, InlineData(-1), InlineData(1)]
    public void ReleasedRockHitsOnceAndSurvivesItsThrowersDeath(int facing)
    {
        var (enemy, _, player) = Create(facing);
        Tick(enemy, player.Position, 140);
        Assert.Single(enemy.CaptureState().Bolts);
        enemy.TakeDamage(6, Vector2.Zero);
        for (var i = 0; i < 180; i++)
        {
            Tick(enemy, player.Position, 1); enemy.TryResolvePlayerHit(player);
        }
        Assert.Equal(28, player.Health.Current);
        Assert.Empty(enemy.CaptureState().Bolts);
    }

    [Fact]
    public void ThinTerrainStopsARockAndMovingAwayDodgesTheCommittedArc()
    {
        foreach (var wall in new[] { false, true })
        {
            var (enemy, physics, player) = Create();
            Tick(enemy, player.Position, 140);
            Assert.Single(enemy.CaptureState().Bolts);
            if (wall)
            {
                var obstacle = new SpatialObject2D(AxisAlignedRectangle2D.FromSize(new Vector2(1, 300)));
                obstacle.Transform.Position = new(100, 100);
                physics.AddBody(obstacle, BodyMotionType2D.Static).CollisionLayer = 1;
            }
            else player.WorldObject.Transform.Position += new Vector2(150, 0);
            enemy.TakeDamage(6, Vector2.Zero);
            for (var i = 0; i < 400; i++) { Tick(enemy, player.Position, 1); enemy.TryResolvePlayerHit(player); }
            Assert.Equal(30, player.Health.Current);
            Assert.Empty(enemy.CaptureState().Bolts);
            if (wall) Assert.Contains(enemy.DrainEvents().OfType<EntityCue2D>(), e => e.Cue == "rock-impact");
        }
    }

    [Fact]
    public void ClosePlayerGetsOneShortRetreatThenAPauseThenAThrow()
    {
        var (enemy, physics, player) = Create();
        var floor = new SpatialObject2D(AxisAlignedRectangle2D.FromSize(new Vector2(500, 20)));
        floor.Transform.Position = new(0, -10);
        physics.AddBody(floor, BodyMotionType2D.Static).CollisionLayer = 1;
        player.WorldObject.Transform.Position = new(60, 26);
        Tick(enemy, player.Position, 1);
        Assert.True(enemy.Body.LinearVelocity.X < 0);
        Tick(enemy, player.Position, 50);
        Assert.Equal(0, enemy.Body.LinearVelocity.X);
        Assert.False(enemy.CaptureState().IsAttacking);
        Tick(enemy, player.Position, 75);
        Assert.True(enemy.CaptureState().IsAttacking);
    }

    [Theory, InlineData(false), InlineData(true)]
    public void SoloAndPairedEncountersSpawnTheirProjectiles(bool paired)
    {
        var map = new EditableTileMap2D(640, 96, 32, 32, SideScrollerLevel2D.WorldOrigin, ["dark-cave"]);
        for (var x = 0; x < 640; x++) map.SetTileKind(x, 19, TileKind2D.Solid);
        var things = new List<WorldThingSpec2D>
        {
            new(1, WorldThingKind2D.PlayerSpawn, null, true, new(-368, 26)),
            new(2, WorldThingKind2D.RockThrower, null, true, new(-168, 40))
        };
        if (paired) things.Add(new(3, WorldThingKind2D.ClubCaveman, null, true, new(-280, 40)));
        using var game = SideScrollerSimulation2D.Create(new(TraversalMetricsLoader2D.Load(TestAssetPath.Root), map, [], things)
        { AuthoredCharacters = Catalog, PlayerMaximumHealth = 100 });
        for (var i = 0; i < 150; i++) game.Session.Advance();
        string[] Run() => [.. Enumerable.Range(0, 240).Select(_ =>
        {
            game.Session.Advance();
            return JsonSerializer.Serialize(game.Session.CaptureEnemies().Select(e => (e.Position, e.ActionId, e.ActionSeconds, e.Bolts)).ToArray(), new JsonSerializerOptions { IncludeFields = true });
        })];
        Run();
        Assert.True(game.Player.Health.Current < 100);
    }
}
