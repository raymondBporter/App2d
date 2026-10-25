namespace App2d.CharacterStudio.Editor;

/// <summary>Character editor viewports, handles, and timelines. Edit these byte RGBA colors and rebuild.</summary>
internal static class StudioTheme
{
    // Viewport surfaces and selection.
    public static readonly Color4b ApplicationBackground = new(19, 23, 29);
    public static readonly Color4b ViewportBackground = new(237, 238, 226);
    public static readonly Color4b GroundGuide = new(154, 169, 158);
    public static readonly Color4b Selection = new(232, 169, 55);
    public static readonly Color4b RotationGuide = new(232, 169, 55, 130);
    public static readonly Color4b AppearanceSelection = new(220, 157, 39);
    public static readonly Color4b MutedText = new(90, 100, 96);
    public static readonly Color4b EmptyMessage = new(90, 90, 80);
    public static readonly Color4b ErrorText = new(160, 70, 40);

    // Rig handles, pose comparisons, and weapon gizmos.
    public static readonly Color4b ControlLabel = new(44, 53, 68);
    public static readonly Color4b ControlOutline = new(64, 78, 89);
    public static readonly Color4b ControlFill = new(230, 233, 229);
    public static readonly Color4b TargetControl = new(50, 145, 160);
    public static readonly Color4b Bone = new(84, 179, 160);
    public static readonly Color4b InactiveControl = new(104, 112, 122);
    public static readonly Color4b ModelBoneLink = new(104, 112, 122, 170);
    public static readonly Color4b AnimationBoneLink = new(104, 112, 122, 130);
    public static readonly Color4b OnionSkin = new(135, 128, 83, 110);
    public static readonly Color4b ConstraintSatisfied = new(36, 140, 104);
    public static readonly Color4b ConstraintWarning = new(240, 106, 50);
    public static readonly Color4b SourceMotionLink = new(240, 106, 50, 150);
    public static readonly Color4b SourceMotionPoint = new(240, 106, 50, 200);
    public static readonly Color4b AutoKey = new(200, 60, 50);
    public static readonly Color4b WeaponAxisY = new(55, 155, 100);
    public static readonly Color4b WeaponAxisZ = new(70, 145, 225);
    public static readonly Color4b WeaponCaption = new(55, 105, 120);

    // Animation timeline tracks and keys.
    public static readonly Color4b TimelineRulerBackground = new(34, 42, 52);
    public static readonly Color4b TimelineRulerText = new(120, 130, 140);
    public static readonly Color4b TimelineText = new(200, 205, 210);
    public static readonly Color4b TimelineGuide = new(60, 70, 80);
    public static readonly Color4b TimelineKey = new(150, 200, 210);
    public static readonly Color4b TimelineMarker = new(210, 140, 220);
    public static readonly Color4b TimelineFaceKey = new(240, 200, 90);
    public static readonly Color4b TimelineDiscreteKey = new(90, 200, 240);
    public static readonly Color4b TimelineTintKey = new(210, 150, 240);
    public static readonly Color4b TimelinePlantText = new(160, 200, 170);
    public static readonly Color4b TimelinePlantGuide = new(50, 60, 60);
    public static readonly Color4b TimelinePlantFill = new(36, 140, 104, 200);

    // Entity collision regions and action previews.
    public static readonly Color4b MovementRegion = new(120, 120, 120);
    public static readonly Color4b HurtRegion = new(60, 120, 220);
    public static readonly Color4b AttackFill = new(235, 60, 50, 90);
    public static readonly Color4b AttackOutline = new(220, 40, 30);
    public static readonly Color4b AttackLabel = new(200, 40, 30);
    public static readonly Color4b AttackDisabled = new(220, 120, 110, 160);
    public static readonly Color4b GuardRegion = new(40, 150, 90);
    public static readonly Color4b TriggerRegion = new(230, 150, 30);
    public static readonly Color4b ColliderSelected = new(255, 255, 255);
    public static readonly Color4b WeaponTip = new(240, 200, 40);
    public static readonly Color4b LayerMaskText = new(70, 80, 90);
    public static readonly Color4b ActionTrackBackground = new(222, 225, 214);
    public static readonly Color4b AnticipationFill = new(240, 214, 150);
    public static readonly Color4b RecoveryFill = new(184, 204, 226);
    public static readonly Color4b AttackWindow = new(228, 96, 80);
    public static readonly Color4b AnticipationText = new(90, 70, 30);
    public static readonly Color4b RecoveryText = new(40, 60, 90);
    public static readonly Color4b ActionBlendCurve = new(40, 110, 90);
    public static readonly Color4b ActionEvent = new(40, 90, 160);
    public static readonly Color4b ActionMarker = new(70, 70, 70);
}
