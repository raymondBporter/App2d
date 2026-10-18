using App2d.Core.Collision.Contacts;
using App2d.Core.Physics;
using App2d.Core.Physics.Constraints;
using App2d.Core.Physics.Integration;
using App2d.Core.Physics.Solvers;
using App2d.Core.Shapes;
using App2d.Core.Spatial;
using System.Numerics;

namespace App2d.Tests.Physics;

public sealed class RigidBodyMassProperties2DTests
{
    [Fact]
    public void DensityAccountsForUniformScaleAndMirroringWhileTheCentroidFollowsTheFullPose()
    {
        var body = Disk(new(3f, -1f));
        body.WorldObject.Transform.Scale = new(-2f, 2f);
        body.WorldObject.Transform.Position = new(10f, 20f);
        body.WorldObject.Transform.Rotation = MathF.PI / 2f;

        body.SetMassFromDensity(3f);

        Assert.Equal(48f * MathF.PI, body.Mass, 4);
        Assert.Equal(384f * MathF.PI, body.MomentOfInertia, 3);
        Assert.Equal(new Vector2(3f, -1f), body.LocalCenterOfMass);
        Near(new(12f, 14f), body.WorldCenterOfMass);
        Assert.Equal(new Vector2(10f, 20f), body.WorldObject.Transform.Position);
    }

    [Fact]
    public void ChosenMassKeepsMassFixedWhileInertiaGrowsWithScaleSquared()
    {
        var body = Disk(Vector2.Zero);
        body.SetMassFromShape(3f);
        Assert.Equal(3f, body.Mass);
        Assert.Equal(6f, body.MomentOfInertia);
        body.WorldObject.Transform.Scale = new(2f);
        body.SetMassFromShape(3f);
        Assert.Equal(3f, body.Mass);
        Assert.Equal(24f, body.MomentOfInertia);
    }

    [Fact]
    public void OffOriginGeometryRotatesAboutItsCenterOfMassAndTranslatesAtItsLinearVelocity()
    {
        var body = Disk(new(3f, 0f));
        body.WorldObject.Transform.Position = new(10f, 20f);
        body.WorldObject.Transform.Scale = new(2f);
        body.SetMassFromShape(2f);
        body.FreezeRotation = false;
        body.AngularVelocity = MathF.PI / 2f;
        body.LinearVelocity = new(4f, -2f);

        new SemiImplicitEulerIntegrator2D().Integrate(body, Vector2.Zero, 1f);

        Near(new(20f, 18f), body.WorldCenterOfMass);
        Near(new(20f, 12f), body.WorldObject.Transform.Position);
    }

    [Theory]
    [InlineData(0f, 0f, 0f)]
    [InlineData(2f, 1.6666667f, -3.3333333f)]
    public void ContactLeverArmsAreMeasuredFromTheComputedCentroid(float contactOffset, float expectedSpin, float expectedVerticalSpeed)
    {
        var world = new PhysicsWorld2D();
        var spatial = new SpatialObject2D(Circle2D.Builder.Create(2f, new(4f, 0f)));
        spatial.Transform.Position = new(0f, 2f);
        var body = world.AddBody(spatial, BodyMotionType2D.Dynamic);
        body.SetMassFromShape(2f);
        body.FreezeRotation = false;
        body.Restitution = 0f;
        body.LinearVelocity = new(0f, -5f);
        var ground = world.AddBody(new SpatialObject2D(HalfSpace2D.Builder.Create(Vector2.UnitY, 0f)), BodyMotionType2D.Static);
        var contact = new PhysicsContact2D(body, ground,
            new CollisionContact2D(new(4f + contactOffset, 0f), Vector2.UnitY, 0f));

        new ImpulseVelocitySolver2D().Solve(contact);

        Assert.Equal(expectedSpin, body.AngularVelocity, 4);
        Assert.Equal(expectedVerticalSpeed, body.LinearVelocity.Y, 4);
    }

    [Fact]
    public void DistanceConstraintConnectsCentersEvenWhenOneShapeIsOffOrigin()
    {
        var first = Disk(new(4f, 0f));
        first.SetMassFromShape(1f);
        var second = Disk(Vector2.Zero);
        second.WorldObject.Transform.Position = new(10f, 0f);
        second.MotionType = BodyMotionType2D.Static;
        var constraint = new DistanceConstraint2D(first, second, 4f);

        constraint.SolvePosition(1f / 120f);

        Assert.Equal(4f, Vector2.Distance(first.WorldCenterOfMass, second.WorldCenterOfMass), 5);
    }

    [Fact]
    public void UpdatingTheCenterPreservesTheVelocityOfTheSpinningShapesPoints()
    {
        var body = Disk(new(4f, 0f));
        body.AngularVelocity = 0.5f;
        body.SetMassFromShape(1f);
        Assert.Equal(new Vector2(0f, 2f), body.LinearVelocity);
        Assert.Equal(Vector2.Zero, body.WorldObject.Transform.Position);
        Assert.Equal(0f, body.WorldObject.Transform.Rotation);
    }

    [Fact]
    public void UnsupportedOrUnrepresentablePropertiesCannotPartiallyChangeTheBody()
    {
        var world = new PhysicsWorld2D();
        var infinite = world.AddBody(new SpatialObject2D(HalfSpace2D.Builder.Create(Vector2.UnitY, 0f)), BodyMotionType2D.Static);
        infinite.Mass = 3f;
        infinite.MomentOfInertia = 7f;
        Assert.Throws<NotSupportedException>(() => infinite.SetMassFromDensity());
        Assert.Equal(3f, infinite.Mass);
        Assert.Equal(7f, infinite.MomentOfInertia);

        var huge = world.AddBody(new SpatialObject2D(Circle2D.Builder.Create(1e10f)), BodyMotionType2D.Dynamic);
        Assert.Throws<ArgumentOutOfRangeException>(() => huge.SetMassFromDensity());
        Assert.Equal(1f, huge.Mass);
        Assert.Equal(1f, huge.MomentOfInertia);
        Assert.Equal(Vector2.Zero, huge.LocalCenterOfMass);

        var nonuniform = Disk(new(3f, 0f));
        nonuniform.WorldObject.Transform.Scale = new(2f, 1f);
        Assert.Throws<InvalidOperationException>(() => nonuniform.SetMassFromShape(2f));
        Assert.Equal(1f, nonuniform.Mass);
        Assert.Equal(1f, nonuniform.MomentOfInertia);
        Assert.Equal(Vector2.Zero, nonuniform.LocalCenterOfMass);
    }

    [Fact]
    public void ExistingExplicitBodySettingsStayOptedOutOfDerivedMassProperties()
    {
        var world = new PhysicsWorld2D();
        var spatial = new SpatialObject2D(Circle2D.Builder.Create(2f, new(4f, 0f)));
        var body = new PhysicsBodySettings2D { Mass = 3f, MomentOfInertia = 7f }.CreateBody(world, spatial);
        Assert.Equal(3f, body.Mass);
        Assert.Equal(7f, body.MomentOfInertia);
        Assert.Equal(Vector2.Zero, body.LocalCenterOfMass);
        Assert.True(body.FreezeRotation);
    }

    private static RigidBody2D Disk(Vector2 center) => new PhysicsWorld2D().AddBody(
        new SpatialObject2D(Circle2D.Builder.Create(2f, center)), BodyMotionType2D.Dynamic);

    private static void Near(Vector2 expected, Vector2 actual) =>
        Assert.InRange(Vector2.Distance(expected, actual), 0f, 0.00001f);
}
