using App2d.Core.Characters.Attachments;
using App2d.Core.Geometry.Shapes;
using System.Collections.Immutable;

namespace App2d.Core.Characters.Skeletons;

/// <summary>Plate armor for the standard person. All corners are in the piece's mount space.</summary>
public static class PersonPlateArmor
{
    private const string DarkSteel = "#566171";
    private static readonly AttachmentMaterial Steel = new()
    {
        Fill = "#adb6c2",
        Outline = new() { Color = "#343c46", Width = .012f },
    };

    public static ImmutableArray<Attachment> Boot(int mount) => Plate("plate-boot", mount, -.036f,
        [new(-.064f, -.036f), new(-.068f, .025f), new(-.037f, .095f), new(.043f, .094f), new(.070f, .042f),
            new(.140f, .025f), new(.158f, -.010f), new(.141f, -.040f)],
        Shade(new(-.080f, -.043f), new(.167f, -.045f), new(.166f, -.012f), new(-.079f, -.009f)));

    public static ImmutableArray<Attachment> Greave(int mount) => Plate("plate-greave", mount, -.014f,
        [new(-.040f, .039f), new(-.064f, .293f), new(-.067f, .384f), new(-.034f, .435f), new(.032f, .439f),
            new(.072f, .390f), new(.061f, .295f), new(.038f, .040f)],
        Shade(new(.002f, .036f), new(.020f, .367f), new(.080f, .408f), new(.053f, .036f)));

    public static ImmutableArray<Attachment> Cuisse(int mount) => Plate("plate-cuisse", mount, -.014f,
        [new(-.057f, .019f), new(-.080f, .317f), new(-.059f, .408f), new(.062f, .413f), new(.088f, .331f),
            new(.064f, .055f), new(.029f, .017f)],
        Shade(new(-.086f, .014f), new(-.095f, .418f), new(-.021f, .415f), new(.002f, .018f)));

    public static ImmutableArray<Attachment> Fauld(int mount) => Plate("plate-fauld", mount, -.045f,
        [new(-.211f, -.075f), new(-.162f, .139f), new(.163f, .139f), new(.213f, -.075f),
            new(.142f, -.138f), new(.028f, -.113f), new(-.019f, -.091f), new(-.116f, -.135f)],
        Shade(new(-.230f, -.045f), new(.230f, -.048f), new(.230f, -.088f), new(-.230f, -.085f)),
        Shade(new(-.203f, .049f), new(.200f, .043f), new(.206f, .010f), new(-.210f, .016f)));

    public static ImmutableArray<Attachment> Cuirass(int mount) => Plate("plate-cuirass", mount, -.024f,
        [new(-.181f, .091f), new(-.183f, .264f), new(-.170f, .550f), new(-.099f, .665f), new(.100f, .665f),
            new(.188f, .551f), new(.200f, .416f), new(.142f, .244f), new(.177f, .116f), new(.023f, .069f)],
        Shade(new(-.188f, .080f), new(-.193f, .612f), new(-.099f, .665f), new(-.031f, .418f), new(.023f, .069f)),
        Shade(new(-.068f, .261f), new(.163f, .304f), new(.157f, .263f), new(-.058f, .224f)));

    public static ImmutableArray<Attachment> Pauldron(int mount) => Plate("plate-pauldron", mount, -.048f,
        [new(-.126f, -.048f), new(-.114f, .058f), new(-.055f, .115f), new(.051f, .110f), new(.122f, .053f),
            new(.127f, -.042f), new(.079f, -.126f), new(-.064f, -.124f)],
        Shade(new(-.134f, -.036f), new(.132f, -.019f), new(.115f, -.141f), new(-.131f, -.140f)));

    public static ImmutableArray<Attachment> Rerebrace(int mount) => Plate("plate-rerebrace", mount, -.011f,
        [new(-.055f, -.043f), new(-.074f, .268f), new(-.050f, .302f), new(.053f, .302f), new(.078f, .267f),
            new(.056f, -.015f), new(.020f, -.059f)],
        Shade(new(-.078f, -.045f), new(-.079f, .305f), new(-.023f, .305f), new(.001f, -.065f)));

    public static ImmutableArray<Attachment> Vambrace(int mount) => Plate("plate-vambrace", mount, -.022f,
        [new(-.044f, .036f), new(-.069f, .272f), new(-.048f, .337f), new(.050f, .349f), new(.076f, .299f),
            new(.047f, .039f)],
        Shade(new(.011f, .037f), new(.013f, .290f), new(.079f, .329f), new(.054f, .037f)));

    public static ImmutableArray<Attachment> Gauntlet(int mount) => Plate("plate-gauntlet", mount, -.038f,
        [new(-.053f, -.068f), new(-.064f, .006f), new(-.070f, .078f), new(.062f, .078f), new(.059f, .024f),
            new(.074f, -.015f), new(.048f, -.076f), new(-.016f, -.088f)],
        Shade(new(-.068f, -.035f), new(.074f, -.024f), new(.061f, -.085f), new(-.066f, -.097f)),
        Shade(new(-.074f, .052f), new(.068f, .053f), new(.069f, .026f), new(-.074f, .025f)));

    public static ImmutableArray<Attachment> Gorget(int mount) => Plate("plate-gorget", mount, -.035f,
        [new(-.127f, -.036f), new(-.079f, .049f), new(-.057f, .149f), new(.057f, .149f), new(.076f, .049f),
            new(.128f, -.034f), new(0, -.077f)],
        Shade(new(-.119f, .036f), new(.116f, .036f), new(.133f, -.013f), new(-.134f, -.014f)));

    public static ImmutableArray<Attachment> Helmet(int mount) => Plate("plate-helmet", mount, -.14f,
        [new(-.294f, -.159f), new(-.338f, .030f), new(-.304f, .207f), new(-.190f, .307f), new(-.009f, .346f),
            new(.181f, .311f), new(.292f, .209f), new(.344f, .070f), new(.376f, -.024f), new(.296f, -.207f),
            new(.126f, -.301f), new(-.131f, -.300f), new(-.248f, -.241f)],
        Shade(new(-.338f, -.330f), new(-.340f, .302f), new(-.188f, .313f), new(-.096f, .064f), new(-.144f, -.307f)),
        Shade(new(-.111f, .093f), new(.351f, .126f), new(.371f, .062f), new(-.090f, .033f)),
        Shade(new(-.103f, -.170f), new(.275f, -.134f), new(.245f, -.185f), new(-.110f, -.225f)));

    private static PaintPatch Shade(params ImmutableArray<Vector2f> corners) => new(DarkSteel, corners);

    private static ImmutableArray<Attachment> Plate(string name, int mount, float layer, Vector2f[] outline, params ImmutableArray<PaintPatch> patches) =>
        [new MountedShape(name, mount, SimplePolygon2D.Builder.Create(outline), Steel, layer) { Patches = patches }];
}
