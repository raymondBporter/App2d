using App2d.Core.Characters;
using System.Numerics;

namespace App2d.CharacterStudio.PlayerMoves;

/// <summary>
/// Swing experiments for the player's sword, authored arc first: each key says where the hand sits on a circle round the
/// sword shoulder (so the body's lean carries the arc) and how the wrist holds the blade across the forearm; the arm IK
/// follows from that.
/// The blade is never laid along the forearm: a fist holds it across, about 90 degrees, and the wrist tilts it only so
/// far. Timing is in 60 fps frames. Art tooling only: nothing here is written into the game's assets yet.
/// </summary>
internal static class SwingLab
{
    public sealed record Variant(string Id, string Title, string Note, MotionClip Clip);

    private const float F = 1 / 60f, Duration = .35f;
    /// <summary>The hand's usual distance from the shoulder: the arm (0.645 long) nearly straight, so the forearm follows the swing.</summary>
    private const float Reach = .6f;

    /// <summary>
    /// A key pose on the arc. <paramref name="Angle"/> is the hand's direction from the shoulder (0 forward, pi/2 up);
    /// <paramref name="Wrist"/> is the blade's angle from the forearm, counter-clockwise (see <see cref="PoseKey.Grip"/>).
    /// </summary>
    private readonly record struct Arc(float Angle, float Wrist, float Radius, float Chest, float HipsX, float HipsY, float Head, float OffX, float OffY)
    {
        public Vector2 Hand => new Vector2(MathF.Cos(Angle), MathF.Sin(Angle)) * Radius;
        public Arc With(float angle, float wrist) => this with { Angle = angle, Wrist = wrist };
    }

    // Guard: hand forward and low, blade up and forward out of the fist.
    private static readonly Arc Guard = new(-.8f, 1.35f, .5f, -.12f, .03f, -.07f, .08f, -.08f, -.54f);
    // Coiled: hand behind the shoulder at about its height, the blade hanging back behind it; chest leaning back.
    private static readonly Arc Coil = new(2.45f, 1.2f, Reach, .12f, -.03f, -.05f, .04f, .18f, -.44f);
    // Overhead, mid-swing: the arm up, the blade still trailing back.
    private static readonly Arc Over = new(1.25f, 1.25f, Reach, -.05f, .01f, -.07f, .08f, .05f, -.46f);
    // Extended: hand forward and low, the blade forward out of the fist; chest pitched over the front foot.
    private static readonly Arc Strike = new(-.72f, .95f, Reach + .03f, -.32f, .06f, -.09f, .18f, -.22f, -.46f);
    /// <summary>The follow-through the downward slash settles into and holds.</summary>
    private static readonly Arc Held = Strike.With(Strike.Angle - .08f, .9f) with { Chest = -.3f };

    private static Arc Mix(Arc a, Arc b, float u) => new(
        float.Lerp(a.Angle, b.Angle, u), float.Lerp(a.Wrist, b.Wrist, u), float.Lerp(a.Radius, b.Radius, u), float.Lerp(a.Chest, b.Chest, u),
        float.Lerp(a.HipsX, b.HipsX, u), float.Lerp(a.HipsY, b.HipsY, u), float.Lerp(a.Head, b.Head, u), float.Lerp(a.OffX, b.OffX, u), float.Lerp(a.OffY, b.OffY, u));

    private static MoveBuilder Pose(this MoveBuilder b, float time, Arc p, string ease = ClipEase.Smooth) =>
        b.Key(time, k => k.Hips(p.HipsX, p.HipsY).Chest(p.Chest).Head(p.Head).RightHand(p.Hand.X, p.Hand.Y).LeftHand(p.OffX, p.OffY).Grip(p.Wrist), ease);

    private static MoveBuilder New(ResolvedModel m, string id, string name, float duration = Duration) =>
        new MoveBuilder(m, id, name, duration, false).Plant("left-leg", 0, duration, -.16f).Plant("right-leg", 0, duration, .16f).Marker(PersonLoadout.DrawMarker, 0);

