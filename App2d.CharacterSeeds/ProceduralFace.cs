using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Shapes;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.CharacterSeeds;

/// <summary>A small, rig-independent facial pose of the retired procedural style. Feature coordinates use X right, Y down, over a unit head.</summary>
public readonly record struct ProceduralFacePose(
    float Eyes = 1,
    float BrowTilt = 0,
    float BrowLift = 0,
    float Smile = 0,
    float MouthOpen = 0,
    float MouthWidth = 1,
    float Gaze = 0,
    float Asymmetry = 0,
    bool CrossEyes = false,
    float Brows = 0,
    float WideEyes = 0);

/// <summary>
/// The procedural face generator, retained as an authoring tool only (character-system migration, P7c): the expression
/// vocabulary the Person's clips and gameplay use, the feature layout the old <c>FaceDrawing</c> drew, and <see cref="Bake"/>,
/// which turns them into the ordinary animated part asset <c>parts/face-person.json</c>: point controls, ellipse and curve
/// pieces, one `expressions` clip holding every word as a pose at its own second (step eases, so a held time is exact), a
/// looping `blink` clip on an eye-only layer with the actor's clock, and expression bindings. Nothing in Core or the game
/// draws a face procedurally any more; regenerate the asset with <c>--bake-face output-directory</c> when the vocabulary changes.
/// </summary>
public static class ProceduralFace
{
    public const string AssetId = "face-person";
    public const string ExpressionsClip = "face-person-expressions", BlinkClip = "face-person-blink";
    public const string BlinkLayer = "blink", EyesGroup = "eyes";
    /// <summary>The stroke the old drawing used: six tenths of the Person's line width, unscaled by the head.</summary>
    public const float Stroke = .045f * .6f;
    private const string Ink = "#222b32";

    public static IReadOnlyList<string> Names { get; } = Array.AsReadOnly(
    [
        "relaxed",
        "happy",
        "focused",
        "determined",
        "angry",
        "worried",
        "surprised",
        "panic",
        "hurt",
        "strained",
        "smug",
        "confused",
        "delighted",
        "tired",
        "blink",
        "knocked-out",
        "grumpy"
    ]);

    /// <summary>The words that keep blinking: the old presentation blinked only while relaxed, focused or worried.</summary>
    public static readonly IReadOnlySet<string> Blinking = new HashSet<string>(StringComparer.Ordinal) { "relaxed", "focused", "worried" };

    public static ProceduralFacePose Get(string id) => id switch
    {
        "relaxed" => new(Eyes: .8f, Smile: .25f),
        "happy" => new(Smile: .9f),
        "focused" => new(Eyes: .55f, Gaze: .4f),
        "determined" => new(Eyes: .3f, MouthWidth: .85f),
        "angry" or "grumpy" => new(Eyes: .7f, BrowTilt: 1, Smile: -.65f, Brows: 1),
        "worried" => new(BrowTilt: -.7f, BrowLift: .2f, Smile: -.65f, MouthWidth: .75f, Brows: 1),
        "surprised" => new(Eyes: 1.35f, MouthOpen: .6f, MouthWidth: .55f, WideEyes: 1),
        "panic" => new(Eyes: 1.65f, BrowTilt: -.35f, BrowLift: .8f, MouthOpen: 1, MouthWidth: 1.1f, Brows: 1, WideEyes: 1),
        "hurt" => new(Eyes: .05f, MouthOpen: .25f, MouthWidth: 1.1f, Asymmetry: .4f),
        "strained" => new(Eyes: .35f, Smile: -.35f, MouthWidth: 1.1f),
        "smug" => new(Eyes: .5f, Smile: .8f, Gaze: .5f, Asymmetry: .7f),
        "confused" => new(Eyes: .8f, Smile: -.2f, MouthWidth: .6f, Gaze: -.5f, Asymmetry: 1, Brows: 1),
        "delighted" => new(Eyes: .1f, Smile: 1, MouthOpen: .8f, MouthWidth: 1.2f),
        "tired" => new(Eyes: .3f, Smile: -.4f, MouthWidth: .7f),
        "blink" => new(Eyes: 0, Smile: .25f),
        "knocked-out" => new(MouthOpen: .3f, MouthWidth: .65f, CrossEyes: true),
        _ => throw ArgGuard.CreateInvalid("Unknown face expression: " + id, nameof(id))
    };

    /// <summary>One feature's layout for a pose: where each control sits (unit head, Y up), each piece's fit and whether it shows.</summary>
    private sealed class Layout
    {
        public Dictionary<string, Vector2> Controls { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, Vector2> Fits { get; } = new(StringComparer.Ordinal);
        public HashSet<string> Shown { get; } = new(StringComparer.Ordinal);
    }

    private static Vector2 Up(float x, float yDown) => new(x, -yDown);

