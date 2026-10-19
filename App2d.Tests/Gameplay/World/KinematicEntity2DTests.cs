using App2d.Core;
using App2d.Core.Geometry.Curves;
using App2d.Core.Mathematics.Progress;
using App2d.Core.Physics;
using App2d.Core.Physics.Motion;
using App2d.Core.Shapes;
using App2d.Gameplay.World;
using System.Numerics;

namespace App2d.Tests.Gameplay.World;

public sealed class KinematicEntity2DTests
{
    [Fact]
    public void CustomMotionDrivesAnyShapeThroughKinematicPhysics()
    {
        var physics = new PhysicsWorld2D { Gravity = Vector2.Zero };
        var motion = new OrbitMotion();
        using var entity = new KinematicEntity2D(EntityId2D.Create(), physics,
            Circle2D.Builder.Create(2f), motion, collisionLayer: 1, collisionMask: uint.MaxValue);

        Assert.Equal(BodyMotionType2D.Kinematic, entity.Body.MotionType);
        Assert.Equal(new Vector2(10f, 0f), entity.WorldObject.Transform.Position);
        var displacement = entity.Update(0.25f);
        Assert.Equal(new Vector2(-10f, 10f), displacement, new Vector2Tolerance(0.001f));
        physics.Step(0.25f);
        Assert.Equal(new Vector2(0f, 10f), entity.WorldObject.Transform.Position, new Vector2Tolerance(0.001f));
        Assert.Equal(0.25d, entity.TimeSeconds);
        Assert.Equal(new Vector2(-20f * MathF.PI, 0f), entity.MotionVelocity,
            new Vector2Tolerance(0.001f));
        entity.Update(0.25f);
        physics.Step(0.25f);
        Assert.Equal(new Vector2(-10f, 0f), entity.WorldObject.Transform.Position,
            new Vector2Tolerance(0.001f));
        entity.Dispose();
        Assert.DoesNotContain(entity.Body, physics.Bodies);
    }

    [Fact]
    public void PingPongMotionReflectsAtEndpointsForArbitrarySampleTimes()
    {
        var motion = new PingPongMotion2D(Vector2.Zero, new Vector2(10f, 0f), 4f);
        Assert.Equal(new Vector2(8f, 0f), motion.Position(2d));
        Assert.Equal(new Vector2(10f, 0f), motion.Position(2.5d));
        Assert.Equal(new Vector2(-4f, 0f), motion.Velocity(2.5d));
        Assert.Equal(new Vector2(8f, 0f), motion.Position(3d));
        Assert.Equal(Vector2.Zero, motion.Position(5d));
        Assert.Equal(new Vector2(4f, 0f), motion.Velocity(5d));
    }

    [Theory]
    [InlineData("smooth", 1.5625f)]
    [InlineData("smoother", 1.03515625f)]
    [InlineData("smoothest", .70556640625f)]
    public void EasedPingPongCurveHasContinuousPositionAndZeroTurnaroundSpeed(string kind, float quarterPosition)
    {
        var easing = kind switch
        {
            "smoother" => Easing.Smoother,
            "smoothest" => Easing.Smoothest,
            _ => Easing.Smooth
        };
        var motion = new CurveMotion2D(
            LineSegmentCurve2D.Builder.Create(Vector2.Zero, new Vector2(10f, 0f)),
            2d,
            ProgressMaps.Compose(ProgressMaps.PingPong, easing));

        Assert.Equal(Vector2.Zero, motion.Position(0d));
        Assert.Equal(new Vector2(10f, 0f), motion.Position(1d));
        Assert.Equal(Vector2.Zero, motion.Position(2d));
        Assert.Equal(Vector2.Zero, motion.Position(4d));
        Assert.Equal(new Vector2(quarterPosition, 0f), motion.Position(0.25d));
        Assert.Equal(Vector2.Zero, motion.Velocity(1d));
        Assert.Equal(Vector2.Zero, motion.Velocity(2d));
        Assert.Equal(motion.Position(0.4d), motion.Position(4.4d), new Vector2Tolerance(0.0001f));
    }

