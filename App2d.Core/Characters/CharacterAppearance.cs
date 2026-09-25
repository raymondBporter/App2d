namespace App2d.Core.Characters;

/// <summary>Serializable appearance values shared by the studio and runtime. No widget state.</summary>
public sealed record CharacterAppearance
{
    public float Size { get; set; } = 1;
    public float Width { get; set; } = 1;
    public float Height { get; set; } = 1;
    public float Head { get; set; } = 1;
    public float CornerRadius { get; set; }
    public float BladeLength { get; set; } = 1;
    public bool Flip { get; set; }
    public string Ink { get; set; } = "#16191d";
    public string Fill { get; set; } = "#ffffff";
    public string Face { get; set; } = "none";
    public bool Weapons { get; set; } = true;
    public string Weapon { get; set; } = "sword";
    public float WeaponHeadSize { get; set; } = 1;
    public HeadShape? CustomHead { get; set; }
    public float Yaw { get; set; }
    public float Body { get; set; } = .3f;
    public float HeadWidth { get; set; } = 1;
    public float HeadHeight { get; set; } = 1;
    public float HeadRoundness { get; set; } = 1;
    public float Thickness { get; set; } = .24f;
    public float Spread { get; set; }
    public bool FarTint { get; set; } = true;
    public bool Curves { get; set; } = true;
    public float LegLength { get; set; } = 1;
    public float ArmLength { get; set; } = 1;
    public float HipWidth { get; set; } = 1;
    public float NeckLength { get; set; } = 1;
    public float TailLength { get; set; } = 1;
    public float WingSize { get; set; } = .3f;
    public float Neck { get; set; } = .42f;
    public float Tail { get; set; } = .65f;
    public float Softness { get; set; } = .3f;
    public float LineWidth { get; set; } = .023f;

    // Appearance ranges are deliberately loose: the same value (leg length, head size) spans different
    // sensible ranges per anatomy, so the editor chooses its slider spans per anatomy and validation
    // only rejects values no anatomy can draw.
    private static readonly Limit Scale = new(.01f, 4), Unit = new(0, 1), Proportion = new(0, 4), Angle = new(-180, 180), WeaponHead = new(.1f, 3);
    public void Validate()
    {
        Scale.Check(Size, "size"); Scale.Check(Width, "width"); Scale.Check(Height, "height"); Scale.Check(Head, "head");
        Scale.Check(HeadWidth, "headWidth"); Scale.Check(HeadHeight, "headHeight"); Scale.Check(BladeLength, "bladeLength");
        Unit.Check(CornerRadius, "cornerRadius"); Unit.Check(HeadRoundness, "headRoundness"); Unit.Check(Softness, "softness");
        Proportion.Check(Body, "body"); Proportion.Check(Thickness, "thickness"); Proportion.Check(Spread, "spread");
        Proportion.Check(LegLength, "legLength"); Proportion.Check(ArmLength, "armLength"); Proportion.Check(HipWidth, "hipWidth");
        Proportion.Check(NeckLength, "neckLength"); Proportion.Check(TailLength, "tailLength"); Proportion.Check(WingSize, "wingSize");
        Proportion.Check(Neck, "neck"); Proportion.Check(Tail, "tail"); Proportion.Check(LineWidth, "lineWidth");
        Angle.Check(Yaw, "yaw");
        Limit.Color(Ink, "ink"); Limit.Color(Fill, "fill");
        if (Face is null) throw new InvalidDataException("face is required; use \"none\" for no face.");
        EntityVocabulary.Require(Weapon, EntityVocabulary.Weapons, "weapon");
        WeaponHead.Check(WeaponHeadSize, "weaponHeadSize");
        CustomHead?.Validate();
    }
}
