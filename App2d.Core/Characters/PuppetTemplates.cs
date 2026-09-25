using System.Numerics;

namespace App2d.Core.Characters;

public static class PuppetTemplates
{
    public static PuppetDefinition StickFigure()
    {
        var puppet = new PuppetDefinition { Name = "Scribble knight" };
        void Control(string id, float x, float y, float z = 0) => puppet.Controls.Add(new() { Id = id, Rest = new(x, y, z) });
        void Bone(string from, string to, bool visible = true)
        {
            puppet.Bones.Add(new() { From = from, To = to });
            if (visible) puppet.Parts.Add(new() { Id = from + "-" + to, Kind = "stroke", A = from, B = to, Width = .045f });
        }
        Control("hips", 0, 1); Control("chest", 0, 1.65f); Control("head", 0, 2.03f);
        Bone("hips", "chest", false); Bone("chest", "head");
        foreach (var (side, x, z) in new[] { ("left", -.13f, .08f), ("right", .13f, -.08f) })
        {
            Control(side + "-shoulder", x, 1.57f, z); Control(side + "-elbow", x * 2, 1.25f, z); Control(side + "-hand", x * 2.5f, .92f, z);
            Control(side + "-hip", x, 1, z); Control(side + "-knee", x + .09f, .51f, z); Control(side + "-foot", x, .025f, z);
            Bone("chest", side + "-shoulder", false); Bone(side + "-shoulder", side + "-elbow"); Bone(side + "-elbow", side + "-hand");
            Bone("hips", side + "-hip", false); Bone(side + "-hip", side + "-knee"); Bone(side + "-knee", side + "-foot");
            puppet.Chains.Add(new() { Root = side + "-shoulder", Joint = side + "-elbow", End = side + "-hand", Bend = -1 });
            puppet.Chains.Add(new() { Root = side + "-hip", Joint = side + "-knee", End = side + "-foot", Bend = 1 });
        }
        puppet.Parts.Add(new() { Id = "body", Kind = "box", A = "hips", B = "chest", Width = .45f, Height = .68f, OffsetY = .32f, Roundness = .3f, Fill = "#d8e9db" });
        puppet.Parts.Add(new() { Id = "head", Kind = "ellipse", A = "head", Width = .58f, Height = .58f, Face = "relaxed", Depth = -.12f });
        puppet.Motions[0].Name = "Pose study";
        puppet.Validate(); return puppet;
    }

