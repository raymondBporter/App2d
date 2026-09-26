using App2d.Core.Characters;
using System.Numerics;

namespace App2d.CharacterStudio.PlayerMoves;

/// <summary>
/// Swing experiments for the player's sword, authored arc first: each key says where the hand sits on a circle round the
/// sword shoulder (so the body's lean carries the arc) and how the wrist holds the blade across the forearm; the arm IK
/// follows from that. The rules the user set: the blade is never laid along the forearm (a fist holds it across), swings
/// go all out with the arm locked straight at the end, and the forward hit draws from the back. Timing is in 60 fps
/// frames. Art tooling only: nothing here is written into the game's assets yet.
/// </summary>
internal static class SwingLab
{
    /// <summary>
    /// One lab card. <paramref name="Timeline"/> is what plays, clip by clip from each start time (lab swings are one clip
    /// holding the whole idle, swing, sheathe, idle cycle; today's is the game's own clips in a row). <paramref name="Clip"/>
    /// is the swing the checks measure; the card plays for <paramref name="Duration"/>.
    /// </summary>
    public sealed record Variant(string Id, string Title, string Note, MotionClip Clip, IReadOnlyList<(MotionClip Clip, float Start)> Timeline, float Duration);

    /// <summary>How long each card idles before the swing and after the sword is back, and how long a swing holds its follow-through.</summary>
    private const float Lead = .3f, Tail = .3f, HoldFor = .2f;

    private const float F = 1 / 60f, Duration = .35f;
    /// <summary>Stands for the arm locked straight: <see cref="Swing.Pose"/> puts the hand at the arm's full length.</summary>
    private const float Straight = 1;

    /// <summary>
    /// A key pose on the arc. <paramref name="Angle"/> is the hand's direction from the shoulder (0 forward, pi/2 up);
    /// <paramref name="Wrist"/> is the blade's angle from the forearm, counter-clockwise (see <see cref="PoseKey.Grip"/>).
    /// <paramref name="Tilt"/> swings the blade in depth, in degrees: positive brings the tip toward the camera.
    /// <paramref name="Yaw"/> turns the shoulders (see <see cref="PlayerMoves.Shoulders"/>): 60 is rest, lower drives the
    /// sword shoulder forward, higher winds it back.
    /// </summary>
    private readonly record struct Arc(float Angle, float Wrist, float Radius, float Chest, float HipsX, float HipsY, float Head, float OffX, float OffY, float Tilt = 0, float Yaw = 60)
    {
        public Vector2 Hand => new Vector2(MathF.Cos(Angle), MathF.Sin(Angle)) * Radius;
        public Arc With(float angle, float wrist, float tilt) => this with { Angle = angle, Wrist = wrist, Tilt = tilt };
    }


    // Over the top, just clear of the back: arm up and forward, blade trailing back, swung round to point almost at the
    // camera, so it reads short (a side swing seen side on).
    private static readonly Arc Over = new(1.1f, 1.3f, Straight, 0, .01f, -.07f, .08f, .05f, -.46f, 80, 85);
    // Contact: arm locked straight, forward and down; the blade forward out of the fist; chest pitched over the front foot
    // and the sword shoulder driven forward into the cut.
    private static readonly Arc Contact = new(-.8f, .9f, Straight, -.34f, .06f, -.1f, .18f, -.22f, -.46f, 0, 20);
    // Past contact the blade keeps going round and down, away from the camera, the knees dipping so the tip sweeps to the
    // bottom of the hit box, and settles there with the arm still straight.
    private static readonly Arc Past = Contact.With(-1.25f, .72f, -30) with { Chest = -.5f, Head = .22f, HipsY = -.17f, Yaw = 10 };
    private static readonly Arc Held = Contact.With(-1.2f, .75f, -20) with { Chest = -.46f, HipsY = -.15f, Yaw = 15 };

