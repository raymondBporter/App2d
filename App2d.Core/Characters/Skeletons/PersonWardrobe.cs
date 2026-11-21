// Generated from the authored JSON parts by LegacyPartEmitter in App2d.Tests. While those parts exist, change them and
// regenerate (set APP2D_WRITE_CLIPS=1 and run the tests named WornPartTests) instead of editing this file.
using App2d.Core.Geometry.Meshes;
using App2d.Core.Geometry.Shapes;
using System.Collections.Immutable;

namespace App2d.Core.Characters;

/// <summary>What a person wears and holds: each function returns one thing's pieces as attachments on a mount of <see cref="PersonSkeleton"/>, at a layer.</summary>
public static class PersonWardrobe
{
    /// <summary>A plain fill, edged in ink unless told otherwise; a stroke width makes it a line.</summary>
    private static RenderMaterialDefinition2D Paint(string fill, float edge = 0, string edgeColor = "#222b32", float? stroke = null) =>
        new() { Fill = fill, StrokeWidth = stroke, Outline = edge > 0 ? new() { Color = edgeColor, Width = edge } : null };

    /// <summary>A flat outline pushed out to a thickness.</summary>
    private static IndexedMesh3D Extrude(float thickness, params ReadOnlySpan<Vector3f> outline) =>
        MeshGenerators3D.Extrude(MeshGenerators3D.CounterClockwise(outline.ToArray()), thickness);

