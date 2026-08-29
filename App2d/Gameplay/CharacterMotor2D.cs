using System.Numerics;
using App2d.Engine.Collision;
using App2d.Engine.Geometry;
using App2d.Engine.Physics;

namespace App2d.Gameplay;

public sealed class CharacterMotor2D
{
    private readonly PhysicsWorld2D _physics;
    private readonly CollisionSystem2D _collision;
    private readonly PhysicsBody2D _body;
    private readonly List<Collider2D> _queryResults = [];
    private PlayerIntent2D _intent;
    private Vector2 _positionBeforePhysics;
    private float _verticalSpeedBeforePhysics;
    private float _gravityScaleBeforePhysics;
    private float _coyoteTime;
    private float _jumpBufferTime;
    private int _airJumpsRemaining;

    public CharacterMotor2D(
        CollisionSystem2D collision,
        PhysicsWorld2D physics,
        PhysicsBody2D body,
        TraversalMetrics2D metrics)
    {
        ArgGuard.ThrowIfNull(collision);
        ArgGuard.ThrowIfNull(physics);
        ArgGuard.ThrowIfNull(body);
        ArgGuard.ThrowIfNull(metrics);

        _collision = collision;
        _physics = physics;
        _body = body;
        Metrics = metrics;
    }

    public TraversalMetrics2D Metrics { get; }
    public event Action? JumpStarted;
    public event Action<float>? Landed;
    public bool IsGrounded { get; private set; }
    public bool IsDucking { get; private set; }
    public float CoyoteTimeRemaining => _coyoteTime;
    public float JumpBufferTimeRemaining => _jumpBufferTime;

    public void UpdateBeforePhysics(PlayerIntent2D intent, float deltaSeconds)
    {
        _intent = intent;
        UpdateIgnoredOneWayPlatforms();
        IsGrounded = HasGroundSupport(Metrics.GroundProbeDistance);
        if (IsGrounded)
            RestoreAirJumps();
        UpdateDucking(intent.DuckHeld && IsGrounded);
        var beganDropThrough = intent.DropThroughPressed && TryBeginDropThrough();
        if (beganDropThrough)
        {
            IsGrounded = false;
            _coyoteTime = 0f;
            _jumpBufferTime = 0f;
        }
        else
        {
            _coyoteTime = IsGrounded ? Metrics.CoyoteDuration : Math.Max(0f, _coyoteTime - deltaSeconds);
            _jumpBufferTime = intent.JumpPressed && !IsDucking
                ? Metrics.JumpBufferDuration
                : Math.Max(0f, _jumpBufferTime - deltaSeconds);
        }

        ApplyHorizontalControl(deltaSeconds);
        if (!beganDropThrough)
            TryConsumeBufferedJump();

        if (intent.JumpReleased && _body.LinearVelocity.Y > 0f)
        {
            _body.LinearVelocity = new Vector2(_body.LinearVelocity.X, _body.LinearVelocity.Y * Metrics.JumpReleaseSpeedMultiplier);
        }

        UpdateGravityScale();
        _positionBeforePhysics = _body.WorldObject.Transform.Position;
        _verticalSpeedBeforePhysics = _body.LinearVelocity.Y;
        _gravityScaleBeforePhysics = _body.GravityScale;
    }

    public void UpdateAfterPhysics(float deltaSeconds)
    {
        TryCorrectUpwardCorner(deltaSeconds);
        UpdateIgnoredOneWayPlatforms();

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
            RestoreAirJumps();
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
        _airJumpsRemaining = 0;
        _intent = default;
        IsGrounded = false;
        IsDucking = false;
        _body.WorldObject.Transform.Scale = Vector2.One;
        _body.GravityScale = 1f;
        _body.ClearIgnoredOneWayPlatforms();
    }

    private void ApplyHorizontalControl(float deltaSeconds)
    {
        var acceleration = IsGrounded
            ? Metrics.GroundAcceleration
            : Metrics.AirAcceleration;
        var maximumSpeed = IsDucking
            ? Metrics.DuckingSpeed
            : Metrics.RunSpeed;
        var velocityX = _body.LinearVelocity.X;
        var keepAirMomentum = !IsGrounded && MathF.Abs(velocityX) > Metrics.RunSpeed && (_intent.MoveX == 0f || MathF.Sign(_intent.MoveX) == MathF.Sign(velocityX));

        if (!keepAirMomentum)
        {
            velocityX = MoveTowards(velocityX, _intent.MoveX * maximumSpeed, acceleration * deltaSeconds);
        }

        _body.LinearVelocity = new Vector2(velocityX, _body.LinearVelocity.Y);
    }

