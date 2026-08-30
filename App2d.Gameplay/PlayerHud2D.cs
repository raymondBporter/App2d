using System.Numerics;
using App2d.Core;
using App2d.Rendering;
using App2d.Rendering.Textures;
using SkiaSharp;

namespace App2d.Gameplay;

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
        var healthRatio = Math.Clamp(currentHealth / (float)maximumHealth, 0f, 1f);
        var lifePanel = new SKRect(left, top, left + 350f, top + panelHeight);
        var barBounds = new SKRect(barLeft, top + 13f, barLeft + barWidth, top + 39f);
        var panelColor = new SKColor(20, 28, 43, 220);
        var accentColor = new SKColor(113, 224, 255);

        renderer.DrawScreenRoundedRectangle(lifePanel, 9f, panelColor);
        renderer.DrawScreenText("LIFE", new Vector2(left + 14f, top + 35f), SKColors.White);
        renderer.DrawScreenRoundedRectangle(barBounds, 6f, new SKColor(7, 12, 20, 235));
        if (healthRatio > 0f)
        {
            var fillBounds = new SKRect(
                barBounds.Left + 3f,
                barBounds.Top + 3f,
                barBounds.Left + 3f + (barBounds.Width - 6f) * healthRatio,
                barBounds.Bottom - 3f);
            renderer.DrawScreenRoundedRectangle(
                fillBounds,
                4f,
                healthRatio > 0.3f
                    ? new SKColor(72, 224, 121)
                    : new SKColor(245, 76, 76));
        }
        renderer.DrawScreenRoundedRectangle(barBounds, 6f, accentColor, 3f);

        const float weaponTop = top + panelHeight + 10f;
        var weaponBounds = new SKRect(left, weaponTop, left + 70f, weaponTop + 70f);
        DrawWeaponIcon(renderer, weaponTexture, weaponBounds, panelColor, accentColor);
        renderer.DrawScreenLabel(
            weaponStatus,
            new Vector2(weaponBounds.Right + 10f, weaponTop + 9f));
    }

    private static void DrawWeaponIcon(
        Renderer2D renderer,
        Texture2D texture,
        SKRect bounds,
        SKColor panelColor,
        SKColor accentColor)
    {
        renderer.DrawScreenRoundedRectangle(bounds, 9f, panelColor);
        renderer.DrawScreenRoundedRectangle(bounds, 9f, accentColor, 3f);
        renderer.DrawScreenTexture(texture, SKRect.Inflate(bounds, -5f, -5f));
    }
}