    private static readonly IndexedMesh3D BootMesh1 = Extrude(0.08f,
        new(-0.058f, -0.023f, 0f), new(0.145f, -0.023f, 0f), new(0.153f, -0.006f, 0f), new(0.145f, 0.027f, 0f), new(0.11f, 0.045f, 0f),
        new(0.047f, 0.055f, 0f), new(0.043f, 0.165f, 0f), new(-0.05f, 0.165f, 0f));
    private static readonly IndexedMesh3D BootMesh2 = Extrude(0.008f, new(-0.06f, -0.024f, -0.048f), new(0.147f, -0.024f, -0.048f), new(0.153f, -0.009f, -0.048f), new(-0.06f, -0.009f, -0.048f));
    private static readonly IndexedMesh3D BootMesh3 = Extrude(0.008f, new(-0.052f, 0.135f, -0.048f), new(0.045f, 0.135f, -0.048f), new(0.045f, 0.163f, -0.048f), new(-0.052f, 0.163f, -0.048f));
    private static readonly IndexedMesh3D BruteBeardMesh1 = Extrude(0.008f,
        new(-0.29925f, -0.19665f, -0.16f), new(-0.247f, -0.207575f, -0.16f), new(-0.285f, -0.32775f, -0.16f), new(-0.19f, -0.294975f, -0.16f),
        new(-0.20425f, -0.41515f, -0.16f), new(-0.09975f, -0.3933f, -0.16f), new(-0.07125f, -0.50255f, -0.16f), new(0.01425f, -0.437f, -0.16f),
        new(0.076f, -0.535325f, -0.16f), new(0.1425f, -0.382375f, -0.16f), new(0.228f, -0.426075f, -0.16f), new(0.228f, -0.273125f, -0.16f),
        new(0.323f, -0.316825f, -0.16f), new(0.266f, -0.185725f, -0.16f), new(0.304f, -0.142025f, -0.16f), new(0.2565f, -0.0491625f, -0.16f),
        new(0.209f, -0.0874f, -0.16f), new(0.19f, -0.19665f, -0.16f), new(-0.076f, -0.19665f, -0.16f), new(-0.09975f, -0.10925f, -0.16f),
        new(-0.171f, -0.0600875f, -0.16f), new(-0.24225f, 0.0163875f, -0.16f));
    private static readonly IndexedMesh3D BruteHairMesh1 = Extrude(0.008f,
        new(-0.2755f, 0.19f, -0.095f), new(-0.3515f, 0.057f, -0.095f), new(-0.2945f, 0.057f, -0.095f), new(-0.3705f, -0.0855f, -0.095f),
        new(-0.2945f, -0.057f, -0.095f), new(-0.304f, -0.2185f, -0.095f), new(-0.1995f, -0.1615f, -0.095f), new(0.2185f, -0.171f, -0.095f),
        new(0.323f, -0.2185f, -0.095f), new(0.2945f, -0.0475f, -0.095f), new(0.361f, -0.095f, -0.095f), new(0.304f, 0.1235f, -0.095f),
        new(0.2185f, 0.2565f, -0.095f));
    private static readonly IndexedMesh3D BruteHairMesh2 = Extrude(0.008f,
        new(-0.209f, 0.0268375f, -0.15f), new(-0.171f, 0.12882f, -0.15f), new(-0.1045f, 0.23617f, -0.15f), new(-0.01425f, 0.203965f, -0.15f),
        new(0.0665f, 0.25764f, -0.15f), new(0.1615f, 0.19323f, -0.15f), new(0.2755f, 0.203965f, -0.15f), new(0.2185f, 0.268375f, -0.15f),
        new(0.266f, 0.32205f, -0.15f), new(0.1425f, 0.332785f, -0.15f), new(0.1805f, 0.40793f, -0.15f), new(0.04275f, 0.36499f, -0.15f),
        new(0.03325f, 0.461605f, -0.15f), new(-0.057f, 0.36499f, -0.15f), new(-0.1425f, 0.4294f, -0.15f), new(-0.1425f, 0.32205f, -0.15f),
        new(-0.2755f, 0.34352f, -0.15f), new(-0.2185f, 0.246905f, -0.15f), new(-0.285f, 0.25764f, -0.15f), new(-0.2565f, 0.10735f, -0.15f));
    private static readonly IndexedMesh3D BruteHideWrapMesh1 = Extrude(0.39179f,
        new(-0.29f, -0.07875f, 0.010875f), new(-0.16675f, -0.0525f, 0.010875f), new(-0.07975f, -0.1155f, 0.010875f),
        new(0.0145f, -0.06825f, 0.010875f), new(0.10875f, -0.12075f, 0.010875f), new(0.18125f, -0.0525f, 0.010875f),
        new(0.29725f, -0.07875f, 0.010875f), new(0.261f, 0.084f, 0.010875f), new(-0.261f, 0.084f, 0.010875f));
    private static readonly IndexedMesh3D BruteHideWrapMesh2 = Extrude(0.00457754f,
        new(-0.2755f, 0.02625f, -0.195319f), new(0.2755f, 0.02625f, -0.195319f), new(0.26825f, 0.08925f, -0.195319f),
        new(-0.26825f, 0.08925f, -0.195319f));
    private static readonly IndexedMesh3D BruteHideWrapMesh3 = Extrude(0.00457754f,
        new(-0.13705f, -0.01575f, -0.195319f), new(-0.142773f, -0.0008952f, -0.195319f), new(-0.155632f, 0.00277363f, -0.195319f),
        new(-0.165945f, -0.00750621f, -0.195319f), new(-0.165945f, -0.0239938f, -0.195319f), new(-0.155632f, -0.0342736f, -0.195319f),
        new(-0.142773f, -0.0306048f, -0.195319f));
    private static readonly IndexedMesh3D BruteHideWrapMesh4 = Extrude(0.00457754f,
        new(0.1481f, -0.021f, -0.195319f), new(0.141473f, -0.0037997f, -0.195319f), new(0.126584f, 0.000448415f, -0.195319f),
        new(0.114643f, -0.0114546f, -0.195319f), new(0.114643f, -0.0305454f, -0.195319f), new(0.126584f, -0.0424484f, -0.195319f),
        new(0.141473f, -0.0382003f, -0.195319f));
    private static readonly IndexedMesh3D CinderBeardMesh1 = Extrude(0.008f,
        new(-0.33075f, -0.17388f, -0.16f), new(-0.273f, -0.18354f, -0.16f), new(-0.315f, -0.2898f, -0.16f), new(-0.21f, -0.26082f, -0.16f),
        new(-0.22575f, -0.36708f, -0.16f), new(-0.11025f, -0.34776f, -0.16f), new(-0.07875f, -0.44436f, -0.16f), new(0.01575f, -0.3864f, -0.16f),
        new(0.084f, -0.47334f, -0.16f), new(0.1575f, -0.3381f, -0.16f), new(0.252f, -0.37674f, -0.16f), new(0.252f, -0.2415f, -0.16f),
        new(0.357f, -0.28014f, -0.16f), new(0.294f, -0.16422f, -0.16f), new(0.336f, -0.12558f, -0.16f), new(0.2835f, -0.04347f, -0.16f),
        new(0.231f, -0.07728f, -0.16f), new(0.21f, -0.17388f, -0.16f), new(-0.084f, -0.17388f, -0.16f), new(-0.11025f, -0.0966f, -0.16f),
        new(-0.189f, -0.05313f, -0.16f), new(-0.26775f, 0.01449f, -0.16f));
    private static readonly IndexedMesh3D CinderHairMesh1 = Extrude(0.008f,
        new(-0.3045f, 0.21f, -0.095f), new(-0.3885f, 0.063f, -0.095f), new(-0.3255f, 0.063f, -0.095f), new(-0.4095f, -0.0945f, -0.095f),
        new(-0.3255f, -0.063f, -0.095f), new(-0.336f, -0.2415f, -0.095f), new(-0.2205f, -0.1785f, -0.095f), new(0.2415f, -0.189f, -0.095f),
        new(0.357f, -0.2415f, -0.095f), new(0.3255f, -0.0525f, -0.095f), new(0.399f, -0.105f, -0.095f), new(0.336f, 0.1365f, -0.095f),
        new(0.2415f, 0.2835f, -0.095f));
    private static readonly IndexedMesh3D CinderHairMesh2 = Extrude(0.008f,
        new(-0.231f, 0.0296625f, -0.15f), new(-0.189f, 0.14238f, -0.15f), new(-0.1155f, 0.26103f, -0.15f), new(-0.01575f, 0.225435f, -0.15f),
        new(0.0735f, 0.28476f, -0.15f), new(0.1785f, 0.21357f, -0.15f), new(0.3045f, 0.225435f, -0.15f), new(0.2415f, 0.296625f, -0.15f),
        new(0.294f, 0.35595f, -0.15f), new(0.1575f, 0.367815f, -0.15f), new(0.1995f, 0.45087f, -0.15f), new(0.04725f, 0.40341f, -0.15f),
        new(0.03675f, 0.510195f, -0.15f), new(-0.063f, 0.40341f, -0.15f), new(-0.1575f, 0.4746f, -0.15f), new(-0.1575f, 0.35595f, -0.15f),
        new(-0.3045f, 0.37968f, -0.15f), new(-0.2415f, 0.272895f, -0.15f), new(-0.315f, 0.28476f, -0.15f), new(-0.2835f, 0.11865f, -0.15f));
    private static readonly IndexedMesh3D CinderHideWrapMesh1 = Extrude(0.28371f,
        new(-0.21f, -0.07125f, 0.007875f), new(-0.12075f, -0.0475f, 0.007875f), new(-0.05775f, -0.1045f, 0.007875f),
        new(0.0105f, -0.06175f, 0.007875f), new(0.07875f, -0.10925f, 0.007875f), new(0.13125f, -0.0475f, 0.007875f),
        new(0.21525f, -0.07125f, 0.007875f), new(0.189f, 0.076f, 0.007875f), new(-0.189f, 0.076f, 0.007875f));
    private static readonly IndexedMesh3D CinderHideWrapMesh2 = Extrude(0.00331477f,
        new(-0.1995f, 0.02375f, -0.141438f), new(0.1995f, 0.02375f, -0.141438f), new(0.19425f, 0.08075f, -0.141438f),
        new(-0.19425f, 0.08075f, -0.141438f));
    private static readonly IndexedMesh3D CinderHideWrapMesh3 = Extrude(0.00331477f,
        new(-0.09505f, -0.01425f, -0.141438f), new(-0.100773f, 0.000604799f, -0.141438f), new(-0.113632f, 0.00427363f, -0.141438f),
        new(-0.123945f, -0.00600621f, -0.141438f), new(-0.123945f, -0.0224938f, -0.141438f), new(-0.113632f, -0.0327736f, -0.141438f),
        new(-0.100773f, -0.0291048f, -0.141438f));
    private static readonly IndexedMesh3D CinderHideWrapMesh4 = Extrude(0.00331477f,
        new(0.1121f, -0.019f, -0.141438f), new(0.105473f, -0.00179971f, -0.141438f), new(0.0905836f, 0.00244841f, -0.141438f),
        new(0.0786429f, -0.00945456f, -0.141438f), new(0.0786429f, -0.0285454f, -0.141438f), new(0.0905836f, -0.0404484f, -0.141438f),
        new(0.105473f, -0.0362003f, -0.141438f));
    private static readonly IndexedMesh3D HairShortMesh1 = Extrude(0.008f,
        new(-0.25f, 0.18f, -0.095f), new(-0.28f, 0.03f, -0.095f), new(-0.25f, -0.14f, -0.095f), new(-0.19f, -0.17f, -0.095f),
        new(-0.19f, 0.19f, -0.095f));
    private static readonly IndexedMesh3D HairShortMesh2 = Extrude(0.008f,
        new(-0.22f, 0.02175f, -0.15f), new(-0.18f, 0.1044f, -0.15f), new(-0.11f, 0.1914f, -0.15f), new(-0.015f, 0.1653f, -0.15f),
        new(0.07f, 0.2088f, -0.15f), new(0.17f, 0.1566f, -0.15f), new(0.29f, 0.1653f, -0.15f), new(0.23f, 0.2175f, -0.15f),
        new(0.28f, 0.261f, -0.15f), new(0.15f, 0.2697f, -0.15f), new(0.19f, 0.3306f, -0.15f), new(0.045f, 0.2958f, -0.15f),
        new(0.035f, 0.3741f, -0.15f), new(-0.06f, 0.2958f, -0.15f), new(-0.15f, 0.348f, -0.15f), new(-0.15f, 0.261f, -0.15f),
        new(-0.29f, 0.2784f, -0.15f), new(-0.23f, 0.2001f, -0.15f), new(-0.3f, 0.2088f, -0.15f), new(-0.27f, 0.087f, -0.15f));
    private static readonly IndexedMesh3D HairShortBackMesh1 = Extrude(0.008f,
        new(-0.28f, -0.06f, -0.15f), new(-0.23f, -0.17f, -0.15f), new(-0.12f, -0.24f, -0.15f), new(0.11f, -0.25f, -0.15f),
        new(0.22f, -0.19f, -0.15f), new(0.28f, -0.08f, -0.15f), new(0.29f, 0.16f, -0.15f), new(0.23f, 0.22f, -0.15f), new(0.28f, 0.26f, -0.15f),
        new(0.15f, 0.27f, -0.15f), new(0.19f, 0.33f, -0.15f), new(0.045f, 0.3f, -0.15f), new(0.035f, 0.37f, -0.15f), new(-0.06f, 0.3f, -0.15f),
        new(-0.15f, 0.35f, -0.15f), new(-0.15f, 0.26f, -0.15f), new(-0.29f, 0.28f, -0.15f), new(-0.23f, 0.2f, -0.15f), new(-0.3f, 0.21f, -0.15f),
        new(-0.27f, 0.1f, -0.15f));
    private static readonly IndexedMesh3D HammerMesh1 = Extrude(0.049f,
        new(-0.128f, -0.028f, 0f), new(0.728f, -0.028f, 0f), new(0.74f, -0.016f, 0f), new(0.74f, 0.016f, 0f), new(0.728f, 0.028f, 0f),
        new(-0.128f, 0.028f, 0f), new(-0.14f, 0.016f, 0f), new(-0.14f, -0.016f, 0f));
    private static readonly IndexedMesh3D HammerMesh2 = Extrude(0.066f,
        new(-0.088f, -0.041f, 0f), new(0.158f, -0.041f, 0f), new(0.17f, -0.029f, 0f), new(0.17f, 0.029f, 0f), new(0.158f, 0.041f, 0f),
        new(-0.088f, 0.041f, 0f), new(-0.1f, 0.029f, 0f), new(-0.1f, -0.029f, 0f));
    private static readonly IndexedMesh3D HammerMesh3 = Extrude(0.075f,
        new(0.598f, -0.046f, 0f), new(0.642f, -0.046f, 0f), new(0.65f, -0.038f, 0f), new(0.65f, 0.038f, 0f), new(0.642f, 0.046f, 0f),
        new(0.598f, 0.046f, 0f), new(0.59f, 0.038f, 0f), new(0.59f, -0.038f, 0f));
    private static readonly IndexedMesh3D HammerMesh4 = Extrude(0.235f,
        new(0.69f, -0.165f, 0f), new(0.91f, -0.165f, 0f), new(0.95f, -0.125f, 0f), new(0.95f, 0.125f, 0f), new(0.91f, 0.165f, 0f),
        new(0.69f, 0.165f, 0f), new(0.65f, 0.125f, 0f), new(0.65f, -0.125f, 0f));
    private static readonly IndexedMesh3D HammerMesh5 = Extrude(0.25f,
        new(0.662f, -0.193f, 0f), new(0.938f, -0.193f, 0f), new(0.96f, -0.171f, 0f), new(0.96f, -0.142f, 0f), new(0.938f, -0.12f, 0f),
        new(0.662f, -0.12f, 0f), new(0.64f, -0.142f, 0f), new(0.64f, -0.171f, 0f));
    private static readonly IndexedMesh3D HammerMesh6 = Extrude(0.25f,
        new(0.662f, 0.12f, 0f), new(0.938f, 0.12f, 0f), new(0.96f, 0.142f, 0f), new(0.96f, 0.171f, 0f), new(0.938f, 0.193f, 0f),
        new(0.662f, 0.193f, 0f), new(0.64f, 0.171f, 0f), new(0.64f, 0.142f, 0f));
    private static readonly IndexedMesh3D HammerMesh7 = Extrude(0.247f,
        new(0.768f, -0.115f, 0f), new(0.832f, -0.115f, 0f), new(0.844f, -0.103f, 0f), new(0.844f, 0.103f, 0f), new(0.832f, 0.115f, 0f),
        new(0.768f, 0.115f, 0f), new(0.756f, 0.103f, 0f), new(0.756f, -0.103f, 0f));
    private static readonly IndexedMesh3D PistolMesh1 = Extrude(0.067f, new(-0.063f, -0.123f, 0f), new(-0.005f, -0.116f, 0f), new(0.031f, 0.036f, 0f), new(-0.045f, 0.041f, 0f));
    private static readonly IndexedMesh3D PistolMesh2 = Extrude(0.079f,
        new(-0.074f, 0.026f, 0f), new(0.26f, 0.026f, 0f), new(0.26f, 0.084f, 0f), new(0.238f, 0.112f, 0f), new(-0.056f, 0.112f, 0f),
        new(-0.082f, 0.09f, 0f));
    private static readonly IndexedMesh3D PistolMesh3 = Extrude(0.068f,
        new(-0.046f, 0.094f, 0f), new(0.225f, 0.094f, 0f), new(0.231f, 0.1f, 0f), new(0.231f, 0.11f, 0f), new(0.225f, 0.116f, 0f),
        new(-0.046f, 0.116f, 0f), new(-0.052f, 0.11f, 0f), new(-0.052f, 0.1f, 0f));
    private static readonly IndexedMesh3D PistolMesh4 = Extrude(0.074f,
        new(-0.065f, -0.128f, 0f), new(-0.008f, -0.128f, 0f), new(-0.004f, -0.124f, 0f), new(-0.004f, -0.112f, 0f), new(-0.008f, -0.108f, 0f),
        new(-0.065f, -0.108f, 0f), new(-0.069f, -0.112f, 0f), new(-0.069f, -0.124f, 0f));
    private static readonly IndexedMesh3D PistolMesh5 = Extrude(0.089f,
        new(0.201f, 0.03f, 0f), new(0.254f, 0.03f, 0f), new(0.26f, 0.036f, 0f), new(0.26f, 0.083f, 0f), new(0.254f, 0.089f, 0f),
        new(0.201f, 0.089f, 0f), new(0.195f, 0.083f, 0f), new(0.195f, 0.036f, 0f));
    private static readonly IndexedMesh3D RockThrowerBagMesh1 = Extrude(0.08f,
        new(-0.39f, -0.12f, 0.12f), new(-0.58f, -0.02f, 0.12f), new(-0.56f, 0.25f, 0.12f), new(-0.39f, 0.29f, 0.12f), new(-0.21f, 0.22f, 0.12f),
        new(-0.23f, -0.05f, 0.12f));
    private static readonly IndexedMesh3D RockThrowerBagMesh2 = Extrude(0.03f, new(-0.42f, 0.18f, 0.12f), new(0.13f, 0.71f, 0.12f), new(0.17f, 0.67f, 0.12f), new(-0.37f, 0.13f, 0.12f));
    private static readonly IndexedMesh3D SandalMesh1 = Extrude(0.06f, new(-0.053f, -0.023f, 0f), new(0.135f, -0.023f, 0f), new(0.15f, -0.012f, 0f), new(0.146f, 0.004f, 0f), new(-0.05f, 0.004f, 0f));
    private static readonly IndexedMesh3D SandalMesh2 = Extrude(0.009f, new(-0.037f, 0.003f, -0.038f), new(-0.015f, 0.003f, -0.038f), new(0.009f, 0.072f, -0.038f), new(-0.014f, 0.078f, -0.038f));
    private static readonly IndexedMesh3D SandalMesh3 = Extrude(0.009f, new(0.054f, 0.003f, -0.038f), new(0.079f, 0.003f, -0.038f), new(0.06f, 0.038f, -0.038f), new(0.037f, 0.033f, -0.038f));
    private static readonly IndexedMesh3D ScavengedShellMesh1 = Extrude(0.1f,
        new(-0.12f, -0.7f, 0f), new(0.22f, -0.65f, 0f), new(0.43f, -0.4f, 0f), new(0.48f, -0.05f, 0f), new(0.38f, 0.37f, 0f), new(0.17f, 0.65f, 0f),
        new(-0.12f, 0.7f, 0f), new(-0.38f, 0.45f, 0f), new(-0.46f, 0.08f, 0f), new(-0.38f, -0.4f, 0f));
    private static readonly IndexedMesh3D ScavengedShellMesh2 = Extrude(0.025f,
        new(-0.0984f, -0.595f, -0.075f), new(0.1804f, -0.5525f, -0.075f), new(0.3526f, -0.34f, -0.075f), new(0.3936f, -0.0425f, -0.075f),
        new(0.3116f, 0.3145f, -0.075f), new(0.1394f, 0.5525f, -0.075f), new(-0.0984f, 0.595f, -0.075f), new(-0.3116f, 0.3825f, -0.075f),
        new(-0.3772f, 0.068f, -0.075f), new(-0.3116f, -0.34f, -0.075f));
    private static readonly IndexedMesh3D ScavengedShellMesh3 = Extrude(0.01f,
        new(-0.28f, -0.32f, -0.095f), new(-0.12f, -0.05f, -0.095f), new(0.1f, -0.15f, -0.095f), new(0.16f, -0.46f, -0.095f),
        new(-0.08f, -0.52f, -0.095f));
    private static readonly IndexedMesh3D ScavengedShellMesh4 = Extrude(0.01f,
        new(-0.1f, 0.02f, -0.095f), new(-0.18f, 0.32f, -0.095f), new(0.05f, 0.48f, -0.095f), new(0.27f, 0.26f, -0.095f), new(0.12f, -0.08f, -0.095f));
    private static readonly IndexedMesh3D SheathMesh1 = Extrude(0.064f, new(0.075f, -0.056f, 0f), new(0.68f, -0.043f, 0f), new(0.85f, 0f, 0f), new(0.68f, 0.043f, 0f), new(0.075f, 0.056f, 0f));
    private static readonly IndexedMesh3D SheathMesh2 = Extrude(0.078f,
        new(0.078f, -0.065f, 0f), new(0.104f, -0.065f, 0f), new(0.112f, -0.057f, 0f), new(0.112f, 0.057f, 0f), new(0.104f, 0.065f, 0f),
        new(0.078f, 0.065f, 0f), new(0.07f, 0.057f, 0f), new(0.07f, -0.057f, 0f));
    private static readonly IndexedMesh3D SheathMesh3 = Extrude(0.067f, new(0.71f, -0.036f, 0f), new(0.85f, 0f, 0f), new(0.71f, 0.036f, 0f));
    private static readonly IndexedMesh3D SpearMesh1 = new([
        new(-0.5f, -0.024f, -0.0195f), new(1.12f, -0.021f, -0.0195f), new(1.12f, 0.021f, -0.0195f), new(-0.5f, 0.024f, -0.0195f),
        new(-0.5f, -0.024f, 0.0195f), new(1.12f, -0.021f, 0.0195f), new(1.12f, 0.021f, 0.0195f), new(-0.5f, 0.024f, 0.0195f)], [0, 2, 1, 4, 5, 6, 0, 3, 2, 4, 6, 7, 0, 1, 5, 0, 5, 4, 1, 2, 6, 1, 6, 5, 2, 3, 7, 2, 7, 6, 3, 0, 4, 3, 4, 7]);
    private static readonly IndexedMesh3D SpearMesh2 = new([
        new(-0.5f, -0.031f, -0.025f), new(-0.41f, -0.031f, -0.025f), new(-0.41f, 0.031f, -0.025f), new(-0.5f, 0.031f, -0.025f),
        new(-0.5f, -0.031f, 0.025f), new(-0.41f, -0.031f, 0.025f), new(-0.41f, 0.031f, 0.025f), new(-0.5f, 0.031f, 0.025f)], [0, 2, 1, 4, 5, 6, 0, 3, 2, 4, 6, 7, 0, 1, 5, 0, 5, 4, 1, 2, 6, 1, 6, 5, 2, 3, 7, 2, 7, 6, 3, 0, 4, 3, 4, 7]);
    private static readonly IndexedMesh3D SpearMesh3 = new([
        new(1.035f, -0.039f, -0.032f), new(1.115f, -0.039f, -0.032f), new(1.115f, 0.039f, -0.032f), new(1.035f, 0.039f, -0.032f),
        new(1.035f, -0.039f, 0.032f), new(1.115f, -0.039f, 0.032f), new(1.115f, 0.039f, 0.032f), new(1.035f, 0.039f, 0.032f)], [0, 2, 1, 4, 5, 6, 0, 3, 2, 4, 6, 7, 0, 1, 5, 0, 5, 4, 1, 2, 6, 1, 6, 5, 2, 3, 7, 2, 7, 6, 3, 0, 4, 3, 4, 7]);
    private static readonly IndexedMesh3D SpearMesh4 = new([
        new(1.1f, -0.076f, 0f), new(1.1f, 0f, -0.022f), new(1.1f, 0.076f, 0f), new(1.1f, 0f, 0.022f), new(1.17f, -0.05472f, 0f),
        new(1.17f, 0f, -0.022f), new(1.17f, 0.05472f, 0f), new(1.17f, 0f, 0.022f), new(1.38f, 0f, 0f)], [0, 2, 1, 0, 3, 2, 0, 1, 5, 0, 5, 4, 4, 5, 8, 1, 2, 6, 1, 6, 5, 5, 6, 8, 2, 3, 7, 2, 7, 6, 6, 7, 8, 3, 0, 4, 3, 4, 7, 7, 4, 8]);
    private static readonly IndexedMesh3D SwordMesh1 = Extrude(0.06f,
        new(0.045f, -0.11f, 0f), new(0.055f, -0.11f, 0f), new(0.065f, -0.1f, 0f), new(0.065f, 0.1f, 0f), new(0.055f, 0.11f, 0f),
        new(0.045f, 0.11f, 0f), new(0.035f, 0.1f, 0f), new(0.035f, -0.1f, 0f));
    private static readonly IndexedMesh3D SwordMesh2 = Extrude(0.03f, new(0.065f, -0.075f, 0f), new(0.88f, -0.075f, 0f), new(1.02f, 0.03f, 0f), new(0.96f, 0.075f, 0f), new(0.065f, 0.075f, 0f));
    private static readonly IndexedMesh3D ThrowingRockMesh1 = Extrude(0.16f,
        new(-0.19f, 0.07f, -0.5f), new(-0.08f, -0.01f, -0.5f), new(0.13f, 0.01f, -0.5f), new(0.22f, 0.15f, -0.5f), new(0.13f, 0.33f, -0.5f),
        new(-0.1f, 0.35f, -0.5f), new(-0.22f, 0.21f, -0.5f));
    private static readonly IndexedMesh3D WoodenClubMesh1 = Extrude(0.09f,
        new(-0.14f, -0.04f, 0f), new(0.35f, -0.055f, 0f), new(0.68f, -0.16f, 0f), new(0.94f, -0.14f, 0f), new(1.06f, -0.07f, 0f),
        new(1.08f, 0.05f, 0f), new(0.99f, 0.13f, 0f), new(0.72f, 0.17f, 0f), new(0.35f, 0.055f, 0f), new(-0.14f, 0.04f, 0f));