    /// <summary>
    /// A side-swing pose. <paramref name="Arm"/> and <paramref name="Blade"/> are yaws about the vertical axis in degrees:
    /// 0 points forward, +90 at the camera, -90 away from it, 180 back; the dips tilt them below horizontal. The sword
    /// shoulder is the far one, so a forehand starts back on the far side (about -150), crosses the front (0) and follows
    /// through toward the camera. <paramref name="Twist"/> rolls the blade about its own length in degrees: 0 shows the
    /// flat, 90 the edge, so a cutting blade reads thin.
    /// </summary>
    private readonly record struct Side(float Arm, float Blade, float Chest, float HipsX, float HipsY, float Head, float Yaw, float OffX, float OffY, float ArmDip = 10, float BladeDip = 5, float Twist = 0)
    {
        public static Vector3 Direction(float yaw, float dip)
        {
            var (sy, cy) = MathF.SinCos(yaw * MathF.PI / 180); var (sd, cd) = MathF.SinCos(dip * MathF.PI / 180);
            return new(cy * cd, -sd, -sy * cd);
        }
    }

    private static MoveBuilder Pose(this MoveBuilder b, float time, Arc p, string ease = ClipEase.Linear) =>
        b.Key(time, k => k.Hips(p.HipsX, p.HipsY).Chest(p.Chest).Head(p.Head).Shoulders(b.Model, p.Yaw).RightHand(p.Hand.X, p.Hand.Y).LeftHand(p.OffX, p.OffY).Grip(p.Wrist), ease);

    /// <summary>The right arm's full length, shoulder to elbow to hand.</summary>
    private static float ArmLength(ResolvedModel m) =>
        (Vector3.Distance(m.Rest["right-shoulder"], m.Rest["right-elbow"]) + Vector3.Distance(m.Rest["right-elbow"], m.Rest["right-hand"]));

    /// <summary>A clip under construction with the blade's depth tilt kept beside it; the tilt becomes orientation keys after the build.</summary>
    private sealed class Swing(ResolvedModel m, string id, string name, float duration)
    {
        public MoveBuilder Builder { get; } = new MoveBuilder(m, id, name, duration, false).Plant("left-leg", 0, duration, -.16f).Plant("right-leg", 0, duration, .16f);
        private readonly List<(float Time, float Twist, float Tilt)> _orient = [];
        public bool Depth { get; init; } = true;
        private readonly float _arm = ArmLength(m);
        private float _lastBlade = float.NaN;

        /// <summary>
        /// A side-swing key: the arm and blade given as 3D directions (see <see cref="Side"/>). The hand goes where the
        /// straight arm projects; the blade becomes its in-plane angle (unwrapped against the last one, so it never spins
        /// the long way between keys) plus a depth tilt.
        /// </summary>
        /// <summary>Continues side keys from a blade angle keyed by other means, so the first one turns the short way from it.</summary>
        public Swing SeedBlade(float angle) { _lastBlade = angle; return this; }

        public Swing SidePose(float time, Side p, string ease = ClipEase.Linear)
        {
            var arm = Side.Direction(p.Arm, p.ArmDip) * _arm; var blade = Side.Direction(p.Blade, p.BladeDip);
            var angle = MathF.Atan2(blade.Y, blade.X);
            if (!float.IsNaN(_lastBlade)) angle = _lastBlade + MathF.IEEERemainder(angle - _lastBlade, MathF.Tau);
            _lastBlade = angle;
            var tilt = MathF.Asin(Math.Clamp(-blade.Z, -1, 1)) * 180 / MathF.PI;
            Builder.Key(time, k => k.Hips(p.HipsX, p.HipsY).Chest(p.Chest).Head(p.Head).Shoulders(Builder.Model, p.Yaw).RightHand(arm.X, arm.Y).LeftHand(p.OffX, p.OffY).Blade(angle), ease);
            _orient.Add((time, p.Twist, tilt)); return this;
        }