    [Fact]
    public void ClosedArcCanLoopWithoutChangingTheMotionDriver()
    {
        var motion = new CurveMotion2D(Arc2D.Builder.Create(Vector2.Zero, 10f, 0f, 2f * MathF.PI),
            4d, ProgressMaps.Loop);

        Assert.Equal(new Vector2(10f, 0f), motion.Position(0d), new Vector2Tolerance(0.0001f));
        Assert.Equal(new Vector2(0f, 10f), motion.Position(1d), new Vector2Tolerance(0.0001f));
        Assert.Equal(motion.Position(0d), motion.Position(4d), new Vector2Tolerance(0.0001f));
        Assert.Equal(new Vector2(0f, 5f * MathF.PI), motion.Velocity(0d),
            new Vector2Tolerance(0.0001f));
    }

    [Fact]
    public void OpenCurveCannotWrapToItsStartWithoutPingPong()
    {
        var line = LineSegmentCurve2D.Builder.Create(Vector2.Zero, new Vector2(10f, 0f));
        Assert.Throws<ArgumentException>(() => new CurveMotion2D(line, 4d, ProgressMaps.Loop));
    }

    [Theory]
    [InlineData(ProgressMapKind.Smooth)]
    [InlineData(ProgressMapKind.Smoother)]
    [InlineData(ProgressMapKind.Smoothest)]
    public void BoundedEasingKeepsNearEndpointKinematicUpdatesValid(ProgressMapKind kind)
    {
        var physics = new PhysicsWorld2D();
        var progress = ProgressMaps.FromKind(kind);
        var motion = new CurveMotion2D(LineSegmentCurve2D.Builder.Create(Vector2.Zero, new(10, 0)), 1, progress, repeat: false);
        using var entity = new KinematicEntity2D(EntityId2D.Create(), physics, Circle2D.Builder.Create(1), motion, 1, uint.MaxValue);

        Assert.Equal(new Vector2(10, 0), motion.Position(.9999999d));
        Assert.Equal(new Vector2(10, 0), entity.Update(.9999999f));
        physics.Step(.9999999f);
        Assert.Equal(new Vector2(10, 0), entity.WorldObject.Transform.Position, new Vector2Tolerance(.0001f));
    }

    [Theory]
    [InlineData(1.0000000000000004d)]
    [InlineData(-4.440892098500626e-16d)]
    [InlineData(1.00000001d)]
    [InlineData(-.00000001d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void OutOfRangeAndNonfiniteProgressFromCustomMapsRemainInvalid(double sampledProgress)
    {
        var progress = new ProgressMap(phase => phase is 0d or 1d ? phase : sampledProgress, _ => 1d);
        var motion = new CurveMotion2D(LineSegmentCurve2D.Builder.Create(Vector2.Zero, new(10, 0)), 1, progress, repeat: false);
        Assert.Throws<ArgumentOutOfRangeException>(() => motion.Position(.25));
        Assert.Throws<ArgumentOutOfRangeException>(() => motion.Velocity(.25));
    }

    private sealed class OrbitMotion : IKinematicMotion2D
    {
        public Vector2 Position(double seconds)
        {
            var angle = (float)(seconds * Math.PI * 2d);
            return new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 10f;
        }

        public Vector2 Velocity(double seconds)
        {
            var angle = (float)(seconds * Math.PI * 2d);
            return new Vector2(-MathF.Sin(angle), MathF.Cos(angle)) * (20f * MathF.PI);
        }
    }

    private sealed class Vector2Tolerance(float tolerance) : IEqualityComparer<Vector2>
    {
        public bool Equals(Vector2 first, Vector2 second) =>
            Vector2.Distance(first, second) <= tolerance;

        public int GetHashCode(Vector2 value) => value.GetHashCode();
    }
}