    /// <summary>Short leather boot.</summary>
    public static ImmutableArray<Attachment> Boot(int mount, float layer = 0) =>
    [
        new MountedSolid("boot/mesh-1", mount, BootMesh1, Paint("#754c38", 0.01f), layer),
        new MountedSolid("boot/mesh-2", mount, BootMesh2, Paint("#3d302b", 0.01f), layer),
        new MountedSolid("boot/mesh-3", mount, BootMesh3, Paint("#b98a59", 0.01f), layer),
    ];

    /// <summary>Short leather boot / left.</summary>
    public static ImmutableArray<Attachment> BootLeft(int mount, float layer = 0) =>
    [
        new MountedSolid("boot-left/mesh-1", mount, BootMesh1, Paint("#754c38", 0.01f), layer),
        new MountedSolid("boot-left/mesh-2", mount, BootMesh2, Paint("#3d302b", 0.01f), layer),
        new MountedSolid("boot-left/mesh-3", mount, BootMesh3, Paint("#b98a59", 0.01f), layer),
    ];

    /// <summary>Short leather boot / right.</summary>
    public static ImmutableArray<Attachment> BootRight(int mount, float layer = 0) =>
    [
        new MountedSolid("boot-right/mesh-1", mount, BootMesh1, Paint("#754c38", 0.01f), layer),
        new MountedSolid("boot-right/mesh-2", mount, BootMesh2, Paint("#3d302b", 0.01f), layer),
        new MountedSolid("boot-right/mesh-3", mount, BootMesh3, Paint("#b98a59", 0.01f), layer),
    ];

