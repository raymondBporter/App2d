using System.Numerics;

namespace App2d.Core.Characters.Authored;

public static partial class ClipAuthoring
{
    /// <summary>The channels edited together when posing or selecting a control's keys.</summary>
    public static IReadOnlyCollection<Channel> ChannelsForControl(ResolvedModel model, string control)
    {
        if (ChannelFor(model, control) is not { } channel) return [];
        if (channel.Kind != MotionClip.TranslateKind) return [channel];
        if (model.Chains.Values.Any(c => c.Solver == ModelChain.BoneSolver && c.Root == control))
            return [channel, new(MotionClip.ScaleKind, control)];
        return [channel, new(MotionClip.RotateKind, control), new(MotionClip.ScaleKind, control), new(MotionClip.ShearKind, control)];
    }

    /// <summary>Keys one vector component while preserving the other sampled components and existing easing.</summary>
    public static void SetComponentKey(MotionClip clip, Channel channel, float time, int component, float value)
    {
        if (component is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(component));
        if (channel.Kind == MotionClip.RotateKind) throw new ArgumentException("Rotation keys use a scalar angle.", nameof(channel));
        var delta = Value(clip, channel, time).Value;
        delta[component] = value;
        SetKey(clip, channel, time, delta);
    }

    /// <summary>Recognizes a straight, linear travel cycle without flattening authored curves or depth motion.</summary>
    public static bool TryGetSteadyTravel(MotionClip clip, out float distance)
    {
        var keys = clip.Travel.Keys;
        distance = keys.Count == 2 ? keys[1].X : 0;
        return keys.Count == 0 || keys.Count == 2 && keys[0] is { Time: 0, X: 0, Y: 0, Z: 0, Ease: ClipEase.Linear, Curve: null, CurveY: null, CurveZ: null }
            && MathF.Abs(keys[1].Time - clip.Duration) < SameTime && keys[1].Y == 0 && keys[1].Z == 0;
    }

    /// <summary>Sets the distance of a steady travel cycle; rejects a curve that the simple control cannot represent.</summary>
    public static void SetSteadyTravel(MotionClip clip, float distance)
    {
        if (!float.IsFinite(distance)) throw new ArgumentOutOfRangeException(nameof(distance));
        if (!TryGetSteadyTravel(clip, out _)) throw new InvalidOperationException("This travel curve is not a steady cycle.");
        clip.Travel.Keys = distance == 0 ? [] : [new() { Time = 0 }, new() { Time = clip.Duration, X = distance }];
    }

    /// <summary>Sets a contact's interval within the clip, keeping at least a hundredth of a second.</summary>
    public static void SetContactRange(MotionClip clip, int index, Vector2 range)
    {
        var contact = clip.Contacts[index];
        contact.Finish = Math.Clamp(range.Y, .01f, clip.Duration);
        contact.Start = Math.Clamp(range.X, 0, contact.Finish - .01f);
    }
}
