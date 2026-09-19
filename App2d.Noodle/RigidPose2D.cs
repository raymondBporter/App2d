using System.Numerics;
using App2d.Core.Mathematics;

namespace App2d.Noodle;

/// <summary>
/// The tiny, art-independent data saved by the pose editor. Targets are stored in
/// hips-local space while angles are radians, so the same pose can be mirrored.
/// </summary>
internal readonly record struct RigidPose2D(
    Vector2 HandLeft,
    Vector2 HandRight,
    Vector2 FootLeft,
    Vector2 FootRight,
    float TorsoAngle,
    float HeadAngle)
{
    public static RigidPose2D Neutral => new(
        new Vector2(-74f, 35f),
        new Vector2(82f, 36f),
        new Vector2(-18f, -190f),
        new Vector2(22f, -190f),
        0f,
        0f);

    public static RigidPose2D Lerp(RigidPose2D from, RigidPose2D to, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return new RigidPose2D(
            Vector2.Lerp(from.HandLeft, to.HandLeft, amount),
            Vector2.Lerp(from.HandRight, to.HandRight, amount),
            Vector2.Lerp(from.FootLeft, to.FootLeft, amount),
            Vector2.Lerp(from.FootRight, to.FootRight, amount),
            float.Lerp(from.TorsoAngle, to.TorsoAngle, amount),
            float.Lerp(from.HeadAngle, to.HeadAngle, amount));
    }

    public Vector2 GetTarget(RigidControl2D control) => control switch
    {
        RigidControl2D.HandLeft => HandLeft,
        RigidControl2D.HandRight => HandRight,
        RigidControl2D.FootLeft => FootLeft,
        RigidControl2D.FootRight => FootRight,
        _ => Vector2.Zero
    };

    public RigidPose2D WithTarget(RigidControl2D control, Vector2 target) => control switch
    {
        RigidControl2D.HandLeft => this with { HandLeft = target },
        RigidControl2D.HandRight => this with { HandRight = target },
        RigidControl2D.FootLeft => this with { FootLeft = target },
        RigidControl2D.FootRight => this with { FootRight = target },
        _ => this
    };
}

internal enum RigidControl2D
{
    HandLeft,
    HandRight,
    FootLeft,
    FootRight
}

internal readonly record struct PoseKeyframe2D(float Time, RigidPose2D Pose);

internal sealed class PoseClip2D
{
    private readonly PoseKeyframe2D[] _frames;

    public PoseClip2D(string name, bool loops, params PoseKeyframe2D[] frames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (frames.Length == 0)
            throw new ArgumentException("A pose clip needs at least one keyframe.", nameof(frames));
        if (frames[0].Time != 0f || frames.Any(frame => !float.IsFinite(frame.Time) || frame.Time < 0f))
            throw new ArgumentException("Pose times must be finite, non-negative, and begin at zero.", nameof(frames));
        for (var index = 1; index < frames.Length; index++)
        {
            if (frames[index].Time <= frames[index - 1].Time)
                throw new ArgumentException("Pose times must be strictly increasing.", nameof(frames));
        }

        Name = name;
        Loops = loops;
        _frames = frames;
    }

    public string Name { get; }
    public bool Loops { get; }
    public float Duration => _frames[^1].Time;
    public IReadOnlyList<PoseKeyframe2D> Frames => _frames;

    public RigidPose2D Sample(float time)
    {
        if (_frames.Length == 1 || Duration <= 0f)
            return _frames[0].Pose;
        if (Loops)
            time = time - MathF.Floor(time / Duration) * Duration;
        else
            time = Math.Clamp(time, 0f, Duration);

        for (var index = 1; index < _frames.Length; index++)
        {
            var after = _frames[index];
            if (time > after.Time)
                continue;
            var before = _frames[index - 1];
            var amount = Interpolation.SmoothStep((time - before.Time) / (after.Time - before.Time));
            return RigidPose2D.Lerp(before.Pose, after.Pose, amount);
        }

        return _frames[^1].Pose;
    }
}
