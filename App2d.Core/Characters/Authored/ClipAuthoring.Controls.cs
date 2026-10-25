using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Characters.Authored;

public static partial class ClipAuthoring
{
    /// <summary>The channels edited together when posing or selecting a control's keys.</summary>
    public static IReadOnlyCollection<Channel> ChannelsForControl(ResolvedModel model, string control)
    {
        if (Nested(model, control) is { } nested)
        {
            // A nested control's channels are the part rig's, namespaced; a wearer keys no IK target or socket inside a part.
            var prefix = nested.Attachment.Id + PartAsset.Namespace;
            return [.. ChannelsForControl(nested.Attachment.Rig!, nested.Target).Where(c => c.Kind != MotionClip.TargetKind).Select(c => c with { Target = prefix + c.Target })];
        }

        if (ChannelFor(model, control) is not { } channel)
        {
            return [];
        }

        if (channel.Kind == MotionClip.FitKind)
        {
            return [channel, new(MotionClip.ShowKind, control)];
        }

        if (channel.Kind != MotionClip.TranslateKind)
        {
            return [channel];
        }

        if (model.Chains.Values.Any(c => c.Solver == ModelChain.BoneSolver && c.Root == control))
        {
            return [channel, new(MotionClip.ScaleKind, control)];
        }

        // A canonical node has a uniform pose scale; shear is the legacy affine adapter's artwork-fit channel; a bone node
        // with declared joint limits also offers the normalized bend (P4b), which owns the rotation in a clip that keys it.
        var spec = model.Controls[control];
        return spec.Node == RigNodeKinds.Affine
            ? [channel, new(MotionClip.RotateKind, control), new(MotionClip.ScaleKind, control), new(MotionClip.ShearKind, control)]
            : spec.Limit is not null
            ? [channel, new(MotionClip.RotateKind, control), new(MotionClip.ScaleKind, control), new(MotionClip.BendKind, control)]
            : [channel, new(MotionClip.RotateKind, control), new(MotionClip.ScaleKind, control)];
    }

    /// <summary>Keys one vector component while preserving the other sampled components and existing easing.</summary>
    public static void SetComponentKey(MotionClip clip, Channel channel, float time, int component, float value)
    {
        ArgGuard.InRange(component, 0, 2);

        if (channel.Kind == MotionClip.RotateKind)
        {
            throw new ArgumentException("Rotation keys use a scalar angle.", nameof(channel));
        }

        var delta = Value(clip, channel, time).Value;
        delta[component] = value;
        SetKey(clip, channel, time, delta);
    }

    /// <summary>Recognizes a straight, linear travel cycle without flattening authored curves or depth motion.</summary>
    public static bool TryGetSteadyTravel(MotionClip clip, out float distance)
    {
        var keys = clip.Travel.Keys;
        distance = keys.Count == 2 ? keys[1].Value.X : 0;
        return keys.Count == 0 || keys.Count == 2 && keys[0] is { Time: 0, Value.X: 0, Value.Y: 0, Value.Z: 0, Ease: ClipEase.Linear, Curve: null, CurveY: null, CurveZ: null }
            && MathF.Abs(keys[1].Time - clip.Duration) < SameTime && keys[1].Value.Y == 0 && keys[1].Value.Z == 0;
    }

    /// <summary>Sets the distance of a steady travel cycle; rejects a curve that the simple control cannot represent.</summary>
    public static void SetSteadyTravel(MotionClip clip, float distance)
    {
        ArgGuard.Finite(distance);

        if (!TryGetSteadyTravel(clip, out _))
        {
            throw new InvalidOperationException("This travel curve is not a steady cycle.");
        }

        clip.Travel.Keys = distance == 0 ? [] : [new() { Time = 0 }, new() { Time = clip.Duration, Value = new(distance, 0, 0) }];
    }

    /// <summary>Sets a contact's interval within the clip, keeping at least a hundredth of a second.</summary>
    public static void SetContactRange(MotionClip clip, int index, Vector2 range)
    {
        var contact = clip.Contacts[index];
        contact.Finish = Math.Clamp(range.Y, .01f, clip.Duration);
        contact.Start = Math.Clamp(range.X, 0, contact.Finish - .01f);
    }
}
