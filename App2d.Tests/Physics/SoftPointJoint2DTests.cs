using App2d.Core.Physics;
using App2d.Core.Physics.Constraints;
using App2d.Core.Shapes;
using App2d.Core.Spatial;
using System.Numerics;

namespace App2d.Tests.Physics;

public sealed class SoftPointJoint2DTests
{
    [Theory]
    [InlineData(0.2f, true)]
    [InlineData(1f, false)]
    public void DampingControlsOvershootAndSpringSettles(float damping, bool shouldOvershoot)
    {
        var (world, body, joint) = CreateSpring();
        joint.FrequencyHertz = 2f;
        joint.DampingRatio = damping;
        var minimum = 1f;
        for (var step = 0; step < 720; step++)
        {
            world.Step(1f / 240f);
            minimum = Math.Min(minimum, body.WorldCenterOfMass.X);
        }
        Assert.Equal(shouldOvershoot, minimum < -0.01f);
        Assert.InRange(MathF.Abs(body.WorldCenterOfMass.X), 0f, 0.001f);
    }

    [Fact]
    public void MoreVelocityIterationsDoNotMultiplySpringStrength()
    {
        var (firstWorld, firstBody, _) = CreateSpring();
        var (secondWorld, secondBody, _) = CreateSpring();
        secondWorld.VelocityIterations = 8;
        for (var step = 0; step < 100; step++)
        {
            firstWorld.Step(1f / 120f);
            secondWorld.Step(1f / 120f);
            Assert.InRange(Vector2.Distance(firstBody.WorldCenterOfMass, secondBody.WorldCenterOfMass), 0f, 0.00001f);
        }
    }

    [Fact]
    public void AnchorImpulsesConserveMomentumAndTurnTheFrameButDoNotLockWheelSpin()
    {
        var world = new PhysicsWorld2D();
        var frame = AddBody(world, BodyMotionType2D.Dynamic);
        frame.Mass = 3f;
        frame.MomentOfInertia = 2f;
        var wheel = AddBody(world, BodyMotionType2D.Dynamic);
        wheel.WorldObject.Transform.Position = Vector2.UnitY;
        wheel.LinearVelocity = Vector2.UnitX * 5f;
        wheel.AngularVelocity = 8f;
        var joint = new SoftPointJoint2D(frame, wheel, Vector2.UnitY, Vector2.Zero);
        var momentum = frame.Mass * frame.LinearVelocity + wheel.Mass * wheel.LinearVelocity;
        var angularMomentum = wheel.MomentOfInertia * wheel.AngularVelocity - wheel.Mass * wheel.LinearVelocity.X;
        joint.PrepareVelocity(1f / 120f);
        joint.SolveVelocity(1f / 120f);
        Assert.InRange(Vector2.Distance(momentum, frame.Mass * frame.LinearVelocity + wheel.Mass * wheel.LinearVelocity), 0f, 0.00001f);
        Assert.NotEqual(0f, frame.AngularVelocity);
        Assert.Equal(8f, wheel.AngularVelocity);
        var afterAngularMomentum = frame.MomentOfInertia * frame.AngularVelocity
            + wheel.MomentOfInertia * wheel.AngularVelocity - wheel.Mass * wheel.LinearVelocity.X;
        Assert.Equal(angularMomentum, afterAngularMomentum, 4);
    }

    [Fact]
    public void LocalAnchorsUseRotationScaleAndCenterOfMass()
    {
        var world = new PhysicsWorld2D();
        var frame = world.AddBody(new SpatialObject2D(Circle2D.Builder.Create(1f, new Vector2(2f, 0f))), BodyMotionType2D.Dynamic);
        frame.FreezeRotation = false;
        frame.IsCollider = false;
        frame.WorldObject.Transform.Position = new(3f, 4f);
        frame.WorldObject.Transform.Rotation = MathF.PI / 2f;
        frame.WorldObject.Transform.Scale = new Vector2(2f);
        frame.SetMassFromShape(2f);
        var wheel = AddBody(world, BodyMotionType2D.Dynamic);
        wheel.WorldObject.Transform.Position = new(4f, 12f);
        var joint = new SoftPointJoint2D(frame, wheel, new(4f, 0f), Vector2.Zero);
        Assert.InRange(Vector2.Distance(new(3f, 12f), joint.FirstWorldAnchor), 0f, 0.00001f);
        joint.PrepareVelocity(1f / 120f);
        joint.SolveVelocity(1f / 120f);
        Assert.True(frame.LinearVelocity.X > 0f);
        Assert.True(frame.AngularVelocity < 0f);
        Assert.True(wheel.LinearVelocity.X < 0f);
    }

    [Fact]
    public void PreparationResetsAccumulatedImpulseEverySubstep()
    {
        var (batchedWorld, batchedBody, _) = CreateSpring();
        var (separateWorld, separateBody, _) = CreateSpring();
        batchedWorld.MaxSubstepSeconds = separateWorld.MaxSubstepSeconds = 1f / 120f;
        batchedWorld.Step(1f / 30f);
        for (var step = 0; step < 4; step++) separateWorld.Step(1f / 120f);
        Assert.Equal(separateBody.WorldCenterOfMass, batchedBody.WorldCenterOfMass);
        Assert.Equal(separateBody.LinearVelocity, batchedBody.LinearVelocity);
    }

    [Fact]
    public void DisabledAndImmovableJointsAreInert()
    {
        var (world, body, joint) = CreateSpring();
        joint.IsEnabled = false;
        world.Step(1f / 60f);
        Assert.Equal(Vector2.Zero, body.LinearVelocity);
        joint.IsEnabled = true;
        body.MotionType = BodyMotionType2D.Static;
        world.Step(1f / 60f);
        Assert.Equal(Vector2.Zero, body.LinearVelocity);
    }

    private static (PhysicsWorld2D World, RigidBody2D Body, SoftPointJoint2D Joint) CreateSpring()
    {
        var world = new PhysicsWorld2D();
        var anchor = AddBody(world, BodyMotionType2D.Static);
        var body = AddBody(world, BodyMotionType2D.Dynamic);
        body.WorldObject.Transform.Position = Vector2.UnitX;
        var joint = new SoftPointJoint2D(anchor, body, Vector2.Zero, Vector2.Zero);
        world.Constraints.Add(joint);
        return (world, body, joint);
    }

    private static RigidBody2D AddBody(PhysicsWorld2D world, BodyMotionType2D type)
    {
        var body = world.AddBody(new SpatialObject2D(Circle2D.Builder.Create(1f)), type);
        body.IsCollider = false;
        body.FreezeRotation = false;
        return body;
    }
}
