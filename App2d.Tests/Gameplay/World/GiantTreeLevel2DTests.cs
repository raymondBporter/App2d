using App2d.Contracts.Persons;
using App2d.Contracts.Player;
using App2d.Contracts.Simulation;
using App2d.Core;
using App2d.Core.Collision;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Physics;
using App2d.Core.Shapes;
using App2d.Core.Spatial;
using App2d.Core.Tiles;
using App2d.Gameplay.Simulation;
using App2d.Gameplay.World;
using App2d.Gameplay.World.Vegetation;
using System.Numerics;

namespace App2d.Tests.Gameplay.World;

public sealed class GiantTreeLevel2DTests
{
    private static TraversalMetrics2D Traversal() => TraversalMetrics2D.FromGeometry(new(128), .9f, new(22, 56), 0);
    private static EditableTileMap2D Map() => new(640, 96, 32, 32, SideScrollerLevel2D.WorldOrigin);

    [Fact]
    public void PlayerCanStandAndJumpOnAGeneratedBranch()
    {
        var tree = new GiantTree2D(Vector2.Zero, new(), 3107);
        var branch = tree.Branches[0];
        var spawn = new Vector2(branch.Bounds.Center.X, branch.Bounds.Top + 48);
        using var game = SideScrollerSimulation2D.Create(new(Traversal(), Map(), [],
            [new(1, WorldThingKind2D.PlayerSpawn, "Branch", true, spawn)])
        {
            GiantTrees = [tree]
        });
        for (var i = 0; i < 90; i++)
        {
            Step(default);
        }

        Assert.True(game.Player.IsGrounded);
        Assert.Equal(branch.Bounds.Top, game.Player.WorldObject.WorldBounds.Bottom, 2);
        var standingY = game.Player.WorldObject.Transform.Position.Y;
        for (var i = 0; i < 12; i++)
        {
            Step(new()
            {
                JumpHeld = true
            });
        }

        Assert.True(game.Player.WorldObject.Transform.Position.Y > standingY + 30);

        void Step(PersonCommand2D command)
        {
            game.Session.Advance(new PlayerInput2D(game.Player.Id, command));
        }
    }

    [Fact]
    public void BranchesAllowUpwardPassageAndAreIndependentOfRootChunkStreaming()
    {
        var tree = new GiantTree2D(Vector2.Zero, new(), 3107);
        var collision = new CollisionSystem2D();
        var physics = new PhysicsWorld2D(collision);
        using var level = new SideScrollerLevel2D(Traversal(), Map(), giantTrees: [tree]);
        level.CreateSimulation(collision, physics, new EntityIdAllocator2D(), 1, 2, 4);
        var branch = tree.Branches[0].Bounds;
        var actor = new SpatialObject2D(AxisAlignedRectangle2D.Builder.CreateFromSize(20, 40));
        actor.Transform.Position = new(branch.Center.X, branch.Bottom - 30);
        var body = physics.AddBody(actor, BodyMotionType2D.Dynamic);
        body.CollisionLayer = 2;
        body.CollisionMask = 1;
        body.LinearVelocity = new(0, 200);
        for (var i = 0; i < 60; i++)
        {
            physics.Step(1f / 120f);
        }

        Assert.True(actor.WorldBounds.Bottom > branch.Top);
        Assert.Equal(200, body.LinearVelocity.Y, 3);

        var platforms = level.GiantTreeBranches.ToArray();
        level.UpdateStreaming(new(10_000, 2_000));
        Assert.Equal(tree.Branches.Length, level.GiantTreeBranches.Count);
        Assert.All(platforms, platform => Assert.Contains(platform, physics.Bodies));
        Assert.Same(tree, Assert.Single(level.CaptureContent().GiantTrees));
    }

    [Fact]
    public void ReloadAndDisposeRemoveOldPlatformsAndPublishChangedContent()
    {
        var tree = new GiantTree2D(Vector2.Zero, new(), 3107);
        var collision = new CollisionSystem2D();
        var physics = new PhysicsWorld2D(collision);
        using var level = new SideScrollerLevel2D(Traversal(), Map(), giantTrees: [tree]);
        level.CreateSimulation(collision, physics, new EntityIdAllocator2D(), 1, 2, 4);
        var oldBodies = level.GiantTreeBranches.ToArray();
        var oldContent = level.CaptureContent();
        Assert.Same(oldContent, level.CaptureContent());
        var replacement = new GiantTree2D(new(900, 0), new(), 8821);
        level.ReloadGiantTrees([replacement]);
        Assert.All(oldBodies, body => Assert.DoesNotContain(body, physics.Bodies));
        Assert.True(level.CaptureContent().Revision > oldContent.Revision);
        Assert.Same(replacement, Assert.Single(level.CaptureContent().GiantTrees));
        for (var i = 0; i < replacement.Branches.Length; i++)
        {
            var expected = replacement.Branches[i].Bounds;
            var actual = level.GiantTreeBranches[i].WorldObject.WorldBounds;
            Assert.InRange(Vector2.Distance(expected.Min, actual.Min), 0, 0.001f);
            Assert.InRange(Vector2.Distance(expected.Max, actual.Max), 0, 0.001f);
        }
        Assert.All(level.GiantTreeBranches, body => Assert.Equal(Vector2.UnitY, PlatformCollisionRules2D.Attach(physics).Surface(body).OneWaySurfaceNormal));
        level.ReloadGiantTrees([]);
        Assert.Empty(physics.Bodies);
        Assert.Empty(level.CaptureContent().GiantTrees);
        level.ReloadGiantTrees([tree]);
        level.Dispose();
        Assert.Empty(physics.Bodies);
    }
}
