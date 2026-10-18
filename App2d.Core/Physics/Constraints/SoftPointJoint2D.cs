using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Physics.Constraints;

/// <summary>
/// A damped spring that brings two local anchors together in both axes, leaving relative rotation free.
/// Uses implicit spring impulses, including angular inertia at offset anchors. It never snaps positions.
/// </summary>
public sealed class SoftPointJoint2D : IPhysicsConstraint2D
{
    private float _frequencyHertz = 6f;
    private float _dampingRatio = 0.65f;
    private Vector2 _firstArm, _secondArm, _bias, _impulse;
    private float _massXX, _massXY, _massYY, _massCoefficient, _impulseCoefficient;

    public SoftPointJoint2D(RigidBody2D first, RigidBody2D second,
        Vector2 firstLocalAnchor, Vector2 secondLocalAnchor)
    {
        First = ArgGuard.RequireNotNull(first);
        Second = ArgGuard.RequireNotNull(second);
        ArgGuard.ThrowIfSameReference(first, second, "A point joint requires two different bodies.");
        ArgGuard.Finite(firstLocalAnchor);
        ArgGuard.Finite(secondLocalAnchor);
        FirstLocalAnchor = firstLocalAnchor;
        SecondLocalAnchor = secondLocalAnchor;
    }

    public RigidBody2D First { get; }
    public RigidBody2D Second { get; }
    public Vector2 FirstLocalAnchor { get; }
    public Vector2 SecondLocalAnchor { get; }
    public Vector2 FirstWorldAnchor => Vector2.Transform(FirstLocalAnchor, First.WorldObject.Transform.Matrix);
    public Vector2 SecondWorldAnchor => Vector2.Transform(SecondLocalAnchor, Second.WorldObject.Transform.Matrix);
    public bool IsEnabled { get; set; } = true;

    /// <summary>Natural frequency in cycles per second. Higher is stiffer; keep below half the substep rate.</summary>
    public float FrequencyHertz
    {
        get => _frequencyHertz;
        set => _frequencyHertz = ArgGuard.Positive(value, nameof(FrequencyHertz));
    }

    /// <summary>0 is undamped, below 1 oscillates, 1 is critically damped, above 1 settles more slowly.</summary>
    public float DampingRatio
    {
        get => _dampingRatio;
        set => _dampingRatio = ArgGuard.NonNegative(value, nameof(DampingRatio));
    }

    public void PrepareVelocity(float deltaSeconds)
    {
        ArgGuard.Positive(deltaSeconds);
        _impulse = Vector2.Zero;
        _massXX = _massXY = _massYY = 0f;
        if (!IsEnabled || First.InverseMass + Second.InverseMass <= 0f) return;

        var firstAnchor = FirstWorldAnchor;
        var secondAnchor = SecondWorldAnchor;
        _firstArm = firstAnchor - First.WorldCenterOfMass;
        _secondArm = secondAnchor - Second.WorldCenterOfMass;
        var inverseMass = First.InverseMass + Second.InverseMass;
        var firstInertia = First.EffectiveInverseInertia;
        var secondInertia = Second.EffectiveInverseInertia;
        var xx = inverseMass + firstInertia * _firstArm.Y * _firstArm.Y + secondInertia * _secondArm.Y * _secondArm.Y;
        var xy = -firstInertia * _firstArm.X * _firstArm.Y - secondInertia * _secondArm.X * _secondArm.Y;
        var yy = inverseMass + firstInertia * _firstArm.X * _firstArm.X + secondInertia * _secondArm.X * _secondArm.X;
        var determinant = (double)xx * yy - (double)xy * xy;
        if (determinant <= 0d) return;
        _massXX = (float)(yy / determinant);
        _massXY = (float)(-xy / determinant);
        _massYY = (float)(xx / determinant);

        // Mass-independent implicit spring coefficients (Solver2D / soft sequential impulses).
        // x'' + 2*zeta*omega*x' + omega^2*x = 0, omega = 2*pi*frequency.
        var omega = 2f * MathF.PI * FrequencyHertz;
        var a1 = 2f * DampingRatio + deltaSeconds * omega;
        var a2 = deltaSeconds * omega * a1;
        _impulseCoefficient = 1f / (1f + a2);
        _massCoefficient = a2 * _impulseCoefficient;
        _bias = (secondAnchor - firstAnchor) * (omega / a1);
    }

    public void SolveVelocity(float deltaSeconds)
    {
        if (!IsEnabled) return;
        var relativeVelocity = Second.LinearVelocity + Second.AngularVelocity * Perpendicular(_secondArm)
            - First.LinearVelocity - First.AngularVelocity * Perpendicular(_firstArm);
        var speed = relativeVelocity + _bias;
        var impulse = -_massCoefficient * new Vector2(
            _massXX * speed.X + _massXY * speed.Y,
            _massXY * speed.X + _massYY * speed.Y) - _impulseCoefficient * _impulse;
        _impulse += impulse;
        First.LinearVelocity -= impulse * First.InverseMass;
        First.AngularVelocity -= Cross(_firstArm, impulse) * First.EffectiveInverseInertia;
        Second.LinearVelocity += impulse * Second.InverseMass;
        Second.AngularVelocity += Cross(_secondArm, impulse) * Second.EffectiveInverseInertia;
    }

    public bool SolvePosition(float deltaSeconds) => false;

    private static Vector2 Perpendicular(Vector2 vector) => new(-vector.Y, vector.X);
    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
}
