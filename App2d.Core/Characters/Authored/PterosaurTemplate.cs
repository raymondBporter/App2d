using App2d.Core.Geometry.Authoring;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Core.Characters.Authored;

/// <summary>A flying starter built entirely from ordinary controls, cutouts, point attachments and motion clips.</summary>
public static class PterosaurTemplate
{
    public static CharacterModel Model(string id = "pterosaur", string name = "Pterosaur")
    {
        var model = new CharacterModel { Id = id, Name = name, Ink = "#302632", LineWidth = .028f };
        void Control(string key, string? parent, float x, float y, float z = 0) => model.Controls.Add(new() { Id = key, Parent = parent, Rest = new(x, y, z) });
        Control("body", null, 0, .70f); Control("body-up", "body", 0, 1.20f);
        Control("head", "body", .30f, .75f); Control("head-up", "head", .30f, 1.25f);
        Control("jaw", "head", .44f, .68f); Control("jaw-up", "jaw", .44f, 1.18f);
        foreach (var (side, sign, z) in new[] { ("near", -1f, -.02f), ("far", 1f, .12f) })
        {
            Control(side + "-shoulder", "body", sign * .10f, .82f, z);
            Control(side + "-bend", side + "-shoulder", sign * .48f, 1.30f, z);
            Control(side + "-tip", side + "-bend", sign * 1.08f, 1.55f, z);
            Control(side + "-leg", "body", sign * .08f, .51f, z);
            Control(side + "-foot", side + "-leg", sign * .09f, .32f, z);
            for (var i = 0; i < 3; i++) Control(side + "-toe-" + i, side + "-foot", sign * .09f + (i - 1) * .045f, .27f + (i == 1 ? -.015f : 0), z);
        }
        PuppetPart Shape(string key, string a, string? b, float w, float h, float x, float y, float depth, string fill, GeometryDefinition2D? geometry = null)
        {
            var part = new PuppetPart { Id = key, A = a, B = b, Width = w, Height = h, OffsetX = x, OffsetY = y, Depth = depth, Material = new() { Fill = fill, Outline = new() }, Geometry = geometry ?? GeometryDefinition2D.CreateDefault(ShapeKinds2D.Ellipse) };
            model.Parts.Add(part); return part;
        }
        PuppetPart Cutout(string key, string a, float w, float h, float x, float y, float depth, string fill, params Vector2[] points)
            => Shape(key, a, a + "-up", w, h, x, y, depth, fill, new SimplePolygon2D.Builder { Vertices = [.. points] });
        foreach (var (side, sign, depth, fill) in new[] { ("far", 1f, .12f, "#9e5f7e"), ("near", -1f, -.02f, "#b56e8b") })
        {
            var wing = Cutout(side + "-wing", "body", 1, 1, 0, 0, depth, fill,
                new(sign * .10f, .12f), new(sign * .31f, -.02f), new(sign * .62f, .12f), new(sign * .79f, .35f), new(sign * 1.08f, .85f), new(sign * .48f, .60f));
            wing.Bindings =
            [
                new() { Point = 0, Control = side + "-shoulder" },
                new() { Point = 1, Control = side + "-shoulder", Toward = side + "-bend" },
                new() { Point = 2, Control = side + "-bend", Toward = side + "-tip" },
                new() { Point = 3, Control = side + "-tip", Toward = side + "-bend" },
                new() { Point = 4, Control = side + "-tip" },
                new() { Point = 5, Control = side + "-bend" },
            ];
            wing.Paint = [new() { Material = new() { Fill = "#80506d" }, Points = [new(sign * .15f, .15f), new(sign * .18f, .15f), new(sign * .53f, .59f), new(sign * .48f, .60f)] }];
            Shape(side + "-leg", side + "-leg", side + "-foot", .025f, .1f, 0, 0, depth, model.Ink, new LineCurveDefinition2D { Start = Vector2.Zero, End = Vector2.UnitY });
            for (var i = 0; i < 3; i++) Shape(side + "-toe-" + i, side + "-foot", side + "-toe-" + i, .021f, .1f, 0, 0, depth, model.Ink, new LineCurveDefinition2D { Start = Vector2.Zero, End = Vector2.UnitY });
        }
        Cutout("tail", "body", .30f, .15f, -.27f, -.05f, -.04f, "#805473", new(-.5f, -.5f), new(.5f, -.25f), new(.5f, .5f));
        Shape("body", "body", "body-up", .45f, .38f, 0, -.01f, -.07f, "#805473");
        Cutout("crest", "head", .18f, .30f, -.075f, .19f, -.15f, "#e4b364", new(-.5f, .5f), new(.5f, -.5f), new(-.4f, -.5f));
        Shape("head", "head", "head-up", .33f, .29f, .015f, .015f, -.16f, "#ba829d");
        Cutout("beak", "head", .35f, .23f, .30f, -.015f, -.17f, "#edbd71", new(-.5f, .5f), new(.5f, -.5f), new(-.5f, -.22f));
        Cutout("lower-beak", "jaw", .26f, .075f, .10f, -.025f, -.18f, "#d99a54", new(-.5f, .5f), new(.5f, -.4f), new(-.5f, -.5f));
        Shape("eye", "head", "head-up", .048f, .069f, .079f, .019f, -.19f, model.Ink).Material = new() { Fill = model.Ink };
        Cutout("brow", "head", .10f, .035f, .075f, .092f, -.19f, model.Ink, new(-.5f, .5f), new(.5f, -.10f), new(.40f, -.5f), new(-.5f, .12f)).Material = new() { Fill = model.Ink };
        model.Sockets = [new() { Id = "head-art", Control = "head", Toward = "head-up" }, new() { Id = "body-art", Control = "body", Toward = "body-up" }];
        model.Groups = [new() { Id = "wings", Targets = ["near-shoulder", "near-bend", "near-tip", "far-shoulder", "far-bend", "far-tip"] }];
        model.HurtLayouts = [new() { Id = "body", Regions = [new() { Id = "body", Controls = ["body", "head"], Pad = .16f }] }];
        model.MotionSets = [new() { Id = "standard", Name = "Flying", Roles = new() { ["idle"] = id + "-hover", ["hover"] = id + "-hover", ["fold"] = id + "-fold", ["dive"] = id + "-dive", ["brake"] = id + "-brake", ["low-recovery"] = id + "-low-recovery", ["reset"] = id + "-reset" } }];
        model.Validate(); return model;
    }

