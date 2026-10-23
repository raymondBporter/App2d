using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Tests.Authored;

internal static class TestModels
{
    /// <summary>A body with a two-bone arm keyed in the shoulder's frame and a two-bone leg keyed in the locomotion frame. Rest IK reproduces rest exactly.</summary>
    public static CharacterModel Creature() => new()
    {
        Id = "creature",
        Name = "Creature",
        Controls =
        [
            new() { Id = "body", Rest = new(0, 1), Scale = "leg" },
            new() { Id = "shoulder", Parent = "body", Rest = new(0, 1.5f) },
            new() { Id = "elbow", Parent = "shoulder", Rest = new(.4f, 1.3f) },
            new() { Id = "hand", Parent = "elbow", Rest = new(.8f, 1.5f) },
            new() { Id = "hip", Parent = "body", Rest = new(0, 1) },
            new() { Id = "knee", Parent = "hip", Rest = new(.1f, .5f) },
            new() { Id = "foot", Parent = "knee", Rest = new(0, 0) },
        ],
        Chains =
        [
            new() { Id = "arm", Root = "shoulder", Joint = "elbow", End = "hand", Bend = -1, Frame = "shoulder", Scale = "arm" },
            new() { Id = "leg", Root = "hip", Joint = "knee", End = "foot", Bend = 1, Scale = "leg" },
        ],
        Measures = [new() { Id = "leg", Path = ["hip", "knee", "foot"] }, new() { Id = "arm", Path = ["shoulder", "elbow", "hand"] }],
        Parts =
        [
            new() { Id = "torso", A = "hip", B = "shoulder", Geometry = RoundedRectangle2D.Builder.FromSize(.4f, .4f, .05f), Material = new() { Fill = "#fff8e7", Outline = new() } },
            new() { Id = "upper-arm", A = "shoulder", B = "elbow", Geometry = new LineSegmentCurve2D { Start = new(0, 0), End = new(0, 1) }.Validated(), Material = new() },
        ],
    };

    /// <summary>
    /// The nonhumanoid mechanical linkage the migration plan keeps in shared tests (P0): a crank on a hub drives a two-bar
    /// rod through the point IK chain "rod", whose root is a fixed piston. Rotating the hub a full turn carries the pin
    /// round, and the rod can only follow for part of the circle, so one clip exercises reached and unreachable (full-reach)
    /// targets, and <see cref="LinkageReach"/> adds a zero-length one, without any humanoid assumption.
    /// </summary>
    public static CharacterModel Linkage() => new()
    {
        Id = "linkage",
        Name = "Crank linkage",
        Controls =
        [
            new() { Id = "frame", Rest = new(0, 0) },
            new() { Id = "hub", Parent = "frame", Rest = new(0, 1) },
            new() { Id = "crank-pin", Parent = "hub", Rest = new(.4f, 1) },
            new() { Id = "piston", Parent = "frame", Rest = new(1.6f, 1) },
            new() { Id = "rod-elbow", Parent = "piston", Rest = new(1, 1.3f) },
            new() { Id = "rod-end", Parent = "rod-elbow", Rest = new(.4f, 1) },
        ],
        Chains = [new() { Id = "rod", Root = "piston", Joint = "rod-elbow", End = "rod-end", Bend = 1 }],
        Parts =
        [
            new() { Id = "bed", A = "frame", Geometry = Rectangle2D.Builder.FromSize(2.6f, .12f, new(.8f, 0)), Material = new() { Fill = "#8a8f94", Outline = new() }, Depth = .05f },
            new() { Id = "hub-disc", A = "hub", Geometry = Ellipse2D.Builder.Create(new(.5f, .5f)), Material = new() { Fill = "#c9d3d8", Outline = new() }, Depth = .02f },
            new() { Id = "crank", A = "hub", B = "crank-pin", Geometry = new LineSegmentCurve2D { Start = new(0, 0), End = new(0, 1) }.Validated(), Material = new() { Fill = "#3b4a52" }, Depth = -.01f },
            new() { Id = "rod-upper", A = "piston", B = "rod-elbow", Geometry = new LineSegmentCurve2D { Start = new(0, 0), End = new(0, 1) }.Validated(), Material = new() { Fill = "#3b4a52" }, Depth = -.02f },
            new() { Id = "rod-lower", A = "rod-elbow", B = "rod-end", Geometry = new LineSegmentCurve2D { Start = new(0, 0), End = new(0, 1) }.Validated(), Material = new() { Fill = "#3b4a52" }, Depth = -.02f },
            new() { Id = "piston-head", A = "piston", Geometry = RoundedRectangle2D.Builder.FromSize(.3f, .3f, .05f), Material = new() { Fill = "#d6c9a5", Outline = new() }, Depth = -.03f },
        ],
    };

