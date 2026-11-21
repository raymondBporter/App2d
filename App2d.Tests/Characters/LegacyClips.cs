using App2d.Core.Animation;
using App2d.Core.Characters.Authored;
using App2d.Core.Kinematics;
using App2d.Core.Mathematics.Progress;

namespace App2d.Tests.Characters;

/// <summary>
/// Turns an authored <see cref="MotionClip"/> into a <see cref="SkeletonClip2D"/> for the standard build of its model, so the
/// two can be compared until the authored clips are retired. Values are scaled into the model's own units here: the authored
/// keys are in the units of the clip's reference measures, which differ from the rig's.
/// </summary>
internal static class LegacyClips
{
    /// <summary>What a conversion left behind because it does not move the skeleton.</summary>
    public sealed record Skipped(int PlayTracks);

    public static SkeletonClip2D Convert(MotionClip clip, ResolvedModel model, Skeleton2D skeleton, out Skipped skipped)
    {
        float Ratio(string scale) => scale == CharacterModel.Unit ? 1 : model.Measure(scale) / clip.Reference[scale];
        int Limb(string chain) => skeleton.Limbs.IndexOf(skeleton.Limbs.Single(l => skeleton.Joints[l.End].Name == model.Chains[chain].End));
        ProgressMapf Progress(ClipKey key)
        {
            Assert.True(key.Curve is null && key.CurveY is null && key.CurveZ is null, $"{clip.Id}: bezier curve fields are not converted.");
            return Easing<float>.FromCurve(key.Ease, key.EaseDirection);
        }
        Track<Vector2f>? Moves(List<ClipKey> keys, float ratio) => keys.Count == 0 ? null : new(Interpolation.Default<Vector2f, float>(),
            [.. keys.Select(k => new Key<Vector2f>(k.Time, k.Value.XY * ratio, k.Interpolator is { } authored
                ? authored.InCoordinates<Vector2f>(static v => new Vector3f(v, 0), static v => v.XY)
                : Interpolation.Default<Vector2f, float>().WithProgress(Progress(k))))]);
        Track<float>? Turns(List<ClipKey> keys) => keys.Count == 0 ? null : new(Interpolation.Linear<float>(),
            [.. keys.Select(k => new Key<float>(k.Time, k.Angle, k.AngleInterpolator ?? Interpolation.Linear<float>().WithProgress(Progress(k))))]);
        // A translate key's third number is a change of layer in the drawing order; it is never scaled.
        Track<float>? Layers(List<ClipKey> keys) => keys.All(k => k.Value.Z == 0) ? null : new(Interpolation.Linear<float>(),
            [.. keys.Select(k => new Key<float>(k.Time, k.Value.Z, k.Interpolator is { } authored
                ? authored.InCoordinates<float>(static v => new Vector3f(0, 0, v), static v => v.Z)
                : Interpolation.Linear<float>().WithProgress(Progress(k))))]);
        Track<int>? Bends(List<ClipKey> keys, int rest)
        {
            var set = keys.Where(k => k.Bend is not null).Select(k => new Key<int>(k.Time, k.Bend!.Value)).ToList();
            if (set.Count == 0) return null;
            // Before the first key that sets a side, the limb's own bend applies.
            if (set[0].Time > 0) set.Insert(0, new(0, rest));
            return Track.Held<int>([.. set]);
        }

        var joints = new Dictionary<int, JointTrack2D>();
        var limbs = new List<LimbTrack2D>();
        var mounts = new List<MountTrack2D>();
        foreach (var track in clip.Tracks)
        {
            Assert.False(track.SetupBeforeFirst, $"{clip.Id}: setup-before-first tracks are not converted.");
            switch (track.Kind)
            {
                case MotionClip.TranslateKind:
                    {
                        var joint = skeleton.IndexOf(track.Target);
                        joints[joint] = joints.GetValueOrDefault(joint, new(joint)) with { Move = Moves(track.Keys, Ratio(track.Scale ?? model.Controls[track.Target].Scale)), Layer = Layers(track.Keys) };
                        break;
                    }
                case MotionClip.RotateKind:
                    {
                        var joint = skeleton.IndexOf(track.Target);
                        joints[joint] = joints.GetValueOrDefault(joint, new(joint)) with { Turn = Turns(track.Keys) };
                        break;
                    }
                case MotionClip.TargetKind:
                    {
                        var chain = model.Chains[track.Target];
                        Assert.True(track.Keys.All(k => k.Value.Z == 0), $"{clip.Id}: a limb target that keys a layer is not converted.");
                        limbs.Add(new(Limb(chain.Id), Moves(track.Keys, Ratio(track.Scale ?? chain.Scale)), Bends(track.Keys, chain.Bend)));
                        break;
                    }
                case MotionClip.OrientKind:
                    if (track.Keys.Count > 0)
                    {
                        mounts.Add(new(skeleton.Mounts.IndexOf(skeleton.Mounts.Single(m => m.Name == track.Target)), new(Interpolation.Default<Vector3f, float>(),
                            [.. track.Keys.Select(k => new Key<Vector3f>(k.Time, k.Value, k.Interpolator ?? Interpolation.Default<Vector3f, float>().WithProgress(Progress(k))))])));
                    }
                    break;
                default:
                    Assert.Fail($"{clip.Id}: track kind '{track.Kind}' is not converted.");
                    break;
            }
        }
        skipped = new(clip.Plays.Count);
        return new(clip.Id, clip.Duration, clip.Loop)
        {
            Joints = [.. joints.Values],
            Limbs = [.. limbs],
            Mounts = [.. mounts],
            Contacts = [.. clip.Contacts.Select(c => new LimbContact2D(Limb(c.Chain), c.Start, c.Finish, c.Target.XYZ.XY * Ratio(model.Chains[c.Chain].Scale)))],
            Markers = [.. clip.Markers.Select(m => new ClipMarker2D(m.Id, m.Time))],
            Travel = Moves(clip.Travel.Keys, Ratio(clip.Travel.Scale)),
        };
    }
}
