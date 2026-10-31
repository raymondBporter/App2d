using App2d.Core.Constraints;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Physics.Constraints;

/// <summary>
/// A telescoping shock between offset attachment points. Spring and damping act only along
/// the line joining the points; neither attachment's angle is locked. Optional length stops are firm.
/// </summary>
public sealed class SpringDamperJoint2D : IPhysicsConstraint2D
{
    private float _frequencyHertz = 6f, _dampingRatio = 0.7f, _restLength;
    private Interval<float> _lengthLimits = Interval.Entire<float>();
    private JointAxis2D _axis;
    private SpringCoefficients2D _spring;
    private float _length, _springImpulse, _lowerImpulse, _upperImpulse;

    public SpringDamperJoint2D(RigidBody2D first, RigidBody2D second, Vector2 firstLocalAnchor, Vector2 secondLocalAnchor, float restLength)
    {
        JointMath2D.Validate(first, second, firstLocalAnchor, secondLocalAnchor);
        First = first;
        Second = second;
        FirstLocalAnchor = firstLocalAnchor;
        SecondLocalAnchor = secondLocalAnchor;
        RestLength = restLength;
    }

    public RigidBody2D First { get; }
    public RigidBody2D Second { get; }
    public Vector2 FirstLocalAnchor { get; }
    public Vector2 SecondLocalAnchor { get; }
    public Vector2 FirstWorldAnchor => JointMath2D.Anchor(First, FirstLocalAnchor);
    public Vector2 SecondWorldAnchor => JointMath2D.Anchor(Second, SecondLocalAnchor);
    public float Length => Vector2.Distance(FirstWorldAnchor, SecondWorldAnchor);
    public bool IsEnabled { get; set; } = true;
    public float RestLength
    {
        get => _restLength;
        set => _restLength = ArgGuard.Positive(value, nameof(RestLength));
    }
    public Interval<float> LengthLimits
    {
        get => _lengthLimits;
        set
        {
            ArgGuard.ThrowIf(value.IsBoundedBelow && value.Min < 0f || value.IsBoundedAbove && value.Max < 0f,
                "Shock length limits cannot be negative.");
            _lengthLimits = value;
        }
    }
    public float FrequencyHertz
    {
        get => _frequencyHertz;
        set => _frequencyHertz = ArgGuard.Positive(value, nameof(FrequencyHertz));
    }
    public float DampingRatio
    {
        get => _dampingRatio;
        set => _dampingRatio = ArgGuard.NonNegative(value, nameof(DampingRatio));
    }

    public void PrepareVelocity(float deltaSeconds)
    {
        ArgGuard.Positive(deltaSeconds);
        _springImpulse = _lowerImpulse = _upperImpulse = 0f;
        if (!IsEnabled) return;
        _axis = CreateAxis();
        _length = Length;
        _spring = SpringCoefficients2D.Create(FrequencyHertz, DampingRatio, deltaSeconds);
    }

    public void SolveVelocity(float deltaSeconds)
    {
        if (!IsEnabled) return;
        var impulse = _spring.Impulse(_axis, _length - RestLength, _springImpulse);
        _springImpulse += impulse;
        _axis.ApplyVelocity(impulse);
        _axis.SolveLimits(_length, LengthLimits, deltaSeconds, ref _lowerImpulse, ref _upperImpulse);
    }

    public bool SolvePosition(float deltaSeconds) => IsEnabled && CreateAxis().CorrectPosition(ConstraintMath1D.EvaluateLimit(LengthLimits, Length).Error);

    private JointAxis2D CreateAxis()
    {
        var firstAnchor = FirstWorldAnchor;
        var secondAnchor = SecondWorldAnchor;
        var delta = secondAnchor - firstAnchor;
        var direction = delta.LengthSquared() > 1e-8f ? Vector2.Normalize(delta) : Vector2.UnitX;
        // length' = n dot (attachmentVelocityB - attachmentVelocityA).
        // Attachment velocity includes w x r; a force at the attachment also produces r x force torque.
        return new(First, Second, direction,
            JointMath2D.Cross(firstAnchor - First.WorldCenterOfMass, direction),
            JointMath2D.Cross(secondAnchor - Second.WorldCenterOfMass, direction));
    }
}
