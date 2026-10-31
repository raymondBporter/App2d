using App2d.Core.Geometry.Primitives;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Physics.Constraints;

internal static class JointMath2D
{
    internal const float PositionTolerance = 0.01f;

    internal static void Validate(RigidBody2D first, RigidBody2D second, Vector2 firstAnchor, Vector2 secondAnchor)
    {
        ArgGuard.ThrowIfNull(first);
        ArgGuard.ThrowIfNull(second);
        ArgGuard.ThrowIfSameReference(first, second, "A joint requires two different bodies.");
        ArgGuard.Finite(firstAnchor);
        ArgGuard.Finite(secondAnchor);
    }

    internal static Vector2 Anchor(RigidBody2D body, Vector2 local) => Vector2.Transform(local, body.WorldObject.Transform.Matrix);
    internal static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
    internal static Vector2 Perpendicular(Vector2 v) => new(-v.Y, v.X);
    internal static Vector2 PointVelocity(RigidBody2D body, Vector2 arm) => body.LinearVelocity + body.AngularVelocity * Perpendicular(arm);

    // Point velocity changes by K * impulse. Angular inertia adds the lever-arm terms.
    // Inverting this 2x2 matrix gives the effective mass of a coincident-anchor constraint.
    internal static Vector2 SolvePointMass(RigidBody2D first, RigidBody2D second, Vector2 firstArm, Vector2 secondArm, Vector2 value)
    {
        var inverseMass = first.InverseMass + second.InverseMass;
        if (inverseMass <= 0f) return Vector2.Zero;
        var ia = first.EffectiveInverseInertia;
        var ib = second.EffectiveInverseInertia;
        var xx = inverseMass + ia * firstArm.Y * firstArm.Y + ib * secondArm.Y * secondArm.Y;
        var xy = -ia * firstArm.X * firstArm.Y - ib * secondArm.X * secondArm.Y;
        var yy = inverseMass + ia * firstArm.X * firstArm.X + ib * secondArm.X * secondArm.X;
        var determinant = (double)xx * yy - (double)xy * xy;
        return determinant > 0d
            ? new Vector2((float)((yy * (double)value.X - xy * (double)value.Y) / determinant),
                (float)((xx * (double)value.Y - xy * (double)value.X) / determinant))
            : Vector2.Zero;
    }

    internal static void ApplyVelocity(RigidBody2D first, RigidBody2D second, Vector2 impulse, float firstAngularImpulse, float secondAngularImpulse)
    {
        first.LinearVelocity -= impulse * first.InverseMass;
        first.AngularVelocity -= firstAngularImpulse * first.EffectiveInverseInertia;
        second.LinearVelocity += impulse * second.InverseMass;
        second.AngularVelocity += secondAngularImpulse * second.EffectiveInverseInertia;
    }

    internal static bool ApplyPosition(RigidBody2D first, RigidBody2D second, Vector2 impulse, float firstAngularImpulse, float secondAngularImpulse)
    {
        var firstAngle = -firstAngularImpulse * first.EffectiveInverseInertia;
        var secondAngle = secondAngularImpulse * second.EffectiveInverseInertia;
        // Keep nonlinear position relaxation local. Scale the entire correction together.
        var scale = Math.Min(1f, 0.2f / Math.Max(0.2f, Math.Max(Math.Abs(firstAngle), Math.Abs(secondAngle))));
        Move(first, -impulse * (first.InverseMass * scale), firstAngle * scale);
        Move(second, impulse * (second.InverseMass * scale), secondAngle * scale);
        return impulse != Vector2.Zero;
    }

    private static void Move(RigidBody2D body, Vector2 translation, float rotation)
    {
        if (translation == Vector2.Zero && rotation == 0f) return;
        var center = body.WorldCenterOfMass + translation;
        body.WorldObject.Transform.Rotation += rotation;
        body.WorldObject.Transform.Position += center - body.WorldCenterOfMass;
    }
}

/// <summary>One scalar constraint direction, including its angular lever arms about both centers of mass.</summary>
internal readonly record struct JointAxis2D(RigidBody2D First, RigidBody2D Second, Vector2 Direction, float FirstLever, float SecondLever)
{
    internal float EffectiveMass
    {
        get
        {
            var inverseMass = First.InverseMass + Second.InverseMass
                + First.EffectiveInverseInertia * FirstLever * FirstLever
                + Second.EffectiveInverseInertia * SecondLever * SecondLever;
            return inverseMass > 0f ? 1f / inverseMass : 0f;
        }
    }

    internal float Speed => Vector2.Dot(Direction, Second.LinearVelocity - First.LinearVelocity)
        + SecondLever * Second.AngularVelocity - FirstLever * First.AngularVelocity;

    internal void ApplyVelocity(float impulse) => JointMath2D.ApplyVelocity(First, Second,
        Direction * impulse, FirstLever * impulse, SecondLever * impulse);

    internal bool CorrectPosition(float error)
    {
        if (Math.Abs(error) <= JointMath2D.PositionTolerance) return false;
        var impulse = -EffectiveMass * Math.Clamp(error, -10f, 10f);
        return JointMath2D.ApplyPosition(First, Second, Direction * impulse, FirstLever * impulse, SecondLever * impulse);
    }

    internal void SolveLimits(float coordinate, Interval<float> limits, float dt, ref float lowerImpulse, ref float upperImpulse)
    {
        var mass = EffectiveMass;
        if (limits.IsBoundedBelow)
        {
            // A positive gap permits inward travel up to gap/dt; at the stop, cancel outward speed.
            // Accumulate a nonnegative reaction so the stop can push, but cannot pull.
            var impulse = -mass * (Speed + Math.Max(0f, coordinate - limits.Min) / dt);
            var previous = lowerImpulse;
            lowerImpulse = Math.Max(0f, previous + impulse);
            ApplyVelocity(lowerImpulse - previous);
        }
        if (limits.IsBoundedAbove)
        {
            var impulse = -mass * (-Speed + Math.Max(0f, limits.Max - coordinate) / dt);
            var previous = upperImpulse;
            upperImpulse = Math.Max(0f, previous + impulse);
            ApplyVelocity(previous - upperImpulse);
        }
    }
}

/// <summary>Implicit integration of q'' + 2*zeta*omega*q' + omega^2*q = 0; q is the joint's scalar error.</summary>
internal readonly record struct SpringCoefficients2D(float BiasRate, float MassScale, float ImpulseScale)
{
    internal static SpringCoefficients2D Create(float frequencyHertz, float dampingRatio, float dt)
    {
        // Frequency/damping express the familiar k = m_eff*omega^2 and c = 2*zeta*m_eff*omega.
        // Factoring m_eff out gives these mass-independent coefficients; the axis supplies m_eff later.
        var omega = 2f * MathF.PI * frequencyHertz;
        var denominator = 2f * dampingRatio + dt * omega;
        var scaledStiffness = dt * omega * denominator;
        var impulseScale = 1f / (1f + scaledStiffness);
        return new(omega / denominator, scaledStiffness * impulseScale, impulseScale);
    }

    internal float Impulse(JointAxis2D axis, float error, float accumulatedImpulse) =>
        -MassScale * axis.EffectiveMass * (axis.Speed + BiasRate * error) - ImpulseScale * accumulatedImpulse;
}
