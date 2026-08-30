using App2d.Gameplay.World;
using App2d.Levels;
using SkiaSharp;
using System.Numerics;

namespace App2d.Things;

internal sealed record ThingTypeDescriptor2D(string TypeKey, string DisplayName);

internal static class ThingTypeRegistry2D
{
    public static ThingTypeDescriptor2D MovingPlatform { get; } =
        new("moving-platform", "Moving platform");

    public static IReadOnlyList<ThingTypeDescriptor2D> All { get; } = [MovingPlatform];

    public static ThingTypeDescriptor2D Require(string typeKey) =>
        All.SingleOrDefault(type => string.Equals(type.TypeKey, typeKey, StringComparison.Ordinal)) ??
        throw new InvalidOperationException($"Thing type '{typeKey}' is not registered.");

    public static MovingPlatformSpec2D ToRuntime(MovingPlatformThingRecord2D record)
    {
        _ = Require(MovingPlatform.TypeKey);
        return new MovingPlatformSpec2D(
            record.ThingId,
            record.Name,
            record.Enabled,
            new Vector2(record.X, record.Y),
            new Vector2(record.TravelX, record.TravelY),
            new Vector2(record.Width, record.Height),
            record.Speed,
            new SKColor(unchecked((uint)record.ColorArgb)));
    }
}