        /// <summary>A depth tilt at a key the swing poses by other means (the draw).</summary>
        public Swing Tilt(float time, float tilt) { _orient.Add((time, 0, Depth ? tilt : 0)); return this; }
        public Swing Pose(float time, Arc p, string ease = ClipEase.Linear)
        {
            Builder.Pose(time, p.Radius >= Straight ? p with { Radius = _arm } : p, ease); _orient.Add((time, 0, Depth ? p.Tilt : 0)); return this;
        }
        public MotionClip Build()
        {
            var clip = Builder.Build();
            foreach (var (time, twist, tilt) in _orient)
                ClipAuthoring.SetKey(clip, new(MotionClip.OrientKind, PersonLoadout.SwordSocket), time, new Vector3(twist, tilt, 0) * (MathF.PI / 180));
            clip.Validate(Builder.Model);
            return clip;
        }
    }

    /// <summary>The idle stance with the sword on the back (<paramref name="blade"/> is the right shoulder's sheathed turn).</summary>
    private static void Idle(Swing s, float time, float blade) =>
        s.Builder.Key(time, k => k.Hips(-.01f, -.04f).Chest(-.03f).Head(.02f).Shoulders(s.Builder.Model, 60).RightHand(.08f, -.585f).LeftHand(.05f, -.6f).Blade(blade), ClipEase.Smooth);

    /// <summary>
    /// The forward hit from the back, where the sword rides whenever it isn't swinging. At <paramref name="start"/> the hand
    /// is on the hilt; a frame later it pulls the sword up out of the back, the fist up behind the head (clear of it, so the
    /// draw reads) and the blade still hanging down the back (the one frame the blade lies along the forearm, as a real draw
    /// from the back does); then <paramref name="travel"/> frames round over the top (one skips from the pull straight to
    /// contact), contact with the arm straight, and a short overshoot settling into the held follow-through. The shoulders
    /// wind back on the draw and drive forward through the swing. Returns the time the follow-through settles.
    /// </summary>
    private static float DrawCut(Swing s, float start, int travel)
    {
        var (hilt, sheathed) = PlayerMoves.Hilt(-.03f, -.04f, .1f);
        s.Builder.Key(start, k => k.Hips(-.03f, -.04f).Chest(.1f).Head(.02f).Shoulders(s.Builder.Model, 90).RightHandAt(hilt.X, hilt.Y).LeftHand(.12f, -.52f).Blade(sheathed), ClipEase.Linear);
        s.Builder.Key(start + F, k => k.Hips(-.03f, -.05f).Chest(.03f).Head(.02f).Shoulders(s.Builder.Model, 125).RightHandAt(-.42f, 2.06f).LeftHand(.16f, -.46f).Blade(sheathed - .15f), ClipEase.Linear);
        s.Tilt(start, 0).Tilt(start + F, 0);
        if (travel > 1) s.Pose(start + 2 * F, Over);
        var contact = start + (1 + travel) * F;
        s.Pose(contact, Contact).Pose(contact + 2 * F, Past, ClipEase.Smooth).Pose(contact + 6 * F, Held, ClipEase.Smooth);
        s.Builder.Marker(PersonLoadout.DrawMarker, start + F).Marker(PersonLoadout.SwooshMarker, start + F).Marker(PersonLoadout.SwooshEndMarker, contact + F)
            .Marker("strike", contact).Marker("recover", contact + 5 * F);
        return contact + 6 * F;
    }


    // The forehand side cut, on a tilted plane: wound back high on the far side, across the front at contact with the arm
    // pointing down and the blade level out of the fist, following through low toward the camera where the blade narrows.
    // Seen side on, a flat (horizontal) swing collapses to a line, so the plane tilts: mostly side to side, low enough to
    // cut through half height (there is no duck), and the blade stays across the forearm on screen. The shoulders turn only
    // a little back on the draw, then drive forward through the cut. The blade rolls its edge toward the camera as it cuts.
    private static readonly Side Wind = new(-150, -165, .05f, -.02f, -.06f, .06f, 100, .18f, -.44f, -30, -45, 30);
    private static readonly Side Across = new(-80, -120, -.1f, .01f, -.08f, .1f, 80, .05f, -.46f, -5, -25, 70);
    private static readonly Side Cut = new(0, -20, -.32f, .06f, -.11f, .18f, 20, -.22f, -.46f, 50, -12, 75);
    private static readonly Side Through = new(12, 40, -.4f, .06f, -.17f, .2f, 5, -.25f, -.44f, 64, -3, 55);
    private static readonly Side SideHeld = new(8, 30, -.37f, .06f, -.16f, .18f, 10, -.24f, -.45f, 58, -8, 20);

