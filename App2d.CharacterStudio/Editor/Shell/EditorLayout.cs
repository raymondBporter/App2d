using App2d.CharacterStudio.Editing;

namespace App2d.CharacterStudio.Editor;

/// <summary>
/// The author's panel sizes, in unscaled pixels so they follow the UI scale as the design sizes do. The shell scales them and
/// fits them to the window every frame; only a drag writes them back, so a small monitor never shrinks the saved sizes.
/// </summary>
internal sealed class EditorLayout
{
    public float LeftWidth { get; set; } = 250;
    public float RightWidth { get; set; } = 330;
    /// <summary>The asset browser's share of the left column; the outline takes the rest.</summary>
    public float BrowserShare { get; set; } = .5f;
    /// <summary>Timeline heights by workspace name. A workspace without one keeps its view's design height.</summary>
    public Dictionary<string, float> TimelineHeights { get; set; } = [];

    public float TimelineHeight(Workspace mode, float fallback) => TimelineHeights.TryGetValue(mode.ToString(), out var height) ? height : fallback;
    public void SetTimelineHeight(Workspace mode, float height) => TimelineHeights[mode.ToString()] = height;

    /// <summary>
    /// Fits the two side panels into <paramref name="width"/>: both keep their asked widths while the stage between them keeps
    /// <paramref name="minStage"/>, otherwise they shrink in proportion, never below <paramref name="minPanel"/>.
    /// </summary>
    public static (float Left, float Right) FitColumns(float width, float left, float right, float minPanel, float minStage)
    {
        var room = MathF.Max(0, width - minStage);
        var asked = left + right;
        if (asked > room && asked > 0)
        {
            left *= room / asked;
            right *= room / asked;
        }

        return (MathF.Max(minPanel, left), MathF.Max(minPanel, right));
    }
}
