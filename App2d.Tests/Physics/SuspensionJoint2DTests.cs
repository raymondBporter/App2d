using App2d.Core.Geometry.Primitives;
using App2d.Core.Physics;
using App2d.Core.Physics.Constraints;
using App2d.Core.Shapes;
using App2d.Core.Spatial;
using System.Numerics;

namespace App2d.Tests.Physics;

public sealed class SuspensionJoint2DTests
{
    private const float Dt = 1f / 120f;

    [Theory]
    [InlineData(0f)]
    [InlineData(0.6f)]
    [InlineData(1.5707963f)]
    public void WheelSlidesOnlyAlongRotatedForkAndRemainsFreeToSpin(float rotation)
    {
        var world = new PhysicsWorld2D();
        var frame = AddBody(world, BodyMotionType2D.Static);
        frame.WorldObject.Transform.Position = new(3f, 4f);
        frame.WorldObject.Transform.Rotation = rotation;
        frame.WorldObject.Transform.Scale = new Vector2(2f);
        var wheel = AddBody(world, BodyMotionType2D.Dynamic);
        var joint = new WheelJoint2D(frame, wheel, new(1f, 2f), Vector2.Zero, new(0f, 8f)) { IsSpringEnabled = false };
        var axis = joint.WorldAxis;
        var sideways = new Vector2(-axis.Y, axis.X);
        wheel.WorldObject.Transform.Position = joint.FirstWorldAnchor + axis * 12f + sideways * 5f;
        wheel.LinearVelocity = axis * 3f + sideways * 7f;
        wheel.AngularVelocity = 8f;
        world.Constraints.Add(joint);
        world.Step(Dt);
        Assert.InRange(Math.Abs(joint.LateralError), 0f, 0.001f);
        Assert.Equal(12f + 3f * Dt, joint.Translation, 4);
        Assert.InRange(Vector2.Distance(axis * 3f, wheel.LinearVelocity), 0f, 0.0001f);
        Assert.Equal(8f, wheel.AngularVelocity);
    }

    [Fact]
    public void WheelVelocityConstraintIncludesRotationOfTheGuideAxis()
    {
        var world = new PhysicsWorld2D();
        var frame = AddBody(world, BodyMotionType2D.Kinematic);
        frame.AngularVelocity = 2f;
        var wheel = AddBody(world, BodyMotionType2D.Dynamic);
        wheel.WorldObject.Transform.Position = Vector2.UnitY * 5f;
        wheel.LinearVelocity = new(-10f, 0f); // w x position: following the rotating guide exactly.
        var joint = new WheelJoint2D(frame, wheel, Vector2.Zero, Vector2.Zero, Vector2.UnitY) { IsSpringEnabled = false };
        joint.PrepareVelocity(Dt);
        joint.SolveVelocity(Dt);
        Assert.Equal(new Vector2(-10f, 0f), wheel.LinearVelocity);
    }

    [Theory]
    [InlineData(-8f, -100f, 0f, 3f)]
    [InlineData(38f, 100f, 28f, -3f)]
    public void ForkStopsBlockOutwardTravelButPermitReturn(float position, float speed, float stop, float returnSpeed)
    {
        var world = new PhysicsWorld2D();
        var frame = AddBody(world, BodyMotionType2D.Static);
        var wheel = AddBody(world, BodyMotionType2D.Dynamic);
        var joint = new WheelJoint2D(frame, wheel, Vector2.Zero, Vector2.Zero, Vector2.UnitY)
        {
            IsSpringEnabled = false,
            TranslationLimits = new Interval<float>(0f, 28f)
        };
        world.Constraints.Add(joint);
        wheel.WorldObject.Transform.Position = Vector2.UnitY * position;
        wheel.LinearVelocity = Vector2.UnitY * speed;
        world.Step(Dt);
        Assert.Equal(stop, joint.Translation, 3);
        Assert.Equal(0f, wheel.LinearVelocity.Y, 3);
        wheel.LinearVelocity = Vector2.UnitY * returnSpeed;
        world.Step(Dt);
        Assert.Equal(returnSpeed, wheel.LinearVelocity.Y, 3);
        Assert.Equal(stop + returnSpeed * Dt, joint.Translation, 3);
    }

    [Theory]
    [InlineData(0.2f, true)]
    [InlineData(1f, false)]
    public void AxialSpringOvershootsAccordingToDampingAndSettles(float damping, bool overshoots)
    {
        var world = new PhysicsWorld2D { MaxSubstepSeconds = 1f / 240f, VelocityIterations = 8 };
        var frame = AddBody(world, BodyMotionType2D.Static);
        var wheel = AddBody(world, BodyMotionType2D.Dynamic);
        wheel.WorldObject.Transform.Position = Vector2.UnitY * 10f;
        wheel.AngularVelocity = 6f;
        var joint = new WheelJoint2D(frame, wheel, Vector2.Zero, Vector2.Zero, Vector2.UnitY)
        {
            RestTranslation = 2f,
            FrequencyHertz = 2f,
            DampingRatio = damping
        };
        world.Constraints.Add(joint);
        var minimum = 10f;
        for (var step = 0; step < 720; step++)
        {
            world.Step(1f / 240f);
            minimum = Math.Min(minimum, joint.Translation);
        }
        Assert.Equal(overshoots, minimum < 1.99f);
        Assert.InRange(Math.Abs(joint.Translation - 2f), 0f, 0.01f);
        Assert.Equal(6f, wheel.AngularVelocity);
    }