    private void TryConsumeBufferedJump()
    {
        if (_jumpBufferTime <= 0f || IsDucking)
            return;

        var isGroundJump = _coyoteTime > 0f;
        if (!isGroundJump && _airJumpsRemaining <= 0)
            return;

        var fullJumpSpeed = isGroundJump
            ? Metrics.JumpSpeed
            : Metrics.AirJumpSpeed;
        var jumpSpeed = _intent.JumpHeld
            ? fullJumpSpeed
            : fullJumpSpeed * Metrics.JumpReleaseSpeedMultiplier;
        _body.LinearVelocity = new Vector2(_body.LinearVelocity.X, jumpSpeed);
        if (!isGroundJump)
            _airJumpsRemaining--;
        _jumpBufferTime = 0f;
        _coyoteTime = 0f;
        IsGrounded = false;
        JumpStarted?.Invoke();
    }

    private void RestoreAirJumps() =>
        _airJumpsRemaining = Metrics.MaximumJumpCount - 1;

    private void UpdateDucking(bool wantsToDuck)
    {
        if (wantsToDuck)
        {
            if (!IsDucking)
                SetDucking(true);
            return;
        }

        if (IsDucking && CanStand())
            SetDucking(false);
    }

    private bool CanStand()
    {
        SetDucking(false);
        try
        {
            QueryBodyBounds(_body.WorldObject.WorldBounds);
            foreach (var collider in _queryResults)
            {
                if (collider.UserData is not PhysicsBody2D other)
                    continue;
                if (ReferenceEquals(other, _body) || other.IsOneWayPlatform || other.IsSensor || !_body.CanCollideWith(other))
                {
                    continue;
                }

                if (_collision.TryGetContact(_body.Collider, other.Collider, out _))
                    return false;
            }

            return true;
        }
        finally
        {
            SetDucking(true);
        }
    }