    /// <summary>
    /// The forward hit as a side cut: the same draw from the back, then wound back on the far side, <paramref name="across"/>
    /// adding a frame where the arm passes behind the body, across the front at contact with the arm straight, and on toward
    /// the camera. Returns when the follow-through settles.
    /// </summary>
    private static float SideCut(Swing s, float start, bool across)
    {
        var (hilt, sheathed) = PlayerMoves.Hilt(-.03f, -.04f, .1f);
        s.Builder.Key(start, k => k.Hips(-.03f, -.04f).Chest(.1f).Head(.02f).Shoulders(s.Builder.Model, 90).RightHandAt(hilt.X, hilt.Y).LeftHand(.12f, -.52f).Blade(sheathed), ClipEase.Linear);
        s.Builder.Key(start + F, k => k.Hips(-.03f, -.05f).Chest(.03f).Head(.02f).Shoulders(s.Builder.Model, 100).RightHandAt(-.42f, 2.06f).LeftHand(.16f, -.46f).Blade(sheathed - .15f), ClipEase.Linear);
        s.Tilt(start, 0).Tilt(start + F, 0).SeedBlade(sheathed - .15f);
        s.SidePose(start + 2 * F, Wind);
        if (across) s.SidePose(start + 3 * F, Across);
        var contact = start + (across ? 4 : 3) * F;
        s.SidePose(contact, Cut).SidePose(contact + 2 * F, Through, ClipEase.Smooth).SidePose(contact + 6 * F, SideHeld, ClipEase.Smooth);
        s.Builder.Marker(PersonLoadout.DrawMarker, start + F).Marker(PersonLoadout.SwooshMarker, start + 2 * F).Marker(PersonLoadout.SwooshEndMarker, contact + F)
            .Marker("strike", contact).Marker("recover", contact + 5 * F);
        return contact + 6 * F;
    }

    private static readonly Side BackHeld = new(-8, -8, -.33f, .04f, -.14f, .16f, 95, .1f, -.46f, 56, -14, 20);

    /// <summary>Time between a combo's contacts: a player mashing the button as fast as the swing allows.</summary>
    private const float Cadence = .25f;
    /// <summary>How many swings the combo card shows.</summary>
    public const int ComboSwings = 4;

    /// <summary>Marker suffix for a clip's nth swing (1-based): none for the first, "-2", "-3" after.</summary>
    private static string Nth(int n) => n == 1 ? "" : "-" + n;

    private static void Mark(Swing s, int n, float swoosh, float contact) =>
        s.Builder.Marker(PersonLoadout.SwooshMarker + Nth(n), swoosh).Marker(PersonLoadout.SwooshMarker + Nth(n) + "-end", contact + F)
            .Marker("strike" + Nth(n), contact).Marker("recover" + Nth(n), contact + 5 * F);

