namespace App2d.Core.Characters;

/// <summary>Names used by the built-in humanoid rig. Head is both a control and a drawing part.</summary>
public static class Humanoid
{
    public const string Chest = "chest";
    public const string Head = "head";
    public const string LeftShoulder = "left-shoulder";
    public const string LeftHand = "left-hand";
    public const string LeftKnee = "left-knee";
    public const string LeftFoot = "left-foot";
    public const string RightShoulder = "right-shoulder";
    public const string RightHand = "right-hand";
    public const string RightKnee = "right-knee";
    public const string RightFoot = "right-foot";
    public const string LeftShin = LeftKnee + "-" + LeftFoot;
    public const string RightShin = RightKnee + "-" + RightFoot;

    // IK chains, control groups and reference measures.
    public const string LeftArm = "left-arm";
    public const string RightArm = "right-arm";
    public const string LeftLeg = "left-leg";
    public const string RightLeg = "right-leg";

    // Attachment sockets.
    public const string HeadSocket = "head-art";
    public const string LeftHandSocket = "left-hand-art";
    public const string RightHandSocket = "right-hand-art";
    public const string LeftFootSocket = "left-foot-art";
    public const string RightFootSocket = "right-foot-art";
    public const string LeftShinSocket = "left-shin-art";
    public const string RightShinSocket = "right-shin-art";
    public const string BackSocket = "back";
    public const string BackViewSocket = "back-view";
    public const string SwordSocket = "sword-hand";
    public const string GunSocket = "gun-hand";
}
