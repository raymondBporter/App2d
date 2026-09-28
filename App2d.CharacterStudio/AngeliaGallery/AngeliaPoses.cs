using System.Numerics;

namespace App2d.CharacterStudio.AngeliaGallery;

/// <summary>
/// One frame of an AngeliA-style pose, in AngeliA's conventions: degrees, positive = clockwise on screen, 0 = limb hanging
/// straight down, lower limbs relative to their upper limb, arms relative to the body. Offsets are metres in App2d space.
/// </summary>
internal sealed class AngeliaPose
{
    public float RootY, HipX, BodyRot, HeadRot, TorsoScale = 1, ChestDrop;
    public float? HipY; // absolute hip height; overrides leg length + RootY
    public float ArmUL, ArmLL, ArmUR, ArmLR, ArmScaleL = 1, ArmScaleR = 1;
    public float LegUL, LegLL, LegUR, LegLR, LowerLegScaleL = 1, LowerLegScaleR = 1, LegXL, LegXR;
    public bool PlantFeet; // solve the legs so both feet stay on the ground (squat)
    public float WholeRot; // spin every point about the hips afterwards (roll, lying down)
}

/// <summary>
/// Ports of AngeliA's PoseAnimation_* classes as pure functions of the 60 fps animation frame, facing right. Runtime inputs
/// AngeliA reads from the character (speeds, horizontal velocity) are stubbed with fixed values.
/// </summary>
internal static class AngeliaPoses
{
    public const float A = .07f; // AngeliA's A2G (one art pixel, 16 units) in App2d metres

    public static readonly (string Name, Func<int, AngeliaPose> Pose)[] All =
    [
        ("Idle", Idle), ("Walk", Walk), ("Run", Run), ("JumpUp", JumpUp), ("JumpDown", JumpDown), ("SquatIdle", SquatIdle),
        ("Dash", Dash), ("Climb", Climb), ("Fly", Fly), ("Slide", Slide), ("SwimMove", SwimMove), ("Rolling", Rolling),
        ("Sleep", Sleep), ("Sit", Sit),
    ];

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    private static float Repeat(float t, float length) => t - MathF.Floor(t / length) * length;
    private static float PingPong(float t, float length) => length - MathF.Abs(Repeat(t, length * 2) - length);
    private static float InOutCirc(float x) => x < .5f ? (1 - MathF.Sqrt(1 - 4 * x * x)) / 2 : (MathF.Sqrt(1 - MathF.Pow(-2 * x + 2, 2)) + 1) / 2;
    private static float InCubic(float x) => x * x * x;
    private static float InOutBack(float x)
    {
        const float c2 = 1.70158f * 1.525f;
        return x < .5f ? MathF.Pow(2 * x, 2) * ((c2 + 1) * 2 * x - c2) / 2 : (MathF.Pow(2 * x - 2, 2) * ((c2 + 1) * (x * 2 - 2) + c2) + 2) / 2;
    }

    // PoseAnimation_Idle: breathing — the chest sinks and rises, no rotations.
    private static AngeliaPose Idle(int f)
    {
        var e = InOutCirc(PingPong(f % 96 * 2f / 96, 1));
        return new() { ChestDrop = e * A };
    }

    // PoseAnimation_Walk: 16-step lookup tables.
    private static readonly float[] WalkEase =
    [
        0, .03125f, .125f, .28125f, .5f, .71875f, .875f, .96875f, 1, .96875f, .875f, .71875f, .5f, .28125f, .125f, .03125f,
        0, .04081633f, .1632653f, .3673469f, .6326531f, .8367347f, .9591837f, 1, 0, .04081633f, .1632653f, .3673469f, .6326531f, .8367347f, .9591837f, 1,
    ];
    private static readonly int[,] WalkRots =
    {
        { -20, 20, 25, -25, 0, 0 }, { -17, 17, 21, -25, 0, 0 }, { -15, 15, 17, -27, 30, 20 }, { -7, 7, 17, -15, 45, 10 },
        { 0, 0, -5, -5, 60, 0 }, { 7, -7, -5, 7, 75, 0 }, { 15, -15, -27, 17, 90, 0 }, { 17, -17, -26, 21, 45, 0 },
        { 20, -20, -25, 25, 0, 0 }, { 17, -17, -26, 21, 10, 15 }, { 15, -15, -27, 17, 20, 30 }, { 7, -7, -15, 7, 10, 45 },
        { 0, 0, -5, -5, 0, 60 }, { -7, 7, 5, -10, 0, 75 }, { -15, 15, 17, -27, 0, 90 }, { -17, 17, 21, -26, 0, 45 },
    };
    private static AngeliaPose Walk(int f)
    {
        const int walkSpeed = 24;
        var i = f * walkSpeed / 47 % 16;
        var shift = Lerp(0, A * .7f, WalkEase[i]);
        return new()
        {
            RootY = WalkEase[i + 16] * A,
            LegXL = shift,
            LegXR = -shift,
            ArmUL = WalkRots[i, 0],
            ArmUR = WalkRots[i, 1],
            LegUL = WalkRots[i, 2],
            LegUR = WalkRots[i, 3],
            LegLL = WalkRots[i, 4],
            LegLR = WalkRots[i, 5],
        };
    }