    /// <summary>
    /// A backhand as swing <paramref name="n"/> of a combo, its contact <see cref="Cadence"/> after the forehand's at
    /// <paramref name="previous"/>. In the forehand's recovery comes the backhand's wind-up: the sword brought up overhead,
    /// the shoulders starting back, held a moment (the time a mashed button leaves anyway). Then the arm is wound across,
    /// back across the front at contact, and follows through away from the camera, the shoulders turning back almost to
    /// where the draw left them. Returns the contact time.
    /// </summary>
    private static float Backhand(Swing s, float previous, int n)
    {
        var raised = new Side(30, 150, -.05f, .02f, -.08f, .1f, 40, -.18f, -.46f, -55, -40, 10);
        var wound = new Side(110, 150, .02f, 0, -.08f, .08f, 25, -.2f, -.46f, -35, -40, 40);
        var cut = new Side(0, 25, -.3f, .06f, -.13f, .18f, 60, .05f, -.46f, 48, -22, 75);
        var through = new Side(-12, -20, -.35f, .05f, -.16f, .18f, 90, .12f, -.46f, 60, -10, 55);
        var contact = previous + Cadence; var start = contact - 2 * F;
        s.SidePose(previous + 7 * F, SideHeld, ClipEase.Smooth).SidePose(contact - 6 * F, raised, ClipEase.Smooth).SidePose(start, raised with { Arm = 40, ArmDip = -58 })
            .SidePose(start + F, wound).SidePose(contact, cut)
            .SidePose(contact + 2 * F, through, ClipEase.Smooth).SidePose(contact + 6 * F, BackHeld, ClipEase.Smooth);
        Mark(s, n, start + F, contact);
        return contact;
    }

    /// <summary>
    /// Another forehand as swing <paramref name="n"/>, after a backhand at <paramref name="previous"/>: in the backhand's
    /// recovery the sword winds back round to the far side, high, the shoulders turning away again (as the draw did), held a
    /// moment; then the same cut as the first. Returns the contact time.
    /// </summary>
    private static float Forehand(Swing s, float previous, int n)
    {
        var contact = previous + Cadence;
        s.SidePose(previous + 7 * F, BackHeld, ClipEase.Smooth).SidePose(contact - 6 * F, Wind with { Twist = 10 }, ClipEase.Smooth).SidePose(contact - 3 * F, Wind)
            .SidePose(contact - 2 * F, Wind).SidePose(contact - F, Across).SidePose(contact, Cut)
            .SidePose(contact + 2 * F, Through, ClipEase.Smooth).SidePose(contact + 6 * F, SideHeld, ClipEase.Smooth);
        Mark(s, n, contact - 2 * F, contact);
        return contact;
    }

    private static float ComboPutAway(Swing s)
    {
        var (settled, held) = Combo(s, ComboSwings);
        return PutAway(s, settled, t => s.SidePose(t, held, ClipEase.Smooth));
    }

    /// <summary>A combo of <paramref name="swings"/> alternating forehand and backhand from the draw; returns when the last settles and which pose it holds.</summary>
    private static (float Settled, Side Held) Combo(Swing s, int swings)
    {
        var contact = SideCut(s, Lead, false) - 6 * F;
        for (var n = 2; n <= swings; n++) contact = n % 2 == 0 ? Backhand(s, contact, n) : Forehand(s, contact, n);
        return (contact + 6 * F, swings % 2 == 0 ? BackHeld : SideHeld);
    }

    /// <summary>
    /// Keys the held pose (<paramref name="held"/>) again after <see cref="HoldFor"/> from <paramref name="settled"/>, then puts the sword back:
    /// the hand carries it up over the shoulder in one continuous turn, slides it onto the back ("sword-sheathe") and
    /// drops into the idle, which then holds for <see cref="Tail"/>. Returns the clip's length.
    /// </summary>
    private static float PutAway(Swing s, float settled, Action<float> held)
    {
        var (hilt, sheathed) = PlayerMoves.Hilt(-.01f, -.04f, .02f);
        var t = settled + HoldFor;
        held(t);
        s.Builder.Key(t + .08f, k => k.Hips(.01f, -.05f).Chest(-.02f).Head(.06f).Shoulders(s.Builder.Model, 70).RightHandAt(.14f, 2f).LeftHand(0, -.58f).Blade(1.6f), ClipEase.Linear)
            .Key(t + .15f, k => k.Hips(-.01f, -.04f).Chest(.04f).Head(.04f).Shoulders(s.Builder.Model, 90).RightHandAt(-.12f, 2.1f).LeftHand(.04f, -.6f).Blade(3.5f), ClipEase.Linear)
            .Key(t + .22f, k => k.Hips(-.01f, -.04f).Chest(.02f).Head(.02f).Shoulders(s.Builder.Model, 80).RightHandAt(hilt.X, hilt.Y).LeftHand(.05f, -.6f).Blade(sheathed + MathF.Tau))
            .Marker(PersonLoadout.SheatheMarker, t + .22f);
        s.Tilt(t + .08f, 0).Tilt(t + .15f, 0).Tilt(t + .22f, 0);
        Idle(s, t + .3f, sheathed + MathF.Tau);
        Idle(s, t + .3f + Tail, sheathed + MathF.Tau);
        return t + .3f + Tail;
    }

