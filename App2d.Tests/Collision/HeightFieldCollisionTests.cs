using App2d.Core.Collision.Contacts;
using App2d.Core.Geometry.HeightFields;
using App2d.Core.Geometry.Operations.Distance;
using App2d.Core.Geometry.Operations.HeightFields;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Physics;
using App2d.Core.Shapes;
using App2d.Core.Spatial;
using System.Numerics;

namespace App2d.Tests.Collision;

public sealed class HeightFieldCollisionTests
{
    private static IShape2D[] OtherShapes() =>
    [
        Circle2D.Builder.Create(0.5f),
        Ellipse2D.Builder.Create(new(0.6f, 0.5f)),
        Capsule2D.Builder.Create(new(-0.25f, 0), new(0.25f, 0), 0.5f),
        Rectangle2D.Builder.Create(new(-0.5f), new(0.5f)),
        AxisAlignedRectangle2D.Builder.Create(new(-0.5f), new(0.5f)),
        RoundedRectangle2D.Builder.Create(new(-0.5f), new(0.5f), 0.1f),
        Triangle2D.Builder.Create(new(-0.5f, -0.5f), new(0.5f, -0.5f), new(0, 0.5f)),
        ConvexPolygon2D.Builder.Create([new(-0.5f, -0.5f), new(0.5f, -0.5f), new(0.5f, 0.5f), new(-0.5f, 0.5f)]),
        SimplePolygon2D.Builder.Create([new(-0.5f, -0.5f), new(0.5f, -0.5f), new(0.5f, 0), new(0, 0), new(0, 0.5f), new(-0.5f, 0.5f)]),
        CompositeShape2D.Builder.Create([Circle2D.Builder.Create(0.5f, new(-0.25f, 0)), Circle2D.Builder.Create(0.5f, new(0.25f, 0))]),
        HeightFieldShape2D.Builder.Create([new(-0.5f, 0.5f), new(0.5f, 0.5f)], -0.5f)
    ];

    [Fact]
    public void EveryFiniteShapeCollidesWithHeightfieldsInBothOrdersWithoutFillingTheValley()
    {
        var flat = HeightFieldShape2D.Builder.Create([new(-20, 0), new(-2, 0), new(0, 0), new(2, 0), new(20, 0)], -10f);
        var valley = HeightFieldShape2D.Builder.Create([new(-10, 6), new(0, 0), new(10, 6)], -10f);
        foreach (var shape in OtherShapes())
        {
            var penetrating = Similarity2D.FromTranslation(new(0, 0.25f));
            Assert.True(ShapeCollision2D.TryGetContact(shape, penetrating, flat, Similarity2D.Identity, out var contact), shape.Kind);
            Assert.True(contact.Normal.Y > 0.99f, $"{shape.Kind}: {contact.Normal}");
            Assert.InRange(contact.PenetrationDepth, 0.249f, 0.251f);
            Assert.True(ShapeCollision2D.TryGetContact(flat, Similarity2D.Identity, shape, penetrating, out var reverse));
            Assert.InRange(Vector2.Distance(contact.Normal, -reverse.Normal), 0f, 0.0001f);
            Assert.Equal(contact.PenetrationDepth, reverse.PenetrationDepth, 4);
            Assert.Equal(0f, ShapeDistance2D.Distance(shape, penetrating, flat, Similarity2D.Identity));
            Assert.False(ShapeCollision2D.TryGetContact(shape, Similarity2D.FromTranslation(new(0, 4)), valley, Similarity2D.Identity, out _), shape.Kind);
            Assert.True(ShapeDistance2D.Distance(shape, Similarity2D.FromTranslation(new(0, 4)), valley, Similarity2D.Identity) > 1f, shape.Kind);
        }
        var halfSpace = HalfSpace2D.Builder.Create(Vector2.UnitY, -5f);
        Assert.True(ShapeCollision2D.TryGetContact(flat, Similarity2D.Identity, halfSpace, Similarity2D.Identity, out _));
        Assert.False(ShapeCollision2D.TryGetContact(flat, Similarity2D.Identity,
            HalfSpace2D.Builder.Create(Vector2.UnitY, -20f), Similarity2D.Identity, out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ContactsRespectRotationScaleMirrorAndFiniteEndCaps(bool mirror)
    {
        var field = HeightFieldShape2D.Builder.Create([new(-10, 0), new(0, 0), new(10, 0)], -10f);
        var pose = Similarity2D.FromAxis(new(20, -13), new(0.8f, 0.6f), mirror: mirror, scale: 2f);
        var wheel = Circle2D.Builder.Create(1f);
        foreach (var (center, normal) in new (Vector2, Vector2)[]
        {
            (new(0, 0.375f), Vector2.UnitY),
            (new(-10.375f, -5f), -Vector2.UnitX),
            (new(10.375f, -5f), Vector2.UnitX),
            (new(0, -10.375f), -Vector2.UnitY)
        })
        {
            var wheelPose = Similarity2D.FromTranslation(pose.TransformPoint(center));
            Assert.True(ShapeCollision2D.TryGetContact(wheel, wheelPose, field, pose, out var contact));
            Assert.Equal(0.25f, contact.PenetrationDepth, 4);
            var expected = Vector2.Normalize(pose.XAxis * normal.X + pose.YAxis * normal.Y);
            Assert.InRange(Vector2.Distance(expected, contact.Normal), 0f, 0.00001f);
        }
    }

    [Fact]
    public void AnalyticSourceBakesIntoTheSameCollisionPath()
    {
        var shape = new FunctionHeightField2D(-20f, 20f, -10f, x => 0.1f * x * x, yMax: 40f).ToShape(0.5f, 0.001f);
        foreach (var other in OtherShapes())
        {
            Assert.False(ShapeCollision2D.TryGetContact(other, Similarity2D.FromTranslation(new(0, 3)), shape, Similarity2D.Identity, out _), other.Kind);
            Assert.True(ShapeCollision2D.TryGetContact(other, Similarity2D.FromTranslation(new(0, 0.25f)), shape, Similarity2D.Identity, out _), other.Kind);
        }
    }

    [Fact]
    public void ABoxSlidesAcrossCollinearSamplesWithoutInternalTriangleSeams()
    {
        var world = new PhysicsWorld2D { Gravity = new(0, -10f) };
        var field = HeightFieldShape2D.Builder.Create(Enumerable.Range(-40, 81).Select(x => new Vector2(x, 0f)), -10f);
        var ground = world.AddBody(new SpatialObject2D(field), BodyMotionType2D.Static);
        ground.Friction = 0f;
        ground.Restitution = 0f;
        var box = world.AddBody(new SpatialObject2D(Rectangle2D.Builder.FromSize(2, 2)), BodyMotionType2D.Dynamic);
        box.WorldObject.Transform.Position = new(-10f, 1f);
        box.LinearVelocity = new(4f, 0f);
        box.Friction = 0f;
        box.Restitution = 0f;
        box.FreezeRotation = true;
        for (var step = 0; step < 600; step++) world.Step(1f / 120f);
        Assert.Equal(10f, box.WorldCenterOfMass.X, 3);
        Assert.Equal(1f, box.WorldCenterOfMass.Y, 3);
        Assert.Equal(4f, box.LinearVelocity.X, 4);
    }
}