    /// <summary>A repeating right-facing walk, slightly turned toward the viewer, with explicit world-space contacts.</summary>
    public static PuppetDefinition StepStudy()
    {
        var puppet = StickFigure(); puppet.Name = "Walk loop study";
        // Begin in right-facing profile: the shoulder and hip axes run into/out of the
        // screen. Rotate that cross-section 30 degrees toward the viewer, with
        // both sockets slightly behind the torso's anatomical centerline.
        // +Z is away. The near (left) socket projects leftward; the far (right)
        // socket projects rightward and stays behind the torso's drawing plane.
        const float yaw = MathF.PI / 6, shoulderHalfSpan = .18f, hipHalfSpan = .12f, shoulderSetback = -.025f;
        foreach (var (side, sign) in new[] { ("left", -1f), ("right", 1f) })
        {
            var lateral = sign * shoulderHalfSpan;
            var shoulderX = shoulderSetback * MathF.Cos(yaw) + lateral * MathF.Sin(yaw);
            var shoulderDepth = -shoulderSetback * MathF.Sin(yaw) + lateral * MathF.Cos(yaw);
            var hipX = sign * hipHalfSpan * MathF.Sin(yaw);
            var hipDepth = sign * hipHalfSpan * MathF.Cos(yaw);
            void Rest(string suffix, float px, float py, float depth) => puppet.Controls.Single(c => c.Id == side + suffix).Rest = new(px, py, depth);
            Rest("-shoulder", shoulderX, 1.57f, shoulderDepth);
            Rest("-elbow", shoulderX + .05f, 1.25f, shoulderDepth); Rest("-hand", shoulderX + .03f, .92f, shoulderDepth);
            Rest("-hip", hipX, 1, hipDepth); Rest("-knee", hipX + .09f, .51f, hipDepth); Rest("-foot", hipX, .025f, hipDepth);
        }
        puppet.Parts.Single(p => p.Id == "body").Width = .36f;
        var head = puppet.Parts.Single(p => p.Id == "head"); head.Width = .56f; head.FaceX = .065f;
        var motion = puppet.Motions[0]; motion.Name = "Walk right"; motion.Duration = 1.2f; motion.Loop = true;
        const float stride = .5f, ground = .025f;
        var leftTrack = puppet.Controls.Single(c => c.Id == "left-foot").Rest;
        var rightTrack = puppet.Controls.Single(c => c.Id == "right-foot").Rest;
        for (var i = 0; i <= 16; i++)
        {
            var phase = i / 16f; var time = phase * motion.Duration; var travel = stride * phase;
            var pose = PuppetPose.Rest(puppet);
            // Rise over the planted foot enough to nearly extend its knee at
            // mid-stance, retaining a little flex rather than locking the joint.
            var bob = .036f * MathF.Pow(MathF.Sin(phase * MathF.Tau), 2);
            pose.Position = new(travel, -.025f + bob, 0);
            float Smooth(float t) => t * t * (3 - 2 * t);
            var leftSwing = Math.Clamp(phase * 2, 0, 1); var rightSwing = Math.Clamp(phase * 2 - 1, 0, 1);
            var leftWorldX = -.125f + stride * Smooth(leftSwing);
            var rightWorldX = .125f + stride * Smooth(rightSwing);
            var leftLift = .12f * MathF.Pow(MathF.Sin(leftSwing * MathF.PI), 2);
            var rightLift = .12f * MathF.Pow(MathF.Sin(rightSwing * MathF.PI), 2);
            pose.Points["left-foot"] = new(leftWorldX - travel + leftTrack.X, ground + leftLift - pose.Position.Y, leftTrack.Z);
            pose.Points["right-foot"] = new(rightWorldX - travel + rightTrack.X, ground + rightLift - pose.Position.Y, rightTrack.Z);
            var swing = MathF.Cos(phase * MathF.Tau);
            foreach (var (side, sign) in new[] { ("left", 1f), ("right", -1f) })
            {
                var shoulder = pose.Points[side + "-shoulder"];
                pose.Points[side + "-hand"] = shoulder + new System.Numerics.Vector3(sign * .18f * swing, -.61f, 0);
            }
            motion.Keys.Add(pose.Key(time));
        }
        // Store an exact repeated local pose, including the hands, at the seam.
        motion.Keys[^1].Points = new(motion.Keys[0].Points);
        motion.Keys[^1].Position = new(stride, motion.Keys[0].Position.Y, 0);
        motion.Contacts.Add(new() { End = "right-foot", Start = 0, Finish = .6f, Target = new(.125f + rightTrack.X, ground, rightTrack.Z) });
        motion.Contacts.Add(new() { End = "left-foot", Start = .6f, Finish = 1.2f, Target = new(.375f + leftTrack.X, ground, leftTrack.Z) });
        puppet.Validate(); return puppet;
    }