    /// <summary>The old drawing's feature layout for a pose, as control positions, piece fits and visibility.</summary>
    private static Layout LayoutOf(ProceduralFacePose pose)
    {
        var layout = new Layout();
        foreach (var (side, name) in new[] { (-1f, "left"), (1f, "right") })
        {
            var x = side * .205f;
            var y = -.12f + side * pose.Asymmetry * .025f;
            var open = MathF.Max(0, pose.Eyes + side * pose.Asymmetry * .15f);
            layout.Controls[name + "-eye"] = Up(x, y);
            layout.Controls[name + "-gaze"] = Up(x + pose.Gaze * .025f + pose.Gaze * .015f, y);
            layout.Controls[name + "-lid-a"] = Up(x - .08f, y);
            layout.Controls[name + "-lid-bend"] = Up(x, y + (pose.Smile > .5f ? -.09f : .045f));
            layout.Controls[name + "-lid-b"] = Up(x + .08f, y);
            if (pose.CrossEyes)
            {
                layout.Shown.Add(name + "-cross");
            }
            else if (open < .18f)
            {
                layout.Shown.Add(name + "-lid");
            }
            else
            {
                var wide = Math.Clamp(pose.WideEyes, 0, 1);
                layout.Controls[name + "-eye"] = Up(x + pose.Gaze * .025f, y);
                var rx = float.Lerp(.045f, .088f, wide);
                var ry = float.Lerp(.065f, .105f, wide) * open;
                layout.Shown.Add(name + "-pupil");
                layout.Fits[name + "-pupil"] = new(rx / .045f, ry / .065f);
                if (wide > .01f)
                {
                    layout.Shown.Add(name + "-white");
                    layout.Fits[name + "-white"] = new(rx / .045f, ry / .065f);
                    var pupil = MathF.Min(.035f / MathF.Max(1, open), ry * .55f);
                    layout.Shown.Add(name + "-dot");
                    layout.Fits[name + "-dot"] = new(pupil / .035f, pupil / .035f);
                }
            }
            var brows = Math.Clamp(pose.Brows, 0, 1);
            var browY = -.29f - pose.BrowLift * .06f + side * pose.Asymmetry * .045f;
            var halfBrow = .095f * MathF.Max(brows, .02f);
            var tilt = side * pose.BrowTilt * .065f * brows;
            layout.Controls[name + "-brow-a"] = Up(x - halfBrow, browY + tilt);
            layout.Controls[name + "-brow-b"] = Up(x + halfBrow, browY - tilt);
            if (brows > .02f)
            {
                layout.Shown.Add(name + "-brow");
            }
        }
        var half = .2f * pose.MouthWidth;
        layout.Controls["mouth-left"] = Up(-half, .2f + pose.Asymmetry * .035f);
        layout.Controls["mouth-bend"] = Up(0, .2f + pose.Smile * .19f);
        layout.Controls["mouth-right"] = Up(half, .2f - pose.Asymmetry * .035f);
        layout.Controls["mouth-oval"] = Up(0, .21f);
        if (pose.MouthOpen > .08f)
        {
            layout.Shown.Add("mouth-open");
            layout.Fits["mouth-open"] = new(half / .2f, (.025f + pose.MouthOpen * .13f) / .1f);
        }
        else
        {
            layout.Shown.Add("mouth");
        }

        return layout;
    }

    private static readonly string[] Pieces = ["left-pupil", "left-white", "left-dot", "left-lid", "left-cross", "left-brow", "right-pupil", "right-white", "right-dot", "right-lid", "right-cross", "right-brow", "mouth", "mouth-open"];

