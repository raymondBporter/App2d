using App2d.CharacterSeeds.Diagnostics;
using App2d.Contracts.Combat;
using App2d.Contracts.Enemies;
using App2d.Core;
using App2d.Core.Characters.Authored;
using App2d.Core.Physics;
using App2d.Core.Shapes;
using App2d.Core.Spatial;
using App2d.Core.Tiles;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Simulation;
using App2d.Gameplay.World;
using System.Numerics;

namespace App2d.Tests.Gameplay.World;

public sealed class PhysicsProp2DTests
{
    private static readonly AuthoredCatalog Catalog = AuthoredCatalog.Load(TestAssetPath.AuthoredRoot);

    [Fact]
    public void DumbbellPreservesHitDeduplicationIndestructibilityAndTumbling()
    {
        var physics = new PhysicsWorld2D();
        var registry = new CombatantRegistry2D();
        var combat = new CombatSystem2D(physics.CollisionSystem, registry);
        var prop = new PhysicsProp2D(new(1), Catalog.PhysicsProps["dumbbell"], physics, Vector2.Zero, 1, 4);
        registry.Register(prop);
        var attack = new SpatialObject2D(Rectangle2D.Builder.FromSize(4, 4));

        Assert.True(combat.ResolveAttack(attack, new(2), 1, CombatFaction2D.Player, 4, 100,
            _ => new Vector2(100, 50)));
        Assert.False(combat.ResolveAttack(attack, new(2), 1, CombatFaction2D.Player, 4, 100,
            _ => new Vector2(100, 50)));
        Assert.Equal(new Vector2(80, 40), prop.Body.LinearVelocity);
        Assert.Equal(-4.5f, prop.Body.AngularVelocity);
        Assert.Equal(prop.Health.Maximum, prop.Health.Current);
        Assert.True(prop.IsAlive);
        Assert.Equal(0, combat.DefeatedEnemies);
        physics.Step(.1f);
        Assert.NotEqual(0f, prop.CaptureState().Rotation);
        Assert.IsType<CompositeShape2D>(prop.WorldObject.Shape);
        Assert.Same(prop.Asset, prop.CaptureState().PhysicsProp);
    }

    [Fact]
    public void StreamingRestoresTheAuthoredMotionType()
    {
        var asset = PhysicsPropAsset.FromJson(Catalog.PhysicsProps["dumbbell"].ToJson());
        asset.Body.MotionType = BodyMotionType2D.Kinematic;
        var prop = new PhysicsProp2D(new(1), asset, new PhysicsWorld2D(), Vector2.Zero, 1, 4);
        prop.TakeDamage(1, new(20, 0));

        prop.SetSimulationEnabled(false);

        Assert.False(prop.Body.IsCollider);
        Assert.Equal(BodyMotionType2D.Static, prop.Body.MotionType);
        Assert.Equal(Vector2.Zero, prop.Body.LinearVelocity);
        Assert.Equal(0f, prop.Body.AngularVelocity);
        prop.SetSimulationEnabled(true);
        Assert.True(prop.Body.IsCollider);
        Assert.Equal(BodyMotionType2D.Kinematic, prop.Body.MotionType);
    }

    [Fact]
    public void ExistingTumblePropPlacementsSpawnTheCatalogDumbbell()
    {
        var map = new EditableTileMap2D(64, 32, 32, 32, Vector2.Zero, ["dark-cave"]);
        using var game = SideScrollerSimulation2D.Create(new(TraversalMetricsLoader2D.Load(TestAssetPath.Root), map,
            [new(1, WorldThingKind2D.PlayerSpawn, null, true, new(100, 100)),
             new(2, WorldThingKind2D.TumbleProp, null, true, new(200, 100))])
        {
            AuthoredCharacters = Catalog
        });

        var prop = Assert.IsType<PhysicsProp2D>(Assert.Single(game.Level.EnemySystem.Combatants));
        var state = Assert.Single(game.Session.CaptureEnemies());
        Assert.Same(Catalog.PhysicsProps["dumbbell"], prop.Asset);
        Assert.Equal(EnemyKind2D.PhysicsProp, state.Kind);
        Assert.Equal("dumbbell", state.TypeId);
        Assert.Same(prop.Asset.Shape, prop.WorldObject.Shape);
    }
}
