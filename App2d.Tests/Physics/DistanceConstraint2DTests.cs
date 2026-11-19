using App2d.Core.Geometry.Shapes;
using App2d.Core.Physics;
using App2d.Core.Physics.Constraints;
using App2d.Core.Spatial;

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
        Assert.Equal(6f, second.WorldObject.Position.X, 4);
    }

    [Fact]
    public void BoundedDistanceCorrectsEitherLimitButLeavesInteriorAlone()
    {
        var (first, second) = CreateBodies(0f, 10f);
        var constraint = new DistanceConstraint2D(first, second, new Interval<float>(4f, 6f));

        Assert.True(constraint.SolvePosition(1f / 60f));
        Assert.Equal(6f, second.WorldObject.Position.X, 4);

        second.WorldObject.Position = new Vector2f(2f, 0f);
        Assert.True(constraint.SolvePosition(1f / 60f));
        Assert.Equal(4f, second.WorldObject.Position.X, 4);

        second.WorldObject.Position = new Vector2f(5f, 0f);
        Assert.False(constraint.SolvePosition(1f / 60f));
        Assert.Equal(5f, second.WorldObject.Position.X, 4);
    }

    [Fact]
    public void UpperLimitOnlyCancelsOutwardVelocity()
    {
        var (first, second) = CreateBodies(0f, 5f);
        var constraint = new DistanceConstraint2D(first, second, Interval.AtMost(5f));

        second.LinearVelocity = new Vector2f(10f, 0f);
        constraint.SolveVelocity(1f / 60f);
        Assert.Equal(Vector2f.Zero, second.LinearVelocity);

        second.LinearVelocity = new Vector2f(-10f, 0f);
        constraint.SolveVelocity(1f / 60f);
        Assert.Equal(new Vector2f(-10f, 0f), second.LinearVelocity);
    }

    [Fact]
    public void LowerLimitOnlyCancelsInwardVelocity()
    {
        var (first, second) = CreateBodies(0f, 5f);
        var constraint = new DistanceConstraint2D(first, second, Interval.AtLeast(5f));

        second.LinearVelocity = new Vector2f(-10f, 0f);
        constraint.SolveVelocity(1f / 60f);
        Assert.Equal(Vector2f.Zero, second.LinearVelocity);

        second.LinearVelocity = new Vector2f(10f, 0f);
        constraint.SolveVelocity(1f / 60f);
        Assert.Equal(new Vector2f(10f, 0f), second.LinearVelocity);
    }

    private static (RigidBody2D First, RigidBody2D Second) CreateBodies(float firstX, float secondX)
    {
        var world = new PhysicsWorld2D { Gravity = Vector2f.Zero };
        var firstObject = new SpatialObject2D(Circle2D.Builder.Create(0.5f))
        {
            Position = new Vector2f(firstX, 0f)
        };
        var secondObject = new SpatialObject2D(Circle2D.Builder.Create(0.5f))
        {
            Position = new Vector2f(secondX, 0f)
        };
        return (
            world.AddBody(firstObject, BodyMotionType2D.Static),
            world.AddBody(secondObject, BodyMotionType2D.Dynamic));
    }
}