    public enum Kind { OverTheTop, Side, SideAcross, SideCombo }

    /// <summary>A whole card: idle, the swing (and for a combo the backhand), a short hold, put the sword back, idle.</summary>
    private static MotionClip Cycle(ResolvedModel m, string id, Kind kind)
    {
        // Author twice: the first pass finds the length, the second builds a clip of exactly that length.
        float Author(Swing s)
        {
            var (_, sheathed) = PlayerMoves.Hilt(-.01f, -.04f, -.03f);
            Idle(s, 0, sheathed); Idle(s, Lead - F, sheathed);
            return kind switch
            {
                Kind.OverTheTop => PutAway(s, DrawCut(s, Lead, 2), t => s.Pose(t, Held, ClipEase.Smooth)),
                Kind.SideCombo => ComboPutAway(s),
                _ => PutAway(s, SideCut(s, Lead, kind == Kind.SideAcross), t => s.SidePose(t, SideHeld, ClipEase.Smooth)),
            };
        }
        var length = Author(new Swing(m, id, id, 5));
        var swing = new Swing(m, id, id, length);
        Author(swing);
        return swing.Build();
    }

    // ---- The game's swings --------------------------------------------------------------------------------------

    public const string SideCutClip = "player-sword-side-cut", BackhandClip = "player-sword-backhand", ForehandClip = "player-sword-forehand",
        PutAwayClip = "player-sword-put-away", PutAwayBackhandClip = "player-sword-put-away-backhand";

    /// <summary>
    /// The chosen swings as the game's clips, cut out of the approved lab cycles so they play exactly as reviewed. Swing n
    /// runs from a frame after the last one's follow-through settles to a frame after its own, so a mashed button chains
    /// them at the combo's cadence: the side cut drawn from the back, then the backhand and forehand, each opening on the
    /// pose the last one holds with its wind-up. The put-aways hold that pose, then sheathe. Swing markers lose their
    /// number ("strike-2" becomes "strike"), and follow-ups start with the sword already in hand.
    /// </summary>
    public static IEnumerable<MotionClip> GameClips(ResolvedModel m)
    {
        var combo = Cycle(m, "lab-combo", Kind.SideCombo); var side = Cycle(m, "lab-side-cut", Kind.Side);
        static float Cut(int n) => Lead + 3 * F + (n - 1) * Cadence + 7 * F;
        MotionClip Swing(float from, float to, string id, string name, int n)
        {
            var clip = ClipAuthoring.Excerpt(combo, from, to, id, name);
            if (n > 1)
            {
                foreach (var marker in clip.Markers) marker.Id = marker.Id.Replace(Nth(n), "");
                ClipAuthoring.SetMarker(clip, PersonLoadout.DrawMarker, 0);
            }
            clip.Validate(m); return clip;
        }
        yield return Swing(Lead, Cut(1), SideCutClip, "Sword draw and side cut", 1);
        yield return Swing(Cut(1), Cut(2), BackhandClip, "Sword backhand", 2);
        yield return Swing(Cut(2), Cut(3), ForehandClip, "Sword forehand", 3);
        yield return ClipAuthoring.Excerpt(side, Cut(1), side.Duration - Tail, PutAwayClip, "Sword put away");
        yield return ClipAuthoring.Excerpt(combo, Cut(ComboSwings), combo.Duration - Tail, PutAwayBackhandClip, "Sword put away after a backhand");
    }

