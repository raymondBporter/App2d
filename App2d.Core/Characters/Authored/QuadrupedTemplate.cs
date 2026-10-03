using System.Numerics;

namespace App2d.Core.Characters.Authored;

/// <summary>Four planted IK feet and independently posed head/tail, with editable cutout artwork.</summary>
public static class QuadrupedTemplate
{
    public static CharacterModel Model(string id = "quadruped", string name = "Quadruped", bool triceratops = false)
    {
        var m = new CharacterModel { Id = id, Name = name, Ink = "#242a29", LineWidth = .045f };
        void Control(string key, string? parent, float x, float y, float z = 0) => m.Controls.Add(new() { Id = key, Parent = parent, Rest = new(x, y, z) });
        Control("body", null, -.25f, .92f);
        Control("body-up", "body", -.25f, 1.42f);
        Control("head", "body", .72f, .86f);
        Control("head-up", "head", .72f, 1.36f);
        Control("tail", "body", -1.04f, .98f);
        Control("tail-up", "tail", -1.04f, 1.48f);
        foreach (var (leg, x, z) in new[] { ("far-hind", -.72f, .16f), ("far-front", .38f, .16f), ("near-hind", -.95f, -.12f), ("near-front", .15f, -.12f) })
        {
            var front = leg.EndsWith("front", StringComparison.Ordinal);
            Control(leg + "-root", "body", x, .84f, z);
            Control(leg + "-joint", leg + "-root", x + (front ? -.12f : .16f), .46f, z);
            Control(leg + "-foot", leg + "-joint", x + .02f, .10f, z);
            m.Chains.Add(new() { Id = leg, Root = leg + "-root", Joint = leg + "-joint", End = leg + "-foot", Bend = front ? 1 : -1 });
        }
        PuppetPart Shape(string key, string a, string? b, float w, float h, float x, float y, float depth, string fill, string kind = "ellipse")
        {
            var p = new PuppetPart { Id = key, A = a, B = b, Width = w, Height = h, OffsetX = x, OffsetY = y, Depth = depth, Fill = fill, Kind = kind };
            m.Parts.Add(p); return p;
        }
        PuppetPart Cutout(string key, string anchor, float w, float h, float x, float y, float depth, string fill, params PuppetPoint[] points)
        {
            var p = Shape(key, anchor, anchor + "-up", w, h, x, y, depth, fill, "polygon"); p.Points = [.. points]; return p;
        }
        const string skin = "#79bdb0", shade = "#527f78", horn = "#f6e4bb";
        Cutout("tail", "tail", .85f, .52f, -.35f, .03f, .02f, skin,
            new(.5f, -.25f), new(-.2f, -.17f), new(-.5f, .32f), new(-.12f, .12f), new(.5f, .45f));
        foreach (var chain in m.Chains)
        {
            var far = chain.Id.StartsWith("far", StringComparison.Ordinal); var color = far ? shade : skin;
            var root = m.Controls.First(c => c.Id == chain.Root).Rest; var joint = m.Controls.First(c => c.Id == chain.Joint).Rest; var foot = m.Controls.First(c => c.Id == chain.End).Rest;
            var upper = Vector2.Distance(root.XY, joint.XY); var lower = Vector2.Distance(joint.XY, foot.XY);
            Shape(chain.Id + "-upper", chain.Root, chain.Joint, .25f, upper + .19f, 0, upper / 2, 0, color);
            Shape(chain.Id + "-lower", chain.Joint, chain.End, .23f, lower + .15f, 0, lower / 2, -.005f, color);
            var p = Shape(chain.Id + "-paw", chain.End, null, .36f, .22f, .015f, -.005f, -.015f, color, "polygon");
            p.Points = [new(-.5f, -.4f), new(.47f, -.4f), new(.5f, -.02f), new(.24f, .45f), new(-.25f, .5f), new(-.47f, .12f)];
            for (var i = 0; i < 3; i++) Shape(chain.Id + "-toe-" + i, chain.End, null, .045f, .065f, -.08f + i * .085f, -.055f, -.025f, horn).OutlineWidth = .012f;
        }
        var body = Shape("body", "body", "body-up", 1.92f, .90f, -.05f, .04f, 0, skin);
        body.Paint = [Spot(-.24f, .15f, .09f, .19f), Spot(-.04f, .03f, .065f, .14f)];
        if (triceratops)
        {
            Cutout("head", "head", .97f, .68f, .12f, -.02f, -.24f, skin,
                new(-.5f, -.18f), new(-.25f, -.42f), new(.25f, -.5f), new(.48f, -.22f), new(.5f, .02f), new(.25f, .25f), new(-.15f, .5f), new(-.48f, .32f));
            Cutout("frill", "head", .88f, 1.38f, -.22f, .35f, -.26f, "#91cebf",
                new(-.30f, -.48f), new(.08f, -.5f), new(.43f, -.23f), new(.47f, .14f), new(.32f, .48f), new(.19f, .5f), new(.06f, .40f), new(-.10f, .43f), new(-.22f, .31f), new(-.38f, .3f), new(-.43f, .13f), new(-.5f, .02f), new(-.45f, -.15f), new(-.48f, -.28f));
            Cutout("far-brow-horn", "head", .31f, .36f, .25f, .47f, -.255f, horn, new(-.5f, -.5f), new(.05f, -.4f), new(.5f, .5f));
            Cutout("near-brow-horn", "head", .36f, .37f, .05f, .48f, -.29f, horn, new(-.5f, -.25f), new(-.13f, -.5f), new(.5f, .5f), new(.12f, .40f));
            Cutout("nose-horn", "head", .22f, .25f, .54f, .20f, -.29f, horn, new(-.5f, -.32f), new(.06f, -.5f), new(.5f, .5f));
        }
        else
        {
            Shape("head", "head", "head-up", .74f, .58f, .13f, .02f, -.24f, skin);
        }

        Shape("eye", "head", "head-up", .09f, .12f, .34f, .01f, -.31f, m.Ink).OutlineWidth = 0;
        Cutout("brow", "head", .16f, .09f, .33f, .11f, -.31f, m.Ink, new(-.5f, .5f), new(.5f, -.08f), new(.35f, -.5f), new(-.5f, .1f)).OutlineWidth = 0;
        Cutout("mouth", "head", .23f, .08f, .35f, -.17f, -.31f, m.Ink, new(-.5f, -.3f), new(-.1f, .5f), new(.5f, -.35f), new(.44f, -.5f), new(-.1f, .10f), new(-.42f, -.5f)).OutlineWidth = 0;
        m.Sockets = [new() { Id = "head-art", Control = "head", Toward = "head-up" }, new() { Id = "body-art", Control = "body", Toward = "body-up" }];
        m.Groups = [new() { Id = "head", Targets = ["head", "head-up"] }, new() { Id = "legs", Targets = [.. m.Chains.Select(c => c.Id)] }];
        m.HurtLayouts = [new() { Id = "body", Regions = [new() { Id = "body", Controls = ["body", "head"], Pad = .35f }] }];
        m.MotionSets = [new() { Id = "standard", Name = "Quadruped", Roles = new() { ["idle"] = id + "-idle", ["walk"] = id + "-walk", ["run"] = id + "-run", ["scrape"] = id + "-scrape", ["head-down"] = id + "-head-down", ["rush"] = id + "-rush", ["brake"] = id + "-brake", ["recover"] = id + "-recover" } }];
        m.Validate(); return m;
    }

