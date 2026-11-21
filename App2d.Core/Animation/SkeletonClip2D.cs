using App2d.Core.Kinematics;
using App2d.Core.Mathematics.Transforms;
using System.Collections.Immutable;

namespace App2d.Core.Animation;

/// <summary>
/// Keys for one joint, as changes from its rest: a move in its parent's axes, a turn in radians, and a change to its layer
/// in the drawing order, which brings a far arm in front of the body or sends a near one behind.
/// </summary>
public readonly record struct JointTrack2D(int Joint, Track<Vector2f>? Move = null, Track<float>? Turn = null, Track<float>? Layer = null);

/// <summary>
/// Keys for one limb, by its index in <see cref="Skeleton2D.Limbs"/>: where its end goes, as a move from where the end rests
/// in the limb's frame, and the side its middle joint folds to. A limb with no keys keeps its end at rest in its frame.
/// </summary>
public readonly record struct LimbTrack2D(int Limb, Track<Vector2f>? Target = null, Track<int>? Bend = null);

/// <summary>
/// Plants a limb's end on the ground from <see cref="Start"/> until <see cref="Finish"/>. <see cref="Target"/> is a move from
/// where the end rests, fixed in the ground at the start of the cycle, so the clip's travel carries the body past it.
/// </summary>
public readonly record struct LimbContact2D(int Limb, float Start, float Finish, Vector2f Target);

/// <summary>
/// One limb as a clip leaves it, before anything is solved: its target's move from rest in the limb's frame, the side it
/// folds to, and where a contact plants its end. <see cref="Release"/> is how far a layer has taken the limb away from that
/// plant: 0 holds it, 1 lets go.
/// </summary>
public readonly record struct LimbSample2D(Vector2f Move, int Bend, Vector2f? Planted = null, float Release = 0);

/// <summary>
/// Keys that turn one mount out of the skeleton's plane, by its index in <see cref="Skeleton2D.Mounts"/>: a twist about the
/// mount's X, a tilt about its Y and a turn about Z, in radians, applied in that order. This is how a swung sword shows its
/// flat or its edge. The pose itself stays 2D; only what is drawn on the mount is turned.
/// </summary>
public readonly record struct MountTrack2D(int Mount, Track<Vector3f> Turn);

/// <summary>The joints, limbs and mounts a layered clip takes over, such as the upper body over a walk.</summary>
public sealed record SkeletonMask2D(ImmutableArray<int> Joints, ImmutableArray<int> Limbs, ImmutableArray<int> Mounts)
{
    /// <summary>This mask for a skeleton that numbers things differently, as a <see cref="SkeletonGraft2D"/> does; what is gone there is left out.</summary>
    public SkeletonMask2D Renumbered(ImmutableArray<int> joints, ImmutableArray<int> limbs, ImmutableArray<int> mounts) =>
        new([.. Joints.Select(j => joints[j]).Where(j => j >= 0)], [.. Limbs.Select(l => limbs[l]).Where(l => l >= 0)], [.. Mounts.Select(m => mounts[m]).Where(m => m >= 0)]);
}

/// <summary>A named moment in a clip, such as a footstep or a strike.</summary>
public readonly record struct ClipMarker2D(string Name, float Time);

/// <summary>
/// An animation for one <see cref="Skeleton2D"/>: keys for joints, goals for limbs, and how far the character travels.
/// Times are seconds from the start of the clip. Sampling writes a pose and limb samples; one clip can then be laid over
/// another, and IK places the limbs last.
/// </summary>
public sealed class SkeletonClip2D(string name, float duration, bool loop = false)
{
    public string Name { get; } = name;
    public float Duration { get; } = duration;
    public bool Loop { get; } = loop;
    public ImmutableArray<JointTrack2D> Joints { get; init; } = [];
    public ImmutableArray<LimbTrack2D> Limbs { get; init; } = [];
    public ImmutableArray<MountTrack2D> Mounts { get; init; } = [];
    public ImmutableArray<LimbContact2D> Contacts { get; init; } = [];
    public ImmutableArray<ClipMarker2D> Markers { get; init; } = [];
    /// <summary>How far the character has moved along the ground. Null stays in place.</summary>
    public Track<Vector2f>? Travel { get; init; }
    /// <summary>
    /// A named look the clip asks for over time, such as a face's expression; each key holds until the next. An empty name
    /// asks for no face at all. Null when the clip asks for nothing.
    /// </summary>
    public Track<string>? Expression { get; init; }

    /// <summary>The time of the first marker with this name, or null.</summary>
    public float? Marker(string name)
    {
        foreach (var marker in Markers)
        {
            if (marker.Name == name) return marker.Time;
        }
        return null;
    }