    /// <summary>The held sword's blade, guard to tip, in actor space; null while it is on the back.</summary>
    public static (Vector3 Guard, Vector3 Tip)? Blade(ResolvedModel model, MotionClip clip, float seconds, PropAsset sword, ModelSocket socket) =>
        PersonLoadout.SwordInHand(clip, seconds) ? PersonLoadout.Blade(PoseEvaluator.Sample(model, clip, seconds), sword, socket) : null;

    /// <summary>Where "in front of the player" starts, in actor units from the feet.</summary>
    private const float Front = .25f;

    /// <summary>
    /// The player's hit for one swing, in actor units (facing +X, feet at the origin): the rectangle bounding what the
    /// blade sweeps ahead of the body from <paramref name="from"/> (the swoosh opening) to <paramref name="recover"/>, no
    /// higher than the shoulders at <paramref name="strike"/>. Made once from the swing, it is fixed to the player while the
    /// hit is live; the blade never drags it about. Null when the blade never gets in front.
    /// </summary>
    public static (Vector2 Min, Vector2 Max)? HitRectangle(ResolvedModel model, MotionClip clip, float from, float strike, float recover, Func<float, (Vector3 Guard, Vector3 Tip)?> blade)
    {
        var points = new List<Vector2>();
        for (var t = from; t <= recover + 1e-4f; t += 1 / 240f)
            if (blade(t) is var (guard, tip))
                for (var k = 0; k <= 8; k++) { var p = Vector3.Lerp(guard, tip, k / 8f); if (p.X > Front) points.Add(new(p.X, p.Y)); }
        if (points.Count < 2) return null;
        var shoulders = PoseEvaluator.Sample(model, clip, strike).Points["right-shoulder"].Y;
        return (new(points.Min(p => p.X), points.Min(p => p.Y)), new(points.Max(p => p.X), MathF.Min(shoulders, points.Max(p => p.Y))));
    }

    public static IEnumerable<Variant> Variants(ResolvedModel m, IReadOnlyDictionary<string, MotionClip> game)
    {
        // The game's own clips back to back, as a mashed button chains them: the check that the cut-up combo still plays as reviewed.
        var chain = new[] { SideCutClip, BackhandClip, ForehandClip, BackhandClip, PutAwayBackhandClip }.Select(id => game[id]).ToList();
        var timeline = new List<(MotionClip, float)> { (game["player-idle"], 0) }; var at = Lead;
        foreach (var clip in chain) { timeline.Add((clip, at)); at += clip.Duration; }
        timeline.Add((game["player-idle"], at));
        yield return new("in-game", "In the game: the combo", $"The game's clips in a row as a mashed button chains them: side cut, backhand, forehand, backhand, put away. It should play as the combo card does.", chain[0], timeline, at + Tail);
        Variant Of(string id, string title, string note, Kind kind)
        {
            var clip = Cycle(m, "lab-" + id, kind); return new(id, title, note, clip, [(clip, 0)], clip.Duration);
        }
        yield return Of("over-the-top", "Round 4: over the top", "Last round's draw-cut with depth, for comparison: it comes down over the top.", Kind.OverTheTop);
        yield return Of("side-cut", "Side cut", "The draw from the back, then wound back on the far side and across the front at shoulder height; contact 50 ms after the hilt, arm straight, following through toward the camera where the blade narrows. A short hold, then the sword goes back.", Kind.Side);
        yield return Of("side-cut-across", "Side cut, one more frame", "As the side cut with one more frame where the arm passes behind the body (contact at 67 ms), so the swing has an in-between.", Kind.SideAcross);
        yield return Of("combo", $"Combo: {ComboSwings} swings", $"Idle, then {ComboSwings} swings as if the button is mashed (contacts 0.25 s apart), then idle. Forehand and backhand alternate; in each swing's recovery the next one winds up: the sword up overhead before a backhand, back round to the far side before a forehand. The shoulders swing one way, then the other. Backhands use the follow-up swoosh.", Kind.SideCombo);
    }
}