    /// <summary>brute / caveman beard.</summary>
    public static ImmutableArray<Attachment> BruteBeard(int mount, float layer = 0) =>
    [
        new MountedSolid("brute-beard/mesh-1", mount, BruteBeardMesh1, Paint("#69402a", 0.045f), layer),
    ];

    /// <summary>brute / wild hair.</summary>
    public static ImmutableArray<Attachment> BruteHair(int mount, float layer = 0) =>
    [
        new MountedSolid("brute-hair/mesh-1", mount, BruteHairMesh1, Paint("#69402a", 0.045f), layer),
        new MountedSolid("brute-hair/mesh-2", mount, BruteHairMesh2, Paint("#69402a", 0.045f), layer),
    ];

    /// <summary>Ochre ragged hide wrap.</summary>
    public static ImmutableArray<Attachment> BruteHideWrap(int mount, float layer = 0) =>
    [
        new MountedSolid("brute-hide-wrap/mesh-1", mount, BruteHideWrapMesh1, Paint("#d7a348", 0.045f), layer),
        new MountedSolid("brute-hide-wrap/mesh-2", mount, BruteHideWrapMesh2, Paint("#705039", 0.045f), layer),
        new MountedSolid("brute-hide-wrap/mesh-3", mount, BruteHideWrapMesh3, Paint("#765033"), layer),
        new MountedSolid("brute-hide-wrap/mesh-4", mount, BruteHideWrapMesh4, Paint("#765033"), layer),
    ];

