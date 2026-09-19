using App2d.Core;
using App2d.Rendering;
using App2d.Rendering.Textures;
using XnaColor = Microsoft.Xna.Framework.Color;
using System.Numerics;

namespace App2d.Gameplay.Player;

public static class PlayerHud2D
{
    public static void Draw(
        Renderer2D renderer,
        int currentHealth,
        int maximumHealth,
        Texture2D weaponTexture,
        string weaponStatus)
    {
        ArgGuard.ThrowIfNull(renderer);
        ArgGuard.ThrowIfNotPositive(maximumHealth);
        ArgGuard.ThrowIfNull(weaponTexture);
        ArgGuard.ThrowIfNullOrWhiteSpace(weaponStatus);

        const float left = 24f;
        const float top = 24f;
        const float panelHeight = 52f;
        const float barLeft = 105f;
        const float barWidth = 245f;
        const float segmentGap = 3f;
        var lifePanel = new ScreenRectangle2D(left, top, left + 350f, top + panelHeight);
        var barBounds = new ScreenRectangle2D(barLeft, top + 13f, barLeft + barWidth, top + 39f);
        var panelColor = new XnaColor(20, 28, 43, 220);
        var accentColor = new XnaColor(113, 224, 255);
        var emptyHealthColor = new XnaColor(7, 12, 20, 235);
        var filledHealthColor = currentHealth > maximumHealth * 0.3f
            ? new XnaColor(72, 224, 121)
            : new XnaColor(245, 76, 76);

        renderer.DrawScreenRoundedRectangle(lifePanel, 9f, panelColor);
        renderer.DrawScreenText("LIFE", new Vector2(left + 14f, top + 35f), XnaColor.White);
        renderer.DrawScreenRoundedRectangle(barBounds, 6f, emptyHealthColor);

        var filledSegments = Math.Clamp(currentHealth, 0, maximumHealth);
        var segmentsBounds = barBounds.InsetBy(3f, 3f);
        var segmentWidth = (segmentsBounds.Width - segmentGap * (maximumHealth - 1)) / maximumHealth;
        for (var segment = 0; segment < maximumHealth; segment++)
        {
            var segmentLeft = segmentsBounds.Left + segment * (segmentWidth + segmentGap);
            var segmentBounds = new ScreenRectangle2D(
                segmentLeft,
                segmentsBounds.Top,
                segmentLeft + segmentWidth,
                segmentsBounds.Bottom);
            renderer.DrawScreenRoundedRectangle(
                segmentBounds,
                3f,
                segment < filledSegments ? filledHealthColor : emptyHealthColor);
        }

        renderer.DrawScreenRoundedRectangle(barBounds, 6f, accentColor, 3f);

        const float weaponTop = top + panelHeight + 10f;
        var weaponBounds = new ScreenRectangle2D(left, weaponTop, left + 70f, weaponTop + 70f);
        DrawWeaponIcon(renderer, weaponTexture, weaponBounds, panelColor, accentColor);
        renderer.DrawScreenLabel(
            weaponStatus,
            new Vector2(weaponBounds.Right + 10f, weaponTop + 9f));
    }

    private static void DrawWeaponIcon(
        Renderer2D renderer,
        Texture2D texture,
        ScreenRectangle2D bounds,
        XnaColor panelColor,
        XnaColor accentColor)
    {
        renderer.DrawScreenRoundedRectangle(bounds, 9f, panelColor);
        renderer.DrawScreenRoundedRectangle(bounds, 9f, accentColor, 3f);
        renderer.DrawScreenTexture(texture, bounds.InsetBy(5f, 5f));
    }
}
