using System.Numerics;
using App2d.Engine;
using App2d.Engine.Collision;
using App2d.Engine.Geometry;

namespace App2d.Tests.Collision;

public sealed class CollisionSystem2DTests
{
    private const uint WorldLayer = 1u << 0;
    private const uint ActorLayer = 1u << 1;

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
