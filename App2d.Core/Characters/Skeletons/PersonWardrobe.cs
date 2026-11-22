using App2d.Core.Characters.Attachments;
using App2d.Core.Geometry.Meshes;
using App2d.Core.Geometry.Shapes;
using System.Collections.Immutable;
using static App2d.Core.Characters.Attachments.Paints;

namespace App2d.Core.Characters.Skeletons;

/// <summary>What a person wears and holds: each function returns one thing's pieces as attachments on a mount of <see cref="PersonSkeleton"/>, at a layer.</summary>
public static class PersonWardrobe
{
    /// <summary>A flat outline pushed out to a thickness.</summary>
    private static IndexedMesh3D Extrude(float thickness, params ReadOnlySpan<Vector3f> outline) =>
        MeshGenerators3D.Extrude(MeshGenerators3D.CounterClockwise(outline.ToArray()), thickness);

    private static readonly IndexedMesh3D BootMesh1 = Extrude(0.08f,
        new(-0.058f, -0.023f, 0f), new(0.145f, -0.023f, 0f), new(0.153f, -0.006f, 0f), new(0.145f, 0.027f, 0f), new(0.11f, 0.045f, 0f),
        new(0.047f, 0.055f, 0f), new(0.043f, 0.165f, 0f), new(-0.05f, 0.165f, 0f));
    private static readonly IndexedMesh3D BootMesh2 = Extrude(0.008f, new(-0.06f, -0.024f, -0.048f), new(0.147f, -0.024f, -0.048f), new(0.153f, -0.009f, -0.048f), new(-0.06f, -0.009f, -0.048f));
    private static readonly IndexedMesh3D BootMesh3 = Extrude(0.008f, new(-0.052f, 0.135f, -0.048f), new(0.045f, 0.135f, -0.048f), new(0.045f, 0.163f, -0.048f), new(-0.052f, 0.163f, -0.048f));
    private static readonly IndexedMesh3D CavemanBeardSmall = Extrude(0.008f,
        new(-0.2993f, -0.1966f, -0.16f), new(-0.247f, -0.2076f, -0.16f), new(-0.285f, -0.3277f, -0.16f), new(-0.19f, -0.295f, -0.16f),
        new(-0.2042f, -0.4152f, -0.16f), new(-0.0998f, -0.3933f, -0.16f), new(-0.0712f, -0.5026f, -0.16f), new(0.0143f, -0.437f, -0.16f),
        new(0.076f, -0.5353f, -0.16f), new(0.1425f, -0.3824f, -0.16f), new(0.228f, -0.4261f, -0.16f), new(0.228f, -0.2731f, -0.16f),
        new(0.323f, -0.3168f, -0.16f), new(0.266f, -0.1857f, -0.16f), new(0.304f, -0.142f, -0.16f), new(0.2565f, -0.0492f, -0.16f),
        new(0.209f, -0.0874f, -0.16f), new(0.19f, -0.1966f, -0.16f), new(-0.076f, -0.1966f, -0.16f), new(-0.0998f, -0.1092f, -0.16f),
        new(-0.171f, -0.0601f, -0.16f), new(-0.2422f, 0.0164f, -0.16f));
    private static readonly IndexedMesh3D WildHairLowerSmall = Extrude(0.008f,
        new(-0.2755f, 0.19f, -0.095f), new(-0.3515f, 0.057f, -0.095f), new(-0.2945f, 0.057f, -0.095f), new(-0.3705f, -0.0855f, -0.095f),
        new(-0.2945f, -0.057f, -0.095f), new(-0.304f, -0.2185f, -0.095f), new(-0.1995f, -0.1615f, -0.095f), new(0.2185f, -0.171f, -0.095f),
        new(0.323f, -0.2185f, -0.095f), new(0.2945f, -0.0475f, -0.095f), new(0.361f, -0.095f, -0.095f), new(0.304f, 0.1235f, -0.095f),
        new(0.2185f, 0.2565f, -0.095f));
    private static readonly IndexedMesh3D WildHairUpperSmall = Extrude(0.008f,
        new(-0.209f, 0.0268f, -0.15f), new(-0.171f, 0.1288f, -0.15f), new(-0.1045f, 0.2362f, -0.15f), new(-0.0143f, 0.204f, -0.15f),
        new(0.0665f, 0.2576f, -0.15f), new(0.1615f, 0.1932f, -0.15f), new(0.2755f, 0.204f, -0.15f), new(0.2185f, 0.2684f, -0.15f),
        new(0.266f, 0.3221f, -0.15f), new(0.1425f, 0.3328f, -0.15f), new(0.1805f, 0.4079f, -0.15f), new(0.0428f, 0.365f, -0.15f),
        new(0.0333f, 0.4616f, -0.15f), new(-0.057f, 0.365f, -0.15f), new(-0.1425f, 0.4294f, -0.15f), new(-0.1425f, 0.3221f, -0.15f),
        new(-0.2755f, 0.3435f, -0.15f), new(-0.2185f, 0.2469f, -0.15f), new(-0.285f, 0.2576f, -0.15f), new(-0.2565f, 0.1074f, -0.15f));
    private static readonly IndexedMesh3D HideWrapBodyWide = Extrude(0.3918f,
        new(-0.29f, -0.0788f, 0.0109f), new(-0.1668f, -0.0525f, 0.0109f), new(-0.0798f, -0.1155f, 0.0109f),
        new(0.0145f, -0.0683f, 0.0109f), new(0.1087f, -0.1207f, 0.0109f), new(0.1812f, -0.0525f, 0.0109f),
        new(0.2973f, -0.0788f, 0.0109f), new(0.261f, 0.084f, 0.0109f), new(-0.261f, 0.084f, 0.0109f));
    private static readonly IndexedMesh3D HideWrapBeltWide = Extrude(0.0046f,
        new(-0.2755f, 0.0262f, -0.1953f), new(0.2755f, 0.0262f, -0.1953f), new(0.2682f, 0.0892f, -0.1953f),
        new(-0.2682f, 0.0892f, -0.1953f));
    private static readonly IndexedMesh3D HideWrapLeftSpotWide = Extrude(0.0046f,
        new(-0.1371f, -0.0158f, -0.1953f), new(-0.1428f, -0.0009f, -0.1953f), new(-0.1556f, 0.0028f, -0.1953f),
        new(-0.1659f, -0.0075f, -0.1953f), new(-0.1659f, -0.024f, -0.1953f), new(-0.1556f, -0.0343f, -0.1953f),
        new(-0.1428f, -0.0306f, -0.1953f));
    private static readonly IndexedMesh3D HideWrapRightSpotWide = Extrude(0.0046f,
        new(0.1481f, -0.021f, -0.1953f), new(0.1415f, -0.0038f, -0.1953f), new(0.1266f, 0.0004f, -0.1953f),
        new(0.1146f, -0.0115f, -0.1953f), new(0.1146f, -0.0305f, -0.1953f), new(0.1266f, -0.0424f, -0.1953f),
        new(0.1415f, -0.0382f, -0.1953f));
    private static readonly IndexedMesh3D CavemanBeardLarge = Extrude(0.008f,
        new(-0.3307f, -0.1739f, -0.16f), new(-0.273f, -0.1835f, -0.16f), new(-0.315f, -0.2898f, -0.16f), new(-0.21f, -0.2608f, -0.16f),
        new(-0.2258f, -0.3671f, -0.16f), new(-0.1103f, -0.3478f, -0.16f), new(-0.0788f, -0.4444f, -0.16f), new(0.0158f, -0.3864f, -0.16f),
        new(0.084f, -0.4733f, -0.16f), new(0.1575f, -0.3381f, -0.16f), new(0.252f, -0.3767f, -0.16f), new(0.252f, -0.2415f, -0.16f),
        new(0.357f, -0.2801f, -0.16f), new(0.294f, -0.1642f, -0.16f), new(0.336f, -0.1256f, -0.16f), new(0.2835f, -0.0435f, -0.16f),
        new(0.231f, -0.0773f, -0.16f), new(0.21f, -0.1739f, -0.16f), new(-0.084f, -0.1739f, -0.16f), new(-0.1103f, -0.0966f, -0.16f),
        new(-0.189f, -0.0531f, -0.16f), new(-0.2677f, 0.0145f, -0.16f));
    private static readonly IndexedMesh3D WildHairLowerLarge = Extrude(0.008f,
        new(-0.3045f, 0.21f, -0.095f), new(-0.3885f, 0.063f, -0.095f), new(-0.3255f, 0.063f, -0.095f), new(-0.4095f, -0.0945f, -0.095f),
        new(-0.3255f, -0.063f, -0.095f), new(-0.336f, -0.2415f, -0.095f), new(-0.2205f, -0.1785f, -0.095f), new(0.2415f, -0.189f, -0.095f),
        new(0.357f, -0.2415f, -0.095f), new(0.3255f, -0.0525f, -0.095f), new(0.399f, -0.105f, -0.095f), new(0.336f, 0.1365f, -0.095f),
        new(0.2415f, 0.2835f, -0.095f));
    private static readonly IndexedMesh3D WildHairUpperLarge = Extrude(0.008f,
        new(-0.231f, 0.0297f, -0.15f), new(-0.189f, 0.1424f, -0.15f), new(-0.1155f, 0.261f, -0.15f), new(-0.0158f, 0.2254f, -0.15f),
        new(0.0735f, 0.2848f, -0.15f), new(0.1785f, 0.2136f, -0.15f), new(0.3045f, 0.2254f, -0.15f), new(0.2415f, 0.2966f, -0.15f),
        new(0.294f, 0.3559f, -0.15f), new(0.1575f, 0.3678f, -0.15f), new(0.1995f, 0.4509f, -0.15f), new(0.0473f, 0.4034f, -0.15f),
        new(0.0367f, 0.5102f, -0.15f), new(-0.063f, 0.4034f, -0.15f), new(-0.1575f, 0.4746f, -0.15f), new(-0.1575f, 0.3559f, -0.15f),
        new(-0.3045f, 0.3797f, -0.15f), new(-0.2415f, 0.2729f, -0.15f), new(-0.315f, 0.2848f, -0.15f), new(-0.2835f, 0.1187f, -0.15f));
    private static readonly IndexedMesh3D HideWrapBody = Extrude(0.2837f,
        new(-0.21f, -0.0712f, 0.0079f), new(-0.1207f, -0.0475f, 0.0079f), new(-0.0578f, -0.1045f, 0.0079f),
        new(0.0105f, -0.0617f, 0.0079f), new(0.0788f, -0.1092f, 0.0079f), new(0.1313f, -0.0475f, 0.0079f),
        new(0.2152f, -0.0712f, 0.0079f), new(0.189f, 0.076f, 0.0079f), new(-0.189f, 0.076f, 0.0079f));
    private static readonly IndexedMesh3D HideWrapBelt = Extrude(0.0033f,
        new(-0.1995f, 0.0238f, -0.1414f), new(0.1995f, 0.0238f, -0.1414f), new(0.1943f, 0.0808f, -0.1414f),
        new(-0.1943f, 0.0808f, -0.1414f));
    private static readonly IndexedMesh3D HideWrapLeftSpot = Extrude(0.0033f,
        new(-0.095f, -0.0143f, -0.1414f), new(-0.1008f, 0.0006f, -0.1414f), new(-0.1136f, 0.0043f, -0.1414f),
        new(-0.1239f, -0.006f, -0.1414f), new(-0.1239f, -0.0225f, -0.1414f), new(-0.1136f, -0.0328f, -0.1414f),
        new(-0.1008f, -0.0291f, -0.1414f));
    private static readonly IndexedMesh3D HideWrapRightSpot = Extrude(0.0033f,
        new(0.1121f, -0.019f, -0.1414f), new(0.1055f, -0.0018f, -0.1414f), new(0.0906f, 0.0024f, -0.1414f),
        new(0.0786f, -0.0095f, -0.1414f), new(0.0786f, -0.0285f, -0.1414f), new(0.0906f, -0.0404f, -0.1414f),
        new(0.1055f, -0.0362f, -0.1414f));
    private static readonly IndexedMesh3D HairShortMesh1 = Extrude(0.008f,
        new(-0.25f, 0.18f, -0.095f), new(-0.28f, 0.03f, -0.095f), new(-0.25f, -0.14f, -0.095f), new(-0.19f, -0.17f, -0.095f),
        new(-0.19f, 0.19f, -0.095f));
    private static readonly IndexedMesh3D HairShortMesh2 = Extrude(0.008f,
        new(-0.22f, 0.0217f, -0.15f), new(-0.18f, 0.1044f, -0.15f), new(-0.11f, 0.1914f, -0.15f), new(-0.015f, 0.1653f, -0.15f),
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
        new(1.1f, -0.076f, 0f), new(1.1f, 0f, -0.022f), new(1.1f, 0.076f, 0f), new(1.1f, 0f, 0.022f), new(1.17f, -0.0547f, 0f),
        new(1.17f, 0f, -0.022f), new(1.17f, 0.0547f, 0f), new(1.17f, 0f, 0.022f), new(1.38f, 0f, 0f)], [0, 2, 1, 0, 3, 2, 0, 1, 5, 0, 5, 4, 4, 5, 8, 1, 2, 6, 1, 6, 5, 5, 6, 8, 2, 3, 7, 2, 7, 6, 6, 7, 8, 3, 0, 4, 3, 4, 7, 7, 4, 8]);
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

    /// <summary>A short leather boot. The same boot goes on either foot.</summary>
    public static ImmutableArray<Attachment> Boot(int mount, float layer = 0) =>
    [
        new MountedSolid("boot/mesh-1", mount, BootMesh1, Paint("#754c38", 0.01f), layer),
        new MountedSolid("boot/mesh-2", mount, BootMesh2, Paint("#3d302b", 0.01f), layer),
        new MountedSolid("boot/mesh-3", mount, BootMesh3, Paint("#b98a59", 0.01f), layer),
    ];

    /// <summary>The colours wild hair and a caveman's beard come in.</summary>
    public const string HairBrown = "#69402a", HairRusset = "#843f27";
    /// <summary>The colours a hide wrap comes in.</summary>
    public const string HideOchre = "#d7a348", HideRusset = "#b97549";

    /// <summary>A caveman's ragged beard, in a hair colour. The large one is a tenth bigger.</summary>
    public static ImmutableArray<Attachment> CavemanBeard(int mount, string color, bool large = false, float layer = 0) =>
    [
        new MountedSolid("caveman-beard", mount, large ? CavemanBeardLarge : CavemanBeardSmall, Paint(color, 0.045f), layer),
    ];

    /// <summary>Wild hair in two ragged pieces, one round the head and one on top, in a hair colour. The large one is a tenth bigger.</summary>
    public static ImmutableArray<Attachment> WildHair(int mount, string color, bool large = false, float layer = 0) =>
    [
        new MountedSolid("wild-hair/lower", mount, large ? WildHairLowerLarge : WildHairLowerSmall, Paint(color, 0.045f), layer),
        new MountedSolid("wild-hair/upper", mount, large ? WildHairUpperLarge : WildHairUpperSmall, Paint(color, 0.045f), layer),
    ];

    /// <summary>A ragged hide wrapped round the body, in a hide colour, with a darker belt and two spots. The wide one fits a broad build.</summary>
    public static ImmutableArray<Attachment> HideWrap(int mount, string color, bool wide = false, float layer = 0) =>
    [
        new MountedSolid("hide-wrap/body", mount, wide ? HideWrapBodyWide : HideWrapBody, Paint(color, 0.045f), layer),
        new MountedSolid("hide-wrap/belt", mount, wide ? HideWrapBeltWide : HideWrapBelt, Paint("#705039", 0.045f), layer),
        new MountedSolid("hide-wrap/left-spot", mount, wide ? HideWrapLeftSpotWide : HideWrapLeftSpot, Paint("#765033"), layer),
        new MountedSolid("hide-wrap/right-spot", mount, wide ? HideWrapRightSpotWide : HideWrapRightSpot, Paint("#765033"), layer),
    ];

    /// <summary>A copper greave for the left shin; flip it for the right.</summary>
    public static ImmutableArray<Attachment> CopperGreaves(int mount, float layer = 0) =>
    [
        new MountedShape("copper-greaves/piece", mount, SimplePolygon2D.Builder.Create([new(0.0081f, 0.0725f), new(-0.0179f, 0.0482f), new(-0.0208f, 0.4086f), new(0.0052f, 0.4115f), new(0.0289f, 0.2781f)]), Paint("#856d43"), layer),
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

    /// <summary>A leather sandal. The same sandal goes on either foot.</summary>
    public static ImmutableArray<Attachment> Sandal(int mount, float layer = 0) =>
    [
        new MountedSolid("sandal/mesh-1", mount, SandalMesh1, Paint("#68432e", 0.008f), layer),
        new MountedSolid("sandal/mesh-2", mount, SandalMesh2, Paint("#bc8853", 0.008f), layer),
        new MountedSolid("sandal/mesh-3", mount, SandalMesh3, Paint("#bc8853", 0.008f), layer),
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

}