    /// <summary>cinder / caveman beard.</summary>
    public static ImmutableArray<Attachment> CinderBeard(int mount, float layer = 0) =>
    [
        new MountedSolid("cinder-beard/mesh-1", mount, CinderBeardMesh1, Paint("#843f27", 0.045f), layer),
    ];

    /// <summary>cinder / wild hair.</summary>
    public static ImmutableArray<Attachment> CinderHair(int mount, float layer = 0) =>
    [
        new MountedSolid("cinder-hair/mesh-1", mount, CinderHairMesh1, Paint("#843f27", 0.045f), layer),
        new MountedSolid("cinder-hair/mesh-2", mount, CinderHairMesh2, Paint("#843f27", 0.045f), layer),
    ];

    /// <summary>Russet ragged hide wrap.</summary>
    public static ImmutableArray<Attachment> CinderHideWrap(int mount, float layer = 0) =>
    [
        new MountedSolid("cinder-hide-wrap/mesh-1", mount, CinderHideWrapMesh1, Paint("#b97549", 0.045f), layer),
        new MountedSolid("cinder-hide-wrap/mesh-2", mount, CinderHideWrapMesh2, Paint("#705039", 0.045f), layer),
        new MountedSolid("cinder-hide-wrap/mesh-3", mount, CinderHideWrapMesh3, Paint("#765033"), layer),
        new MountedSolid("cinder-hide-wrap/mesh-4", mount, CinderHideWrapMesh4, Paint("#765033"), layer),
    ];