    /// <summary>How far the clip travels over one full play: the stride a walk covers per cycle.</summary>
    public Vector2f Stride(Skeleton2D skeleton) => TravelAt(skeleton, Duration) - TravelAt(skeleton, 0);

    /// <summary>How far this skeleton has travelled at <paramref name="time"/>.</summary>
    /// <summary>
    /// This clip for a skeleton that numbers things differently, as a <see cref="SkeletonGraft2D"/> does. A track or contact
    /// whose joint, limb or mount is gone there is left out. A half that only rides along gives up its travel and contacts.
    /// </summary>
    public SkeletonClip2D Renumbered(ImmutableArray<int> joints, ImmutableArray<int> limbs, ImmutableArray<int> mounts, bool rides = false) => new(Name, Duration, Loop)
    {
        Joints = [.. Joints.Where(t => joints[t.Joint] >= 0).Select(t => t with { Joint = joints[t.Joint] })],
        Limbs = [.. Limbs.Where(t => limbs[t.Limb] >= 0).Select(t => t with { Limb = limbs[t.Limb] })],
        Mounts = [.. Mounts.Where(t => mounts[t.Mount] >= 0).Select(t => t with { Mount = mounts[t.Mount] })],
        Contacts = rides ? [] : [.. Contacts.Where(c => limbs[c.Limb] >= 0).Select(c => c with { Limb = limbs[c.Limb] })],
        Markers = Markers,
        Travel = rides ? null : Travel,
        Expression = Expression,
    };

    public Vector2f TravelAt(Skeleton2D skeleton, float time) => (Travel?.Sample(time) ?? default) * skeleton.TravelScale;

    /// <summary>
    /// Writes the keyed pose and one sample per limb, in the order of <see cref="Skeleton2D.Limbs"/>. The pose is in place:
    /// travel is not added to it. Nothing is solved yet, so another clip can still be laid over this one.
    /// </summary>
    public void Sample(Skeleton2D skeleton, float time, Span<RigidTransform2D> local, Span<LimbSample2D> limbs, bool reversed = false)
    {
        // Reversed, every sideways move and every turn goes the other way, while the rest pose stays as it is.
        var side = new Vector2f(reversed ? -1 : 1, 1);
        for (var i = 0; i < skeleton.Count; i++) local[i] = skeleton.Joints[i].Rest;
        foreach (var track in Joints)
        {
            var rest = local[track.Joint];
            var move = (track.Move?.Sample(time) ?? default) * skeleton.Joints[track.Joint].MoveScale * side;
            local[track.Joint] = new(rest.Translation + move, rest.Rotation + (track.Turn?.Sample(time) ?? 0) * side.X);
        }
        for (var i = 0; i < skeleton.Limbs.Length; i++) limbs[i] = new(default, skeleton.Limbs[i].Bend);
        foreach (var track in Limbs)
        {
            var limb = skeleton.Limbs[track.Limb];
            limbs[track.Limb] = new((track.Target?.Sample(time) ?? default) * limb.TargetScale * side, track.Bend?.Sample(time) ?? limb.Bend);
        }
        foreach (var contact in Contacts)
        {
            if (time < contact.Start || time >= contact.Finish && !(time == Duration && contact.Finish == Duration)) continue;
            // The ground does not travel with the character, so in place the planted point slides back by the travel so far.
            var limb = skeleton.Limbs[contact.Limb];
            var planted = skeleton.RestWorld[limb.End].Translation + contact.Target * limb.TargetScale * side + TravelAt(skeleton, 0) - TravelAt(skeleton, time);
            limbs[contact.Limb] = limbs[contact.Limb] with { Planted = planted };
        }
    }

    /// <summary>
    /// Lays one sampled clip over another. The joints and limbs in <paramref name="mask"/> move toward the clip on top by
    /// <paramref name="weight"/> (0 keeps what is underneath, 1 replaces it); a limb takes the top clip's bend from half way. A
    /// foot the clip underneath has planted lets go as the top clip takes its leg over.
    /// </summary>
    public static void Overlay(Span<RigidTransform2D> local, Span<LimbSample2D> limbs, ReadOnlySpan<RigidTransform2D> overLocal,
        ReadOnlySpan<LimbSample2D> overLimbs, SkeletonMask2D mask, float weight = 1)
    {
        weight = MathF.Min(weight, 1);
        foreach (var joint in mask.Joints)
        {
            local[joint] = weight >= 1 ? overLocal[joint] : RigidTransform2D.Lerp(local[joint], overLocal[joint], weight);
        }
        foreach (var limb in mask.Limbs)
        {
            var under = limbs[limb];
            var over = overLimbs[limb];
            limbs[limb] = new(weight >= 1 ? over.Move : Vector2f.Lerp(under.Move, over.Move, weight), weight >= .5f ? over.Bend : under.Bend,
                under.Planted, MathF.Max(under.Release, weight));
        }
    }