    [Fact]
    public void RevoluteBearingPreservesPivotWhileTheArmSwingsAndWheelSpins()
    {
        var world = new PhysicsWorld2D { PositionIterations = 12, VelocityIterations = 12 };
        var frame = AddBody(world, BodyMotionType2D.Static);
        var arm = AddBody(world, BodyMotionType2D.Dynamic);
        arm.WorldObject.Transform.Position = new(-4f, 0f);
        arm.LinearVelocity = new(0f, -8f);
        arm.AngularVelocity = 2f;
        var wheel = AddBody(world, BodyMotionType2D.Dynamic);
        wheel.WorldObject.Transform.Position = new(-8f, 0f);
        wheel.AngularVelocity = 7f;
        var pivot = new RevoluteJoint2D(frame, arm, Vector2.Zero, new(4f, 0f));
        var bearing = new RevoluteJoint2D(arm, wheel, new(-4f, 0f), Vector2.Zero);
        world.Constraints.Add(pivot);
        world.Constraints.Add(bearing);
        for (var step = 0; step < 120; step++) world.Step(Dt);
        Assert.InRange(Vector2.Distance(pivot.FirstWorldAnchor, pivot.SecondWorldAnchor), 0f, 0.02f);
        Assert.InRange(Vector2.Distance(bearing.FirstWorldAnchor, bearing.SecondWorldAnchor), 0f, 0.02f);
        Assert.InRange(Math.Abs(arm.WorldObject.Transform.Rotation), 0.1f, 5f);
        Assert.Equal(7f, wheel.AngularVelocity);
    }

    [Fact]
    public void ShockDoesNotDampPerpendicularMotionButDoesDampAxialMotion()
    {
        var world = new PhysicsWorld2D();
        var frame = AddBody(world, BodyMotionType2D.Static);
        var wheel = AddBody(world, BodyMotionType2D.Dynamic);
        wheel.WorldObject.Transform.Position = new(3f, 4f);
        var shock = new SpringDamperJoint2D(frame, wheel, Vector2.Zero, Vector2.Zero, 5f);
        wheel.LinearVelocity = new(-4f, 3f);
        wheel.AngularVelocity = 9f;
        shock.PrepareVelocity(Dt);
        shock.SolveVelocity(Dt);
        Assert.Equal(new Vector2(-4f, 3f), wheel.LinearVelocity);
        wheel.LinearVelocity = new(3f, 4f);
        shock.PrepareVelocity(Dt);
        shock.SolveVelocity(Dt);
        Assert.InRange(wheel.LinearVelocity.Length(), 0f, 4.99f);
        Assert.InRange(Math.Abs(Vector2.Dot(wheel.LinearVelocity, new(-0.8f, 0.6f))), 0f, 0.0001f);
        Assert.Equal(9f, wheel.AngularVelocity);
    }

    [Fact]
    public void ShockForcesAtOffsetMountsProduceTorqueAndConserveMomentum()
    {
        var world = new PhysicsWorld2D();
        var frame = AddBody(world, BodyMotionType2D.Dynamic);
        frame.Mass = 3f;
        var arm = AddBody(world, BodyMotionType2D.Dynamic);
        arm.WorldObject.Transform.Position = new(4f, 2f);
        arm.LinearVelocity = new(4f, 0f);
        var shock = new SpringDamperJoint2D(frame, arm, new(0f, 2f), Vector2.Zero, 4f);
        shock.PrepareVelocity(Dt);
        shock.SolveVelocity(Dt);
        Assert.True(frame.AngularVelocity < 0f);
        Assert.Equal(0f, arm.AngularVelocity);
        Assert.InRange(Vector2.Distance(new(4f, 0f), frame.LinearVelocity * frame.Mass + arm.LinearVelocity * arm.Mass), 0f, 0.0001f);
    }

    [Fact]
    public void ShockDampsAttachmentMotionCausedByRotation()
    {
        var world = new PhysicsWorld2D();
        var frame = AddBody(world, BodyMotionType2D.Static);
        var arm = AddBody(world, BodyMotionType2D.Dynamic);
        arm.WorldObject.Transform.Position = new(4f, 0f);
        arm.AngularVelocity = 2f;
        var shock = new SpringDamperJoint2D(frame, arm, Vector2.Zero, new(0f, 2f), MathF.Sqrt(20f));
        shock.PrepareVelocity(Dt);
        shock.SolveVelocity(Dt);
        Assert.InRange(arm.AngularVelocity, 0f, 1.99f);
        Assert.True(arm.LinearVelocity.X > 0f);
    }

    [Fact]
    public void ShockStopsLimitLengthAndDisabledJointsDoNothing()
    {
        var world = new PhysicsWorld2D();
        var frame = AddBody(world, BodyMotionType2D.Static);
        var arm = AddBody(world, BodyMotionType2D.Dynamic);
        arm.WorldObject.Transform.Position = new(2f, 0f);
        var shock = new SpringDamperJoint2D(frame, arm, Vector2.Zero, Vector2.Zero, 5f)
        {
            IsEnabled = false,
            LengthLimits = new Interval<float>(4f, 6f)
        };
        world.Constraints.Add(shock);
        world.Step(Dt);
        Assert.Equal(2f, shock.Length);
        Assert.Equal(Vector2.Zero, arm.LinearVelocity);
        shock.IsEnabled = true;
        world.Step(Dt);
        Assert.Equal(4f, shock.Length, 3);
        arm.WorldObject.Transform.Position = new(10f, 0f);
        world.Step(Dt);
        Assert.Equal(6f, shock.Length, 3);
        Assert.True(arm.LinearVelocity.X <= 0f);
    }

    private static RigidBody2D AddBody(PhysicsWorld2D world, BodyMotionType2D type)
    {
        var body = world.AddBody(new SpatialObject2D(Circle2D.Builder.Create(1f)), type);
        body.IsCollider = false;
        body.FreezeRotation = false;
        return body;
    }
}