    private static PartPaint Spot(float x, float y, float rx, float ry) => new()
    {
        Fill = "#397783",
        Points = [.. Enumerable.Range(0, 12).Select(i => new PuppetPoint(x + rx * MathF.Cos(i * MathF.Tau / 12), y + ry * MathF.Sin(i * MathF.Tau / 12)))]
    };

    public static IReadOnlyList<MotionClip> Clips(CharacterModel model)
    {
        var resolved = ResolvedModel.From(model); var clips = new List<MotionClip>();
        MotionClip Clip(string role, float duration, bool loop)
        {
            var c = ClipAuthoring.New(resolved, model.Id + "-" + role, model.Name + " · " + role, duration, loop); clips.Add(c); return c;
        }
        void Key(MotionClip c, string kind, string target, float t, float x = 0, float y = 0, float angle = 0) => ClipAuthoring.SetKey(c, new(kind, target), t, new(x, y, 0), angle);
        void Rotate(MotionClip c, string target, params float[] angles)
        {
            for (var i = 0; i < angles.Length; i++) Key(c, "rotate", target, c.Duration * i / (angles.Length - 1), angle: angles[i]);
        }
        void Plant(MotionClip c)
        {
            foreach (var chain in model.Chains) c.Contacts.Add(new() { Chain = chain.Id, Start = 0, Finish = c.Duration });
        }
        var idle = Clip("idle", 2, true); Plant(idle);
        Key(idle, "translate", "body", 0); Key(idle, "translate", "body", 1, y: .025f); Key(idle, "translate", "body", 2);
        Rotate(idle, "head", 0, -.025f, 0); Rotate(idle, "tail", 0, .12f, 0);
        foreach (var (role, duration, stride, lift) in new[] { ("walk", 1.2f, .30f, .13f), ("run", .65f, .42f, .19f), ("rush", .48f, .46f, .15f) })
        {
            var c = Clip(role, duration, true); const float stance = .6f; var distance = stride / stance;
            c.Travel.Keys = [new(), new() { Time = duration, X = distance }];
            foreach (var chain in model.Chains)
            {
                var phase = chain.Id is "near-front" or "far-hind" ? 0f : .5f;
                var times = Enumerable.Range(0, 25).Select(i => i / 24f).Concat([(1 - phase) % 1, (stance - phase + 1) % 1]).Append(1f).Distinct().Order().ToArray();
                foreach (var t in times)
                {
                    var p = (t + phase) % 1; var swing = Math.Clamp((p - stance) / (1 - stance), 0, 1);
                    var x = p < stance ? stride * .5f - distance * p : -stride * .5f + stride * swing;
                    Key(c, "target", chain.Id, t * duration, x, p < stance ? 0 : lift * MathF.Sin(swing * MathF.PI));
                }
                // Split a planted interval at the loop boundary so its anchor advances with cycle travel.
                foreach (var (start, finish) in phase == 0 ? [(0f, stance)] : new[] { (0f, stance - phase), (1 - phase, 1f) })
                {
                    var p = (start + phase) % 1;
                    c.Contacts.Add(new() { Chain = chain.Id, Start = start * duration, Finish = finish * duration, Target = new(stride * .5f - distance * p + distance * start, 0) });
                }
            }
            for (var i = 0; i <= 8; i++) Key(c, "translate", "body", duration * i / 8, y: (role == "rush" ? -.055f : 0) + .025f * MathF.Sin(i * MathF.PI / 2));
            Rotate(c, "head", role == "rush" ? -.32f : 0, role == "rush" ? -.36f : -.04f, role == "rush" ? -.32f : 0);
            Rotate(c, "tail", .05f, -.12f, .05f);
        }
        var down = Clip("head-down", .55f, false); Plant(down); Rotate(down, "head", 0, -.40f, -.95f);
        Key(down, "translate", "head", 0); Key(down, "translate", "head", down.Duration, y: -.18f);
        var scrape = Clip("scrape", 1.1f, true);
        foreach (var chain in model.Chains.Where(c => c.Id != "near-front")) scrape.Contacts.Add(new() { Chain = chain.Id, Finish = scrape.Duration });
        Rotate(scrape, "head", -.12f, -.18f, -.12f);
        Key(scrape, "target", "near-front", 0); Key(scrape, "target", "near-front", .25f, .16f, .12f); Key(scrape, "target", "near-front", .45f, .22f); Key(scrape, "target", "near-front", .78f, -.18f); Key(scrape, "target", "near-front", 1.1f);
        scrape.Markers = [new() { Id = "scrape", Time = .45f }];
        var brake = Clip("brake", .65f, false); Rotate(brake, "head", -.32f, .10f, 0); Rotate(brake, "body", 0, .08f, 0);
        Key(brake, "translate", "body", 0, y: -.055f); Key(brake, "translate", "body", .25f, x: .08f, y: -.1f); Key(brake, "translate", "body", .65f);
        foreach (var chain in model.Chains) { Key(brake, "target", chain.Id, 0, .15f); Key(brake, "target", chain.Id, .3f, .22f); Key(brake, "target", chain.Id, .65f); }
        brake.Markers = [new() { Id = "stop", Time = .3f }];
        var recover = Clip("recover", 1.2f, false); Plant(recover); Rotate(recover, "head", -.95f, -.35f, 0);
        Key(recover, "translate", "head", 0, y: -.18f); Key(recover, "translate", "head", recover.Duration); Rotate(recover, "tail", -.15f, .16f, 0);
        foreach (var c in clips) c.Validate(resolved);
        return clips;
    }

    /// <summary>Explicit regeneration; use the editor to save subsequent artwork and animation edits.</summary>
    public static void Write(string root)
    {
        foreach (var dinosaur in new[] { false, true })
        {
            var m = Model(dinosaur ? "triceratops" : "quadruped", dinosaur ? "Triceratops" : "Quadruped", dinosaur);
            Directory.CreateDirectory(Path.Combine(root, "models")); m.Save(Path.Combine(root, "models", m.Id + ".json"));
            Directory.CreateDirectory(Path.Combine(root, "animations"));
            foreach (var c in Clips(m)) c.Save(Path.Combine(root, "animations", c.Id + ".json"));
        }
    }
}