    /// <summary>Club Caveman Beard.</summary>
    public static ImmutableArray<Attachment> ClubCavemanBeard(int mount, float layer = 0) =>
    [
        new MountedSolid("club-caveman-beard/mesh-1", mount, BruteBeardMesh1, Paint("#69402a", 0.045f), layer),
    ];

    /// <summary>Club Caveman Hair.</summary>
    public static ImmutableArray<Attachment> ClubCavemanHair(int mount, float layer = 0) =>
    [
        new MountedSolid("club-caveman-hair/mesh-1", mount, BruteHairMesh1, Paint("#69402a", 0.045f), layer),
        new MountedSolid("club-caveman-hair/mesh-2", mount, BruteHairMesh2, Paint("#69402a", 0.045f), layer),
    ];

    /// <summary>Club Caveman Hide Wrap.</summary>
    public static ImmutableArray<Attachment> ClubCavemanHideWrap(int mount, float layer = 0) =>
    [
        new MountedSolid("club-caveman-hide-wrap/mesh-1", mount, CinderHideWrapMesh1, Paint("#d7a348", 0.045f), layer),
        new MountedSolid("club-caveman-hide-wrap/mesh-2", mount, CinderHideWrapMesh2, Paint("#705039", 0.045f), layer),
        new MountedSolid("club-caveman-hide-wrap/mesh-3", mount, CinderHideWrapMesh3, Paint("#765033"), layer),
        new MountedSolid("club-caveman-hide-wrap/mesh-4", mount, CinderHideWrapMesh4, Paint("#765033"), layer),
    ];

    /// <summary>Russet leather gloves.</summary>
    public static ImmutableArray<Attachment> GloveLeather(int mount, float layer = 0) =>
    [
        new MountedShape("glove-leather/palm", mount, Ellipse2D.Builder.Create(new(0.053f, 0.068f), new(0f, -0.018f)), Paint("#b7593f", 0.01f, "#402d32"), layer - 0.016f),
        new MountedShape("glove-leather/cuff", mount, Rectangle2D.Builder.FromSize(0.114f, 0.032f, new(0f, 0.034f)), Paint("#e6b87a", 0.008f, "#402d32"), layer - 0.018f),
        new MountedShape("glove-leather/stitch", mount, Rectangle2D.Builder.FromSize(0.058f, 0.009f, new(0f, -0.012f)), Paint("#e6b87a"), layer - 0.02f),
    ];

    /// <summary>Blue steel gauntlets.</summary>
    public static ImmutableArray<Attachment> GloveSteel(int mount, float layer = 0) =>
    [
        new MountedShape("glove-steel/palm", mount, Ellipse2D.Builder.Create(new(0.058f, 0.071f), new(0f, -0.018f)), Paint("#679fb8", 0.011f, "#24384c"), layer - 0.016f),
        new MountedShape("glove-steel/cuff", mount, Rectangle2D.Builder.FromSize(0.124f, 0.055f, new(0f, 0.045f)), Paint("#365b79", 0.009f, "#24384c"), layer - 0.018f),
        new MountedShape("glove-steel/plate", mount, Rectangle2D.Builder.FromSize(0.073f, 0.018f, new(0f, -0.014f)), Paint("#c6e4e9", 0.004f, "#365b79"), layer - 0.02f),
    ];

    /// <summary>Short tousled hair.</summary>
    public static ImmutableArray<Attachment> HairShort(int mount, float layer = 0) =>
    [
        new MountedSolid("hair-short/mesh-1", mount, HairShortMesh1, Paint("#654334", 0.045f), layer),
        new MountedSolid("hair-short/mesh-2", mount, HairShortMesh2, Paint("#654334", 0.045f), layer),
    ];

    /// <summary>Short tousled hair / back.</summary>
    public static ImmutableArray<Attachment> HairShortBack(int mount, float layer = 0) =>
    [
        new MountedSolid("hair-short-back/mesh-1", mount, HairShortBackMesh1, Paint("#654334", 0.045f), layer),
    ];

