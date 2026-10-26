namespace App2d.Core.Characters;

/// <summary>Names used by the built-in humanoid rig. Head is both a control and a drawing part.</summary>
public static class Humanoid
{
    // Controls and filled parts.
    public const string Hips = "hips";
    public const string Chest = "chest";
    public const string Head = "head";
    public const string Body = "body";
    public const string LeftShoulder = "left-shoulder";
    public const string LeftElbow = "left-elbow";
    public const string LeftHand = "left-hand";
    public const string LeftHip = "left-hip";
    public const string LeftKnee = "left-knee";
    public const string LeftFoot = "left-foot";
    public const string RightShoulder = "right-shoulder";
    public const string RightElbow = "right-elbow";
    public const string RightHand = "right-hand";
    public const string RightHip = "right-hip";
    public const string RightKnee = "right-knee";
    public const string RightFoot = "right-foot";
    /// <summary>The solved upper-arm bones (P11a): the shoulders stay the authored pivots that frame the grips and carry the clips' turns.</summary>
    public const string LeftUpperArmBone = "left-upper-arm";
    public const string RightUpperArmBone = "right-upper-arm";

    // Drawing parts between controls.
    public const string Neck = Chest + "-" + Head;
    public const string LeftUpperArm = LeftShoulder + "-" + LeftElbow;
    public const string LeftForearm = LeftElbow + "-" + LeftHand;
    public const string LeftThigh = LeftHip + "-" + LeftKnee;
    public const string LeftShin = LeftKnee + "-" + LeftFoot;
    public const string RightUpperArm = RightShoulder + "-" + RightElbow;
    public const string RightForearm = RightElbow + "-" + RightHand;
    public const string RightThigh = RightHip + "-" + RightKnee;
    public const string RightShin = RightKnee + "-" + RightFoot;

    // IK chains, control groups and reference measures.
    public const string LeftArm = "left-arm";
    public const string RightArm = "right-arm";
    public const string LeftLeg = "left-leg";
    public const string RightLeg = "right-leg";
    public const string UpperGroup = "upper";
    public const string LowerGroup = "lower";
    public const string ArmMeasure = "arm";
    public const string LegMeasure = "leg";
    public const string TorsoMeasure = "torso";

    // Attachment sockets.
    public const string HeadSocket = "head-art";
    public const string BodySocket = "body-art";
    public const string LeftHandSocket = "left-hand-art";
    public const string RightHandSocket = "right-hand-art";
    public const string LeftFootSocket = "left-foot-art";
    public const string RightFootSocket = "right-foot-art";
    public const string LeftGripSocket = "left-grip";
    public const string RightGripSocket = "right-grip";
    public const string BackSocket = "back";
    public const string BackViewSocket = "back-view";
    public const string SwordSocket = "sword-hand";
    public const string GunSocket = "gun-hand";
}
