using App2d.Core.Geometry.Primitives;
using App2d.Core.Objects;
using App2d.Core.Validation;
using App2d.Gameplay.World;
using App2d.Levels;
using System.Globalization;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Things;

internal sealed record ThingTypeDescriptor2D(
    string TypeKey,
    string DisplayName,
    WorldThingKind2D? WorldKind,
    XnaColor EditorColor)
{
    /// <summary>A placed authored object: the definition names an asset file and the thing is a position.</summary>
    public bool IsObject => string.Equals(TypeKey, LevelDatabase2D.ObjectTypeKey, StringComparison.Ordinal);
}

internal static class ThingTypeRegistry2D
{
    public static ThingTypeDescriptor2D Object { get; } = new(LevelDatabase2D.ObjectTypeKey, "Object", null, new XnaColor(37, 210, 190));

    public static ThingTypeDescriptor2D PlayerSpawn { get; } = new("player-spawn", "Player spawn", WorldThingKind2D.PlayerSpawn, new XnaColor(90, 220, 130));

    public static ThingTypeDescriptor2D SavePoint { get; } = new("save-point", "Save point", WorldThingKind2D.SavePoint, new XnaColor(105, 225, 255));

    public static ThingTypeDescriptor2D Goal { get; } = new("goal", "Goal", WorldThingKind2D.Goal, new XnaColor(255, 79, 120));

    public static ThingTypeDescriptor2D Shieldback { get; } = new("shieldback", "Needle", WorldThingKind2D.Shieldback, new XnaColor(150, 190, 230));

    public static ThingTypeDescriptor2D BoilerBrute { get; } = new("boiler-brute", "Maul", WorldThingKind2D.BoilerBrute, new XnaColor(240, 160, 100));

    public static ThingTypeDescriptor2D ClubCaveman { get; } = new("club-caveman", "Club caveman", WorldThingKind2D.ClubCaveman, new XnaColor(215, 163, 72));

    public static ThingTypeDescriptor2D RockThrower { get; } = new("rock-thrower", "Rock thrower", WorldThingKind2D.RockThrower, new XnaColor(83, 142, 137));

    public static ThingTypeDescriptor2D BabyTriceratops { get; } = new("baby-triceratops", "Baby triceratops", WorldThingKind2D.BabyTriceratops, new XnaColor(121, 189, 176));

    public static ThingTypeDescriptor2D ShieldDefender { get; } = new("shield-defender", "Shell-shield defender", WorldThingKind2D.ShieldDefender, new XnaColor(145, 162, 118));

    public static ThingTypeDescriptor2D Pterosaur { get; } = new("pterosaur", "Pterosaur", WorldThingKind2D.Pterosaur, new XnaColor(181, 110, 139));

    public static ThingTypeDescriptor2D Rival { get; } = new("rival", "Cinder", WorldThingKind2D.Rival, new XnaColor(240, 210, 110));

    public static ThingTypeDescriptor2D GreenDinosaur { get; } = new("green-dinosaur", "Scrap Hound", WorldThingKind2D.GreenDinosaur, new XnaColor(142, 205, 92));

    public static ThingTypeDescriptor2D TumbleProp { get; } = new("tumble-prop", "Tumble prop", WorldThingKind2D.TumbleProp, new XnaColor(255, 154, 59));

    public static IReadOnlyList<ThingTypeDescriptor2D> All { get; } =
        [
            Object,
            PlayerSpawn,
            SavePoint,
            Goal,
            Shieldback,
            BoilerBrute,
            ClubCaveman,
            RockThrower,
            BabyTriceratops,
            ShieldDefender,
            Pterosaur,
            Rival,
            GreenDinosaur,
            TumbleProp
        ];

    public static ThingTypeDescriptor2D Require(string typeKey) =>
        All.SingleOrDefault(type => string.Equals(type.TypeKey, typeKey, StringComparison.Ordinal)) ??
        throw new InvalidOperationException($"Thing type '{typeKey}' is not registered.");

    /// <summary>An object thing becomes a placement whose id is the world-wide thing id, so placements stay unique across maps.</summary>
    public static ObjectPlacement2D ToPlacement(PositionThingRecord2D record, ObjectAsset2D asset, long worldThingId)
    {
        ArgGuard.ThrowIfNull(record);
        ArgGuard.ThrowIfNull(asset);
        StateGuard.ThrowIf(!Require(record.TypeKey).IsObject, $"Thing type '{record.TypeKey}' is not an object.");
        return new ObjectPlacement2D(worldThingId.ToString(CultureInfo.InvariantCulture), asset, new Vector2(record.X, record.Y));
    }

    public static WorldThingSpec2D ToRuntime(PositionThingRecord2D record)
    {
        var descriptor = Require(record.TypeKey);
        if (descriptor.WorldKind is not { } kind)
        {
            throw new InvalidOperationException($"Thing type '{record.TypeKey}' is not position-only.");
        }

        return new WorldThingSpec2D(
            record.ThingId,
            kind,
            record.Name,
            record.Enabled,
            new Vector2(record.X, record.Y));
    }
}