    private void SetDucking(bool isDucking)
    {
        if (IsDucking == isDucking)
            return;

        var transform = _body.WorldObject.Transform;
        var oldHeight = IsDucking
            ? Metrics.PlayerDuckingColliderSize.Y
            : Metrics.PlayerColliderSize.Y;
        var newHeight = isDucking
            ? Metrics.PlayerDuckingColliderSize.Y
            : Metrics.PlayerColliderSize.Y;
        transform.Scale = new Vector2(
            transform.Scale.X,
            newHeight / Metrics.PlayerColliderSize.Y);
        transform.Position += new Vector2(0f, (newHeight - oldHeight) * 0.5f);
        IsDucking = isDucking;
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
        if (HasGroundContact())
            return true;

        var bodyBounds = _body.WorldObject.WorldBounds;
        QueryBodyBounds(ExpandedDown(bodyBounds, maximumGap));
        foreach (var collider in _queryResults)
        {
            if (collider.UserData is not PhysicsBody2D other)
                continue;
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

    private bool TryBeginDropThrough()
    {
        List<PhysicsBody2D>? oneWaySupports = null;
        var bodyBounds = _body.WorldObject.WorldBounds;
        QueryBodyBounds(ExpandedDown(bodyBounds, Metrics.GroundProbeDistance));
        foreach (var collider in _queryResults)
        {
            if (collider.UserData is not PhysicsBody2D other)
                continue;
            if (!CanSupport(other))
                continue;

            var otherBounds = other.WorldObject.WorldBounds;
            if (!otherBounds.IsFinite || !HasHorizontalSupport(bodyBounds, otherBounds))
                continue;

            var gap = bodyBounds.Bottom - otherBounds.Top;
            if (gap < -other.OneWaySlop || gap > Metrics.GroundProbeDistance)
                continue;

            if (!other.IsOneWayPlatform)
                return false;

            oneWaySupports ??= [];
            oneWaySupports.Add(other);
        }

        if (oneWaySupports is null)
            return false;

        foreach (var support in oneWaySupports)
            _body.IgnoreOneWayPlatform(support);
        _body.LinearVelocity = new Vector2(
            _body.LinearVelocity.X,
            Math.Min(_body.LinearVelocity.Y, -Metrics.OneWayDropSpeed));
        return true;
    }

    private void UpdateIgnoredOneWayPlatforms()
    {
        if (_body.IgnoredOneWayPlatformCount == 0)
            return;

        var bodyTop = _body.WorldObject.WorldBounds.Top;
        _body.RemoveIgnoredOneWayPlatformsWhere(platform =>
            !_physics.Bodies.Contains(platform) ||
            bodyTop < platform.WorldObject.WorldBounds.Bottom);
    }

    private bool HasGroundContact()
    {
        foreach (var contact in _physics.LastContacts)
        {
            if (contact.First == _body && contact.Geometry.Normal.Y >= 0.55f && CanSupport(contact.Second))
            {
                return true;
            }

            if (contact.Second == _body && -contact.Geometry.Normal.Y >= 0.55f && CanSupport(contact.First))
            {
                return true;
            }
        }

        return false;
    }

    private bool TrySnapToGround()
    {
        if (_body.LinearVelocity.Y > 0f)
            return false;

        var bodyBounds = _body.WorldObject.WorldBounds;
        var nearestGap = float.PositiveInfinity;
        QueryBodyBounds(ExpandedDown(bodyBounds, Metrics.LandingSnapDistance));
        foreach (var collider in _queryResults)
        {
            if (collider.UserData is not PhysicsBody2D other)
                continue;
            if (!CanSupport(other))
                continue;

            var otherBounds = other.WorldObject.WorldBounds;
            if (!otherBounds.IsFinite || !HasHorizontalSupport(bodyBounds, otherBounds))
                continue;

            var gap = bodyBounds.Bottom - otherBounds.Top;
            if (gap > Metrics.GroundProbeDistance && gap <= Metrics.LandingSnapDistance && gap < nearestGap)
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

        var integratedVerticalSpeed = _verticalSpeedBeforePhysics + _physics.Gravity.Y * _gravityScaleBeforePhysics * deltaSeconds;
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
            var first = new Vector2(_positionBeforePhysics.X + preferredDirection * distance, desiredY);
            if (CanOccupy(first))
            {
                CompleteCornerCorrection(first, integratedVerticalSpeed);
                return;
            }

            var second = new Vector2(_positionBeforePhysics.X - preferredDirection * distance, desiredY);
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
            QueryBodyBounds(_body.WorldObject.WorldBounds);
            foreach (var collider in _queryResults)
            {
                if (collider.UserData is not PhysicsBody2D other)
                    continue;
                if (ReferenceEquals(other, _body) ||
                    other.IsOneWayPlatform ||
                    other.IsSensor ||
                    !_body.CanCollideWith(other))
                {
                    continue;
                }

                if (_collision.TryGetContact(_body.Collider, other.Collider, out _))
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
        !_body.IsIgnoringOneWayPlatform(other) &&
        other.MotionType != BodyMotionType2D.Dynamic &&
        !other.IsSensor &&
        _body.CanCollideWith(other);

    private void QueryBodyBounds(Bounds2D bounds) =>
        _collision.QueryBounds(
            bounds,
            _queryResults,
            _body.CollisionMask,
            includeSensors: false,
            excluded: _body.Collider);

    private static Bounds2D ExpandedDown(Bounds2D bounds, float distance) =>
        new(bounds.Min - new Vector2(0f, distance), bounds.Max);

    private bool HasHorizontalSupport(Engine.Geometry.Bounds2D bodyBounds, Engine.Geometry.Bounds2D supportBounds) =>
        bodyBounds.Right + Metrics.HorizontalSupportGrace >= supportBounds.Left &&
        bodyBounds.Left - Metrics.HorizontalSupportGrace <= supportBounds.Right;

    private static float MoveTowards(float current, float target, float maxDelta)
    {
        if (MathF.Abs(target - current) <= maxDelta)
            return target;
        return current + MathF.Sign(target - current) * maxDelta;
    }
}
