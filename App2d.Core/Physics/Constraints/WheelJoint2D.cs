using App2d.Core.Constraints;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Physics.Constraints;

/// <summary>
/// Keeps the second anchor on a line fixed in the first body. Suspension acts along that line;
/// perpendicular motion is constrained, and the second body remains free to spin.
/// </summary>
public sealed class WheelJoint2D : IPhysicsConstraint2D
{
    private float _frequencyHertz = 6f, _dampingRatio = 0.7f, _restTranslation;
    private JointAxis2D _axial, _lateral;
    private SpringCoefficients2D _spring;
    private float _translation, _springImpulse, _lowerImpulse, _upperImpulse;

    public WheelJoint2D(RigidBody2D first, RigidBody2D second, Vector2 firstLocalAnchor, Vector2 secondLocalAnchor, Vector2 firstLocalAxis)
    {
        JointMath2D.Validate(first, second, firstLocalAnchor, secondLocalAnchor);
        ArgGuard.NonZero(firstLocalAxis);
        First = first;
        Second = second;
        FirstLocalAnchor = firstLocalAnchor;
        SecondLocalAnchor = secondLocalAnchor;
        FirstLocalAxis = Vector2.Normalize(firstLocalAxis);
    }

    public RigidBody2D First { get; }
    public RigidBody2D Second { get; }
    public Vector2 FirstLocalAnchor { get; }
    public Vector2 SecondLocalAnchor { get; }
    public Vector2 FirstLocalAxis { get; }
    public Vector2 FirstWorldAnchor => JointMath2D.Anchor(First, FirstLocalAnchor);
    public Vector2 SecondWorldAnchor => JointMath2D.Anchor(Second, SecondLocalAnchor);
    public Vector2 WorldAxis => Vector2.Normalize(Vector2.TransformNormal(FirstLocalAxis, First.WorldObject.Transform.Matrix));
    public float Translation => Vector2.Dot(SecondWorldAnchor - FirstWorldAnchor, WorldAxis);
    public float LateralError => Vector2.Dot(SecondWorldAnchor - FirstWorldAnchor, JointMath2D.Perpendicular(WorldAxis));
    public bool IsEnabled { get; set; } = true;
    public bool IsSpringEnabled { get; set; } = true;
    /// <summary>Allowed travel in world units along the guide axis, measured from coincident anchors.</summary>
    public ConstraintLimit1D TranslationLimits { get; set; } = ConstraintLimit1D.Free;
    /// <summary>Unloaded spring coordinate in world units; zero means the anchors coincide.</summary>
    public float RestTranslation
    {
        get => _restTranslation;
        set => _restTranslation = ArgGuard.Finite(value, nameof(RestTranslation));
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
        _axial = CreateAxis(WorldAxis);
        _lateral = CreateAxis(JointMath2D.Perpendicular(WorldAxis));
        _translation = Translation;
        _spring = SpringCoefficients2D.Create(FrequencyHertz, DampingRatio, deltaSeconds);
    }

    public void SolveVelocity(float deltaSeconds)
    {
        if (!IsEnabled) return;
        if (IsSpringEnabled)
        {
            var impulse = _spring.Impulse(_axial, _translation - RestTranslation, _springImpulse);
            _springImpulse += impulse;
            _axial.ApplyVelocity(impulse);
        }
        _axial.SolveLimits(_translation, TranslationLimits, deltaSeconds, ref _lowerImpulse, ref _upperImpulse);
        _lateral.ApplyVelocity(-_lateral.EffectiveMass * _lateral.Speed);
    }

    public bool SolvePosition(float deltaSeconds)
    {
        if (!IsEnabled) return false;
        var moved = CreateAxis(JointMath2D.Perpendicular(WorldAxis)).CorrectPosition(LateralError);
        var error = TranslationLimits.Evaluate(Translation).Error;
        return CreateAxis(WorldAxis).CorrectPosition(error) || moved;
    }

    private JointAxis2D CreateAxis(Vector2 direction)
    {
        var firstAnchor = FirstWorldAnchor;
        var secondAnchor = SecondWorldAnchor;
        var firstArm = firstAnchor - First.WorldCenterOfMass;
        var secondArm = secondAnchor - Second.WorldCenterOfMass;
        // q = direction dot (pB - pA). The direction itself rotates with body A.
        // Differentiating adds wA x direction, giving the d + rA lever instead of just rA.
        // q' = direction dot (vB-vA) + cross(rB,direction)*wB - cross(d+rA,direction)*wA.
        return new(First, Second, direction,
            JointMath2D.Cross(secondAnchor - firstAnchor + firstArm, direction), JointMath2D.Cross(secondArm, direction));
    }
}
