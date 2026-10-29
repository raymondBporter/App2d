using App2d.Core.Characters;
using App2d.Core.Characters.Authored;

namespace App2d.CharacterSeeds;

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
        var origin = model.Rest[Humanoid.Hips].Z;
        float Z(string id) => model.Rest[id].Z - origin;
        var leftArm = new[] { Z(Humanoid.LeftShoulder), Z(Humanoid.LeftElbow), Z(Humanoid.LeftHand) };
        var rightArm = new[] { Z(Humanoid.RightShoulder), Z(Humanoid.RightElbow), Z(Humanoid.RightHand) };
        var near = leftArm.Average() < rightArm.Average() ? leftArm : rightArm;
        var far = ReferenceEquals(near, leftArm) ? rightArm : leftArm;
        var nearArm = near.Max();
        var farArm = far.Min();
        var nearLeg = MathF.Min(Z(Humanoid.LeftHip), Z(Humanoid.RightHip));
        var farLeg = MathF.Max(Z(Humanoid.LeftHip), Z(Humanoid.RightHip));
        var gap = nearLeg - nearArm;
        if (gap < .01f || farArm - farLeg < .01f)
        {
            throw new InvalidDataException("The wardrobe needs separated arm and hip depth lanes.");
        }
        // Reserve space in front of the detail face for its outline and the entire near arm.
        return new(nearArm, float.Lerp(nearLeg, nearArm, .82f), float.Lerp(nearLeg, nearArm, .60f),
            nearLeg, farLeg, float.Lerp(farLeg, farArm, .60f), farArm, gap * .08f);
    }
}
