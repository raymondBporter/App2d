using System.Numerics;

namespace App2d.Core.Characters.Authored;

/// <summary>What the Person carries: nothing, the sword on its back, or the sword on its back and a pistol in hand.</summary>
public enum PersonGear { None, Sword, Gun }

/// <summary>
/// The player move set's prop rules, shared by the game and the move review. There is no scabbard: the sword rides the
/// back socket, except between a clip's "sword-draw" and "sword-sheathe" markers (from the start of a clip
/// that only sheathes), when it is in the hand. The latest "view-back" / "view-profile" marker at or before the sample
/// picks the back socket: seen from behind the sword is worn across the back. The pistol is in hand whenever carried.
/// </summary>
public static class PersonLoadout
{
    public const string BackSocket = "back", BackViewSocket = "back-view", SwordSocket = "sword-hand", GunSocket = "gun-hand";
    public const string BackViewMarker = "view-back", ProfileViewMarker = "view-profile", DrawMarker = "sword-draw", SheatheMarker = "sword-sheathe";
    /// <summary>
    /// The blade leaves a swoosh from a "swoosh" marker to the next "swoosh-end" (or the clip's end). A clip holding several
    /// swings numbers the later ones: "swoosh-2" and "swoosh-2-end", and so on.
    /// </summary>
    public const string SwooshMarker = "swoosh", SwooshEndMarker = "swoosh-end";

    public const string Sword = "sword", Sheath = "sheath", Pistol = "pistol";
    /// <summary>The upper-body group: channels an arms-only overlay (a gun shot) owns over any legs. The Person model carries it as its "upper" control group.</summary>
    public static readonly IReadOnlySet<string> UpperBody = new HashSet<string>(StringComparer.Ordinal) { "chest", "head", "left-shoulder", "right-shoulder", "left-arm", "right-arm" };
    public static readonly IReadOnlySet<string> SwordUpperBody = new HashSet<string>(UpperBody, StringComparer.Ordinal) { SwordSocket };

    public static bool SeenFromBehind(MotionClip clip, float seconds) =>
        clip.Markers.Where(m => m.Id is BackViewMarker or ProfileViewMarker && m.Time <= seconds + 1e-4f).MaxBy(m => m.Time)?.Id == BackViewMarker;

    /// <summary>Whether this clip holds the sword in hand at this time.</summary>
    public static bool SwordInHand(MotionClip clip, float seconds)
    {
        var draw = clip.Markers.FirstOrDefault(m => m.Id == DrawMarker)?.Time; var sheathe = clip.Markers.FirstOrDefault(m => m.Id == SheatheMarker)?.Time;
        return (draw is { } d ? seconds >= d : sheathe is not null) && (sheathe is not { } s || seconds < s);
    }

    /// <summary>Whether the held blade is leaving a swoosh at this time of <paramref name="clip"/>.</summary>
    public static bool Swooshing(MotionClip clip, float seconds)
    {
        var last = clip.Markers.Where(m => IsSwoosh(m.Id) && m.Time <= seconds + 1e-4f).MaxBy(m => m.Time);
        return last is not null && OpensSwoosh(last.Id);
    }

    /// <summary>Which swing of the clip a swoosh at this time belongs to: 0 for the first, 1 for "swoosh-2", and so on.</summary>
    public static int SwooshIndex(MotionClip clip, float seconds) =>
        Math.Max(0, clip.Markers.Count(m => OpensSwoosh(m.Id) && m.Time <= seconds + 1e-4f) - 1);

    private static bool IsSwoosh(string id) => id == SwooshMarker || id.StartsWith(SwooshMarker + "-", StringComparison.Ordinal);
    private static bool OpensSwoosh(string id) => IsSwoosh(id) && !id.EndsWith("-end", StringComparison.Ordinal);

    /// <summary>The held sword's blade, guard to tip, in the pose's actor space (along the blade's own axis; mesh swords are centred on it in depth).</summary>
    public static (Vector3 Guard, Vector3 Tip) Blade(EvaluatedPose pose, PropAsset sword, ModelSocket socket)
    {
        var frame = new ActorPose(pose, Vector2.Zero, 1).Socket(socket);
        return (ActorPose.PropPoint(frame, sword, new(.07f, 0, 0)), ActorPose.PropPoint(frame, sword, new(sword.Tip.X, 0, 0)));
    }

    /// <summary>
    /// Whether a clip's first swoosh is a backhand: the blade turning round the body (about the vertical axis) the opposite
    /// way to a forehand, which crosses from the far side toward the camera. Read from the clip, finely sampled over the
    /// swoosh; false when it has none.
    /// </summary>
    public static bool Backhand(ResolvedModel model, MotionClip clip, PropAsset sword, ModelSocket socket)
    {
        if (clip.Markers.FirstOrDefault(m => m.Id == SwooshMarker) is not { } start) return false;
        var end = clip.Markers.FirstOrDefault(m => m.Id == SwooshEndMarker && m.Time > start.Time)?.Time ?? clip.Duration;
        var yaw = 0f; Vector3? last = null;
        for (var t = start.Time; t <= end + 1e-4f; t += 1 / 240f)
        {
            var (guard, tip) = Blade(PoseEvaluator.Sample(model, clip, t), sword, socket);
            var d = Vector3.Normalize(tip - guard);
            if (last is { } a) yaw += Vector3.Cross(a, d).Y;
            last = d;
        }
        return yaw < 0;
    }

    /// <summary>The props worn at this moment of <paramref name="clip"/>, each with the model socket it sits on.</summary>
    public static IEnumerable<(string Prop, string Socket)> Worn(MotionClip clip, float seconds, PersonGear gear)
    {
        if (gear == PersonGear.None) yield break;
        var back = SeenFromBehind(clip, seconds) ? BackViewSocket : BackSocket;
        yield return (Sword, gear == PersonGear.Sword && SwordInHand(clip, seconds) ? SwordSocket : back);
        if (gear == PersonGear.Gun) yield return (Pistol, GunSocket);
    }

    /// <summary>Player appearance plus the carried weapons. Hair stays on even when unarmed.</summary>
    public static IEnumerable<(string Prop, string Socket)> Dressed(MotionClip clip, float seconds, PersonGear gear, ResolvedEntity? wearer = null)
    {
        if (wearer is null)
        {
            yield return (SeenFromBehind(clip, seconds) ? PersonWardrobe.ShortHairBack : PersonWardrobe.ShortHair, PersonWardrobe.HeadSocket);
        }
        else
        {
            foreach (var item in wearer.Equipment.Where(e => e.Prop.Usage is "hair" or "clothing"))
                yield return (SeenFromBehind(clip, seconds) && item.Prop.BackView is { } back ? back : item.Prop.Id, item.Socket.Id);
        }

        foreach (var item in Worn(clip, seconds, gear)) yield return item;
    }
}
