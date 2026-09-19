using System.Numerics;

namespace App2d.Noodle;

internal sealed class RigidCharacterDemo2D
{
    public const float GroundY = -260f;
    private const float StandingHipsY = GroundY + 190f;
    private const float RunSpeed = 275f;
    private const float Gravity = 1280f;

    private readonly ProceduralLocomotion2D _locomotion = new();
    private PoseClip2D? _activeClip;
    private float _clipTime;
    private float _animationTime;
    private float _hipBob;

    public RigidCharacterDemo2D()
    {
        Hips = new Vector2(0f, StandingHipsY);
        _locomotion.Reset(Hips, GroundY);
        Pose = RigidPose2D.Neutral;
    }

    public Vector2 Hips { get; private set; }
    public Vector2 DisplayHips => Hips + new Vector2(0f, _hipBob);
    public Vector2 Velocity { get; private set; }
    public RigidPose2D Pose { get; private set; }
    public int Facing { get; private set; } = 1;
    public bool IsGrounded { get; private set; } = true;
    public string AnimationLabel { get; private set; } = "authored idle + joint curves";
    public bool LeftFootPlanted => _locomotion.LeftFootPlanted;
    public bool RightFootPlanted => _locomotion.RightFootPlanted;

    public void Update(float deltaSeconds, int movement)
    {
        _animationTime += deltaSeconds;
        if (movement != 0)
            Facing = movement;

        var desiredVelocityX = movement * RunSpeed;
        var acceleration = IsGrounded ? 1550f : 720f;
        var velocityX = MoveTowards(Velocity.X, desiredVelocityX, acceleration * deltaSeconds);
        var velocityY = Velocity.Y;
        if (!IsGrounded)
            velocityY -= Gravity * deltaSeconds;
        Velocity = new Vector2(velocityX, velocityY);
        Hips += Velocity * deltaSeconds;

        if (!IsGrounded && Hips.Y <= StandingHipsY)
        {
            Hips = new Vector2(Hips.X, StandingHipsY);
            Velocity = new Vector2(Velocity.X, 0f);
            IsGrounded = true;
            _locomotion.Reset(Hips, GroundY);
        }

        RigidPose2D basePose;
        if (!IsGrounded)
        {
            _hipBob = 0f;
            basePose = ProceduralLocomotion2D.SampleAirborne(Velocity.Y);
            AnimationLabel = Velocity.Y >= 0f ? "procedural jump pose" : "procedural fall pose";
        }
        else
        {
            basePose = _locomotion.SampleGrounded(Hips, GroundY, Velocity.X, deltaSeconds);
            _hipBob = _locomotion.HipBob;
            basePose = basePose with
            {
                FootLeft = basePose.FootLeft - new Vector2(0f, _hipBob),
                FootRight = basePose.FootRight - new Vector2(0f, _hipBob)
            };
            AnimationLabel = MathF.Abs(Velocity.X) > 8f
                ? "procedural planted-foot locomotion"
                : "IK-authored idle";
        }

        if (_activeClip is not null)
        {
            _clipTime += deltaSeconds;
            basePose = _activeClip.Sample(_clipTime);
            AnimationLabel = _activeClip.Name;
            if (_clipTime >= _activeClip.Duration)
            {
                _activeClip = null;
                _clipTime = 0f;
            }
        }

        Pose = ApplyJointCurves(basePose, _animationTime, IsGrounded);
    }

    public void Jump()
    {
        if (!IsGrounded)
            return;
        IsGrounded = false;
        Velocity = new Vector2(Velocity.X, 505f);
        _activeClip = null;
    }

    public void PlayAttack() => Play(AuthoredClips2D.SwordAttack);
    public void PlayHit() => Play(AuthoredClips2D.Hit);

    public void Reset()
    {
        Hips = new Vector2(0f, StandingHipsY);
        Velocity = Vector2.Zero;
        IsGrounded = true;
        Facing = 1;
        _activeClip = null;
        _clipTime = 0f;
        _animationTime = 0f;
        _hipBob = 0f;
        _locomotion.Reset(Hips, GroundY);
        Pose = RigidPose2D.Neutral;
    }

    private void Play(PoseClip2D clip)
    {
        _activeClip = clip;
        _clipTime = 0f;
        if (IsGrounded)
            Velocity = new Vector2(0f, Velocity.Y);
    }

    private static RigidPose2D ApplyJointCurves(RigidPose2D pose, float time, bool grounded)
    {
        var amount = grounded ? 1f : 0.25f;
        var breath = MathF.Sin(time * 2.35f) * amount;
        var headSway = MathF.Sin(time * 1.17f + 0.8f) * amount;
        return pose with
        {
            HandLeft = pose.HandLeft + new Vector2(0f, breath * 2.4f),
            HandRight = pose.HandRight + new Vector2(0f, -breath * 1.7f),
            TorsoAngle = pose.TorsoAngle + breath * 0.009f,
            HeadAngle = pose.HeadAngle + headSway * 0.018f
        };
    }

    private static float MoveTowards(float current, float target, float maximumDelta)
    {
        var delta = target - current;
        return MathF.Abs(delta) <= maximumDelta ? target : current + MathF.Sign(delta) * maximumDelta;
    }
}

