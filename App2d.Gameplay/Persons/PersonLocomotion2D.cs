using App2d.Collision;
using App2d.Core;
using App2d.Core.Geometry;
using App2d.Gameplay.Player;
using App2d.Physics;
using System.Numerics;

namespace App2d.Gameplay.Persons;

public sealed class PersonLocomotion2D
{
    private readonly PhysicsWorld2D _physics;
    private readonly CollisionSystem2D _collision;
    private readonly PhysicsBody2D _body;
    private readonly List<Collider2D> _queryResults = [];
    private PersonMovementIntent2D _intent;
    private Vector2 _positionBeforePhysics;
    private float _verticalSpeedBeforePhysics;
    private float _gravityScaleBeforePhysics;
    private bool _wasGroundedBeforePhysics;
    private float _coyoteTime;
    private float _jumpBufferTime;
    private float _jumpInitialSpeed;
    private float _wallJumpBufferTime;
    private float _wallRelatchTime;
    private float _wallDirection;
    private float _dashTimeRemaining;
    private float _dashCooldownRemaining;
    private float _dashDirection;
    private bool _airDashAvailable = true;
    private int _airJumpsRemaining;

    public PersonLocomotion2D(
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
    public bool IsWallGripping { get; private set; }
    public bool IsDashing { get; private set; }
    public float WallDirection => _wallDirection;
    public float CoyoteTimeRemaining => _coyoteTime;
    public float JumpBufferTimeRemaining => _jumpBufferTime;
    public bool IsSustainingJump =>
        _jumpInitialSpeed > 0f &&
        _intent.JumpHeld &&
        !IsDashing &&
        !IsGrounded &&
        _body.LinearVelocity.Y > 0f;
    public float JumpPower => IsSustainingJump
        ? Math.Clamp(
            1f - (_body.LinearVelocity.Y / _jumpInitialSpeed),
            0f,
            1f)
        : 0f;

    public void UpdateBeforePhysics(
        PersonMovementIntent2D intent,
        float facing,
        float deltaSeconds)
    {
        _wasGroundedBeforePhysics = IsGrounded;
        _intent = intent;
        _wallRelatchTime = Math.Max(0f, _wallRelatchTime - deltaSeconds);
        _dashCooldownRemaining = Math.Max(0f, _dashCooldownRemaining - deltaSeconds);
        UpdateIgnoredOneWayPlatforms();
        IsGrounded = HasGroundSupport(Metrics.GroundProbeDistance);
        if (IsGrounded)
        {
            _jumpInitialSpeed = 0f;
            RestoreAirJumps();
            _airDashAvailable = true;
        }

        if (IsDashing && _dashTimeRemaining <= 0f)
            EndDash();
        if (!IsDashing &&
            intent.DashPressed &&
            _dashCooldownRemaining <= 0f &&
            (IsGrounded || _airDashAvailable))
        {
            BeginDash(facing);
        }
        if (IsDashing)
        {
            _dashTimeRemaining = Math.Max(0f, _dashTimeRemaining - deltaSeconds);
            _body.LinearVelocity = new Vector2(_dashDirection * Metrics.DashSpeed, 0f);
            IsWallGripping = false;
            _wallDirection = 0f;
            _body.GravityScale = 0f;
            RecordPrePhysicsState();
            return;
        }

        var beganDropThrough = intent.DropThroughPressed && TryBeginDropThrough();
        if (beganDropThrough)
        {
            IsGrounded = false;
            _coyoteTime = 0f;
            _jumpBufferTime = 0f;
            _wallJumpBufferTime = 0f;
        }
        else
        {
            _coyoteTime = IsGrounded ? Metrics.CoyoteDuration : Math.Max(0f, _coyoteTime - deltaSeconds);
            _jumpBufferTime = intent.JumpPressed
                ? Metrics.JumpBufferDuration
                : Math.Max(0f, _jumpBufferTime - deltaSeconds);
            _wallJumpBufferTime = intent.JumpPressed
                ? Metrics.JumpBufferDuration
                : Math.Max(0f, _wallJumpBufferTime - deltaSeconds);
        }

        UpdateWallGrip();
        ApplyHorizontalControl(deltaSeconds);
        if (!beganDropThrough)
        {
            TryConsumeBufferedWallJump();
            TryConsumeBufferedJump();
        }

        if (intent.JumpReleased && _body.LinearVelocity.Y > 0f)
        {
            _body.LinearVelocity = new Vector2(_body.LinearVelocity.X, _body.LinearVelocity.Y * Metrics.JumpReleaseSpeedMultiplier);
        }

        UpdateGravityScale();
        RecordPrePhysicsState();
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

        UpdateWallGrip();
        TryConsumeBufferedWallJump();

        if (IsGrounded)
        {
            RestoreAirJumps();
            _coyoteTime = Metrics.CoyoteDuration;
            TryConsumeBufferedJump();
        }

        if (!_wasGroundedBeforePhysics && IsGrounded)
        {
            var relativeLandingSpeed =
                GetGroundSupportVerticalSpeed() - _verticalSpeedBeforePhysics;
            if (relativeLandingSpeed > 60f)
                Landed?.Invoke(relativeLandingSpeed);
        }

        UpdateGravityScale();
    }

    public void EnterPassiveState()
    {
        _intent = default;
        _coyoteTime = 0f;
        _jumpBufferTime = 0f;
        _jumpInitialSpeed = 0f;
        _wallJumpBufferTime = 0f;
        _wallRelatchTime = 0f;
        _wallDirection = 0f;
        _dashTimeRemaining = 0f;
        _dashCooldownRemaining = 0f;
        _dashDirection = 0f;
        IsGrounded = false;
        _wasGroundedBeforePhysics = false;
        IsWallGripping = false;
        IsDashing = false;
        _body.GravityScale = 1f;
        _body.ClearIgnoredOneWayPlatforms();
    }

    public void UpdatePassiveAfterPhysics()
    {
        IsGrounded = HasGroundSupport(Metrics.GroundProbeDistance);
        if (!IsGrounded)
            IsGrounded = TrySnapToGround();
    }

    public void Reset()
    {
        _coyoteTime = 0f;
        _jumpBufferTime = 0f;
        _jumpInitialSpeed = 0f;
        _wallJumpBufferTime = 0f;
        _wallRelatchTime = 0f;
        _wallDirection = 0f;
        _dashTimeRemaining = 0f;
        _dashCooldownRemaining = 0f;
        _dashDirection = 0f;
        _airDashAvailable = true;
        _airJumpsRemaining = 0;
        _intent = default;
        IsGrounded = false;
        _wasGroundedBeforePhysics = false;
        IsWallGripping = false;
        IsDashing = false;
        _body.WorldObject.Transform.Scale = Vector2.One;
        _body.GravityScale = 1f;
        _body.ClearIgnoredOneWayPlatforms();
    }

    private void ApplyHorizontalControl(float deltaSeconds)
    {
        var acceleration = IsGrounded
            ? Metrics.GroundAcceleration
            : Metrics.AirAcceleration;
        var velocityX = _body.LinearVelocity.X;
        var keepAirMomentum = !IsGrounded && MathF.Abs(velocityX) > Metrics.RunSpeed && (_intent.MoveX == 0f || MathF.Sign(_intent.MoveX) == MathF.Sign(velocityX));

        if (!keepAirMomentum)
        {
            velocityX = MoveTowards(velocityX, _intent.MoveX * Metrics.RunSpeed, acceleration * deltaSeconds);
        }

        _body.LinearVelocity = new Vector2(velocityX, _body.LinearVelocity.Y);
    }

    private void BeginDash(float facing)
    {
        _jumpInitialSpeed = 0f;
        _dashDirection = MathF.Abs(_intent.MoveX) > 0.01f
            ? MathF.Sign(_intent.MoveX)
            : MathF.Sign(facing);
        if (_dashDirection == 0f)
            _dashDirection = 1f;

        IsDashing = true;
        IsWallGripping = false;
        _wallDirection = 0f;
        _dashTimeRemaining = Metrics.DashDuration;
        _dashCooldownRemaining = Metrics.DashCooldown;
        if (!IsGrounded)
            _airDashAvailable = false;
    }

    private void EndDash()
    {
        IsDashing = false;
        _body.LinearVelocity = new Vector2(
            _dashDirection * Metrics.RunSpeed,
            _body.LinearVelocity.Y);
    }

    private void RecordPrePhysicsState()
    {
        _positionBeforePhysics = _body.WorldObject.Transform.Position;
        _verticalSpeedBeforePhysics = _body.LinearVelocity.Y;
        _gravityScaleBeforePhysics = _body.GravityScale;
    }

    private void TryConsumeBufferedJump()
    {
        if (_jumpBufferTime <= 0f)
            return;

        if (IsWallGripping)
        {
            BeginWallJump(_wallDirection);
            return;
        }

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
        _jumpInitialSpeed = jumpSpeed;
        if (!isGroundJump)
            _airJumpsRemaining--;
        _jumpBufferTime = 0f;
        _coyoteTime = 0f;
        IsGrounded = false;
        JumpStarted?.Invoke();
    }

    private void TryConsumeBufferedWallJump()
    {
        if (_wallJumpBufferTime <= 0f ||
            IsDashing ||
            IsGrounded ||
            _wallRelatchTime > 0f ||
            MathF.Abs(_intent.MoveX) <= 0.01f)
        {
            return;
        }

        var wallDirection = MathF.Sign(_intent.MoveX);
        if (!HasWallSupport(wallDirection, Metrics.WallGripProbeDistance))
            return;

        BeginWallJump(wallDirection);
    }

    private void BeginWallJump(float wallDirection)
    {
        _body.LinearVelocity = new Vector2(
            -wallDirection * Metrics.WallJumpHorizontalSpeed,
            Metrics.JumpSpeed);
        _jumpInitialSpeed = Metrics.JumpSpeed;
        _jumpBufferTime = 0f;
        _wallJumpBufferTime = 0f;
        _coyoteTime = 0f;
        _wallRelatchTime = Metrics.WallJumpRelatchDelay;
        _wallDirection = 0f;
        IsWallGripping = false;
        JumpStarted?.Invoke();
    }

    private void RestoreAirJumps() =>
        _airJumpsRemaining = Metrics.MaximumJumpCount - 1;

    private void UpdateGravityScale()
    {
        _body.GravityScale = IsDashing || IsWallGripping
            ? 0f
            : _intent.JumpHeld &&
            MathF.Abs(_body.LinearVelocity.Y) < Metrics.ApexVelocityThreshold &&
            !IsGrounded
            ? Metrics.ApexGravityScale
            : 1f;
    }

    private void UpdateWallGrip()
    {
        IsWallGripping = false;
        _wallDirection = 0f;
        if (IsDashing ||
            IsGrounded ||
            _wallRelatchTime > 0f ||
            _body.LinearVelocity.Y > 0f ||
            MathF.Abs(_intent.MoveX) <= 0.01f)
        {
            return;
        }

        var direction = MathF.Sign(_intent.MoveX);
        if (!HasWallSupport(direction, Metrics.WallGripProbeDistance))
            return;

        _wallDirection = direction;
        IsWallGripping = true;
        _body.LinearVelocity = new Vector2(_body.LinearVelocity.X, 0f);
    }

    private bool HasWallSupport(float direction, float maximumGap)
    {
        var bodyBounds = _body.WorldObject.WorldBounds;
        QueryBodyBounds(ExpandedSide(bodyBounds, direction, maximumGap));
        foreach (var collider in _queryResults)
        {
            if (collider.UserData is not PhysicsBody2D other ||
                ReferenceEquals(other, _body) ||
                other.MotionType == BodyMotionType2D.Dynamic ||
                other.IsOneWayPlatform ||
                !other.IsWallGrippable ||
                other.IsSensor ||
                !_body.CanCollideWith(other))
            {
                continue;
            }

            var otherBounds = other.WorldObject.WorldBounds;
            if (!otherBounds.IsFinite)
                continue;

            var verticalOverlap = Math.Min(bodyBounds.Top, otherBounds.Top) -
                Math.Max(bodyBounds.Bottom, otherBounds.Bottom);
            if (verticalOverlap < Metrics.WallGripMinimumOverlap)
                continue;

            var gap = direction > 0f
                ? otherBounds.Left - bodyBounds.Right
                : bodyBounds.Left - otherBounds.Right;
            if (gap >= -0.01f && gap <= maximumGap)
                return true;
        }

        return false;
    }

    private bool HasGroundSupport(float maximumGap)
    {
        if (HasGroundContact())
            return true;
        if (_body.LinearVelocity.Y > 0f)
            return false;

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

    private float GetGroundSupportVerticalSpeed()
    {
        var supportSpeed = float.NegativeInfinity;
        foreach (var contact in _physics.LastContacts)
        {
            if (contact.First == _body &&
                contact.Geometry.Normal.Y >= 0.55f &&
                CanSupport(contact.Second))
            {
                supportSpeed = Math.Max(supportSpeed, contact.Second.LinearVelocity.Y);
            }

            if (contact.Second == _body &&
                -contact.Geometry.Normal.Y >= 0.55f &&
                CanSupport(contact.First))
            {
                supportSpeed = Math.Max(supportSpeed, contact.First.LinearVelocity.Y);
            }
        }

        if (float.IsFinite(supportSpeed))
            return supportSpeed;

        var bodyBounds = _body.WorldObject.WorldBounds;
        QueryBodyBounds(ExpandedDown(bodyBounds, Metrics.LandingSnapDistance));
        foreach (var collider in _queryResults)
        {
            if (collider.UserData is not PhysicsBody2D other || !CanSupport(other))
                continue;

            var otherBounds = other.WorldObject.WorldBounds;
            if (!otherBounds.IsFinite || !HasHorizontalSupport(bodyBounds, otherBounds))
                continue;

            var gap = bodyBounds.Bottom - otherBounds.Top;
            if (gap >= -0.01f && gap <= Metrics.LandingSnapDistance)
                supportSpeed = Math.Max(supportSpeed, other.LinearVelocity.Y);
        }

        return float.IsFinite(supportSpeed) ? supportSpeed : 0f;
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

    private static Bounds2D ExpandedSide(Bounds2D bounds, float direction, float distance) =>
        direction > 0f
            ? new Bounds2D(bounds.Min, bounds.Max + new Vector2(distance, 0f))
            : new Bounds2D(bounds.Min - new Vector2(distance, 0f), bounds.Max);

    private bool HasHorizontalSupport(Bounds2D bodyBounds, Bounds2D supportBounds) =>
        bodyBounds.Right + Metrics.HorizontalSupportGrace >= supportBounds.Left &&
        bodyBounds.Left - Metrics.HorizontalSupportGrace <= supportBounds.Right;

    private static float MoveTowards(float current, float target, float maxDelta)
    {
        if (MathF.Abs(target - current) <= maxDelta)
            return target;
        return current + MathF.Sign(target - current) * maxDelta;
    }
}
