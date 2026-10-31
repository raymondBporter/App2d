using App2d.CharacterStudio.Authoring;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Curves;

namespace App2d.CharacterStudio.PlayerMoves;

internal static partial class PlayerMoves
{
    // Deliberately broad shapes and restrained colors: the character is only about 50 pixels tall in game.
    private const string Steel = "#dce5e7", Brass = "#c5a26b", Leather = "#714b3e", DarkSteel = "#465963";

    private static PartGeometry Plate(string color, float depth, params PuppetPoint[] points) => PartGeometryBuilder.Extrude(points, depth, color);
    private static PartGeometry Block(string color, float x0, float x1, float y0, float y1, float depth, float bevel = 0)
        => Plate(color, depth, new(x0 + bevel, y0), new(x1 - bevel, y0), new(x1, y0 + bevel), new(x1, y1 - bevel),
            new(x1 - bevel, y1), new(x0 + bevel, y1), new(x0, y1 - bevel), new(x0, y0 + bevel));

    private static Part SwordArt() => PartGeometryBuilder.Named(new()
    {
        Id = Sword,
        Name = "Sword",
        Usage = "prop",
        Locations = [new() { Id = EquipmentPoints.Grip }, new() { Id = EquipmentPoints.Tip, X = .82f, Y = 0 }],
        LineWidth = .013f,
        Geometry =
        [
            Block(Leather, -.12f, .04f, -.026f, .026f, .043f, .006f),
            Block(Brass, -.142f, -.112f, -.036f, .036f, .058f, .008f),
            Plate(Brass, .05f, new(.028f, -.105f), new(.064f, -.092f), new(.08f, -.035f), new(.08f, .035f), new(.064f, .092f), new(.028f, .105f)),
            Block(DarkSteel, .064f, .11f, -.045f, .045f, .04f, .006f),
            PartGeometryBuilder.Blade(.105f, .65f, .82f, .047f, .014f, Steel),
        ],
    });

    /// <summary>
    /// A cartoon sword after the RGS stick-figure art: a broad blade about six times as long as it is wide with a chisel
    /// tip, a thin guard, and a grip short enough to disappear in the fist. Larger than <see cref="SwordArt"/> so the blade,
    /// not the hilt, is what reads at game size. Approved for the player's runtime sword.
    /// </summary>
    internal static Part CartoonSwordArt()
    {
        var prop = new Part
        {
            Id = Sword,
            Name = "Sword",
            Usage = "prop",
            Locations = [new() { Id = EquipmentPoints.Grip }, new() { Id = EquipmentPoints.Tip, X = 1.02f, Y = .03f }],
            LineWidth = .02f,
            // A narrow, round-ended stroke keeps the grip as simple as the character's hand.
            Geometry =
        [
            Block(DarkSteel, .035f, .065f, -.11f, .11f, .06f, .01f),
            Plate(Steel, .03f, new(.065f, -.075f), new(.88f, -.075f), new(1.02f, .03f), new(.96f, .075f), new(.065f, .075f)),
        ],
        };
        prop.Geometry.Add(new() { Id = "shape-1", A = PartLocation.Origin, Frame = PartLocation.Origin, Geometry = new LineSegmentCurve2D { Start = new(-.055f, 0), End = new(.025f, 0) }.Validated(), Material = new() { Fill = Leather, StrokeWidth = .006f, Outline = new() } });
        return PartGeometryBuilder.Named(prop);
    }

    private static Part SheathArt()
    {
        var prop = new Part
        {
            Id = Sheath,
            Name = "Sheath",
            Usage = "prop",
            Locations = [new() { Id = EquipmentPoints.Grip }, new() { Id = EquipmentPoints.Tip, X = .85f, Y = 0 }],
            LineWidth = .012f,
            Geometry =
            [
                Plate("#344b53", .064f, new(.075f, -.056f), new(.68f, -.043f), new(.85f, 0), new(.68f, .043f), new(.075f, .056f)),
                Block(Brass, .07f, .112f, -.065f, .065f, .078f, .008f),
                Plate(Brass, .067f, new(.71f, -.036f), new(.85f, 0), new(.71f, .036f)),
            ],
        };
        // The broad front of the scabbard covers the blade on the shared back socket.
        foreach (var solid in prop.Geometry)
        {
            solid.Mesh!.Outline = solid.Mesh.Outline?.Select(p => p with { Z = p.Z - .022f }).ToList();
            solid.Mesh.Vertices = [.. solid.Mesh.Vertices.Select(p => p with { Z = p.Z - .022f })];
        }

        return PartGeometryBuilder.Named(prop);
    }

    private static Part PistolArt() => PartGeometryBuilder.Named(new()
    {
        Id = Pistol,
        Name = "Pistol",
        Usage = "prop",
        Locations = [new() { Id = EquipmentPoints.Grip }, new() { Id = EquipmentPoints.Tip, X = .26f, Y = .06f }, new() { Id = EquipmentPoints.Muzzle, X = .26f, Y = .06f }],
        LineWidth = .011f,
        Geometry =
        [
            Plate(Leather, .067f, new(-.063f, -.123f), new(-.005f, -.116f), new(.031f, .036f), new(-.045f, .041f)),
            Plate(DarkSteel, .079f, new(-.074f, .026f), new(.26f, .026f), new(.26f, .084f), new(.238f, .112f), new(-.056f, .112f), new(-.082f, .09f)),
            // Raised top rail is a readable silver accent on the dark frame in either facing.
            Block(Steel, -.052f, .231f, .094f, .116f, .068f, .006f),
            Block(Brass, -.069f, -.004f, -.128f, -.108f, .074f, .004f),
            Block("#293941", .195f, .26f, .03f, .089f, .089f, .006f),
        ],
    });

    private static Part HammerArt() => PartGeometryBuilder.Named(new()
    {
        Id = "hammer",
        Name = "Hammer",
        Usage = "prop",
        Locations = [new() { Id = EquipmentPoints.Grip }, new() { Id = EquipmentPoints.Tip, X = .8f, Y = 0 }],
        LineWidth = .016f,
        Geometry =
        [
            Block("#866344", -.14f, .74f, -.028f, .028f, .049f, .012f),
            Block(Leather, -.1f, .17f, -.041f, .041f, .066f, .012f),
            Block(Brass, .59f, .65f, -.046f, .046f, .075f, .008f),
            Block("#798c96", .65f, .95f, -.165f, .165f, .235f, .04f),
            Block(Steel, .64f, .96f, -.193f, -.12f, .25f, .022f),
            Block(Steel, .64f, .96f, .12f, .193f, .25f, .022f),
            Block(DarkSteel, .756f, .844f, -.115f, .115f, .247f, .012f),
        ],
    });
}
