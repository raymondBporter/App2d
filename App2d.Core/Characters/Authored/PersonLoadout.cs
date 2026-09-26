namespace App2d.Core.Characters;

/// <summary>What the Person carries: nothing, the sword on its back, or the sword on its back and a pistol in hand.</summary>
public enum PersonGear { None, Sword, Gun }

/// <summary>
/// The player move set's prop rules, shared by the game and the move review. The scabbard is always worn with the sword.
/// The sword rides the scabbard, except between a clip's "sword-draw" and "sword-sheathe" markers (from the start of a clip
/// that only sheathes), when it is in the hand. The latest "view-back" / "view-profile" marker at or before the sample
/// picks the back socket: seen from behind the sheath is worn across the back. The pistol is in hand whenever carried.
/// </summary>
public static class PersonLoadout
{
    public const string BackSocket = "back", BackViewSocket = "back-view", SwordSocket = "sword-hand", GunSocket = "gun-hand";
    public const string BackViewMarker = "view-back", ProfileViewMarker = "view-profile", DrawMarker = "sword-draw", SheatheMarker = "sword-sheathe";
    public const string Sword = "sword", Sheath = "sheath", Pistol = "pistol";
    /// <summary>The upper-body group: channels an arms-only overlay (a gun shot) owns over any legs. The Person model carries it as its "upper" control group.</summary>
    public static readonly IReadOnlySet<string> UpperBody = new HashSet<string>(StringComparer.Ordinal) { "chest", "head", "left-shoulder", "right-shoulder", "left-arm", "right-arm" };

    public static bool SeenFromBehind(MotionClip clip, float seconds) =>
        clip.Markers.Where(m => m.Id is BackViewMarker or ProfileViewMarker && m.Time <= seconds + 1e-4f).MaxBy(m => m.Time)?.Id == BackViewMarker;

    /// <summary>Whether this clip holds the sword in hand at this time.</summary>
    public static bool SwordInHand(MotionClip clip, float seconds)
    {
        var draw = clip.Markers.FirstOrDefault(m => m.Id == DrawMarker)?.Time; var sheathe = clip.Markers.FirstOrDefault(m => m.Id == SheatheMarker)?.Time;
        return (draw is { } d ? seconds >= d : sheathe is not null) && (sheathe is not { } s || seconds < s);
    }

    /// <summary>The props worn at this moment of <paramref name="clip"/>, each with the model socket it sits on.</summary>
    public static IEnumerable<(string Prop, string Socket)> Worn(MotionClip clip, float seconds, PersonGear gear)
    {
        if (gear == PersonGear.None) yield break;
        var back = SeenFromBehind(clip, seconds) ? BackViewSocket : BackSocket;
        yield return (Sheath, back);
        yield return (Sword, gear == PersonGear.Sword && SwordInHand(clip, seconds) ? SwordSocket : back);
        if (gear == PersonGear.Gun) yield return (Pistol, GunSocket);
    }
}
