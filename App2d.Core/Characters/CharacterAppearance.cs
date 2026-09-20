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

    public void Validate()
    {
        static void Range(float value, float min, float max)
        { if (!float.IsFinite(value) || value < min || value > max) throw new InvalidDataException("Appearance value outside supported range."); }
        foreach (var value in new[] { Size, Width, Height, Head, HeadWidth, HeadHeight, BladeLength }) Range(value, .01f, 4);
        foreach (var value in new[] { CornerRadius, HeadRoundness, Softness }) Range(value, 0, 1);
        foreach (var value in new[] { Body, Thickness, Spread, LegLength, ArmLength, HipWidth, NeckLength, TailLength, WingSize, Neck, Tail, LineWidth }) Range(value, 0, 4);
        Range(Yaw, -180, 180);
        foreach (var color in new[] { Ink, Fill })
            if (color is null || color.Length != 7 || color[0] != '#' || !uint.TryParse(color.AsSpan(1), System.Globalization.NumberStyles.HexNumber, null, out _))
                throw new InvalidDataException("Invalid appearance color.");
        if (Face is null) throw new InvalidDataException("Missing face selection.");
        if (Weapon is not ("sword" or "rapier" or "mace" or "hammer" or "pistol")) throw new InvalidDataException("Unknown weapon selection.");
        Range(WeaponHeadSize, .1f, 3);
        CustomHead?.Validate();
    }
}
