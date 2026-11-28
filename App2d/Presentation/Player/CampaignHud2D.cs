using App2d.Contracts.World;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Rendering;
using App2d.Rendering;

namespace App2d.Presentation.Player;

internal enum CampaignScreen
{
    Briefing,
    Playing,
    Paused,
    Cleared,
}

internal static class CampaignHud2D
{
    public const string Title = "Ember Crossing";
    private const float PanelWidth = 530f;
    private const float PanelHeight = 294f;
    private static readonly ScreenRectangle2D Action = new(28f, 212f, PanelWidth - 28f, 260f);
    private static readonly Color4b Ink = new(18, 26, 31, 245);
    private static readonly Color4b Copper = new(250, 183, 111);
    private static readonly Color4b Muted = new(180, 198, 198);
    private static readonly Color4b Scrim = new(9, 14, 20, 150);

    public static bool HitsAction(Vector2f viewport, Vector2f pointer)
    {
        var (origin, scale) = Layout(viewport);
        if (scale <= 0f)
        {
            return false;
        }

        var local = (pointer - origin) / scale;
        return Action.Contains(local.X, local.Y);
    }

    public static void DrawObjective(Renderer2D renderer, WorldZone2D? zone, bool exitOpen, bool gamepad,
        GoalGuardianState2D? guardian, Vector2f playerPosition)
    {
        var isNearBoss = zone?.Id == "wasteland" && guardian is { HitPoints: > 0 } boss &&
            MathF.Abs(playerPosition.X - boss.Position.X) < 480f;
        var (objective, hint) = zone?.Id switch
        {
            "prehistoric" => ("Cross the ravine", gamepad ? "A jump  /  X strike" : "Space jump  /  F strike"),
            "medieval" => ("Ride the bridge ferry", gamepad ? "Up climbs  /  RT dashes" : "W climbs  /  Shift dashes"),
            "wasteland" => (exitOpen ? "Reach the signal flag" : "Defeat the Centaur",
                isNearBoss ? "Jump the rush. Punish the skid." : gamepad ? "Hold Y to charge  /  B to heal" : "Hold Q to charge  /  R to heal"),
            _ => ("Reach the signal flag", string.Empty),
        };
        var size = renderer.View.TargetBounds.Size;
        var width = MathF.Min(306f, size.X - 24f);
        var height = isNearBoss ? 131f : 105f;
        if (width <= 28f || size.Y < height + 20f)
        {
            return;
        }

        var textScale = width / 306f;
        var x = MathF.Max(12f, size.X - width - 24f);
        var y = size.X >= 720f ? 24f : size.Y - height - 16f;
        renderer.DrawScreenRoundedRectangle(new(x, y, x + width, y + height), 8f, Ink);
        renderer.DrawScreenText(zone?.Name ?? Title, new(x + 14f, y + 22f), Copper, .8f * textScale);
        renderer.DrawScreenText(objective, new(x + 14f, y + 48f), GameUiTheme.Text, .92f * textScale);
        if (hint.Length > 0)
        {
            var hintScale = MathF.Min(.72f * textScale, (width - 28f) / renderer.MeasureScreenText(hint));
            renderer.DrawScreenText(hint, new(x + 14f, y + 71f), Muted, hintScale);
        }
        if (isNearBoss && guardian is { } health)
        {
            var left = x + 14f;
            var trackWidth = width - 28f;
            var fill = trackWidth * health.HitPoints / health.MaximumHitPoints;
            renderer.DrawScreenRoundedRectangle(new(left, y + 83f, left + trackWidth, y + 94f), 4f, GameUiTheme.EmptyBar);
            renderer.DrawScreenRoundedRectangle(new(left, y + 83f, left + fill, y + 94f), 4f, Copper);
        }

        renderer.DrawScreenText(gamepad ? "Menu  Pause" : "P  Pause", new(x + 14f, y + height - 13f), Muted, .68f * textScale);
    }

    public static void DrawScreen(Renderer2D renderer, CampaignScreen screen, bool hasCheckpoint, bool savedClear, bool gamepad)
    {
        var size = renderer.View.TargetBounds.Size;
        var (origin, scale) = Layout(size);
        if (scale <= 0f)
        {
            return;
        }

        var (label, heading, body, action) = screen switch
        {
            CampaignScreen.Briefing => ("EXPEDITION", Title,
                "Cross Fernbank and Stonewatch.\nDefeat the Centaur and raise the signal.",
                hasCheckpoint ? "Continue from checkpoint" : "Begin expedition"),
            CampaignScreen.Paused => (Title, "Expedition paused",
                "Checkpoint lanterns restore life and save your place.", "Resume expedition"),
            CampaignScreen.Cleared => (Title, "Signal lit",
                savedClear ? "The Centaur fell. The crossing is open." : "The crossing is open. Completion could not be saved.",
                "Play again"),
            _ => throw new ArgumentOutOfRangeException(nameof(screen)),
        };
        renderer.DrawScreenRoundedRectangle(new(0f, 0f, size.X, size.Y), 0f, Scrim);
        renderer.DrawScreenRoundedRectangle(Box(0f, 0f, PanelWidth, PanelHeight), 12f * scale, Ink);
        renderer.DrawScreenRoundedRectangle(Box(28f, 29f, 64f, 32f), 0f, Copper);
        renderer.DrawScreenText(label, At(28f, 54f), Copper, .9f * scale);
        renderer.DrawScreenText(heading, At(28f, 106f), GameUiTheme.Text, 1.95f * scale);
        renderer.DrawScreenTextWrapped(body, At(28f, 149f), (PanelWidth - 56f) * scale, Muted, .92f * scale, maxLines: 2);
        renderer.DrawScreenRoundedRectangle(Box(Action.Left, Action.Top, Action.Right, Action.Bottom), 6f * scale, Copper);
        renderer.DrawScreenText(action, At(44f, 243f), Ink, scale);
        var key = gamepad ? "A" : "Enter";
        var keyWidth = renderer.MeasureScreenText(key, .73f * scale) / scale;
        var chipLeft = Action.Right - keyWidth - 32f;
        renderer.DrawScreenRoundedRectangle(Box(chipLeft, 223f, Action.Right - 10f, 249f), 4f * scale, Ink);
        renderer.DrawScreenText(key, At(chipLeft + 11f, 243f), Copper, .73f * scale);

        Vector2f At(float x, float y) => origin + new Vector2f(x, y) * scale;
        ScreenRectangle2D Box(float left, float top, float right, float bottom) =>
            new(origin.X + left * scale, origin.Y + top * scale, origin.X + right * scale, origin.Y + bottom * scale);
    }

    private static (Vector2f Origin, float Scale) Layout(Vector2f size)
    {
        var scale = MathF.Min(1.25f, MathF.Min((size.X - 36f) / PanelWidth, (size.Y - 36f) / PanelHeight));
        return ((size - new Vector2f(PanelWidth, PanelHeight) * scale) * .5f, scale);
    }
}
