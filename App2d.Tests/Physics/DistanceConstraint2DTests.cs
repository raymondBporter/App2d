using App2d.Core.Geometry.Primitives;
using App2d.Core.Physics;
using App2d.Core.Physics.Constraints;
using App2d.Core.Shapes;
using App2d.Core.Spatial;
using System.Numerics;

namespace App2d.Tests.Physics;

public sealed class DistanceConstraint2DTests
{
    [Fact]
    public void LockedConvenienceConstructorProjectsToExactLength()
    {
        var (first, second) = CreateBodies(0f, 10f);
        var constraint = new DistanceConstraint2D(first, second, 6f);

        Assert.True(constraint.Limits.IsSingleton);
        Assert.True(constraint.SolvePosition(1f / 60f));
        Assert.Equal(6f, second.WorldObject.Transform.Position.X, 4);
    }

    [Fact]
    public void BoundedDistanceCorrectsEitherLimitButLeavesInteriorAlone()
    {
        var (first, second) = CreateBodies(0f, 10f);
        var constraint = new DistanceConstraint2D(first, second, new Interval<float>(4f, 6f));

        Assert.True(constraint.SolvePosition(1f / 60f));
        Assert.Equal(6f, second.WorldObject.Transform.Position.X, 4);

        second.WorldObject.Transform.Position = new Vector2(2f, 0f);
        Assert.True(constraint.SolvePosition(1f / 60f));
        Assert.Equal(4f, second.WorldObject.Transform.Position.X, 4);

        second.WorldObject.Transform.Position = new Vector2(5f, 0f);
        Assert.False(constraint.SolvePosition(1f / 60f));
        Assert.Equal(5f, second.WorldObject.Transform.Position.X, 4);
    }

    [Fact]
    public void UpperLimitOnlyCancelsOutwardVelocity()
    {
        var (first, second) = CreateBodies(0f, 5f);
        var constraint = new DistanceConstraint2D(first, second, Interval.AtMost(5f));

        second.LinearVelocity = new Vector2(10f, 0f);
        constraint.SolveVelocity(1f / 60f);
        Assert.Equal(Vector2.Zero, second.LinearVelocity);

        second.LinearVelocity = new Vector2(-10f, 0f);
        constraint.SolveVelocity(1f / 60f);
        Assert.Equal(new Vector2(-10f, 0f), second.LinearVelocity);
    }

    [Fact]
    public void LowerLimitOnlyCancelsInwardVelocity()
    {
        var (first, second) = CreateBodies(0f, 5f);
        var constraint = new DistanceConstraint2D(first, second, Interval.AtLeast(5f));

        second.LinearVelocity = new Vector2(-10f, 0f);
        constraint.SolveVelocity(1f / 60f);
        Assert.Equal(Vector2.Zero, second.LinearVelocity);

        second.LinearVelocity = new Vector2(10f, 0f);
        constraint.SolveVelocity(1f / 60f);
        Assert.Equal(new Vector2(10f, 0f), second.LinearVelocity);
    }

    private static (RigidBody2D First, RigidBody2D Second) CreateBodies(float firstX, float secondX)
    {
        var world = new PhysicsWorld2D { Gravity = Vector2.Zero };
        var firstObject = new SpatialObject2D(Circle2D.Builder.Create(0.5f));
        firstObject.Transform.Position = new Vector2(firstX, 0f);
        var secondObject = new SpatialObject2D(Circle2D.Builder.Create(0.5f));
        secondObject.Transform.Position = new Vector2(secondX, 0f);
        return (
            world.AddBody(firstObject, BodyMotionType2D.Static),
            world.AddBody(secondObject, BodyMotionType2D.Dynamic));
    }
}