    // PoseAnimation_Run: ping-pong limbs, forward lean from horizontal speed, bouncing root.
    private static AngeliaPose Run(int f)
    {
        const float runSpeed = 40, dx = 20;
        var t = Repeat(f * runSpeed / 57 / 16, 1);
        float p = PingPong(2 * t, 1), q = 1 - p;
        float pAlt = PingPong(2 * Repeat(t - .3f, 1), 1), qAlt = 1 - pAlt;
        var easeD = InCubic(2 * t % 1);
        var lean = Math.Clamp(dx, -42, 42) * 2 / 3;
        var shift = Lerp(0, A * .9f, p);
        return new()
        {
            RootY = ((1 - easeD) * 2 - 1) * A,
            BodyRot = lean,
            HeadRot = -lean * 2 / 3,
            ArmUL = Lerp(-10, 80, p) - lean / 2,
            ArmUR = Lerp(-10, 80, q) - lean / 2,
            ArmLL = Lerp(-65, -90, p),
            ArmLR = Lerp(-65, -90, q),
            LegXL = shift,
            LegXR = -shift,
            LegUL = Lerp(55, -65, p) + lean / 3,
            LegUR = Lerp(55, -65, q) + lean / 3,
            LegLL = Lerp(90, 0, InOutBack(pAlt)),
            LegLR = Lerp(90, 0, InOutBack(qAlt)),
        };
    }

    // PoseAnimation_JumpUp: arms flung wide and flickering, one knee tucked.
    private static AngeliaPose JumpUp(int f)
    {
        var alt = f % 8 >= 4;
        return new()
        {
            RootY = A,
            ChestDrop = alt ? -A / 4 : 0,
            ArmUL = alt ? 65 : 55,
            ArmUR = alt ? -65 : -55,
            ArmLL = alt ? -55 : -45,
            ArmLR = alt ? 55 : 45,
            LegXL = -A / 2,
            LegXR = -A / 2,
            LegUL = 0,
            LegUR = -20,
            LegLL = 0,
            LegLR = 45,
            LowerLegScaleR = .75f,
        };
    }

    // PoseAnimation_JumpDown: arms up over the head, legs reaching for the ground.
    private static AngeliaPose JumpDown(int f)
    {
        var alt = f % 8 >= 4;
        return new()
        {
            RootY = -A,
            ArmUL = alt ? 135 : 125,
            ArmUR = alt ? -125 : -135,
            ArmLL = alt ? 35 : 45,
            ArmLR = alt ? -45 : -35,
            LegXL = A / 2,
            LegXR = A / 2,
            LegUL = 0,
            LegUR = -25,
            LegLL = 3,
            LegLR = 15,
            LowerLegScaleL = .75f,
            LowerLegScaleR = .75f,
        };
    }

    // PoseAnimation_SquatIdle: half height, short torso breathing, forearms folded.
    private static AngeliaPose SquatIdle(int f)
    {
        var a = f % 64 / 16f;
        var e = InOutCirc(a / 4);
        if (a >= 2) e = 1 - e;
        return new()
        {
            HipY = .5f,
            PlantFeet = true,
            TorsoScale = .6f,
            ChestDrop = e * A,
            ArmUL = 25,
            ArmUR = -25,
            ArmLL = -90,
            ArmLR = 90,
            ArmScaleL = .85f,
            ArmScaleR = .85f,
        };
    }

    // PoseAnimation_Dash: low, leaning back, legs kicked out in front.
    private static AngeliaPose Dash(int f)
    {
        var alt = f % 8 >= 4;
        var wobble = alt ? 2 : -2;
        return new()
        {
            HipY = .12f,
            HipX = 2 * A,
            LegXL = -2 * A,
            LegXR = -2 * A,
            BodyRot = -40,
            HeadRot = 20,
            TorsoScale = .85f,
            ArmUL = 60 + wobble,
            ArmUR = -135 - wobble,
            ArmLL = -60 + wobble,
            ArmLR = 45 + wobble,
            LegUL = -90,
            LegUR = -135,
            LegLL = alt ? 2 : 0,
            LegLR = 90 - (alt ? 2 : 0),
        };
    }