    /// <summary>
    /// The downward slash: one frame to coil, <paramref name="travel"/> frames over the top and down, contact, a short
    /// overshoot settling into a held follow-through, then back to guard (or held, for a combo to continue from).
    /// <paramref name="whip"/> keeps the blade trailing hard over the top and snaps the wrist nearly flat on the contact
    /// frame, the one "broken" frame, back in range on the next.
    /// </summary>
    private static MoveBuilder Slash(MoveBuilder b, float start, int travel, bool whip, float? returnAt)
    {
        var contact = start + (1 + travel) * F;
        b.Pose(start, Guard, ClipEase.Linear).Pose(start + F, Coil, ClipEase.Linear);
        for (var i = 1; i < travel; i++)
        {
            // The arm reaches the top early and the blade catches up late, so the fastest step lands on contact.
            var over = travel == 2 ? Over : i == 1 ? Mix(Coil, Over, .6f) : Over.With(.55f, Over.Wrist);
            b.Pose(start + (1 + i) * F, whip ? over with { Wrist = 1.7f } : over, ClipEase.Linear);
        }
        b.Pose(contact, Strike.With(Strike.Angle, whip ? .3f : Strike.Wrist), ClipEase.Linear)
            .Pose(contact + 2 * F, Strike.With(Strike.Angle - .22f, .8f) with { Chest = -.36f, Head = .2f })
            .Pose(contact + 6 * F, Strike.With(Strike.Angle - .12f, .88f));
        if (returnAt is { } back) b.Pose(back - .08f, Held).Pose(back, Guard);
        return b.Marker(PersonLoadout.SwooshMarker, start + F).Marker(PersonLoadout.SwooshEndMarker, contact + F).Marker("strike", contact);
    }

    /// <summary>The combo's second swing from the held follow-through: drop under and back, then a rising cut forward and up.</summary>
    private static MoveBuilder Rising(MoveBuilder b, float start, float returnAt)
    {
        var under = new Arc(-2.3f, 1.35f, Reach, .06f, -.02f, -.1f, .04f, .16f, -.46f);
        // Finish forward and up with the blade upright in front of the face, not laid back over the head.
        var high = new Arc(.6f, 1.1f, Reach + .02f, -.2f, .06f, -.06f, .12f, -.2f, -.46f);
        var contact = start + 3 * F;
        b.Pose(start, Held, ClipEase.Linear).Pose(start + F, under, ClipEase.Linear)
            .Pose(start + 2 * F, Mix(under, high, .4f) with { Wrist = 1.2f }, ClipEase.Linear)
            .Pose(contact, high, ClipEase.Linear)
            .Pose(contact + 2 * F, high.With(high.Angle + .18f, 1.02f) with { Chest = -.24f })
            .Pose(contact + 6 * F, high.With(high.Angle + .1f, 1.05f))
            .Pose(returnAt - .08f, high.With(high.Angle + .08f, 1.05f))
            .Pose(returnAt, Guard);
        return b.Marker(PersonLoadout.SwooshMarker + "-2", start + F).Marker(PersonLoadout.SwooshMarker + "-2-end", contact + F).Marker("strike-2", contact);
    }

    public static IEnumerable<Variant> Variants(ResolvedModel m, MotionClip today)
    {
        var marked = MotionClip.FromJson(today.ToJson());
        marked.Markers = [.. marked.Markers, new() { Id = PersonLoadout.SwooshMarker, Time = .09f }, new() { Id = PersonLoadout.SwooshEndMarker, Time = .21f }];
        marked.Markers.Sort((a, b) => a.Time.CompareTo(b.Time));
        yield return new("today", "Today's follow-up slash", "The current clip, with the swoosh on its fast part (0.09–0.21 s). Damage starts at 0.10 s. The blade lies along the forearm.", marked);
        yield return new("snap", "Arc slash: snap", "One frame to coil, two frames over the top and down, contact on frame 3 (50 ms), a short overshoot, then held. The blade stays across the fist.",
            Slash(New(m, "lab-snap", "Snap"), 0, 2, false, Duration).Build());
        yield return new("whip", "Arc slash: snap + wrist whip", "As Snap, but the blade trails harder over the top and the wrist snaps almost flat on the contact frame, the one broken frame.",
            Slash(New(m, "lab-whip", "Whip"), 0, 2, true, Duration).Build());
        yield return new("three", "Arc slash: three-frame travel", "As Snap with one more frame across the arc: contact on frame 4 (67 ms). Is Snap too fast to read?",
            Slash(New(m, "lab-three", "Three"), 0, 3, false, Duration).Build());
        yield return new("combo", "Combo: snap, then rising follow-up", "The second swing comes out of the first's held pose at 0.25 s and uses the follow-up swoosh: same family, split nearer the tip.",
            Rising(Slash(New(m, "lab-combo", "Combo", .6f), 0, 2, false, null), .25f, .6f).Build());
    }
}
