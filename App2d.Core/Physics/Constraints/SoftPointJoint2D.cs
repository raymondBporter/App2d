using App2d.Core.Validation;

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
    private float _massXX, _massXY, _massYY;
    private SpringCoefficients2D _spring;

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
    public Vector2f FirstWorldAnchor => JointMath2D.Anchor(First, FirstLocalAnchor);
    public Vector2f SecondWorldAnchor => JointMath2D.Anchor(Second, SecondLocalAnchor);
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

        _spring = SpringCoefficients2D.Create(FrequencyHertz, DampingRatio, deltaSeconds);
        _bias = (secondAnchor - firstAnchor) * _spring.BiasRate;
    }

    public void SolveVelocity(float deltaSeconds)
    {
        if (!IsEnabled) return;
        var relativeVelocity = Second.VelocityAtOffset(_secondArm)
            - First.LinearVelocity - First.AngularVelocity * _firstArm.PerpCcw;
        var speed = relativeVelocity + _bias;
        var impulse = -_spring.MassScale * new Vector2f(
            _massXX * speed.X + _massXY * speed.Y,
            _massXY * speed.X + _massYY * speed.Y) - _spring.ImpulseScale * _impulse;
        _impulse += impulse;
        JointMath2D.ApplyVelocity(First, Second, impulse, _firstArm.Cross(impulse), _secondArm.Cross(impulse));
    }

    public bool SolvePosition(float deltaSeconds) => false;
}