    /// <summary>Hammer.</summary>
    public static ImmutableArray<Attachment> Hammer(int mount, float layer = 0) =>
    [
        new MountedSolid("hammer/mesh-1", mount, HammerMesh1, Paint("#866344", 0.016f), layer),
        new MountedSolid("hammer/mesh-2", mount, HammerMesh2, Paint("#714b3e", 0.016f), layer),
        new MountedSolid("hammer/mesh-3", mount, HammerMesh3, Paint("#c5a26b", 0.016f), layer),
        new MountedSolid("hammer/mesh-4", mount, HammerMesh4, Paint("#798c96", 0.016f), layer),
        new MountedSolid("hammer/mesh-5", mount, HammerMesh5, Paint("#dce5e7", 0.016f), layer),
        new MountedSolid("hammer/mesh-6", mount, HammerMesh6, Paint("#dce5e7", 0.016f), layer),
        new MountedSolid("hammer/mesh-7", mount, HammerMesh7, Paint("#465963", 0.016f), layer),
    ];
    public static readonly Vector2f HammerTip = new(0.8f, 0f);

    /// <summary>Pistol.</summary>
    public static ImmutableArray<Attachment> Pistol(int mount, float layer = 0) =>
    [
        new MountedSolid("pistol/mesh-1", mount, PistolMesh1, Paint("#714b3e", 0.011f), layer),
        new MountedSolid("pistol/mesh-2", mount, PistolMesh2, Paint("#465963", 0.011f), layer),
        new MountedSolid("pistol/mesh-3", mount, PistolMesh3, Paint("#dce5e7", 0.011f), layer),
        new MountedSolid("pistol/mesh-4", mount, PistolMesh4, Paint("#c5a26b", 0.011f), layer),
        new MountedSolid("pistol/mesh-5", mount, PistolMesh5, Paint("#293941", 0.011f), layer),
    ];
    public static readonly Vector2f PistolTip = new(0.26f, 0.06f);
    public static readonly Vector2f PistolMuzzle = new(0.26f, 0.06f);

    /// <summary>Hide stone bag with teal sling.</summary>
    public static ImmutableArray<Attachment> RockThrowerBag(int mount, float layer = 0) =>
    [
        new MountedSolid("rock-thrower-bag/mesh-1", mount, RockThrowerBagMesh1, Paint("#987347", 0.025f, "#30373a"), layer),
        new MountedSolid("rock-thrower-bag/mesh-2", mount, RockThrowerBagMesh2, Paint("#538e89", 0.025f, "#30373a"), layer),
    ];

    /// <summary>Rock thrower / beard.</summary>
    public static ImmutableArray<Attachment> RockThrowerBeard(int mount, float layer = 0) =>
    [
        new MountedSolid("rock-thrower-beard/mesh-1", mount, CinderBeardMesh1, Paint("#69402a", 0.045f), layer),
    ];

    /// <summary>Rock thrower / hair.</summary>
    public static ImmutableArray<Attachment> RockThrowerHair(int mount, float layer = 0) =>
    [
        new MountedSolid("rock-thrower-hair/mesh-1", mount, CinderHairMesh1, Paint("#69402a", 0.045f), layer),
        new MountedSolid("rock-thrower-hair/mesh-2", mount, CinderHairMesh2, Paint("#69402a", 0.045f), layer),
    ];

    /// <summary>Rock thrower / hide-wrap.</summary>
    public static ImmutableArray<Attachment> RockThrowerHideWrap(int mount, float layer = 0) =>
    [
        new MountedSolid("rock-thrower-hide-wrap/mesh-1", mount, CinderHideWrapMesh1, Paint("#b97549", 0.045f), layer),
        new MountedSolid("rock-thrower-hide-wrap/mesh-2", mount, CinderHideWrapMesh2, Paint("#705039", 0.045f), layer),
        new MountedSolid("rock-thrower-hide-wrap/mesh-3", mount, CinderHideWrapMesh3, Paint("#765033"), layer),
        new MountedSolid("rock-thrower-hide-wrap/mesh-4", mount, CinderHideWrapMesh4, Paint("#765033"), layer),
    ];

    /// <summary>Leather sandal.</summary>
    public static ImmutableArray<Attachment> Sandal(int mount, float layer = 0) =>
    [
        new MountedSolid("sandal/mesh-1", mount, SandalMesh1, Paint("#68432e", 0.008f), layer),
        new MountedSolid("sandal/mesh-2", mount, SandalMesh2, Paint("#bc8853", 0.008f), layer),
        new MountedSolid("sandal/mesh-3", mount, SandalMesh3, Paint("#bc8853", 0.008f), layer),
    ];

    /// <summary>Leather sandal / left.</summary>
    public static ImmutableArray<Attachment> SandalLeft(int mount, float layer = 0) =>
    [
        new MountedSolid("sandal-left/mesh-1", mount, SandalMesh1, Paint("#68432e", 0.008f), layer),
        new MountedSolid("sandal-left/mesh-2", mount, SandalMesh2, Paint("#bc8853", 0.008f), layer),
        new MountedSolid("sandal-left/mesh-3", mount, SandalMesh3, Paint("#bc8853", 0.008f), layer),
    ];

    /// <summary>Leather sandal / right.</summary>
    public static ImmutableArray<Attachment> SandalRight(int mount, float layer = 0) =>
    [
        new MountedSolid("sandal-right/mesh-1", mount, SandalMesh1, Paint("#68432e", 0.008f), layer),
        new MountedSolid("sandal-right/mesh-2", mount, SandalMesh2, Paint("#bc8853", 0.008f), layer),
        new MountedSolid("sandal-right/mesh-3", mount, SandalMesh3, Paint("#bc8853", 0.008f), layer),
    ];

    /// <summary>Scavenged shell shield.</summary>
    public static ImmutableArray<Attachment> ScavengedShell(int mount, float layer = 0) =>
    [
        new MountedSolid("scavenged-shell/mesh-1", mount, ScavengedShellMesh1, Paint("#4b6653", 0.03f, "#303b32"), layer),
        new MountedSolid("scavenged-shell/mesh-2", mount, ScavengedShellMesh2, Paint("#91a276", 0.03f, "#303b32"), layer),
        new MountedSolid("scavenged-shell/mesh-3", mount, ScavengedShellMesh3, Paint("#738e65", 0.03f, "#303b32"), layer),
        new MountedSolid("scavenged-shell/mesh-4", mount, ScavengedShellMesh4, Paint("#738e65", 0.03f, "#303b32"), layer),
    ];

