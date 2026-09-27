using App2d.Core;
using App2d.Core.Collision;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Shapes;
using System.Numerics;

namespace App2d.Tests.Collision;

public sealed class CollisionSystem2DTests
{
    private const uint WorldLayer = 1u << 0;
    private const uint ActorLayer = 1u << 1;

    [Fact]
    public void GridQueriesMatchBoundsScanningAcrossNegativeCoordinatesAndCellEdges()
    {
        var system = new CollisionSystem2D { CellSize = 16f };
        var random = new Random(173);
        for (var i = 0; i < 80; i++)
        {
            var shape = new SpatialObject2D(Rectangle2D.FromSize(new(8 + i % 7, 10)));
            shape.Transform.Position = new(random.Next(-8, 9) * 16, random.Next(-4, 5) * 16);
            system.AddCollider(shape, i % 2 == 0 ? ColliderMobility2D.Static : ColliderMobility2D.Dynamic);
        }
        var results = new List<Collider2D>();
        for (var i = 0; i < 40; i++)
        {
            var min = new Vector2(random.Next(-10, 10) * 16, random.Next(-6, 6) * 16);
            var query = new Bounds2D(min, min + new Vector2(i % 3 * 16, i % 4 * 16));
            system.QueryBounds(query, results);
            var expected = system.Colliders.Where(c => query.Intersects(c.WorldObject.WorldBounds)).Select(c => c.Id).Order();
            Assert.Equal(expected, results.Select(c => c.Id).Order());
        }
    }

    [Fact]
    public void UnrepresentableGridCoordinatesUseTheOverflowPathWithoutLosingColliders()
    {
        var system = new CollisionSystem2D { CellSize = 1e-20f };
        var shape = new SpatialObject2D(new Circle2D(2));
        shape.Transform.Position = new(50, -50);
        var collider = system.AddCollider(shape);
        var results = new List<Collider2D>();
        system.QueryBounds(new Bounds2D(new(49, -51), new(51, -49)), results);
        Assert.Same(collider, Assert.Single(results));
        system.QueryBounds(Bounds2D.Unbounded, results);
        Assert.Same(collider, Assert.Single(results));
    }

    [Fact]
    public void RegistersAndRemovesColliders()
    {
        var system = new CollisionSystem2D();
        var collider = system.AddCollider(
            new SpatialObject2D(new Circle2D(10f)));

        Assert.Same(collider, Assert.Single(system.Colliders));
        Assert.True(system.RemoveCollider(collider));
        Assert.Empty(system.Colliders);
    }

    [Fact]
    public void BoundsQueriesUseLayersAndTrackDynamicMovement()
    {
        var system = new CollisionSystem2D { CellSize = 32f };
        var worldObject = new SpatialObject2D(new Circle2D(4f));
        var collider = system.AddCollider(
            worldObject,
            ColliderMobility2D.Dynamic);
        collider.CollisionLayer = ActorLayer;
        var results = new List<Collider2D>();

        Assert.Equal(1, system.QueryBounds(
            new Bounds2D(new Vector2(-5f), new Vector2(5f)),
            results,
            ActorLayer));
        Assert.Equal(0, system.QueryBounds(
            new Bounds2D(new Vector2(-5f), new Vector2(5f)),
            results,
            WorldLayer));

        worldObject.Transform.Position = new Vector2(100f, 0f);

        Assert.Equal(0, system.QueryBounds(
            new Bounds2D(new Vector2(-5f), new Vector2(5f)),
            results,
            ActorLayer));
        Assert.Equal(1, system.QueryBounds(
            new Bounds2D(new Vector2(95f, -5f), new Vector2(105f, 5f)),
            results,
            ActorLayer));
    }

    [Fact]
    public void StaticTransformChangesRefreshTheStaticIndex()
    {
        var system = new CollisionSystem2D { CellSize = 32f };
        var worldObject = new SpatialObject2D(new Circle2D(4f));
        system.AddCollider(worldObject, ColliderMobility2D.Static);
        var results = new List<Collider2D>();
        system.QueryBounds(
            new Bounds2D(new Vector2(-5f), new Vector2(5f)),
            results);

        worldObject.Transform.Position = new Vector2(100f, 0f);

        Assert.Equal(1, system.QueryBounds(
            new Bounds2D(new Vector2(95f, -5f), new Vector2(105f, 5f)),
            results));
    }

    [Fact]
    public void CollectContactsFindsDynamicAgainstStaticWithoutStaticPairs()
    {
        var system = new CollisionSystem2D();
        var firstStatic = system.AddCollider(
            new SpatialObject2D(new Circle2D(10f)));
        var secondStaticObject = new SpatialObject2D(new Circle2D(10f));
        secondStaticObject.Transform.Position = new Vector2(5f, 0f);
        system.AddCollider(secondStaticObject);
        var dynamicObject = new SpatialObject2D(new Circle2D(10f));
        dynamicObject.Transform.Position = new Vector2(15f, 0f);
        var dynamicCollider = system.AddCollider(
            dynamicObject,
            ColliderMobility2D.Dynamic);
        var contacts = new List<CollisionPair2D>();

        system.CollectContacts(contacts);

        Assert.Equal(2, contacts.Count);
        Assert.All(contacts, contact =>
            Assert.True(
                ReferenceEquals(contact.First, dynamicCollider) ||
                ReferenceEquals(contact.Second, dynamicCollider)));
        Assert.DoesNotContain(contacts, contact =>
            ReferenceEquals(contact.First, firstStatic) &&
            contact.Second.Mobility == ColliderMobility2D.Static);
    }

    [Fact]
    public void OverlapReturnsExactContactsFromNearbyCandidates()
    {
        var system = new CollisionSystem2D();
        var target = new SpatialObject2D(new Circle2D(10f));
        target.Transform.Position = new Vector2(15f, 0f);
        var collider = system.AddCollider(target);
        collider.CollisionLayer = ActorLayer;
        var overlaps = new List<CollisionOverlap2D>();

        var count = system.Overlap(
            new SpatialObject2D(new Circle2D(10f)),
            overlaps,
            ActorLayer);

        var overlap = Assert.Single(overlaps);
        Assert.Equal(1, count);
        Assert.Same(collider, overlap.Collider);
        Assert.Equal(5f, overlap.Contact.PenetrationDepth, 5);
    }
}
