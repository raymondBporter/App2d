using App2d.Contracts.Combat;
using App2d.Contracts.Enemies;
using App2d.Contracts.World;
using App2d.Core;
using App2d.Core.Characters.Authored;
using App2d.Core.Geometry;
using App2d.Core.Physics;
using App2d.Core.Shapes;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Enemies;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Simulation;
using App2d.Gameplay.World;
using App2d.Levels;
using App2d.Tiles;
using System.Numerics;
using Xunit;

namespace App2d.Gameplay.Tests.Enemies;

public sealed class ShieldDefenderTests
{
    private static readonly AuthoredCatalog Catalog = AuthoredCatalog.Load(Path.GetFullPath(Path.Combine(TestAssetPath.Root, "..", "Characters", "authored")));
    private static (AuthoredEntityEnemy2D Enemy, Person2D Player, CombatSystem2D Combat) Create(int facing = 1)
    {
        Assert.True(Catalog.Errors.Count == 0, string.Join("\n", Catalog.Errors));
        var physics = new PhysicsWorld2D { Gravity = Vector2.Zero };
        var enemy = new AuthoredEntityEnemy2D(EntityId2D.Create(), Catalog.Entities["shield-defender"], physics, new(0, 40), 1, 4);
        enemy.SetSimulationEnabled(true);
        enemy.Update(0, new(facing * 400, 26)); enemy.SyncAfterPhysics();
        var player = new Person2D(EntityId2D.Create(), physics.CollisionSystem, physics, TraversalMetricsLoader2D.Load(TestAssetPath.Root), new(facing * 90, 26), 2, 1, CombatFaction2D.Player, 30);
        var registry = new CombatantRegistry2D(); registry.Register(enemy); registry.Register(player);
        return (enemy, player, new CombatSystem2D(physics.CollisionSystem, registry));
    }
    private static Rect2D Shell(AuthoredEntityEnemy2D enemy)
    {
        var g = enemy.Entity.Asset.Guard!;
        var region = EntityCollision.Attack(enemy.Entity, enemy.Pose, new(new HitWindow { Prop = g.Prop, Width = g.Width, Height = g.Height }, 0, 1));
        return ShapeBounds2D.Calculate(region.Scaled(GameWorldUnits2D.WorldUnitsPerAuthoredUnit).Shape);
    }
    private static SpatialObject2D Box(Vector2 position, float size = 4)
    {
        var box = new SpatialObject2D(AxisAlignedRectangle2D.FromSize(new Vector2(size)));
        box.Transform.Position = position; return box;
    }
    private static void Tick(AuthoredEntityEnemy2D enemy, Vector2 target, int ticks)
    {
        for (var i = 0; i < ticks; i++) { enemy.Update(1f / 120, target); enemy.SyncAfterPhysics(); }
    }

    [Theory, InlineData(-1), InlineData(1)]
    public void FrontContactBlocksOutsideTheBodyAndDeduplicatesWithoutDamageFeedback(int facing)
    {
        var (enemy, player, combat) = Create(facing);
        var shield = Shell(enemy);
        var box = Box(new(shield.Center.X + facing * 12, shield.Center.Y));
        Assert.False(enemy.OverlapsHurt(box.WorldBounds));
        var damageFacts = new List<CombatDamage2D>(); combat.DamageResolved += damageFacts.Add;
        var snapshot = enemy.CaptureSimulation();
        bool Strike() => combat.ResolveAttack(box, player.Id, 1, CombatFaction2D.Player, 4, 2, _ => new(-facing * 200, 0));
        Assert.True(Strike()); Assert.False(Strike());
        Assert.Equal(8, enemy.Health.Current); Assert.Empty(damageFacts);
        Assert.Single(enemy.DrainEvents().OfType<EntityCue2D>(), e => e.Cue == "shield-block");
        enemy.RestoreSimulation(snapshot);
        Assert.True(Strike()); Assert.Equal(8, enemy.Health.Current);
    }

    [Theory, InlineData(-1), InlineData(1)]
    public void RearAttacksBypassProtectionEvenWhenTheHitboxSpansTheShield(int facing)
    {
        var (enemy, player, combat) = Create(facing);
        player.WorldObject.Transform.Position = new(-facing * 90, 26);
        Assert.True(combat.ResolveAttack(Box(new(0, 35), 70), player.Id, 2, CombatFaction2D.Player, 4, 2, _ => new(-facing * 100, 0)));
        Assert.Equal(6, enemy.Health.Current);
        Assert.Equal("hit", enemy.CaptureState().ActionId);
    }

