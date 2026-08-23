using System.Numerics;
using App2d.Engine;
using App2d.Engine.Geometry;
using App2d.Engine.Physics;
using App2d.Engine.Physics.Constraints;
using App2d.Engine.Rendering;
using SkiaSharp;

namespace App2d.Gameplay;

// Bionic Commando style grapple. The hook is a plain animated point while it
// flies (no physics link to the player at all); latching enables a single
// rope-mode constraint to a static anchor, so the player pendulum-swings and
// keeps real momentum on release. No hidden impulses anywhere.
public sealed class GrappleArm2D
{
    private const float HookSpeed = 1_650f;
    private const float RetractSpeed = 2_400f;
    private const float CatchRadius = 26f;
    private const float MinimumRopeLength = 24f;
    private const float FireOffset = 20f;

    private readonly PhysicsBody2D _ownerBody;
    private readonly PhysicsBody2D _anchorBody;
    private readonly DistanceConstraint2D _rope;
    private readonly RopeVisual2D _ropeVisual;
    private ArmState _state;
    private Vector2 _fireOrigin;
    private Vector2 _fireDirection = Vector2.UnitX;
    private float _traveled;
    private bool _reachedExtensionLimit;

    public GrappleArm2D(
        Scene2D scene,
        PhysicsWorld2D physics,
        PhysicsBody2D ownerBody,
        float maxReach = 430f,
        float rangeGrace = 8f)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(physics);
        ArgumentNullException.ThrowIfNull(ownerBody);
        if (!float.IsFinite(maxReach) || maxReach <= MinimumRopeLength)
            throw new ArgumentOutOfRangeException(nameof(maxReach));
        if (!float.IsFinite(rangeGrace) || rangeGrace < 0f)
            throw new ArgumentOutOfRangeException(nameof(rangeGrace));

        _ownerBody = ownerBody;
        MaxReach = maxReach;
        RangeGrace = rangeGrace;

        Head = new WorldObject2D(
            new Circle2D(10f),
            new LinearGradientShader(
                new SKColor(255, 220, 92),
                new SKColor(237, 91, 58)))
        {
            IsVisible = false
        };
        scene.Add(Head);

        _anchorBody = physics.AddBody(Head, BodyMotionType2D.Static);
        _anchorBody.IsCollider = false;

        _rope = new DistanceConstraint2D(_anchorBody, _ownerBody, maxReach)
        {
            Mode = DistanceConstraintMode2D.Rope,
            IsEnabled = false
        };
        physics.Constraints.Add(_rope);

        _ropeVisual = new RopeVisual2D(
            scene,
            new LinearGradientShader(
                new SKColor(78, 130, 168),
                new SKColor(190, 229, 244)),
            linkCount: 12,
            thickness: 3.5f);
    }

    public WorldObject2D Head { get; }
    public float HeadRadius => ((Circle2D)Head.Shape).Radius;
    public float MaxReach { get; }
    public float RangeGrace { get; }
    public Vector2 FireOrigin => _fireOrigin;
    public Vector2 PreviousHeadPosition { get; private set; }
    public int AttackId { get; private set; }
    public bool IsActive => _state != ArmState.Idle;
    public bool IsExtending => _state == ArmState.Extending;
    public bool IsLatched => _state == ArmState.Latched;

    public bool TryFire(Vector2 target)
    {
        if (IsActive)
            return false;

        var origin = _ownerBody.WorldObject.Transform.Position;
        var direction = target - origin;
        _fireDirection = direction.LengthSquared() > float.Epsilon
            ? Vector2.Normalize(direction)
            : Vector2.UnitX;

        AttackId++;
        _state = ArmState.Extending;
        _fireOrigin = origin;
        _traveled = Math.Min(FireOffset, MaxReach);
        _reachedExtensionLimit = _traveled >= MaxReach;
        Head.Transform.Position = origin + _fireDirection * _traveled;
        PreviousHeadPosition = Head.Transform.Position;
        Head.IsVisible = true;
        _ropeVisual.Show();
        return true;
    }

    public void Update(float deltaSeconds)
    {
        switch (_state)
        {
            case ArmState.Extending:
                PreviousHeadPosition = Head.Transform.Position;
                var step = Math.Min(HookSpeed * deltaSeconds, MaxReach - _traveled);
                Head.Transform.Position += _fireDirection * step;
                _traveled += step;
                _reachedExtensionLimit = _traveled >= MaxReach;
                break;

            case ArmState.Retracting:
                var toOwner = _ownerBody.WorldObject.Transform.Position - Head.Transform.Position;
                var distance = toOwner.Length();
                var retractStep = RetractSpeed * deltaSeconds;
                if (distance <= CatchRadius || retractStep >= distance)
                {
                    Cancel();
                    break;
                }

                Head.Transform.Position += toOwner / distance * retractStep;
                break;
        }
    }

    public void FinishExtensionStep()
    {
        if (_state == ArmState.Extending && _reachedExtensionLimit)
            BeginRetract();
    }

    public bool TryLatch(Vector2 point)
    {
        if (_state != ArmState.Extending || !IsFinite(point))
            return false;

        var ropeLength = Vector2.Distance(_ownerBody.WorldObject.Transform.Position, point);
        if (ropeLength > MaxReach + RangeGrace)
            return false;

        // A grace hit keeps its actual length rather than snapping the player inward.
        Head.Transform.Position = point;
        _rope.RestLength = Math.Max(ropeLength, MinimumRopeLength);
        _rope.IsEnabled = true;
        _state = ArmState.Latched;
        return true;
    }

    // Player keeps whatever swing velocity they have; the fling is pure momentum.
    public bool Release()
    {
        if (!IsLatched)
            return false;

        BeginRetract();
        return true;
    }

    public void BeginRetract()
    {
        if (_state is ArmState.Idle or ArmState.Retracting)
            return;

        _rope.IsEnabled = false;
        _state = ArmState.Retracting;
    }

    public void Cancel()
    {
        _rope.IsEnabled = false;
        _state = ArmState.Idle;
        _reachedExtensionLimit = false;
        Head.IsVisible = false;
        Head.Transform.Position = _ownerBody.WorldObject.Transform.Position;
        _ropeVisual.Hide();
    }

    public void UpdateVisuals()
    {
        if (!IsActive)
            return;

        var start = _ownerBody.WorldObject.Transform.Position;
        var end = Head.Transform.Position;
        var slack = IsLatched
            ? Math.Max(0f, _rope.RestLength - Vector2.Distance(start, end))
            : 8f;
        _ropeVisual.Update(start, end, slack);
    }

    private static bool IsFinite(Vector2 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y);

    private enum ArmState
    {
        Idle,
        Extending,
        Latched,
        Retracting
    }
}
