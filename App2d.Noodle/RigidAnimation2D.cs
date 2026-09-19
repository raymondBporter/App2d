using System.Numerics;
using System.Text.Json;
using App2d.Core.Mathematics;

namespace App2d.Noodle;

internal static class AuthoredClips2D
{
    public static PoseClip2D Idle { get; } = new(
        "authored idle",
        loops: true,
        new(0f, RigidPose2D.Neutral),
        new(0.9f, RigidPose2D.Neutral with
        {
            HandLeft = new Vector2(-69f, 39f),
            HandRight = new Vector2(77f, 32f),
            TorsoAngle = -0.012f
        }),
        new(1.8f, RigidPose2D.Neutral));

    public static PoseClip2D SwordAttack { get; } = new(
        "IK-authored sword attack",
        loops: false,
        new(0f, RigidPose2D.Neutral),
        new(0.13f, RigidPose2D.Neutral with
        {
            HandLeft = new Vector2(15f, 116f),
            HandRight = new Vector2(-42f, 140f),
            TorsoAngle = 0.12f,
            HeadAngle = -0.08f
        }),
        new(0.27f, RigidPose2D.Neutral with
        {
            HandLeft = new Vector2(94f, 96f),
            HandRight = new Vector2(168f, 49f),
            FootLeft = new Vector2(-31f, -188f),
            FootRight = new Vector2(39f, -188f),
            TorsoAngle = -0.18f,
            HeadAngle = 0.1f
        }),
        new(0.48f, RigidPose2D.Neutral with
        {
            HandRight = new Vector2(112f, 18f),
            TorsoAngle = -0.08f
        }),
        new(0.72f, RigidPose2D.Neutral));

    public static PoseClip2D Hit { get; } = new(
        "IK-authored hit reaction",
        loops: false,
        new(0f, RigidPose2D.Neutral),
        new(0.08f, RigidPose2D.Neutral with
        {
            HandLeft = new Vector2(-112f, 91f),
            HandRight = new Vector2(34f, 116f),
            TorsoAngle = 0.2f,
            HeadAngle = 0.25f
        }),
        new(0.22f, RigidPose2D.Neutral with
        {
            HandLeft = new Vector2(-129f, 53f),
            HandRight = new Vector2(12f, 88f),
            TorsoAngle = 0.13f,
            HeadAngle = 0.14f
        }),
        new(0.52f, RigidPose2D.Neutral));
}

/// <summary>
/// A planted-foot gait. During the stance portion Target never changes in world
/// space; only the swinging foot is allowed to move.
/// </summary>
internal sealed class ProceduralLocomotion2D
{
    private FootState _left;
    private FootState _right;
    private float _phase;
    private float _idleTime;
    private bool _initialized;

    public float HipBob { get; private set; }
    public bool LeftFootPlanted => !_left.Swinging;
    public bool RightFootPlanted => !_right.Swinging;

    public void Reset(Vector2 hips, float groundY)
    {
        _phase = 0.38f;
        _idleTime = 0f;
        _left = FootState.Planted(new Vector2(hips.X - 24f, groundY + 13f));
        _right = FootState.Planted(new Vector2(hips.X + 25f, groundY + 13f));
        _initialized = true;
        HipBob = 0f;
    }

    public RigidPose2D SampleGrounded(Vector2 hips, float groundY, float velocityX, float deltaSeconds)
    {
        if (!_initialized)
            Reset(hips, groundY);

        var speed = MathF.Abs(velocityX);
        _idleTime += deltaSeconds;
        var speedAmount = Math.Clamp(speed / 270f, 0f, 1f);
        if (speed > 8f)
            _phase = Wrap01(_phase + speed * deltaSeconds / float.Lerp(155f, 205f, speedAmount));

        var direction = MathF.Abs(velocityX) > 8f ? MathF.Sign(velocityX) : 1f;
        UpdateFoot(ref _left, Wrap01(_phase), hips, groundY, direction, speedAmount);
        UpdateFoot(ref _right, Wrap01(_phase + 0.5f), hips, groundY, direction, speedAmount);

        HipBob = speed > 8f ? -MathF.Abs(MathF.Sin(_phase * MathF.Tau)) * float.Lerp(2f, 7f, speedAmount) : 0f;
        var armSwing = MathF.Sin(_phase * MathF.Tau) * float.Lerp(8f, 48f, speedAmount);
        var pose = RigidPose2D.Neutral with
        {
            HandLeft = new Vector2(-72f - armSwing, 40f),
            HandRight = new Vector2(78f + armSwing, 37f),
            FootLeft = _left.Target - hips,
            FootRight = _right.Target - hips,
            TorsoAngle = -0.035f * speedAmount
        };

        if (speed <= 8f)
        {
            // The authored idle owns the upper body, but the current planted feet
            // stay in place so stopping never introduces a visible pop or slide.
            var idle = AuthoredClips2D.Idle.Sample(_idleTime);
            pose = idle with { FootLeft = pose.FootLeft, FootRight = pose.FootRight };
        }
        return pose;
    }

