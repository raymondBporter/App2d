namespace App2d.Core.Characters.Authored;

/// <summary>
/// Local Z slots for the Person's waist, derived from its built rest pose. Positive Z is away from the camera.
/// Garment surfaces and details occupy the gaps between legs and arms; animation retains its authored XYZ motion.
/// </summary>
public readonly record struct PersonWardrobeDepths(float NearArm, float FrontDetail, float WrapFront,
    float NearLeg, float FarLeg, float WrapBack, float FarArm, float DetailThickness)
{
    public float WrapCenter => (WrapFront + WrapBack) / 2;
    public float WrapThickness => WrapBack - WrapFront;
    public float DetailCenter => FrontDetail + DetailThickness / 2;

    public static PersonWardrobeDepths From(ResolvedModel model)
    {
        var origin = model.Rest["hips"].Z;
        float Z(string id) => model.Rest[id].Z - origin;
        var leftArm = new[] { Z("left-shoulder"), Z("left-elbow"), Z("left-hand") };
        var rightArm = new[] { Z("right-shoulder"), Z("right-elbow"), Z("right-hand") };
        var near = leftArm.Average() < rightArm.Average() ? leftArm : rightArm;
        var far = ReferenceEquals(near, leftArm) ? rightArm : leftArm;
        var nearArm = near.Max(); var farArm = far.Min();
        var nearLeg = MathF.Min(Z("left-hip"), Z("right-hip"));
        var farLeg = MathF.Max(Z("left-hip"), Z("right-hip"));
        var gap = nearLeg - nearArm;
        if (gap < .01f || farArm - farLeg < .01f)
            throw new InvalidDataException("The wardrobe needs separated arm and hip depth lanes.");
        // Reserve space in front of the detail face for its outline and the entire near arm.
        return new(nearArm, float.Lerp(nearLeg, nearArm, .82f), float.Lerp(nearLeg, nearArm, .60f),
            nearLeg, farLeg, float.Lerp(farLeg, farArm, .60f), farArm, gap * .08f);
    }
}
