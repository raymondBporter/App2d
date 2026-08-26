using System.Numerics;
using App2d.Engine;
using App2d.Engine.Physics;

namespace App2d.Gameplay;

public sealed class CharacterMotor2D
{
    private readonly PhysicsWorld2D _physics;
    private readonly PhysicsBody2D _body;
    private PlayerIntent2D _intent;
    private Vector2 _positionBeforePhysics;
    private float _verticalSpeedBeforePhysics;
    private float _gravityScaleBeforePhysics;
    private float _coyoteTime;
    private float _jumpBufferTime;

    public CharacterMotor2D(
        PhysicsWorld2D physics,
        PhysicsBody2D body,
        TraversalMetrics2D metrics)
    {
        ArgGuard.ThrowIfNull(physics);
        ArgGuard.ThrowIfNull(body);
        ArgGuard.ThrowIfNull(metrics);

        _physics = physics;
        _body = body;
        Metrics = metrics;
    }

    public TraversalMetrics2D Metrics { get; }
    public event Action? JumpStarted;
    public event Action<float>? Landed;
    public bool IsGrounded { get; private set; }
    public float CoyoteTimeRemaining => _coyoteTime;
    public float JumpBufferTimeRemaining => _jumpBufferTime;

    public void UpdateBeforePhysics(PlayerIntent2D intent, float deltaSeconds)
    {
        _intent = intent;
        IsGrounded = HasGroundSupport(Metrics.GroundProbeDistance);
        _coyoteTime = IsGrounded
            ? Metrics.CoyoteDuration
            : Math.Max(0f, _coyoteTime - deltaSeconds);

        _jumpBufferTime = intent.JumpPressed
            ? Metrics.JumpBufferDuration
            : Math.Max(0f, _jumpBufferTime - deltaSeconds);

        ApplyHorizontalControl(deltaSeconds);
        TryConsumeBufferedJump();

        if (intent.JumpReleased && _body.LinearVelocity.Y > 0f)
        {
            _body.LinearVelocity = new Vector2(
                _body.LinearVelocity.X,
                _body.LinearVelocity.Y * Metrics.JumpReleaseSpeedMultiplier);
        }

        UpdateGravityScale();
        _positionBeforePhysics = _body.WorldObject.Transform.Position;
        _verticalSpeedBeforePhysics = _body.LinearVelocity.Y;
        _gravityScaleBeforePhysics = _body.GravityScale;
    }

    public void UpdateAfterPhysics(float deltaSeconds)
    {
        TryCorrectUpwardCorner(deltaSeconds);

        if (_body.LinearVelocity.Y < -Metrics.MaximumFallSpeed)
        {
            _body.LinearVelocity = new Vector2(
                _body.LinearVelocity.X,
                -Metrics.MaximumFallSpeed);
        }

        IsGrounded = HasGroundSupport(Metrics.GroundProbeDistance);
        if (!IsGrounded)
            IsGrounded = TrySnapToGround();

        if (IsGrounded)
        {
            _coyoteTime = Metrics.CoyoteDuration;
            TryConsumeBufferedJump();
        }

        if (IsGrounded && _verticalSpeedBeforePhysics < -60f)
            Landed?.Invoke(-_verticalSpeedBeforePhysics);

        UpdateGravityScale();
    }

    public void Reset()
    {
        _coyoteTime = 0f;
        _jumpBufferTime = 0f;
        _intent = default;
        IsGrounded = false;
        _body.GravityScale = 1f;
    }

    private void ApplyHorizontalControl(float deltaSeconds)
    {
        var acceleration = IsGrounded
            ? Metrics.GroundAcceleration
            : Metrics.AirAcceleration;
        var velocityX = _body.LinearVelocity.X;
        var keepAirMomentum = !IsGrounded &&
            MathF.Abs(velocityX) > Metrics.RunSpeed &&
            (_intent.MoveX == 0f || MathF.Sign(_intent.MoveX) == MathF.Sign(velocityX));

        if (!keepAirMomentum)
        {
            velocityX = MoveTowards(
                velocityX,
                _intent.MoveX * Metrics.RunSpeed,
                acceleration * deltaSeconds);
        }

        _body.LinearVelocity = new Vector2(velocityX, _body.LinearVelocity.Y);
    }

    private void TryConsumeBufferedJump()
    {
        if (_jumpBufferTime <= 0f || _coyoteTime <= 0f)
            return;

        var jumpSpeed = _intent.JumpHeld
            ? Metrics.JumpSpeed
            : Metrics.JumpSpeed * Metrics.JumpReleaseSpeedMultiplier;
        _body.LinearVelocity = new Vector2(_body.LinearVelocity.X, jumpSpeed);
        _jumpBufferTime = 0f;
        _coyoteTime = 0f;
        IsGrounded = false;
        JumpStarted?.Invoke();
    }

    private void UpdateGravityScale()
    {
        _body.GravityScale = _intent.JumpHeld &&
            MathF.Abs(_body.LinearVelocity.Y) < Metrics.ApexVelocityThreshold &&
            !IsGrounded
            ? Metrics.ApexGravityScale
            : 1f;
    }

