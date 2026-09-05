using App2d.Core;
using App2d.Core.Geometry;
using App2d.Gameplay.Enemies;
using App2d.Physics;
using App2d.Rendering;
using App2d.Rendering.Textures;
using System.Numerics;
using Xunit;

namespace App2d.Gameplay.Tests.Enemies;

public sealed class GreenDinosaur2DTests
{
    [Fact]
    public void LoadsWalkCycleAndTracksPatrolState()
    {
        var scene = new Scene2D();
        var physics = new PhysicsWorld2D();
        var spatialObject = new SpatialObject2D(
            new Capsule2D(new Vector2(0f, -14f), new Vector2(0f, 14f), 17f));
        var body = physics.AddBody(spatialObject, BodyMotionType2D.Dynamic);
        var enemy = new PatrolEnemy2D(
            spatialObject,
            body,
            patrolMinX: -64f,
            patrolMaxX: 64f,
            speed: 82f,
            health: 4);
        using var textures = new TextureCache2D(TestAssetPath.Root);
        var dinosaur = new GreenDinosaur2D(scene, textures, enemy);

        dinosaur.Update(0.2f, Vector2.Zero);

        var visual = Assert.Single(scene);
        Assert.True(visual.IsVisible);
        Assert.Equal(6, textures.Count);
        Assert.True(body.LinearVelocity.X > 0f);

        Assert.True(enemy.TakeDamage(4, Vector2.Zero));
        dinosaur.Update(0f, Vector2.Zero);

        Assert.False(visual.IsVisible);
    }
}