    /// <summary>The Person face as an animated part asset, laid out over a unit head (the instance's fit carries the head's size).</summary>
    public static PartAsset Bake()
    {
        var rest = LayoutOf(Get("relaxed"));
        var asset = new PartAsset
        {
            Id = AssetId,
            Name = "Person face",
            Usage = "face",
            Payload = PartPayloads.Animated,
            Fitting = PartFittings.Stretch,
            Ink = Ink,
            LineWidth = Stroke,
            Expression = "relaxed",
        };
        foreach (var (id, position) in rest.Controls.OrderBy(c => c.Key, StringComparer.Ordinal))
        {
            asset.Controls.Add(new() { Id = id, Rest = new(position.X, position.Y) });
        }

        static PuppetPart Oval(string id, string control, float rx, float ry, string fill, float depth = 0) =>
            new() { Id = id, A = control, Frame = control, Geometry = Ellipse2D.Builder.Create(new(rx, ry)), Material = new() { Fill = fill }, Depth = depth };
        static PuppetPart Curve(string id, string a, string bend, string b, float weight) => new()
        {
            Id = id, A = a, B = b,
            Geometry = QuadraticBezier2D.Builder.Create(Vector2.Zero, new(0, .5f), Vector2.UnitY),
            Bindings = [new() { Point = 0, Control = a }, new() { Point = 1, Control = bend }, new() { Point = 2, Control = b }],
            Material = new() { Fill = Ink, StrokeWidth = Stroke * weight },
        };
        static PuppetPart Line(string id, string a, string b, float weight) => new()
        {
            Id = id, A = a, B = b, Geometry = LineSegmentCurve2D.Create(Vector2.Zero, Vector2.UnitY), Material = new() { Fill = Ink, StrokeWidth = Stroke * weight },
        };
        static PuppetPart Cross(string id, string control)
        {
            // Two bars of the old X, as one cutout: a plus sign of half-length .085 and half-thickness .011, turned 45 degrees.
            const float L = .085f, T = .011f;
            var plus = new Vector2[]
            {
                new(-T, L), new(T, L), new(T, T), new(L, T), new(L, -T), new(T, -T), new(T, -L), new(-T, -L), new(-T, -T), new(-L, -T), new(-L, T), new(-T, T),
            };
            var turned = plus.Select(p => Vector2.Transform(p, Matrix3x2.CreateRotation(MathF.PI / 4))).ToArray();
            return new() { Id = id, A = control, Frame = control, Geometry = SimplePolygon2D.Builder.Create(turned), Material = new() { Fill = Ink } };
        }
        foreach (var side in new[] { "left", "right" })
        {
            asset.Parts.Add(Oval(side + "-pupil", side + "-eye", .045f, .065f, Ink));
            asset.Parts.Add(Oval(side + "-white", side + "-eye", .045f * .77f, .065f * .84f, "#ffffff", -.002f));
            asset.Parts.Add(Oval(side + "-dot", side + "-gaze", .035f, .035f, Ink, -.004f));
            asset.Parts.Add(Curve(side + "-lid", side + "-lid-a", side + "-lid-bend", side + "-lid-b", 1));
            asset.Parts.Add(Cross(side + "-cross", side + "-eye"));
            asset.Parts.Add(Line(side + "-brow", side + "-brow-a", side + "-brow-b", 1.2f));
        }
        asset.Parts.Add(Curve("mouth", "mouth-left", "mouth-bend", "mouth-right", 1.15f));
        asset.Parts.Add(Oval("mouth-open", "mouth-oval", .2f, .1f, Ink));
        asset.Groups.Add(new() { Id = EyesGroup, Targets = ["left-pupil", "right-pupil"] });
        asset.Exposed = [.. asset.Controls.Select(c => c.Id).Concat(asset.Parts.Select(p => p.Id))];

        // One pose per word at its own second; step eases hold each pose exactly at its time.
        var expressions = new MotionClip { Id = ExpressionsClip, Name = "Expressions", Model = AssetId, Duration = Names.Count, Loop = false };
        var tracks = new Dictionary<(string Kind, string Target), ClipTrack>();
        ClipTrack Track(string kind, string target)
        {
            if (!tracks.TryGetValue((kind, target), out var track))
            {
                tracks[(kind, target)] = track = new() { Kind = kind, Target = target };
                expressions.Tracks.Add(track);
            }

            return track;
        }
        for (var i = 0; i < Names.Count; i++)
        {
            var layout = LayoutOf(Get(Names[i]));
            foreach (var (id, position) in layout.Controls)
            {
                var delta = position - rest.Controls[id];
                Track(MotionClip.TranslateKind, id).Keys.Add(new() { Time = i, Value = new(delta.X, delta.Y, 0), Ease = ClipEase.Step });
            }

            foreach (var piece in Pieces)
            {
                var fit = layout.Fits.GetValueOrDefault(piece, Vector2.One);
                Track(MotionClip.FitKind, piece).Keys.Add(new() { Time = i, Value = new(fit.X - 1, fit.Y - 1, 0), Ease = ClipEase.Step });
                Track(MotionClip.ShowKind, piece).Keys.Add(new() { Time = i, Value = new(layout.Shown.Contains(piece) ? 1 : 0, 0, 0), Ease = ClipEase.Step });
            }
        }
        asset.Clips.Add(expressions);
        // The old blink: over a 3.7 s cycle the eyes close and open again between 3.54 and 3.7 s; the pupils carry it as a fit.
        var blink = new MotionClip { Id = BlinkClip, Name = "Blink", Model = AssetId, Duration = 3.7f, Loop = true };
        foreach (var pupil in new[] { "left-pupil", "right-pupil" })
        {
            blink.Tracks.Add(new()
            {
                Kind = MotionClip.FitKind, Target = pupil,
                Keys = [new() { Time = 0, Value = new(0, -.2f, 0) }, new() { Time = 3.54f, Value = new(0, -.2f, 0) }, new() { Time = 3.62f, Value = new(0, -1, 0) }, new() { Time = 3.7f, Value = new(0, -.2f, 0) }],
            });
        }
        asset.Clips.Add(blink);
        asset.Layers.Add(new() { Id = "expression" });
        asset.Layers.Add(new() { Id = BlinkLayer, Mask = EyesGroup, Clock = PartClocks.Local, Clip = BlinkClip });
        for (var i = 0; i < Names.Count; i++)
        {
            asset.Expressions[Names[i]] = new() { Clip = ExpressionsClip, Time = i, Mutes = Blinking.Contains(Names[i]) ? [] : [BlinkLayer] };
        }

        asset.Validate();
        return asset;
    }
}
