using App2d.Core.Animation;
using App2d.Core.Kinematics;
using App2d.Core.Validation;

namespace App2d.Core.Characters;

/// <summary>
/// Keeps planted limbs where they landed while the character moves. A clip plants a limb for a stretch of time; the first
/// sample of that stretch records where that puts it in the world, and later samples hold it there. A limb the clip lets go
/// is forgotten. Turning round lets go of everything.
/// </summary>
public sealed class PlantedLimbs
{
    private readonly Dictionary<int, Vector2f> _anchors = [];
    private readonly HashSet<int> _held = [];
    private int _facing = 1;

    public void Clear() => _anchors.Clear();

    /// <summary>The ground moved, as a platform does: everything planted moves with it.</summary>
    public void Shift(Vector2f delta)
    {
        foreach (var limb in _anchors.Keys.ToList()) _anchors[limb] += delta;
    }

    /// <summary>Samples a clip in place at a position. With <paramref name="hold"/> false nothing is held and limbs follow the clip.</summary>
    public CharacterPose Sample(Skeleton2D skeleton, SkeletonClip2D clip, double seconds, bool repeat, Vector2f position, int facing, PoseOverlay? overlay = null, bool reversed = false, bool hold = true)
    {
        if (facing is not (1 or -1)) Ensure.ThrowOutOfRange(facing, "Facing must be -1 or 1.");
        if (facing != _facing || !hold)
        {
            _facing = facing;
            _anchors.Clear();
        }
        if (!hold) return CharacterPose.Sample(skeleton, clip, seconds, repeat, overlay, null, reversed).At(position, facing);

        _held.Clear();
        Vector2f Place(Vector2f point) => position + new Vector2f(point.X * facing, point.Y);
        var pose = CharacterPose.Sample(skeleton, clip, seconds, repeat, overlay, (limb, planted) =>
        {
            _held.Add(limb);
            if (_anchors.TryGetValue(limb, out var anchor)) return new((anchor.X - position.X) * facing, anchor.Y - position.Y);
            _anchors[limb] = Place(planted);
            return planted;
        }, reversed);
        foreach (var limb in _anchors.Keys.Where(l => !_held.Contains(l)).ToList()) _anchors.Remove(limb);
        return pose.At(position, facing);
    }
}