    /// <summary>One-second looping full turn of the hub; the rod's target follows the pin through eight linear keys.</summary>
    public static MotionClip LinkageCrank(ResolvedModel linkage)
    {
        var clip = new MotionClip { Id = "linkage-crank", Name = "Linkage / crank", Model = linkage.Base.Id, Loop = true };
        clip.Tracks.Add(new()
        {
            Kind = MotionClip.RotateKind,
            Target = "hub",
            Keys = [new() { Time = 0 }, new() { Time = .5f, Angle = MathF.PI }, new() { Time = 1, Angle = MathF.Tau }],
        });
        var target = new ClipTrack { Kind = MotionClip.TargetKind, Target = "rod" };
        for (var i = 0; i <= 8; i++)
        {
            var theta = MathF.Tau * i / 8;
            target.Keys.Add(new() { Time = i / 8f, Value = new(.4f * MathF.Cos(theta) - .4f, .4f * MathF.Sin(theta), 0) });
        }

        clip.Tracks.Add(target);
        clip.Validate(linkage);
        return clip;
    }

    /// <summary>The rod target at its own root (zero length), then far past full reach, then back at rest.</summary>
    public static MotionClip LinkageReach(ResolvedModel linkage)
    {
        var clip = new MotionClip { Id = "linkage-reach", Name = "Linkage / reach", Model = linkage.Base.Id };
        clip.Tracks.Add(new()
        {
            Kind = MotionClip.TargetKind,
            Target = "rod",
            Keys = [new() { Time = 0, Value = new(1.2f, 0, 0) }, new() { Time = .5f, Value = new(-1.5f, 0, 0) }, new() { Time = 1 }],
        });
        clip.Validate(linkage);
        return clip;
    }

    /// <summary>An empty one-second looping clip authored against this model's own measures, so every ratio is 1.</summary>
    public static MotionClip Clip(ResolvedModel model) => new()
    {
        Id = "test-clip",
        Name = "Test",
        Model = model.Base.Id,
        Loop = true,
        Reference = model.Measures.ToDictionary(p => p.Key, p => p.Value),
    };

    public static void Near(Vector3 expected, Vector3 actual, float tolerance = 1e-4f, string what = "") =>
        Assert.True(Vector3.Distance(expected, actual) <= tolerance, $"{what}: expected {expected}, found {actual}");

    /// <summary>
    /// Two full cycles at 240 samples each: every control's world position must match the prototype evaluator.
    /// Instants exactly at a contact's finish are skipped: the prototype holds contacts over a closed interval,
    /// the authored runtime over a half-open one, so they release the foot one instant apart by design.
    /// </summary>
    public static void AssertMatchesPrototype(PuppetDefinition puppet, ResolvedModel model, MotionClip clip)
    {
        var motion = puppet.Motions[0];
        for (var i = 0; i <= 480; i++)
        {
            var seconds = i * motion.Duration / 240.0;
            var cycleTime = seconds % motion.Duration;
            if (motion.Contacts.Any(c => Math.Abs(cycleTime - c.Finish) < 1e-5))
            {
                continue;
            }

            var expected = PuppetPose.Sample(puppet, motion, seconds, repeat: true);
            var actual = PoseEvaluator.Sample(model, clip, seconds, repeat: true);
            foreach (var control in puppet.Controls)
            {
                Near(expected.World(control.Id), actual.World(control.Id), 1e-4f, $"{control.Id} at {seconds:F4}s");
            }
        }
    }

    public static string AuthoredRoot
    {
        get
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            {
                var path = Path.Combine(directory.FullName, "Assets", "Characters");
                if (File.Exists(Path.Combine(path, "catalog.json")))
                {
                    return Path.Combine(path, "authored");
                }
            }
            throw new DirectoryNotFoundException("Character assets not found.");
        }
    }

    public static void CopyModelTemplates(string root)
    {
        var target = Path.Combine(root, "templates");
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(Path.Combine(AuthoredRoot, "templates"), "*.json"))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }
    }
}
