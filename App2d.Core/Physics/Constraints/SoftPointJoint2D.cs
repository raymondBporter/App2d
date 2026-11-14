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
    private Vector2f _firstArm, _secondArm, _bias, _impulse;
    private float _massXX, _massXY, _massYY, _massCoefficient, _impulseCoefficient;

    public SoftPointJoint2D(RigidBody2D first, RigidBody2D second,
        Vector2f firstLocalAnchor, Vector2f secondLocalAnchor)
    {
        First = Ensure.RequireNotNull(first);
        Second = Ensure.RequireNotNull(second);
        Ensure.ThrowIfSameReference(first, second, "A point joint requires two different bodies.");
        Ensure.Finite(firstLocalAnchor);
        Ensure.Finite(secondLocalAnchor);
        FirstLocalAnchor = firstLocalAnchor;
        SecondLocalAnchor = secondLocalAnchor;
    }

    public RigidBody2D First { get; }
    public RigidBody2D Second { get; }
    public Vector2f FirstLocalAnchor { get; }
    public Vector2f SecondLocalAnchor { get; }
    public Vector2f FirstWorldAnchor => Vector2f.Transform(FirstLocalAnchor, First.WorldObject.Transform.Matrix);
    public Vector2f SecondWorldAnchor => Vector2f.Transform(SecondLocalAnchor, Second.WorldObject.Transform.Matrix);
    public bool IsEnabled { get; set; } = true;

    /// <summary>Natural frequency in cycles per second. Higher is stiffer; keep below half the substep rate.</summary>
    public float FrequencyHertz
    {
        get => _frequencyHertz;
        set => _frequencyHertz = Ensure.Positive(value, nameof(FrequencyHertz));
    }

    /// <summary>0 is undamped, below 1 oscillates, 1 is critically damped, above 1 settles more slowly.</summary>
    public float DampingRatio
    {
        get => _dampingRatio;
        set => _dampingRatio = Ensure.NonNegative(value, nameof(DampingRatio));
    }

    public void PrepareVelocity(float deltaSeconds)
    {
        Ensure.Positive(deltaSeconds);
        _impulse = Vector2f.Zero;
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
        var impulse = -_massCoefficient * new Vector2f(
            _massXX * speed.X + _massXY * speed.Y,
            _massXY * speed.X + _massYY * speed.Y) - _impulseCoefficient * _impulse;
        _impulse += impulse;
        First.LinearVelocity -= impulse * First.InverseMass;
        First.AngularVelocity -= Cross(_firstArm, impulse) * First.EffectiveInverseInertia;
        Second.LinearVelocity += impulse * Second.InverseMass;
        Second.AngularVelocity += Cross(_secondArm, impulse) * Second.EffectiveInverseInertia;
    }

    public bool SolvePosition(float deltaSeconds) => false;

    private static Vector2f Perpendicular(Vector2f vector) => new(-vector.Y, vector.X);
    private static float Cross(Vector2f a, Vector2f b) => a.X * b.Y - a.Y * b.X;
}