    public static RigidPose2D SampleAirborne(float verticalVelocity)
    {
        var falling = Math.Clamp((-verticalVelocity + 80f) / 620f, 0f, 1f);
        var risingPose = RigidPose2D.Neutral with
        {
            HandLeft = new Vector2(-89f, 112f),
            HandRight = new Vector2(87f, 123f),
            FootLeft = new Vector2(-42f, -130f),
            FootRight = new Vector2(39f, -151f),
            TorsoAngle = -0.04f
        };
        var fallingPose = RigidPose2D.Neutral with
        {
            HandLeft = new Vector2(-118f, 71f),
            HandRight = new Vector2(113f, 73f),
            FootLeft = new Vector2(-22f, -186f),
            FootRight = new Vector2(31f, -174f),
            TorsoAngle = 0.035f
        };
        return RigidPose2D.Lerp(risingPose, fallingPose, falling);
    }

    private static void UpdateFoot(
        ref FootState foot,
        float phase,
        Vector2 hips,
        float groundY,
        float direction,
        float speedAmount)
    {
        const float swingFraction = 0.34f;
        var shouldSwing = phase < swingFraction && speedAmount > 0.03f;
        if (shouldSwing && !foot.Swinging)
        {
            foot.Swinging = true;
            foot.Start = foot.Target;
            var stride = float.Lerp(62f, 118f, speedAmount);
            foot.Goal = new Vector2(hips.X + direction * stride * 0.62f, groundY + 13f);
        }
        else if (!shouldSwing && foot.Swinging)
        {
            foot.Swinging = false;
            foot.Target = foot.Goal;
        }

        if (!foot.Swinging)
            return;
        var amount = Math.Clamp(phase / swingFraction, 0f, 1f);
        var smooth = Interpolation.SmoothStep(amount);
        foot.Target = Vector2.Lerp(foot.Start, foot.Goal, smooth);
        foot.Target.Y += MathF.Sin(amount * MathF.PI) * float.Lerp(20f, 43f, speedAmount);
    }

    private static float Wrap01(float value) => value - MathF.Floor(value);

    private struct FootState
    {
        public Vector2 Target;
        public Vector2 Start;
        public Vector2 Goal;
        public bool Swinging;

        public static FootState Planted(Vector2 target) => new()
        {
            Target = target,
            Start = target,
            Goal = target
        };
    }
}

internal sealed class PoseAuthoring2D
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly List<PoseKeyframe2D> _frames = [];

    public IReadOnlyList<PoseKeyframe2D> Frames => _frames;
    public RigidPose2D Pose { get; set; } = RigidPose2D.Neutral;

    public void Capture()
    {
        var time = _frames.Count == 0 ? 0f : _frames[^1].Time + 0.18f;
        _frames.Add(new PoseKeyframe2D(time, Pose));
    }

    public void Clear() => _frames.Clear();

    public PoseClip2D? CreateClip() => _frames.Count == 0
        ? null
        : new PoseClip2D("user pose clip", loops: true, [.. _frames]);

    public string Save(string path)
    {
        var saved = new SavedPoseFile(
            "app2d-standard-humanoid-v1",
            _frames.Select(frame => new SavedPoseFrame(
                frame.Time,
                SavedPoint.From(frame.Pose.HandLeft),
                SavedPoint.From(frame.Pose.HandRight),
                SavedPoint.From(frame.Pose.FootLeft),
                SavedPoint.From(frame.Pose.FootRight),
                frame.Pose.TorsoAngle,
                frame.Pose.HeadAngle)).ToArray());
        var json = JsonSerializer.Serialize(saved, JsonOptions);
        File.WriteAllText(path, json);
        return path;
    }

    private sealed record SavedPoseFile(string Skeleton, SavedPoseFrame[] Frames);
    private sealed record SavedPoseFrame(
        float Time,
        SavedPoint HandLeft,
        SavedPoint HandRight,
        SavedPoint FootLeft,
        SavedPoint FootRight,
        float TorsoAngle,
        float HeadAngle);
    private readonly record struct SavedPoint(float X, float Y)
    {
        public static SavedPoint From(Vector2 value) => new(value.X, value.Y);
    }
}