    // PoseAnimation_Climb: hand over hand, knees alternating.
    private static AngeliaPose Climb(int f)
    {
        const int climbSpeed = 10;
        var rate = Math.Max(560 / climbSpeed / 8, 1);
        var a = f % (rate * 10 - 1) / rate;
        var d = (a + 1) % 10;
        if (a >= 5) a = 8 - a;
        if (d >= 5) d = 8 - d;
        a = Math.Clamp(a, 0, 4);
        var pose = new AngeliaPose
        {
            RootY = -Math.Abs(a - 2) * A,
            BodyRot = 2 * a - 4,
            ArmUL = Math.Clamp(135 - 35 * (3 - d), 45, 135),
            ArmUR = Math.Clamp(35 * d - 135, -135, -45),
            LegUL = Math.Clamp(35 * a, 0, 60),
            LegUR = Math.Clamp(-35 * (3 - a), -60, 0),
        };
        pose.ArmLL = 180 - pose.ArmUL; pose.ArmLR = -180 - pose.ArmUR;
        pose.LegLL = -pose.LegUL - 5; pose.LegLR = -pose.LegUR + 5;
        return pose;
    }

    // PoseAnimation_Fly: hovering bob, legs paddling.
    private static AngeliaPose Fly(int f)
    {
        var fr = f % 16 / 2;
        var pp = fr < 4 ? fr : 8 - fr;
        return new()
        {
            RootY = A - (fr < 6 ? fr / 2f : 8 - fr) * A / 2,
            LegUL = 20,
            LegUR = -20,
            LegLL = 4 * pp - 20,
            LegLR = -4 * pp + 20,
        };
    }

    // PoseAnimation_Slide (wall slide): one arm up the wall, legs braced.
    private static AngeliaPose Slide(int f)
    {
        var alt = f / 4 % 2 == 0;
        return new()
        {
            HeadRot = -6,
            BodyRot = 8,
            ArmUL = 70,
            ArmUR = -175,
            ArmLL = -65,
            ArmLR = 0,
            ArmScaleR = 1.5f + (alt ? .1f : 0),
            LegUL = -15 + (alt ? 1 : 0),
            LegUR = -30 + (alt ? 0 : 1),
            LegLL = -30,
            LegLR = 20,
        };
    }

    // PoseAnimation_SwimMove: arms stroking forward, legs kicking behind.
    private static AngeliaPose SwimMove(int f)
    {
        const int loop = 28; // 1200 / swimSpeed 40, rounded to a multiple of 4
        var fr = f % loop / (loop / 4);
        var k = fr == 3 ? 1 : fr;
        var reach = 1 + (2 - fr) * A / .33f;
        var pose = new AngeliaPose
        {
            BodyRot = 6,
            HeadRot = -6,
            ArmUL = -90,
            ArmUR = -90,
            ArmScaleL = reach,
            ArmScaleR = reach,
            LegUL = 25 + 15 * k,
            LegUR = 25 - 15 * k,
        };
        pose.LegLL = 85 - pose.LegUL; pose.LegLR = 85 - pose.LegUR;
        return pose;
    }

    // PoseAnimation_Rolling: AngeliA flips the head below the body frame by frame; approximated here as a tucked forward spin.
    private static readonly int[] RollRootY = [1450, 1200, 850, 300, 650, 850, 950, 1200];
    private static AngeliaPose Rolling(int f)
    {
        var i = f % 24 / 3;
        return new()
        {
            HipY = .35f + RollRootY[i] / 1500f * .35f,
            WholeRot = i * 45,
            TorsoScale = .75f,
            ArmUL = -40,
            ArmUR = -45,
            ArmLL = -70,
            ArmLR = -70,
            LegUL = -110,
            LegUR = -100,
            LegLL = 140,
            LegLR = 140,
        };
    }

    // PoseAnimation_Sleep: lying down, slow breathing.
    private static AngeliaPose Sleep(int f)
    {
        var alt = f % 120 >= 60;
        return new() { HipY = .12f, WholeRot = -90, ChestDrop = alt ? A / 3 : 0, ArmUL = 0, ArmUR = 10, LegUL = 0, LegUR = 5, LegLR = -5 };
    }

    // PoseAnimation_Sit (Platformer): thighs forward, shins hanging.
    private static AngeliaPose Sit(int f) => new()
    {
        HipY = .5f,
        ArmUL = -20,
        ArmUR = 20,
        LegUL = -80,
        LegUR = -80,
        LegLL = 120,
        LegLR = 120,
    };
}