    /// <summary>
    /// A right-facing run built from animator key poses: contact, down, push-off and flight.
    /// Each foot is planted for 30% of the cycle, so both are airborne twice per loop.
    /// </summary>
    public static PuppetDefinition RunStudy()
    {
        var puppet = StepStudy(); puppet.Name = "Run loop study";
        puppet.Parts.Single(p => p.Id == "head").Face = "determined";
        var motion = puppet.Motions[0]; motion.Name = "Run right"; motion.Duration = .72f; motion.Loop = true;
        motion.Keys.Clear(); motion.Contacts.Clear();
        var rest = PuppetPose.Rest(puppet);
        // Travel per cycle, planted fraction, strike distance ahead of the hips and ground height.
        const float travel = 1.6f, stance = .3f, strike = .18f, ground = .025f;

        // Cubic Hermite through keyed values; interior tangents are Catmull-Rom.
        static Vector2 Curve(float t, float[] times, Vector2[] values, Vector2 startTangent, Vector2 endTangent)
        {
            var i = 0; while (i < times.Length - 2 && t > times[i + 1]) i++;
            Vector2 Tangent(int k) => k == 0 ? startTangent : k == times.Length - 1 ? endTangent
                : (values[k + 1] - values[k - 1]) / (times[k + 1] - times[k - 1]);
            var span = times[i + 1] - times[i]; var u = Math.Clamp((t - times[i]) / span, 0, 1);
            float u2 = u * u, u3 = u2 * u;
            return (2 * u3 - 3 * u2 + 1) * values[i] + (u3 - 2 * u2 + u) * span * Tangent(i)
                + (-2 * u3 + 3 * u2) * values[i + 1] + (u3 - u2) * span * Tangent(i + 1);
        }
        static float Wrap(float value) => value - MathF.Floor(value);

        // Swing path of one foot relative to the hips: x forward, y height above the ground.
        // Peel back, heel kicks toward the seat, knee drives through, reach, then paw back into contact.
        float[] swingTimes = [stance, .40f, .52f, .64f, .76f, .88f, 1];
        Vector2[] swingPath = [new(strike - travel * stance, 0), new(-.42f, .20f), new(-.30f, .45f), new(-.08f, .50f), new(.16f, .40f), new(.36f, .18f), new(strike, 0)];
        Vector2 Foot(float p) => p < stance ? new(strike - travel * p, 0)
            : Curve(p, swingTimes, swingPath, new(-1.4f, 1f), new(-1.5f, -1.5f));

        // Hip height over one step: settle after contact, lowest while loading, rise through push-off, peak in flight.
        // Padded by one key on each side so the loop seam uses Catmull-Rom tangents too.
        float[] bobTimes = [-.2f, 0, .24f, .6f, .8f, 1, 1.24f];
        Vector2[] bobHeights = [new(.985f), new(.945f), new(.875f), new(.935f), new(.985f), new(.945f), new(.875f)];
        float Hips(float q) => Curve(Wrap(q), bobTimes, bobHeights, default, default).X;

        Vector3 Lean(Vector3 point, float angle)
        {
            var (sin, cos) = MathF.SinCos(angle); var y = point.Y - 1;
            return new(point.X * cos + y * sin, 1 - point.X * sin + y * cos, point.Z);
        }

        const int keyCount = 36;
        for (var i = 0; i <= keyCount; i++)
        {
            var phase = i / (float)keyCount; var step = Wrap(phase * 2);
            var hipHeight = Hips(step);
            var pose = PuppetPose.Rest(puppet);
            pose.Position = new(travel * phase, hipHeight - 1, 0);
            // Lean most at push-off. The chest and head trail the hips' bob slightly.
            var lean = .21f + .03f * MathF.Cos(MathF.Tau * (step - .6f));
            var chestLag = .25f * (Hips(step - .06f) - hipHeight);
            var headLag = .5f * (Hips(step - .08f) - hipHeight);
            pose.Points["chest"] = Lean(rest.Points["chest"], lean) + new Vector3(0, chestLag, 0);
            pose.Points["head"] = Lean(rest.Points["head"], lean * 1.1f) + new Vector3(0, headLag, 0);
            foreach (var (side, legPhase, armAngle) in new[] { ("right", phase, MathF.Tau * (phase - .03f) + MathF.PI), ("left", Wrap(phase + .5f), MathF.Tau * (phase - .03f)) })
            {
                var foot = Foot(legPhase);
                // The pelvis turns with the forward leg while the shoulders counter-rotate with the arms.
                var hip = rest.Points[side + "-hip"];
                pose.Points[side + "-hip"] = hip with { X = hip.X + .08f * foot.X };
                var track = rest.Points[side + "-foot"];
                pose.Points[side + "-foot"] = new(foot.X + track.X, ground + foot.Y - pose.Position.Y, track.Z);
                var swing = MathF.Cos(armAngle);
                var shoulder = Lean(rest.Points[side + "-shoulder"], lean) + new Vector3(.03f * swing, chestLag, 0);
                pose.Points[side + "-shoulder"] = shoulder;
                // Elbows stay bent near 90 degrees: high and forward, then low and back.
                var hand = new Vector2(.03f + .26f * swing, -.33f + .09f * swing - .05f * MathF.Sin(armAngle));
                var (sin, cos) = MathF.SinCos(lean * .6f);
                pose.Points[side + "-hand"] = shoulder + new Vector3(hand.X * cos + hand.Y * sin, hand.Y * cos - hand.X * sin, 0);
            }
            motion.Keys.Add(pose.Key(phase * motion.Duration));
        }
        motion.Keys[^1].Points = new(motion.Keys[0].Points);
        motion.Keys[^1].Position = new(travel, motion.Keys[0].Position.Y, 0);
        motion.Contacts.Add(new() { End = "right-foot", Start = 0, Finish = stance * motion.Duration, Target = new(strike + rest.Points["right-foot"].X, ground, rest.Points["right-foot"].Z) });
        motion.Contacts.Add(new() { End = "left-foot", Start = .5f * motion.Duration, Finish = (.5f + stance) * motion.Duration, Target = new(strike + travel / 2 + rest.Points["left-foot"].X, ground, rest.Points["left-foot"].Z) });
        puppet.Validate(); return puppet;
    }
}