    /// <summary>Sheath.</summary>
    public static ImmutableArray<Attachment> Sheath(int mount, float layer = 0) =>
    [
        new MountedSolid("sheath/mesh-1", mount, SheathMesh1, Paint("#344b53", 0.012f), layer),
        new MountedSolid("sheath/mesh-2", mount, SheathMesh2, Paint("#c5a26b", 0.012f), layer),
        new MountedSolid("sheath/mesh-3", mount, SheathMesh3, Paint("#c5a26b", 0.012f), layer),
    ];
    public static readonly Vector2f SheathTip = new(0.85f, 0f);

    /// <summary>Shell defender / beard.</summary>
    public static ImmutableArray<Attachment> ShieldDefenderBeard(int mount, float layer = 0) =>
    [
        new MountedSolid("shield-defender-beard/mesh-1", mount, BruteBeardMesh1, Paint("#69402a", 0.045f), layer),
    ];

    /// <summary>Shell defender / hair.</summary>
    public static ImmutableArray<Attachment> ShieldDefenderHair(int mount, float layer = 0) =>
    [
        new MountedSolid("shield-defender-hair/mesh-1", mount, BruteHairMesh1, Paint("#69402a", 0.045f), layer),
        new MountedSolid("shield-defender-hair/mesh-2", mount, BruteHairMesh2, Paint("#69402a", 0.045f), layer),
    ];

    /// <summary>Shell defender / hide-wrap.</summary>
    public static ImmutableArray<Attachment> ShieldDefenderHideWrap(int mount, float layer = 0) =>
    [
        new MountedSolid("shield-defender-hide-wrap/mesh-1", mount, CinderHideWrapMesh1, Paint("#d7a348", 0.045f), layer),
        new MountedSolid("shield-defender-hide-wrap/mesh-2", mount, CinderHideWrapMesh2, Paint("#705039", 0.045f), layer),
        new MountedSolid("shield-defender-hide-wrap/mesh-3", mount, CinderHideWrapMesh3, Paint("#765033"), layer),
        new MountedSolid("shield-defender-hide-wrap/mesh-4", mount, CinderHideWrapMesh4, Paint("#765033"), layer),
    ];

    /// <summary>Spear.</summary>
    public static ImmutableArray<Attachment> Spear(int mount, float layer = 0) =>
    [
        new MountedSolid("spear/mesh-1", mount, SpearMesh1, Paint("#876646", 0.013f), layer),
        new MountedSolid("spear/mesh-2", mount, SpearMesh2, Paint("#465963", 0.013f), layer),
        new MountedSolid("spear/mesh-3", mount, SpearMesh3, Paint("#c5a26b", 0.013f), layer),
        new MountedSolid("spear/mesh-4", mount, SpearMesh4, Paint("#dce5e7", 0.013f), layer),
    ];
    public static readonly Vector2f SpearTip = new(1.38f, 0f);
    public static readonly Vector2f SpearSecondGrip = new(-0.3f, 0f);

    /// <summary>Sword.</summary>
    public static ImmutableArray<Attachment> Sword(int mount, float layer = 0) =>
    [
        new MountedSolid("sword/mesh-1", mount, SwordMesh1, Paint("#465963", 0.02f), layer),
        new MountedSolid("sword/mesh-2", mount, SwordMesh2, Paint("#dce5e7", 0.02f), layer),
        new MountedPath("sword/shape-1", mount, [new(-0.055f, 0f), new(0.025f, 0f)], Paint("#714b3e", 0.02f, stroke: 0.006f), layer),
    ];
    public static readonly Vector2f SwordTip = new(1.02f, 0.03f);

    /// <summary>Throwing rock.</summary>
    public static ImmutableArray<Attachment> ThrowingRock(int mount, float layer = 0) =>
    [
        new MountedSolid("throwing-rock/mesh-1", mount, ThrowingRockMesh1, Paint("#858b86", 0.025f, "#30373a"), layer),
    ];
    public static readonly Vector2f ThrowingRockTip = new(0f, 0.17f);
    public static readonly Vector2f ThrowingRockMuzzle = new(0f, 0.17f);

    /// <summary>Heavy wooden club.</summary>
    public static ImmutableArray<Attachment> WoodenClub(int mount, float layer = 0) =>
    [
        new MountedSolid("wooden-club/mesh-1", mount, WoodenClubMesh1, Paint("#895a32", 0.025f, "#392b23"), layer),
    ];
    public static readonly Vector2f WoodenClubTip = new(0.98f, 0f);

    /// <summary>Every thing by the name its authored part had.</summary>
    public static IReadOnlyDictionary<string, Func<int, float, ImmutableArray<Attachment>>> ByName { get; } =
        new Dictionary<string, Func<int, float, ImmutableArray<Attachment>>>(StringComparer.Ordinal)
        {
            ["boot"] = Boot,
            ["boot-left"] = BootLeft,
            ["boot-right"] = BootRight,
            ["brute-beard"] = BruteBeard,
            ["brute-hair"] = BruteHair,
            ["brute-hide-wrap"] = BruteHideWrap,
            ["cinder-beard"] = CinderBeard,
            ["cinder-hair"] = CinderHair,
            ["cinder-hide-wrap"] = CinderHideWrap,
            ["club-caveman-beard"] = ClubCavemanBeard,
            ["club-caveman-hair"] = ClubCavemanHair,
            ["club-caveman-hide-wrap"] = ClubCavemanHideWrap,
            ["glove-leather"] = GloveLeather,
            ["glove-steel"] = GloveSteel,
            ["hair-short"] = HairShort,
            ["hair-short-back"] = HairShortBack,
            ["hammer"] = Hammer,
            ["pistol"] = Pistol,
            ["rock-thrower-bag"] = RockThrowerBag,
            ["rock-thrower-beard"] = RockThrowerBeard,
            ["rock-thrower-hair"] = RockThrowerHair,
            ["rock-thrower-hide-wrap"] = RockThrowerHideWrap,
            ["sandal"] = Sandal,
            ["sandal-left"] = SandalLeft,
            ["sandal-right"] = SandalRight,
            ["scavenged-shell"] = ScavengedShell,
            ["sheath"] = Sheath,
            ["shield-defender-beard"] = ShieldDefenderBeard,
            ["shield-defender-hair"] = ShieldDefenderHair,
            ["shield-defender-hide-wrap"] = ShieldDefenderHideWrap,
            ["spear"] = Spear,
            ["sword"] = Sword,
            ["throwing-rock"] = ThrowingRock,
            ["wooden-club"] = WoodenClub,
        };
}