    [Fact]
    public void HitsAboveTheShieldHurtAndRearShieldOnlyContactDoesNotConsumeTheAttack()
    {
        var (enemy, player, combat) = Create();
        var head = enemy.Pose.World("head") * GameWorldUnits2D.WorldUnitsPerAuthoredUnit;
        var box = Box(new(head.X, head.Y));
        Assert.False(enemy.OverlapsGuard(box.WorldBounds));
        Assert.True(combat.ResolveAttack(box, player.Id, 3, CombatFaction2D.Player, 4, 1, _ => new(-100, 0)));
        Assert.Equal(7, enemy.Health.Current);
        var (rearEnemy, rearPlayer, rearCombat) = Create();
        rearPlayer.WorldObject.Transform.Position = new(-90, 26);
        var shell = Shell(rearEnemy);
        var rearBox = Box(new(shell.Center.X + 12, shell.Center.Y));
        Assert.False(rearCombat.ResolveAttack(rearBox, rearPlayer.Id, 4, CombatFaction2D.Player, 4, 1, _ => new(100, 0)));
        rearBox.Transform.Position = new(0, 35);
        Assert.True(rearCombat.ResolveAttack(rearBox, rearPlayer.Id, 4, CombatFaction2D.Player, 4, 1, _ => new(100, 0)));
        Assert.Equal(7, rearEnemy.Health.Current);
    }

    [Fact]
    public void AProjectileStopsOnTheRaisedShield()
    {
        var (enemy, _, combat) = Create();
        var shell = Shell(enemy);
        Assert.True(combat.TryDamageFirst(Box(new(shell.Center.X + 12, shell.Center.Y)), CombatFaction2D.Player, 4, 2, _ => new(-100, 0)));
        Assert.Equal(8, enemy.Health.Current);
        Assert.Contains(enemy.DrainEvents().OfType<EntityCue2D>(), e => e.Cue == "shield-block");
    }

    [Theory, InlineData(30), InlineData(100)]
    public void PullbackAndRecoveryExposeTheBodyAndCanBeInterrupted(int ticks)
    {
        var (enemy, player, combat) = Create();
        player.WorldObject.Transform.Position = new(40, 26);
        Tick(enemy, player.Position, ticks);
        Assert.True(enemy.CaptureState().IsAttacking);
        Assert.True(combat.ResolveAttack(Box(new(0, 35), 30), player.Id, 5, CombatFaction2D.Player, 4, 2, _ => new(-100, 0)));
        Assert.Equal(6, enemy.Health.Current); Assert.Equal("hit", enemy.CaptureState().ActionId);
        Assert.Empty(enemy.GetActiveAttackHitboxes());
    }

    [Theory, InlineData(-1), InlineData(1)]
    public void BashIsCommittedAndDamagesOnceInItsWindow(int facing)
    {
        var (enemy, player, _) = Create(facing);
        player.WorldObject.Transform.Position = new(facing * 40, 26);
        for (var i = 0; i < 165; i++)
        {
            Tick(enemy, i < 1 ? player.Position : new(-facing * 40, 26), 1);
            Assert.Equal(facing, enemy.Pose.Facing);
            var before = player.Health.Current; enemy.TryResolvePlayerHit(player);
            if (before != player.Health.Current) Assert.InRange(enemy.CaptureState().ActionSeconds, .55f, .7f);
        }
        Assert.Equal(28, player.Health.Current);
        Assert.Empty(enemy.GetActiveAttackHitboxes());
    }

    [Fact]
    public void PlacementAndBashReplayExactly()
    {
        var map = new EditableTileMap2D(640, 96, 32, 32, SideScrollerLevel2D.WorldOrigin, ["dark-cave"]);
        for (var x = 0; x < 640; x++) map.SetTileKind(x, 19, TileKind2D.Solid);
        using var game = SideScrollerSimulation2D.Create(new(TraversalMetricsLoader2D.Load(TestAssetPath.Root), map, [],
            [new(1, WorldThingKind2D.PlayerSpawn, null, true, new(-368, 26)),
             new(2, WorldThingKind2D.ShieldDefender, null, true, new(-218, 40))])
            { AuthoredCharacters = Catalog, PlayerMaximumHealth = 100 });
        Assert.Equal("shield-defender", Assert.Single(game.Session.CaptureEnemies()).TypeId);
        for (var i = 0; i < 150; i++) game.Session.Advance();
        var checkpoint = game.Session.CaptureCheckpoint();
        string[] Run() => [.. Enumerable.Range(0, 400).Select(_ =>
        {
            game.Session.Advance(); var state = Assert.Single(game.Session.CaptureEnemies());
            return $"{state.Position};{state.ActionId};{state.ActionSeconds};{game.Player.Health.Current}";
        })];
        var first = Run(); game.Session.RestoreCheckpoint(checkpoint); Assert.Equal(first, Run());
        Assert.True(game.Player.Health.Current < 100);
    }
}