/// <summary>Forward kinematics from an <see cref="AngeliaPose"/> onto the person model's rest proportions.</summary>
internal sealed class AngeliaRig
{
    private readonly Dictionary<string, Vector3> _rest;
    private readonly float _torso, _neck, _upperArm, _lowerArm, _upperLeg, _lowerLeg, _legLength;

    public AngeliaRig(IReadOnlyDictionary<string, Vector3> rest)
    {
        _rest = new(rest);
        float Dist(string a, string b) => Vector3.Distance(rest[a], rest[b]);
        _torso = Dist("hips", "chest"); _neck = Dist("chest", "head");
        _upperArm = Dist("left-shoulder", "left-elbow"); _lowerArm = Dist("left-elbow", "left-hand");
        _upperLeg = Dist("left-hip", "left-knee"); _lowerLeg = Dist("left-knee", "left-foot");
        _legLength = rest["hips"].Y - rest["left-foot"].Y;
    }

    /// <summary>Clockwise rotation, matching AngeliA's sign.</summary>
    private static Vector2 Rot(Vector2 v, float degrees)
    {
        var r = degrees * MathF.PI / 180; var (s, c) = MathF.SinCos(r);
        return new(v.X * c + v.Y * s, -v.X * s + v.Y * c);
    }

    /// <summary>A limb pointing at the given AngeliA angle: 0 hangs down, positive swings the tip toward -X.</summary>
    private static Vector2 Limb(float degrees) => Rot(new(0, -1), degrees);

    public void Apply(AngeliaPose p, Dictionary<string, Vector3> points)
    {
        var hips = new Vector2(p.HipX, p.HipY ?? _legLength + p.RootY);
        var up = Rot(Vector2.UnitY, p.BodyRot);
        var chest = hips + up * (_torso * p.TorsoScale - p.ChestDrop);
        var head = chest + Rot(Vector2.UnitY, p.BodyRot + p.HeadRot) * _neck;
        var restChest = _rest["chest"];

        Set(points, "hips", hips); Set(points, "chest", chest); Set(points, "head", head);
        Arm("left", p.ArmUL, p.ArmLL, p.ArmScaleL);
        Arm("right", p.ArmUR, p.ArmLR, p.ArmScaleR);
        Leg("left", p.LegUL, p.LegLL, p.LowerLegScaleL, p.LegXL);
        Leg("right", p.LegUR, p.LegLR, p.LowerLegScaleR, p.LegXR);

        if (p.WholeRot != 0)
            foreach (var id in points.Keys.ToList())
            {
                var v = points[id]; var local = Rot(new Vector2(v.X, v.Y) - hips, p.WholeRot) + hips;
                points[id] = new(local.X, local.Y, v.Z);
            }

        void Arm(string side, float upper, float lower, float scale)
        {
            var restShoulder = _rest[$"{side}-shoulder"];
            var shoulder = chest + Rot(new Vector2(restShoulder.X - restChest.X, restShoulder.Y - restChest.Y), p.BodyRot);
            var elbow = shoulder + Limb(p.BodyRot + upper) * _upperArm * scale;
            var hand = elbow + Limb(p.BodyRot + upper + lower) * _lowerArm * scale;
            Set(points, $"{side}-shoulder", shoulder); Set(points, $"{side}-elbow", elbow); Set(points, $"{side}-hand", hand);
        }

        void Leg(string side, float upper, float lower, float lowerScale, float shiftX)
        {
            var restHip = _rest[$"{side}-hip"];
            var hip = hips + new Vector2(restHip.X - _rest["hips"].X + shiftX, 0);
            Vector2 knee, foot;
            if (p.PlantFeet)
            {
                // Two-bone solve with the knee forward (+X): both feet under the hip, on the ground.
                foot = new(hip.X, 0);
                var toFoot = foot - hip; var d = Math.Clamp(toFoot.Length(), .01f, _upperLeg + _lowerLeg - .001f);
                var cos = (_upperLeg * _upperLeg + d * d - _lowerLeg * _lowerLeg) / (2 * _upperLeg * d);
                var bend = MathF.Acos(Math.Clamp(cos, -1, 1)) * 180 / MathF.PI;
                knee = hip + Rot(Vector2.Normalize(toFoot), -bend) * _upperLeg;
            }
            else
            {
                knee = hip + Limb(upper) * _upperLeg;
                foot = knee + Limb(upper + lower) * _lowerLeg * lowerScale;
            }
            Set(points, $"{side}-hip", hip); Set(points, $"{side}-knee", knee); Set(points, $"{side}-foot", foot);
        }
    }

    private void Set(Dictionary<string, Vector3> points, string id, Vector2 xy) => points[id] = new(xy.X, xy.Y, _rest[id].Z);
}
