using App2d.Core.Characters.Authored;

namespace App2d.CharacterSeeds;

/// <summary>The declared conversion plans for the shipped faces (P7b): which clip each expression word plays, which group and clip blink, and the default.</summary>
public static class FaceConversionPlans
{
    /// <summary>The curve-face example: its nine clips become expression bindings named as the template's roles; blink loops over the eye group on the actor's clock.</summary>
    public static FaceConversion.Plan CurveFace(string partId) => new(partId, "Curve face")
    {
        Blink = ("eyes", "curve-face-blink"),
        Expressions =
        {
            ["smile"] = ("curve-face-smile", null),
            ["blink"] = ("curve-face-blink", null),
            ["wink"] = ("curve-face-wink", null),
            ["angry"] = ("curve-face-angry", null),
            ["worried"] = ("curve-face-worried", null),
            ["surprised"] = ("curve-face-surprised", null),
            ["crossed-eyes"] = ("curve-face-crossed-eyes", null),
            ["hair-wave"] = ("curve-face-hair-wave", null),
            ["expressions"] = ("curve-face-expressions", null),
            ["neutral"] = ("curve-face-smile", 0),
        },
        OnBlinkLayer = { "blink" },
        Groups = { ["eyes"] = ["left-eye-upper-bend", "left-eye-lower-bend", "right-eye-upper-bend", "right-eye-lower-bend", "left-pupil", "right-pupil"] },
        Default = "neutral",
    };

    public static FaceConversion.Plan For(string modelId, string partId) => modelId switch
    {
        "curve-face" => CurveFace(partId),
        _ => throw new InvalidDataException($"No declared face conversion plan for model '{modelId}'; add one to FaceConversionPlans."),
    };
}
