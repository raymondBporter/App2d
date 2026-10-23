using App2d.Contracts.Persons;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Rendering;
using App2d.Core.Rendering.Textures;
using App2d.Core.Validation;
using App2d.Rendering;
using System.Numerics;

namespace App2d.Presentation.Player;

public static class PlayerHud2D
{
    public static void Draw(
        Renderer2D renderer,
        int currentHealth,
        int maximumHealth,
        Texture2D weaponTexture, SpellState2D spells = default, bool charging = false)
    {
        ArgGuard.ThrowIfNull(renderer);
        ArgGuard.Positive(maximumHealth);
        ArgGuard.ThrowIfNull(weaponTexture);

        const float left = 24f;
        const float top = 24f;
        const float panelHeight = 52f;
        const float barLeft = 105f;
        const float barWidth = 245f;
        const float segmentGap = 3f;
        var lifePanel = new ScreenRectangle2D(left, top, left + 350f, top + panelHeight);
        var barBounds = new ScreenRectangle2D(barLeft, top + 13f, barLeft + barWidth, top + 39f);
        var panelColor = GameUiTheme.HudPanel;
        var accentColor = GameUiTheme.HudAccent;
        var emptyHealthColor = GameUiTheme.EmptyBar;
        var filledHealthColor = currentHealth > maximumHealth * 0.3f
            ? GameUiTheme.Health
            : GameUiTheme.LowHealth;

        renderer.DrawScreenRoundedRectangle(lifePanel, 9f, panelColor);
        renderer.DrawScreenText("LIFE", new Vector2(left + 14f, top + 35f), GameUiTheme.Text);
        renderer.DrawScreenRoundedRectangle(barBounds, 6f, emptyHealthColor);

        var filledSegments = Math.Clamp(currentHealth, 0, maximumHealth);
        var segmentsBounds = barBounds.InsetBy(3f, 3f);
        var segmentCount = maximumHealth > 20 ? 1 : maximumHealth;
        var segmentWidth = (segmentsBounds.Width - segmentGap * (segmentCount - 1)) / segmentCount;
        for (var segment = 0; segment < segmentCount; segment++)
        {
            var segmentLeft = segmentsBounds.Left + segment * (segmentWidth + segmentGap);
            var segmentBounds = new ScreenRectangle2D(
                segmentLeft,
                segmentsBounds.Top,
                segmentLeft + segmentWidth * (maximumHealth > 20 ? filledSegments / (float)maximumHealth : 1),
                segmentsBounds.Bottom);
            renderer.DrawScreenRoundedRectangle(
                segmentBounds,
                3f,
                segment < filledSegments ? filledHealthColor : emptyHealthColor);
        }

        renderer.DrawScreenRoundedRectangle(barBounds, 6f, accentColor, 3f);
        if (maximumHealth > 20)
        {
            renderer.DrawScreenText($"{currentHealth}/{maximumHealth}", new Vector2(barLeft + 88, top + 33), GameUiTheme.Text);
        }

        const float weaponTop = top + panelHeight + 10f;
        var weaponBounds = ScreenRectangle2D.FromTopLeftAndSize(new(left, weaponTop), new Size2D(70f, 70f));
        if (!spells.Enabled)
        {
            DrawWeaponIcon(renderer, weaponTexture, weaponBounds, panelColor, accentColor);
        }
        else
        {
            renderer.DrawScreenRoundedRectangle(new(left, weaponTop, left + 350, weaponTop + 92), 9, panelColor);
            renderer.DrawScreenText($"ENERGY  {spells.Energy}/{spells.MaximumEnergy}", new(left + 12, weaponTop + 22), accentColor);
            const float energyLeft = left + 12;
            renderer.DrawScreenRoundedRectangle(new(energyLeft, weaponTop + 30, left + 338, weaponTop + 43), 4, emptyHealthColor);
            var fill = 326f * spells.Energy / spells.MaximumEnergy;
            if (fill > 0)
            {
                renderer.DrawScreenRoundedRectangle(new(energyLeft, weaponTop + 30, energyLeft + fill, weaponTop + 43), 4, accentColor);
            }

            renderer.DrawScreenText("Y / Q  SHOT", new(left + 12, weaponTop + 66), spells.Energy >= spells.ShotCost ? GameUiTheme.Text : GameUiTheme.DisabledText);
            renderer.DrawScreenText("B / R  HEAL", new(left + 182, weaponTop + 66), spells.Energy >= spells.HealCost && currentHealth < maximumHealth ? GameUiTheme.Text : GameUiTheme.DisabledText);
            var progress = charging ? spells.ChargeProgress : spells.IsHealing ? spells.HealProgress : 0;
            if (progress > 0)
            {
                var x = left + (charging ? 12 : 182);
                renderer.DrawScreenRoundedRectangle(new(x, weaponTop + 75, x + 145 * progress, weaponTop + 80), 2, accentColor);
            }
        }
    }

    private static void DrawWeaponIcon(Renderer2D renderer, Texture2D texture, ScreenRectangle2D bounds, Color4b panelColor, Color4b accentColor)
    {
        renderer.DrawScreenRoundedRectangle(bounds, 9f, panelColor);
        renderer.DrawScreenRoundedRectangle(bounds, 9f, accentColor, 3f);
        renderer.DrawScreenTexture(texture, bounds.InsetBy(5f, 5f));
    }
}