    /// <summary>
    /// Writes how far each mount is turned out of the plane, in the order of <see cref="Skeleton2D.Mounts"/>. A mount with no
    /// keys lies flat.
    /// </summary>
    public void SampleMounts(float time, Span<Vector3f> turns, bool reversed = false)
    {
        turns.Clear();
        foreach (var track in Mounts)
        {
            var turn = track.Turn.Sample(time);
            turns[track.Mount] = reversed ? new(turn.X, -turn.Y, -turn.Z) : turn;
        }
    }

    /// <summary>Lays one clip's mount turns over another's: the mounts in <paramref name="mask"/> move toward the clip on top by <paramref name="weight"/>.</summary>
    public static void OverlayMounts(Span<Vector3f> turns, ReadOnlySpan<Vector3f> overTurns, SkeletonMask2D mask, float weight = 1)
    {
        weight = MathF.Min(weight, 1);
        foreach (var mount in mask.Mounts) turns[mount] = Vector3f.Lerp(turns[mount], overTurns[mount], weight);
    }

    /// <summary>
    /// Writes the change this clip keys to each joint's layer in the drawing order, zero where it keys none. Give the result to
    /// <see cref="Skeleton2D.ToLayers"/> for each joint's layer.
    /// </summary>
    public void SampleLayers(float time, Span<float> keyed)
    {
        keyed.Clear();
        foreach (var track in Joints)
        {
            if (track.Layer is { } layer) keyed[track.Joint] = layer.Sample(time);
        }
    }

    /// <summary>Lays one clip's layer changes over another's: the joints in <paramref name="mask"/> move toward the clip on top by <paramref name="weight"/>.</summary>
    public static void OverlayLayers(Span<float> keyed, ReadOnlySpan<float> overKeyed, SkeletonMask2D mask, float weight = 1)
    {
        weight = MathF.Min(weight, 1);
        foreach (var joint in mask.Joints) keyed[joint] = float.Lerp(keyed[joint], overKeyed[joint], weight);
    }

    /// <summary>Turns limb samples into goals in the skeleton's own space, against the finished pose of the joints that carry them.</summary>
    public static void Goals(Skeleton2D skeleton, ReadOnlySpan<RigidTransform2D> local, ReadOnlySpan<LimbSample2D> limbs, Span<LimbGoal2D> goals)
    {
        for (var i = 0; i < skeleton.Limbs.Length; i++)
        {
            var goal = Goal(skeleton, local, skeleton.Limbs[i], limbs[i].Move);
            if (limbs[i].Planted is { } planted && limbs[i].Release < 1)
            {
                goal = goal with { Translation = Vector2f.Lerp(planted, goal.Translation, limbs[i].Release) };
            }
            goals[i] = new(goal, limbs[i].Bend);
        }
    }

    /// <summary>Samples the clip and places the limbs: the finished pose at <paramref name="time"/>, in place.</summary>
    public void Pose(Skeleton2D skeleton, float time, Span<RigidTransform2D> local)
    {
        Span<LimbSample2D> limbs = stackalloc LimbSample2D[skeleton.Limbs.Length];
        Sample(skeleton, time, local, limbs);
        Place(skeleton, local, limbs);
    }

    /// <summary>Finishes a sampled, possibly layered, pose: works out each limb's goal and places it with IK.</summary>
    public static void Place(Skeleton2D skeleton, Span<RigidTransform2D> local, ReadOnlySpan<LimbSample2D> limbs)
    {
        Span<LimbGoal2D> goals = stackalloc LimbGoal2D[skeleton.Limbs.Length];
        Goals(skeleton, local, limbs, goals);
        SkeletonIk2D.Place(skeleton, local, goals);
    }

    /// <summary>Where a limb's end rests in its frame, moved by <paramref name="move"/>, carried to where that frame is in the pose.</summary>
    private static RigidTransform2D Goal(Skeleton2D skeleton, ReadOnlySpan<RigidTransform2D> local, Limb2D limb, Vector2f move)
    {
        var rest = skeleton.RestWorld[limb.End];
        // The end holds its resting angle in the skeleton's space, plus whatever its own keys turned it by.
        var turn = rest.Rotation + local[limb.End].Rotation - skeleton.Joints[limb.End].Rest.Rotation;
        if (limb.Frame == Skeleton2D.NoParent) return new(rest.Translation + move, turn);
        var inFrame = skeleton.RestWorld[limb.Frame].Inverse().TransformPoint(rest.Translation) + move;
        return new(skeleton.WorldOf(local, limb.Frame).TransformPoint(inFrame), turn);
    }
}
