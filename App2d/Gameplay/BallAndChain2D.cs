using System.Numerics;
using App2d.Engine;
using App2d.Engine.Geometry;
using App2d.Engine.Physics;
using App2d.Engine.Physics.Constraints;
using App2d.Engine.Rendering;
using SkiaSharp;

namespace App2d.Gameplay;

// Heavy tethered anchor. The ball flies as a free dynamic body clamped to the
// chain length by moving only the ball (throwing it can never yank the player).
// Once landed it sits dead and the rope-mode tether limits how far the player
// can walk away; yanking pulls it back through everything in its path.
public sealed class BallAndChain2D
{
    private const float ChainLength = 360f;
    private const float ThrowSpeed = 820f;
    private const float YankSpeed = 1_450f;
    private const float CatchRadius = 48f;
    private const float FireOffset = 30f;

    private readonly PhysicsBody2D _ownerBody;
    private readonly PhysicsBody2D _ballBody;
    private readonly DistanceConstraint2D _tether;
    private readonly RopeVisual2D _chainVisual;
    private BallState _state;

    public BallAndChain2D(
        Scene2D scene,
        PhysicsWorld2D physics,
        PhysicsBody2D ownerBody,
        uint collisionLayer,
        uint collisionMask)
    {
        ArgGuard.ThrowIfNull(scene);
        ArgGuard.ThrowIfNull(physics);
        ArgGuard.ThrowIfNull(ownerBody);

        _ownerBody = ownerBody;

        Ball = new WorldObject2D(new Circle2D(17f), new LinearGradientShader(new SKColor(112, 117, 130), new SKColor(43, 46, 56)))
        {
            IsVisible = false
        };
        scene.Add(Ball);

        _ballBody = physics.AddBody(Ball, BodyMotionType2D.Kinematic);
        _ballBody.IsCollider = false;
        _ballBody.Mass = 8f;
        _ballBody.Restitution = 0f;
        _ballBody.GravityScale = 1.15f;
        _ballBody.CollisionLayer = collisionLayer;
        _ballBody.CollisionMask = collisionMask;

        _tether = new DistanceConstraint2D(_ownerBody, _ballBody, ChainLength)
        {
            Mode = DistanceConstraintMode2D.Rope,
            IsEnabled = false
        };
        physics.Constraints.Add(_tether);

        _chainVisual = new RopeVisual2D(
            scene,
            new SolidColorShader(new SKColor(74, 78, 90)),
            linkCount: 10,
            thickness: 4f);
    }

    public WorldObject2D Ball { get; }
    public int AttackId { get; private set; }
    public bool IsActive => _state != BallState.Idle;
    public bool IsFlying => _state == BallState.Flying;
    public bool IsLanded => _state == BallState.Landed;
    public bool IsReturning => _state == BallState.Returning;
    public bool CanYank => _state is BallState.Flying or BallState.Landed;
    public bool DealsDamage => _state is BallState.Flying or BallState.Returning;

    public Vector2 TravelDirection
    {
        get
        {
            var direction = IsReturning
                ? _ownerBody.WorldObject.Transform.Position - Ball.Transform.Position
                : _ballBody.LinearVelocity;
            return direction.LengthSquared() > float.Epsilon
                ? Vector2.Normalize(direction)
                : Vector2.UnitX;
        }
    }

    public bool TryThrow(Vector2 target)
    {
        if (IsActive)
            return false;

        var origin = _ownerBody.WorldObject.Transform.Position;
        var direction = target - origin;
        direction = direction.LengthSquared() > float.Epsilon
            ? Vector2.Normalize(direction)
            : Vector2.UnitX;

        AttackId++;
        _state = BallState.Flying;
        Ball.Transform.Position = origin + direction * FireOffset;
        Ball.IsVisible = true;
        _ballBody.MotionType = BodyMotionType2D.Dynamic;
        _ballBody.IsCollider = true;
        _ballBody.LinearVelocity = direction * ThrowSpeed;
        _ballBody.AngularVelocity = 0f;
        _chainVisual.Show();
        return true;
    }

    // The return trip gets its own attack id so enemies hit on the way out can
    // be hit again on the way back.
    public bool TryYank()
    {
        if (!CanYank)
            return false;

        AttackId++;
        _tether.IsEnabled = false;
        _ballBody.MotionType = BodyMotionType2D.Kinematic;
        _ballBody.IsCollider = false;
        _ballBody.LinearVelocity = Vector2.Zero;
        _state = BallState.Returning;
        return true;
    }

    public void UpdateBeforePhysics(float deltaSeconds)
    {
        if (_state != BallState.Returning)
            return;

        var toOwner = _ownerBody.WorldObject.Transform.Position - Ball.Transform.Position;
        var distance = toOwner.Length();
        var step = YankSpeed * deltaSeconds;
        if (distance <= CatchRadius || step >= distance)
        {
            Cancel();
            return;
        }

        Ball.Transform.Position += toOwner / distance * step;
    }

    public void UpdateAfterPhysics(PhysicsWorld2D physics)
    {
        ArgGuard.ThrowIfNull(physics);
        if (!IsActive)
            return;

        if (IsFlying)
        {
            ClampFlightToChain();
            if (physics.IsTouching(_ballBody, Vector2.UnitY, 0.55f))
            {
                _ballBody.LinearVelocity = Vector2.Zero;
                _tether.IsEnabled = true;
                _state = BallState.Landed;
            }
        }

        var start = _ownerBody.WorldObject.Transform.Position;
        var end = Ball.Transform.Position;
        var slack = IsReturning
            ? 8f
            : Math.Max(0f, ChainLength - Vector2.Distance(start, end));
        _chainVisual.Update(start, end, slack);
    }

    public void Cancel()
    {
        _tether.IsEnabled = false;
        _state = BallState.Idle;
        _ballBody.MotionType = BodyMotionType2D.Kinematic;
        _ballBody.IsCollider = false;
        _ballBody.LinearVelocity = Vector2.Zero;
        _ballBody.AngularVelocity = 0f;
        Ball.Transform.Position = _ownerBody.WorldObject.Transform.Position;
        Ball.IsVisible = false;
        _chainVisual.Hide();
    }

    // Moves only the ball, so an upward throw hitting the chain limit swings
    // the ball around the player instead of slingshotting the player.
    private void ClampFlightToChain()
    {
        var ownerPosition = _ownerBody.WorldObject.Transform.Position;
        var offset = Ball.Transform.Position - ownerPosition;
        var distance = offset.Length();
        if (distance <= ChainLength || distance <= float.Epsilon)
            return;

        var direction = offset / distance;
        Ball.Transform.Position = ownerPosition + direction * ChainLength;
        var radialSpeed = Vector2.Dot(_ballBody.LinearVelocity, direction);
        if (radialSpeed > 0f)
            _ballBody.LinearVelocity -= direction * radialSpeed;
    }

    private enum BallState
    {
        Idle,
        Flying,
        Landed,
        Returning
    }
}
