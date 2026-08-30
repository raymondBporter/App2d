using App2d.Core;
using System.Numerics;
using System.Text.Json;

namespace App2d.Gameplay;

internal static class PlayerGeometryAssets2D
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public static PlayerGeometry2D Load(string contentRoot)
    {
        ArgGuard.ThrowIfNullOrWhiteSpace(contentRoot);

        var path = Path.Combine(contentRoot, "characters", "player-geometry.json");
        if (!File.Exists(path))
            throw new FileNotFoundException("Player geometry manifest was not found.", path);

        var manifest = JsonSerializer.Deserialize<PlayerGeometryManifest>(
            File.ReadAllText(path),
            JsonOptions) ?? throw new InvalidDataException(
                $"Player geometry manifest is empty: {path}");
        Validate(manifest, path);

        return new PlayerGeometry2D(
            new Vector2(manifest.VisualSize.Width, manifest.VisualSize.Height),
            manifest.FootAnchorYFraction,
            new Vector2(
                manifest.StandingCollider.Size.Width,
                manifest.StandingCollider.Size.Height),
            manifest.StandingCollider.CenterOffsetX);
    }

    private static void Validate(PlayerGeometryManifest manifest, string path)
    {
        if (manifest.SchemaVersion != 1)
        {
            throw new InvalidDataException(
                $"Unsupported player geometry schema version {manifest.SchemaVersion}: {path}");
        }

        var canvasSize = new Vector2(
            manifest.CanvasSizePixels.Width,
            manifest.CanvasSizePixels.Height);
        var visualSize = new Vector2(
            manifest.VisualSize.Width,
            manifest.VisualSize.Height);
        var standingSize = new Vector2(
            manifest.StandingCollider.Size.Width,
            manifest.StandingCollider.Size.Height);

        if (!IsPositive(canvasSize) ||
            !IsPositive(visualSize) ||
            !IsPositive(standingSize))
        {
            throw new InvalidDataException(
                $"Player canvas, visual, and collider sizes must be positive: {path}");
        }

        var canvasAspect = canvasSize.X / canvasSize.Y;
        var visualAspect = visualSize.X / visualSize.Y;
        if (MathF.Abs(canvasAspect - visualAspect) > 0.001f)
        {
            throw new InvalidDataException(
                $"Player visual size must preserve the authored canvas aspect ratio: {path}");
        }

        if (!float.IsFinite(manifest.FootAnchorYFraction) ||
            manifest.FootAnchorYFraction <= 0f ||
            manifest.FootAnchorYFraction >= 1f)
        {
            throw new InvalidDataException(
                $"Player foot anchor must be a fraction between zero and one: {path}");
        }

        if (!float.IsFinite(manifest.StandingCollider.CenterOffsetX))
        {
            throw new InvalidDataException(
                $"Player collider center offset must be finite: {path}");
        }
    }

    private static bool IsPositive(Vector2 value) =>
        float.IsFinite(value.X) &&
        float.IsFinite(value.Y) &&
        value.X > 0f &&
        value.Y > 0f;

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "System.Text.Json creates manifest objects through reflection.")]
    private sealed class PlayerGeometryManifest
    {
        public int SchemaVersion { get; init; }
        public SizeManifest CanvasSizePixels { get; init; } = new();
        public SizeManifest VisualSize { get; init; } = new();
        public float FootAnchorYFraction { get; init; }
        public ColliderManifest StandingCollider { get; init; } = new();
    }

    private sealed class ColliderManifest
    {
        public SizeManifest Size { get; init; } = new();
        public float CenterOffsetX { get; init; }
    }

    private sealed class SizeManifest
    {
        public float Width { get; init; }
        public float Height { get; init; }
    }
}

internal readonly record struct PlayerGeometry2D(
    Vector2 VisualSize,
    float FootAnchorYFraction,
    Vector2 StandingColliderSize,
    float ColliderCenterOffsetX);
