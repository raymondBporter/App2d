using App2d.Gameplay.World;
using App2d.Levels;
using XnaColor = Microsoft.Xna.Framework.Color;
using System.Numerics;

namespace App2d.Things;

internal sealed record ThingTypeDescriptor2D(
    string TypeKey,
    string DisplayName,
    WorldThingKind2D? WorldKind,
    XnaColor EditorColor)
{
    public bool IsMovingPlatform => WorldKind is null;
}

internal static class ThingTypeRegistry2D
{
    public static ThingTypeDescriptor2D MovingPlatform { get; } =
        new("moving-platform", "Moving platform", null, new XnaColor(37, 210, 190));

    public static ThingTypeDescriptor2D PlayerSpawn { get; } =
        new("player-spawn", "Player spawn", WorldThingKind2D.PlayerSpawn, new XnaColor(90, 220, 130));

    public static ThingTypeDescriptor2D SavePoint { get; } =
        new("save-point", "Save point", WorldThingKind2D.SavePoint, new XnaColor(105, 225, 255));

    public static ThingTypeDescriptor2D Goal { get; } =
        new("goal", "Goal", WorldThingKind2D.Goal, new XnaColor(255, 79, 120));

    public static ThingTypeDescriptor2D Shieldback { get; } =
        new("shieldback", "Shieldback", WorldThingKind2D.Shieldback, new XnaColor(235, 190, 75));

    public static ThingTypeDescriptor2D BoilerBrute { get; } =
        new("boiler-brute", "Boiler brute", WorldThingKind2D.BoilerBrute, new XnaColor(240, 105, 75));

    public static ThingTypeDescriptor2D Rival { get; } =
        new("rival", "Rival", WorldThingKind2D.Rival, new XnaColor(255, 46, 166));

    public static ThingTypeDescriptor2D GreenDinosaur { get; } =
        new(
            "green-dinosaur",
            "Green dinosaur",
            WorldThingKind2D.GreenDinosaur,
            new XnaColor(142, 205, 92));

    public static ThingTypeDescriptor2D TumbleProp { get; } =
        new("tumble-prop", "Tumble prop", WorldThingKind2D.TumbleProp, new XnaColor(255, 154, 59));

    public static IReadOnlyList<ThingTypeDescriptor2D> All { get; } =
        [
            MovingPlatform,
            PlayerSpawn,
            SavePoint,
            Goal,
            Shieldback,
            BoilerBrute,
            Rival,
            GreenDinosaur,
            TumbleProp
        ];

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
            new XnaColor((byte)(record.ColorArgb >> 16), (byte)(record.ColorArgb >> 8), (byte)record.ColorArgb, (byte)(record.ColorArgb >> 24)));
    }

    public static WorldThingSpec2D ToRuntime(PositionThingRecord2D record)
    {
        var descriptor = Require(record.TypeKey);
        if (descriptor.WorldKind is not { } kind)
            throw new InvalidOperationException($"Thing type '{record.TypeKey}' is not position-only.");
        return new WorldThingSpec2D(
            record.ThingId,
            kind,
            record.Name,
            record.Enabled,
            new Vector2(record.X, record.Y));
    }
}
