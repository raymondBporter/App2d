using System.Text.Json;
using App2d.Core.Characters;

namespace App2d.Tests.Authored;

public sealed class MotionClipTests
{
    private static readonly ResolvedModel Model = ResolvedModel.From(TestModels.Creature());

    private static MotionClip Valid()
    {
        var clip = TestModels.Clip(Model);
        clip.Travel = new() { Scale = "leg", Keys = [new() { Time = 0 }, new() { Time = 1, X = .4f }] };
        clip.Tracks.Add(new() { Kind = MotionClip.TranslateKind, Target = "body", Keys = [new() { Time = 0 }, new() { Time = .5f, Y = -.1f }] });
        clip.Tracks.Add(new() { Kind = MotionClip.RotateKind, Target = "shoulder", Keys = [new() { Time = 0, Angle = .2f }] });
        clip.Tracks.Add(new() { Kind = MotionClip.TargetKind, Target = "arm", Keys = [new() { Time = 0, X = .1f }] });
        clip.Contacts.Add(new() { Chain = "leg", Start = 0, Finish = .5f });
        clip.Contacts.Add(new() { Chain = "leg", Start = .5f, Finish = 1, Target = new(.2f, 0) });
        return clip;
    }

    [Fact]
    public void AValidClipPassesBothChecksAndRoundTrips()
    {
        var clip = Valid(); clip.Validate(Model);
        var json = clip.ToJson();
        Assert.Equal(json, MotionClip.FromJson(json).ToJson());
        Assert.Throws<JsonException>(() => MotionClip.FromJson(json.Replace("\"contacts\":", "\"contcats\":")));
    }

    public static TheoryData<string, string> Rejections => new()
    {
        { "revision", "revision 2" }, { "model", "for model 'hound'" }, { "solved-control", "solved by IK" },
        { "unknown-chain", "unknown chain" }, { "duplicate-track", "duplicate" }, { "key-order", "strictly increasing" },
        { "key-beyond", "time" }, { "rotate-shape", "angle only" }, { "translate-shape", "x, y and z only" },
        { "contact-frame", "locomotion frame" }, { "contact-scale", "must match the travel scale" },
        { "missing-reference", "no reference measurement" }, { "unknown-measure", "no measure" }, { "contact-overlap", "overlap" },
        { "null-tracks", "null" },
    };

    [Theory, MemberData(nameof(Rejections))]
    public void InvalidOrIncompatibleClipsAreRejected(string edit, string fragment)
    {
        var clip = Valid();
        switch (edit)
        {
            case "revision": clip.StructureRevision = 2; break;
            case "model": clip.Model = "hound"; break;
            case "solved-control": clip.Tracks.Add(new() { Target = "knee", Keys = [new() { X = .1f }] }); break;
            case "unknown-chain": clip.Tracks.Add(new() { Kind = MotionClip.TargetKind, Target = "tail", Keys = [new()] }); break;
            case "duplicate-track": clip.Tracks.Add(new() { Target = "body", Keys = [new()] }); break;
            case "key-order": clip.Tracks[0].Keys.Add(new() { Time = .25f }); break;
            case "key-beyond": clip.Tracks[0].Keys.Add(new() { Time = 1.5f }); break;
            case "rotate-shape": clip.Tracks[1].Keys[0].X = 1; break;
            case "translate-shape": clip.Tracks[0].Keys[0].Angle = 1; break;
            case "contact-frame": clip.Contacts.Add(new() { Chain = "arm", Start = 0, Finish = .2f }); break;
            case "contact-scale": clip.Travel.Scale = "arm"; break;
            case "missing-reference": clip.Reference.Remove("arm"); break;
            case "unknown-measure": clip.Tracks[0].Scale = "tail"; clip.Reference["tail"] = 1; break;
            case "contact-overlap": clip.Contacts[1].Start = .4f; break;
            case "null-tracks": clip.Tracks = null!; break;
        }
        var error = Assert.Throws<InvalidDataException>(() => clip.Validate(Model));
        Assert.Contains(fragment, error.Message);
    }

    [Fact]
    public void RevisionMismatchStatesBothRevisions()
    {
        var clip = Valid(); clip.StructureRevision = 2;
        var message = Assert.Throws<InvalidDataException>(() => clip.Validate(Model)).Message;
        Assert.Contains("revision 2", message); Assert.Contains("revision 1", message);
    }
}
