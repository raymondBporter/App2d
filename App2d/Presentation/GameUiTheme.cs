namespace App2d.Presentation;

/// <summary>Game HUD, level picker, and level-editor overlays. Edit these byte RGBA colors and rebuild.</summary>
internal static class GameUiTheme
{
    public static readonly Color4b Text = Color4b.White;
    public static readonly Color4b DisabledText = Color4b.Gray;
    public static readonly Color4b HudPanel = new(20, 28, 43, 220);
    public static readonly Color4b HudAccent = new(113, 224, 255);
    public static readonly Color4b EmptyBar = new(7, 12, 20, 235);
    public static readonly Color4b Health = new(72, 224, 121);
    public static readonly Color4b LowHealth = new(245, 76, 76);

    public static readonly Color4b MenuPanel = new(20, 28, 43, 245);
    public static readonly Color4b MenuHeading = new(255, 214, 100);
    public static readonly Color4b MenuRow = new(34, 46, 64);
    public static readonly Color4b MenuRowSelected = new(62, 85, 115);
    public static readonly Color4b MenuHelp = new(165, 182, 201);

    public static readonly Color4b EditorSelection = new(255, 214, 64);
    // Faint reference lines keep the cursor and authored tiles readable.
    public static readonly Color4b EditorGrid = new(255, 255, 255, 28);
    public static readonly Color4b EditorDisabled = new(130, 130, 140, 180);
    public static readonly Color4b EditorPlacementPreview = new(105, 245, 180, 220);
}
