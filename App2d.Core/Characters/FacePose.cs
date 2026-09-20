namespace App2d.Core.Characters;

/// <summary>Small, rig-independent facial pose. Feature coordinates use X right, Y down.</summary>
public readonly record struct FacePose(
    float Eyes = 1, float BrowTilt = 0, float BrowLift = 0, float Smile = 0,
    float MouthOpen = 0, float MouthWidth = 1, float Gaze = 0,
    float Asymmetry = 0, bool CrossEyes = false, float Brows = 0, float WideEyes = 0)
{
    public static FacePose Blend(FacePose a, FacePose b, float t) => new(
        float.Lerp(a.Eyes, b.Eyes, t), float.Lerp(a.BrowTilt, b.BrowTilt, t),
        float.Lerp(a.BrowLift, b.BrowLift, t), float.Lerp(a.Smile, b.Smile, t),
        float.Lerp(a.MouthOpen, b.MouthOpen, t), float.Lerp(a.MouthWidth, b.MouthWidth, t),
        float.Lerp(a.Gaze, b.Gaze, t), float.Lerp(a.Asymmetry, b.Asymmetry, t),
        b.CrossEyes, float.Lerp(a.Brows, b.Brows, t), float.Lerp(a.WideEyes, b.WideEyes, t));
}

public static class FaceExpressions
{
    public static IReadOnlyList<string> Names { get; } = Array.AsReadOnly(new[]
    {
        "relaxed", "happy", "focused", "determined", "angry", "worried", "surprised", "panic",
        "hurt", "strained", "smug", "confused", "delighted", "tired", "blink", "knocked-out"
    });

    public static bool Contains(string id) => Names.Contains(id) || id == "grumpy";

    public static FacePose Get(string id) => id switch
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
        _ => throw new ArgumentException("Unknown face expression: " + id, nameof(id))
    };

    public static FacePose Blink(FacePose pose, double seconds)
    {
        var phase = seconds % 3.7;
        return phase < 3.54 || pose.CrossEyes ? pose : pose with
        { Eyes = pose.Eyes * (float)Math.Abs((phase - 3.62) / .08) };
    }
}