    private bool HasGroundSupport(float maximumGap)
    {
        if (_body.LinearVelocity.Y > 0f)
            return false;
        if (_physics.IsTouching(_body, Vector2.UnitY, 0.55f))
            return true;

        var bodyBounds = _body.WorldObject.WorldBounds;
        foreach (var other in _physics.Bodies)
        {
            if (!CanSupport(other))
                continue;

            var otherBounds = other.WorldObject.WorldBounds;
            if (!otherBounds.IsFinite || !HasHorizontalSupport(bodyBounds, otherBounds))
                continue;

            var gap = bodyBounds.Bottom - otherBounds.Top;
            if (gap >= -0.01f && gap <= maximumGap)
                return true;
        }

        return false;
    }

    private bool TrySnapToGround()
    {
        if (_body.LinearVelocity.Y > 0f)
            return false;

        var bodyBounds = _body.WorldObject.WorldBounds;
        var nearestGap = float.PositiveInfinity;
        foreach (var other in _physics.Bodies)
        {
            if (!CanSupport(other))
                continue;

            var otherBounds = other.WorldObject.WorldBounds;
            if (!otherBounds.IsFinite || !HasHorizontalSupport(bodyBounds, otherBounds))
                continue;

            var gap = bodyBounds.Bottom - otherBounds.Top;
            if (gap > Metrics.GroundProbeDistance &&
                gap <= Metrics.LandingSnapDistance &&
                gap < nearestGap)
            {
                nearestGap = gap;
            }
        }

        if (!float.IsFinite(nearestGap))
            return false;

        _body.WorldObject.Transform.Position -= new Vector2(0f, nearestGap);
        _body.LinearVelocity = new Vector2(_body.LinearVelocity.X, 0f);
        return true;
    }

    private void TryCorrectUpwardCorner(float deltaSeconds)
    {
        if (_verticalSpeedBeforePhysics <= 0f || !HasCeilingContact())
            return;

        var integratedVerticalSpeed = _verticalSpeedBeforePhysics +
            _physics.Gravity.Y * _gravityScaleBeforePhysics * deltaSeconds;
        if (integratedVerticalSpeed <= 0f)
            return;

        var desiredY = _positionBeforePhysics.Y + integratedVerticalSpeed * deltaSeconds;
        var preferredDirection = MathF.Abs(_intent.MoveX) > 0.01f
            ? MathF.Sign(_intent.MoveX)
            : MathF.Sign(_body.LinearVelocity.X);
        if (preferredDirection == 0)
            preferredDirection = 1;

        for (var distance = 1; distance <= Metrics.UpwardCornerCorrection; distance++)
        {
            var first = new Vector2(
                _positionBeforePhysics.X + preferredDirection * distance,
                desiredY);
            if (CanOccupy(first))
            {
                CompleteCornerCorrection(first, integratedVerticalSpeed);
                return;
            }

            var second = new Vector2(
                _positionBeforePhysics.X - preferredDirection * distance,
                desiredY);
            if (CanOccupy(second))
            {
                CompleteCornerCorrection(second, integratedVerticalSpeed);
                return;
            }
        }
    }

    private bool HasCeilingContact()
    {
        foreach (var contact in _physics.LastContacts)
        {
            if (contact.First == _body && contact.Geometry.Normal.Y <= -0.9f)
                return true;
            if (contact.Second == _body && -contact.Geometry.Normal.Y <= -0.9f)
                return true;
        }

        return false;
    }

    private bool CanOccupy(Vector2 position)
    {
        var originalPosition = _body.WorldObject.Transform.Position;
        _body.WorldObject.Transform.Position = position;
        try
        {
            foreach (var other in _physics.Bodies)
            {
                if (ReferenceEquals(other, _body) ||
                    other.IsOneWayPlatform ||
                    other.IsSensor ||
                    !_body.CanCollideWith(other))
                {
                    continue;
                }

                if (!_body.WorldObject.WorldBounds.Intersects(other.WorldObject.WorldBounds))
                    continue;
                if (_physics.ContactProvider.TryGetContact(_body, other, out _))
                    return false;
            }

            return true;
        }
        finally
        {
            _body.WorldObject.Transform.Position = originalPosition;
        }
    }

    private void CompleteCornerCorrection(Vector2 position, float verticalSpeed)
    {
        _body.WorldObject.Transform.Position = position;
        _body.LinearVelocity = new Vector2(_body.LinearVelocity.X, verticalSpeed);
    }

    private bool CanSupport(PhysicsBody2D other) =>
        !ReferenceEquals(other, _body) &&
        other.MotionType != BodyMotionType2D.Dynamic &&
        !other.IsSensor &&
        _body.CanCollideWith(other);

    private bool HasHorizontalSupport(
        Engine.Geometry.Bounds2D bodyBounds,
        Engine.Geometry.Bounds2D supportBounds) =>
        bodyBounds.Right + Metrics.HorizontalSupportGrace >= supportBounds.Left &&
        bodyBounds.Left - Metrics.HorizontalSupportGrace <= supportBounds.Right;

    private static float MoveTowards(float current, float target, float maxDelta)
    {
        if (MathF.Abs(target - current) <= maxDelta)
            return target;
        return current + MathF.Sign(target - current) * maxDelta;
    }
}