    public static IReadOnlyList<MotionClip> Clips(CharacterModel model)
    {
        var resolved = ResolvedModel.From(model); var clips = new List<MotionClip>();
        MotionClip Clip(string role, float duration, bool loop)
        {
            var c = ClipAuthoring.New(resolved, model.Id + "-" + role, model.Name + " - " + role, duration, loop); clips.Add(c); return c;
        }
        void Rotate(MotionClip c, string target, params float[] angles)
        {
            for (var i = 0; i < angles.Length; i++) ClipAuthoring.SetKey(c, new("rotate", target), c.Duration * i / (angles.Length - 1), Vector3.Zero, angles[i]);
        }
        void Wings(MotionClip c, float[] near, float[] far, float[] fold, float[]? farFold = null)
        {
            Rotate(c, "near-shoulder", near); Rotate(c, "far-shoulder", far);
            Rotate(c, "near-bend", fold); Rotate(c, "far-bend", farFold ?? [.. fold.Select(a => -a)]);
        }
        var hover = Clip("hover", 1, true); Wings(hover, [-.22f, .28f, -.22f], [.22f, -.28f, .22f], [0, -.1f, 0]);
        ClipAuthoring.SetKey(hover, new("translate", "body"), 0, Vector3.Zero);
        ClipAuthoring.SetKey(hover, new("translate", "body"), .5f, new(0, .05f, 0));
        ClipAuthoring.SetKey(hover, new("translate", "body"), 1, Vector3.Zero); Rotate(hover, "head", -.08f, -.12f, -.08f);
        var fold = Clip("fold", .48f, false); Wings(fold, [-.22f, -.15f, -.15f], [.22f, 1.3f, 1.3f], [0, -1.1f, -1.1f], [0, .1f, .1f]);
        Rotate(fold, "body", 0, -.22f, -.40f); Rotate(fold, "jaw", 0, -.5f, 0);
        fold.Markers = [new() { Id = "screech", Time = .20f }, new() { Id = "commit", Time = .40f }];
        var dive = Clip("dive", .65f, false); Wings(dive, [-.15f, -.15f], [1.3f, 1.3f], [-1.1f, -1.1f], [.1f, .1f]); Rotate(dive, "body", -.40f, -.65f);
        Rotate(dive, "near-leg", .6f, .6f); Rotate(dive, "far-leg", .6f, .6f);
        var brake = Clip("brake", .32f, false); Wings(brake, [-.15f, .10f, .28f], [1.3f, .4f, -.28f], [-1.1f, -.3f, 0], [.1f, .05f, 0]); Rotate(brake, "body", -.65f, -.12f, 0);
        Rotate(brake, "near-leg", .6f, .3f, .1f); Rotate(brake, "far-leg", .6f, .3f, .1f); brake.Markers = [new() { Id = "open", Time = .16f }];
        var recovery = Clip("low-recovery", 1, true); Wings(recovery, [.28f, .12f, .28f], [-.28f, -.12f, -.28f], [0, -.08f, 0]);
        Rotate(recovery, "body", 0, -.02f, 0); Rotate(recovery, "near-leg", .1f, .15f, .1f); Rotate(recovery, "far-leg", .1f, .15f, .1f);
        var reset = Clip("reset", 1.2f, false); Wings(reset, [.28f, -.22f, .28f, -.22f], [-.28f, .22f, -.28f, .22f], [0, -.08f, 0]); Rotate(reset, "body", 0, .12f, .06f, 0);
        foreach (var clip in clips) clip.Validate(resolved);
        return clips;
    }

    public static void Write(string root)
    {
        var model = Model(); Directory.CreateDirectory(Path.Combine(root, "models")); model.Save(Path.Combine(root, "models", model.Id + ".json"));
        Directory.CreateDirectory(Path.Combine(root, "animations")); foreach (var clip in Clips(model)) clip.Save(Path.Combine(root, "animations", clip.Id + ".json"));
    }
}
