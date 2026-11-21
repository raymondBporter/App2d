using App2d.Core.Animation;

namespace App2d.Core.Characters;

/// <summary>The marker names clips use to tell whoever draws a person what to show.</summary>
public static class ClipMarkers
{
    /// <summary>From this marker the character is seen from behind, until the next <see cref="ProfileView"/>.</summary>
    public const string BackView = "view-back";
    public const string ProfileView = "view-profile";
    /// <summary>The sword leaves the back for the hand here, and returns at <see cref="Sheathe"/>.</summary>
    public const string Draw = "sword-draw";
    public const string Sheathe = "sword-sheathe";
    /// <summary>
    /// The blade leaves a trail from "swoosh" to the next "swoosh-end", or the clip's end. A clip with several swings numbers
    /// the later ones: "swoosh-2" and "swoosh-2-end".
    /// </summary>
    public const string Swoosh = "swoosh";
    public const string SwooshEnd = "swoosh-end";

    private const float Slack = 1e-4f;

    /// <summary>The last of the named markers at or before a time, or null.</summary>
    private static string? Last(SkeletonClip2D clip, float seconds, Func<string, bool> named)
    {
        ClipMarker2D? last = null;
        foreach (var marker in clip.Markers)
        {
            if (named(marker.Name) && marker.Time <= seconds + Slack && (last is not { } found || marker.Time > found.Time)) last = marker;
        }
        return last?.Name;
    }

    public static bool SeenFromBehind(this SkeletonClip2D clip, float seconds) => Last(clip, seconds, name => name is BackView or ProfileView) == BackView;

    /// <summary>Whether the clip holds the sword in the hand at a time. A clip that only sheathes holds it from its start.</summary>
    public static bool SwordInHand(this SkeletonClip2D clip, float seconds)
    {
        var (draw, sheathe) = (clip.Marker(Draw), clip.Marker(Sheathe));
        return (draw is { } drawn ? seconds >= drawn : sheathe is not null) && (sheathe is not { } sheathed || seconds < sheathed);
    }

    private static bool IsSwoosh(string name) => name == Swoosh || name.StartsWith(Swoosh + "-", StringComparison.Ordinal);

    /// <summary>Whether the blade is leaving a trail at a time.</summary>
    public static bool Swooshing(this SkeletonClip2D clip, float seconds) => Last(clip, seconds, IsSwoosh) is { } last && !last.EndsWith("-end", StringComparison.Ordinal);
}
