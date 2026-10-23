namespace App2d.Core.Rendering;

/// <summary>Shared world diagnostics and screen labels. Colors use straight alpha; edit and rebuild to change the palette.</summary>
public static class DebugTheme
{
    public static readonly Color4b Background = new(24, 27, 36);
    public static readonly Color4b LabelBackground = new(20, 28, 43, 220);
    public static readonly Color4b LabelText = Color4b.White;

    public static readonly Color4b GridAxis = new(255, 255, 255, 85);
    public static readonly Color4b GridMajor = new(255, 255, 255, 35);
    public static readonly Color4b GridMinor = new(255, 255, 255, 18);
    public static readonly Color4b ColliderFill = new(70, 245, 190, 55);
    public static readonly Color4b ColliderOutline = new(70, 245, 190, 235);
    public static readonly Color4b AttackFill = new(255, 82, 92, 70);
    public static readonly Color4b AttackOutline = new(255, 105, 70, 245);

    public static readonly Color4b RunningJump = new(255, 92, 137, 220);
    public static readonly Color4b StandingJump = new(110, 235, 255, 220);
    public static readonly Color4b BallisticsReference = new(255, 220, 65);
    public static readonly Color4b BallisticsWorld = new(255, 100, 180);
    public const byte BallisticsRayAlpha = 80;
    public static readonly Color4b ActiveZone = Color4b.Gold;
    public static readonly Color4b ZoneOutline = Color4b.Cyan;
}
