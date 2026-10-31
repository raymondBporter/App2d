using App2d.Core.Characters.Authored;
using App2d.Core.Geometry.Curves;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.CharacterStudio.CharacterSeeds;

/// <summary>
/// Proof library parts for the character-system migration. <see cref="Tail"/> (P7a) is the small non-face animated
/// attachment: three controls in a line, a stroke and a tuft, a looping wag on its base layer and a flick on a tip-only
/// layer with a local clock, two exposed targets and five expression bindings. Tests, the editor smoke and the surface
/// smoke instance it on the Person's body socket.
/// </summary>
public static class PartSeeds
{
    public const string TailId = "tail";

    public static Part Tail(string fitting = PartFittings.Layout)
    {
        var asset = new Part
        {
            Id = TailId,
            Name = "Tail",
            Usage = "tail",
            Payload = PartPayloads.Animated,
            Fitting = fitting,
            Controls =
            [
                new() { Id = "root" },
                new() { Id = "mid", Parent = "root", Rest = new(0, .2f) },
                new() { Id = "tip", Parent = "mid", Rest = new(0, .4f) },
            ],
            Groups = [new() { Id = "tip-group", Targets = ["tip"] }],
            Geometry =
            [
                new() { Id = "stroke", A = "root", B = "tip", Geometry = LineSegmentCurve2D.Create(Vector2.Zero, Vector2.UnitY), Material = new() { StrokeWidth = .03f, Fill = "#8c4d2c" } },
                new() { Id = "tuft", A = "tip", Frame = "tip", Geometry = Ellipse2D.Builder.Create(new(.05f, .05f)), Material = new() { Fill = "#8c4d2c", Outline = new() } },
            ],
            Exposed = ["tip", "tuft"],
            Layers =
            [
                new() { Id = "motion", Clip = "wag" },
                new() { Id = "twitch", Mask = "tip-group", Clock = PartClocks.Local },
            ],
            Expressions =
            {
                ["wag"] = new() { Clip = "wag" },
                ["flick"] = new() { Layer = "twitch", Clip = "flick" },
                ["still"] = new(),
                ["tucked"] = new() { Hidden = true },
                ["peak"] = new() { Clip = "wag", Time = .25f },
            },
            Expression = "wag",
            Clips =
            [
                new()
                {
                    Id = "wag", Name = "Wag", Model = TailId, Duration = 1, Loop = true,
                    Tracks = [new() { Kind = MotionClip.RotateKind, Target = "root", Keys = [new() { Time = 0, Angle = -.5f }, new() { Time = .5f, Angle = .5f }, new() { Time = 1, Angle = -.5f }] }],
                    Markers = [new() { Id = "swish", Time = .5f }],
                },
                new()
                {
                    Id = "flick", Name = "Flick", Model = TailId, Duration = .5f,
                    Tracks = [new() { Kind = MotionClip.TranslateKind, Target = "tip", Keys = [new() { Time = 0 }, new() { Time = .25f, Value = new(.1f, 0, 0) }, new() { Time = .5f }] }],
                },
            ]
        };
        asset.Validate();
        return asset;
    }
}
