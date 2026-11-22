using App2d.Core.Characters.Attachments;
using App2d.Core.Geometry.Shapes;
using System.Collections.Immutable;
using static App2d.Core.Characters.Attachments.Paints;
using static App2d.Core.Characters.Skeletons.QuadrupedSkeleton;

namespace App2d.Core.Characters.Skeletons;

/// <summary>The drawn bodies of the four-legged animals, as attachments on the builds of <see cref="QuadrupedSkeleton"/>.</summary>
public static class QuadrupedBodies
{
    /// <summary>The plain quadruped, on <see cref="QuadrupedSkeleton.Skeleton"/>.</summary>
    public static ImmutableArray<Attachment> Quadruped { get; } =
    [
        new MountedShape("tail", TailToTailUp, SimplePolygon2D.Builder.Create([
            new(0.075f, -0.1f), new(-0.52f, -0.0584f), new(-0.775f, 0.1964f), new(-0.452f, 0.0924f), new(0.075f, 0.264f)]), Paint("#79bdb0", 0.045f, "#242a29"), 0.02f),
        new MountedShape("far-hind-upper", FarHindRootToFarHindJoint, Ellipse2D.Builder.Create(new(0.125f, 0.3012f), new(0f, 0.2062f)), Paint("#527f78", 0.045f, "#242a29")),
        new MountedShape("far-hind-lower", FarHindJointToFarHindFoot, Ellipse2D.Builder.Create(new(0.115f, 0.2681f), new(0f, 0.1931f)), Paint("#527f78", 0.045f, "#242a29"), -0.005f),
        new MountedShape("far-hind-paw", FarHindFootUpright, SimplePolygon2D.Builder.Create([
            new(-0.165f, -0.093f), new(0.1842f, -0.093f), new(0.195f, -0.0094f), new(0.1014f, 0.094f), new(-0.075f, 0.105f), new(-0.1542f, 0.0214f)]), Paint("#527f78", 0.045f, "#242a29"), -0.015f),
        new MountedShape("far-hind-toe-0", FarHindFootUpright, Ellipse2D.Builder.Create(new(0.0225f, 0.0325f), new(-0.08f, -0.055f)), Paint("#f6e4bb", 0.012f, "#242a29"), -0.025f),
        new MountedShape("far-hind-toe-1", FarHindFootUpright, Ellipse2D.Builder.Create(new(0.0225f, 0.0325f), new(0.005f, -0.055f)), Paint("#f6e4bb", 0.012f, "#242a29"), -0.025f),
        new MountedShape("far-hind-toe-2", FarHindFootUpright, Ellipse2D.Builder.Create(new(0.0225f, 0.0325f), new(0.09f, -0.055f)), Paint("#f6e4bb", 0.012f, "#242a29"), -0.025f),
        new MountedShape("far-front-upper", FarFrontRootToFarFrontJoint, Ellipse2D.Builder.Create(new(0.125f, 0.2942f), new(0f, 0.1992f)), Paint("#527f78", 0.045f, "#242a29")),
        new MountedShape("far-front-lower", FarFrontJointToFarFrontFoot, Ellipse2D.Builder.Create(new(0.115f, 0.2681f), new(0f, 0.1931f)), Paint("#527f78", 0.045f, "#242a29"), -0.005f),
        new MountedShape("far-front-paw", FarFrontFootUpright, SimplePolygon2D.Builder.Create([
            new(-0.165f, -0.093f), new(0.1842f, -0.093f), new(0.195f, -0.0094f), new(0.1014f, 0.094f), new(-0.075f, 0.105f), new(-0.1542f, 0.0214f)]), Paint("#527f78", 0.045f, "#242a29"), -0.015f),
        new MountedShape("far-front-toe-0", FarFrontFootUpright, Ellipse2D.Builder.Create(new(0.0225f, 0.0325f), new(-0.08f, -0.055f)), Paint("#f6e4bb", 0.012f, "#242a29"), -0.025f),
        new MountedShape("far-front-toe-1", FarFrontFootUpright, Ellipse2D.Builder.Create(new(0.0225f, 0.0325f), new(0.005f, -0.055f)), Paint("#f6e4bb", 0.012f, "#242a29"), -0.025f),
        new MountedShape("far-front-toe-2", FarFrontFootUpright, Ellipse2D.Builder.Create(new(0.0225f, 0.0325f), new(0.09f, -0.055f)), Paint("#f6e4bb", 0.012f, "#242a29"), -0.025f),
        new MountedShape("near-hind-upper", NearHindRootToNearHindJoint, Ellipse2D.Builder.Create(new(0.125f, 0.3012f), new(0f, 0.2062f)), Paint("#79bdb0", 0.045f, "#242a29")),
        new MountedShape("near-hind-lower", NearHindJointToNearHindFoot, Ellipse2D.Builder.Create(new(0.115f, 0.2681f), new(0f, 0.1931f)), Paint("#79bdb0", 0.045f, "#242a29"), -0.005f),
        new MountedShape("near-hind-paw", NearHindFootUpright, SimplePolygon2D.Builder.Create([
            new(-0.165f, -0.093f), new(0.1842f, -0.093f), new(0.195f, -0.0094f), new(0.1014f, 0.094f), new(-0.075f, 0.105f), new(-0.1542f, 0.0214f)]), Paint("#79bdb0", 0.045f, "#242a29"), -0.015f),
        new MountedShape("near-hind-toe-0", NearHindFootUpright, Ellipse2D.Builder.Create(new(0.0225f, 0.0325f), new(-0.08f, -0.055f)), Paint("#f6e4bb", 0.012f, "#242a29"), -0.025f),
        new MountedShape("near-hind-toe-1", NearHindFootUpright, Ellipse2D.Builder.Create(new(0.0225f, 0.0325f), new(0.005f, -0.055f)), Paint("#f6e4bb", 0.012f, "#242a29"), -0.025f),
        new MountedShape("near-hind-toe-2", NearHindFootUpright, Ellipse2D.Builder.Create(new(0.0225f, 0.0325f), new(0.09f, -0.055f)), Paint("#f6e4bb", 0.012f, "#242a29"), -0.025f),
        new MountedShape("near-front-upper", NearFrontRootToNearFrontJoint, Ellipse2D.Builder.Create(new(0.125f, 0.2942f), new(0f, 0.1992f)), Paint("#79bdb0", 0.045f, "#242a29")),
        new MountedShape("near-front-lower", NearFrontJointToNearFrontFoot, Ellipse2D.Builder.Create(new(0.115f, 0.2681f), new(0f, 0.1931f)), Paint("#79bdb0", 0.045f, "#242a29"), -0.005f),
        new MountedShape("near-front-paw", NearFrontFootUpright, SimplePolygon2D.Builder.Create([
            new(-0.165f, -0.093f), new(0.1842f, -0.093f), new(0.195f, -0.0094f), new(0.1014f, 0.094f), new(-0.075f, 0.105f), new(-0.1542f, 0.0214f)]), Paint("#79bdb0", 0.045f, "#242a29"), -0.015f),
        new MountedShape("near-front-toe-0", NearFrontFootUpright, Ellipse2D.Builder.Create(new(0.0225f, 0.0325f), new(-0.08f, -0.055f)), Paint("#f6e4bb", 0.012f, "#242a29"), -0.025f),
        new MountedShape("near-front-toe-1", NearFrontFootUpright, Ellipse2D.Builder.Create(new(0.0225f, 0.0325f), new(0.005f, -0.055f)), Paint("#f6e4bb", 0.012f, "#242a29"), -0.025f),
        new MountedShape("near-front-toe-2", NearFrontFootUpright, Ellipse2D.Builder.Create(new(0.0225f, 0.0325f), new(0.09f, -0.055f)), Paint("#f6e4bb", 0.012f, "#242a29"), -0.025f),
        new MountedShape("body", BodyToBodyUp, Ellipse2D.Builder.Create(new(0.96f, 0.45f), new(-0.05f, 0.04f)), Paint("#79bdb0", 0.045f, "#242a29")) { Patches = [new("#397783", [new(-0.338f, 0.175f), new(-0.3612f, 0.2605f), new(-0.4244f, 0.3231f), new(-0.5108f, 0.346f), new(-0.5972f, 0.3231f), new(-0.6604f, 0.2605f), new(-0.6836f, 0.175f), new(-0.6604f, 0.0895f), new(-0.5972f, 0.0269f), new(-0.5108f, 0.004f), new(-0.4244f, 0.0269f), new(-0.3612f, 0.0895f)]), new("#397783", [new(-0.002f, 0.067f), new(-0.0187f, 0.13f), new(-0.0644f, 0.1761f), new(-0.1268f, 0.193f), new(-0.1892f, 0.1761f), new(-0.2349f, 0.13f), new(-0.2516f, 0.067f), new(-0.2349f, 0.004f), new(-0.1892f, -0.0421f), new(-0.1268f, -0.059f), new(-0.0644f, -0.0421f), new(-0.0187f, 0.004f)])] },
        new MountedShape("head", HeadToHeadUp, Ellipse2D.Builder.Create(new(0.37f, 0.29f), new(0.13f, 0.02f)), Paint("#79bdb0", 0.045f, "#242a29"), -0.24f),
        new MountedShape("eye", HeadToHeadUp, Ellipse2D.Builder.Create(new(0.045f, 0.06f), new(0.34f, 0.01f)), Paint("#242a29"), -0.31f),
        new MountedShape("brow", HeadToHeadUp, SimplePolygon2D.Builder.Create([
            new(0.25f, 0.155f), new(0.41f, 0.1028f), new(0.386f, 0.065f), new(0.25f, 0.119f)]), Paint("#242a29"), -0.31f),
        new MountedShape("mouth", HeadToHeadUp, SimplePolygon2D.Builder.Create([
            new(0.235f, -0.194f), new(0.327f, -0.13f), new(0.465f, -0.198f), new(0.4512f, -0.21f), new(0.327f, -0.162f), new(0.2534f, -0.21f)]), Paint("#242a29"), -0.31f),
    ];

    /// <summary>The triceratops, on <see cref="QuadrupedSkeleton.Skeleton"/>: the quadruped with a frill and horns.</summary>
    public static ImmutableArray<Attachment> Triceratops { get; } =
    [
        new MountedShape("tail", TailToTailUp, SimplePolygon2D.Builder.Create([
            new(0.075f, -0.1f), new(-0.52f, -0.0584f), new(-0.775f, 0.1964f), new(-0.452f, 0.0924f), new(0.075f, 0.264f)]), Paint("#79bdb0", 0.045f, "#242a29"), 0.02f),
        new MountedShape("far-hind-upper", FarHindRootToFarHindJoint, Ellipse2D.Builder.Create(new(0.125f, 0.3012f), new(0f, 0.2062f)), Paint("#527f78", 0.045f, "#242a29")),
        new MountedShape("far-hind-lower", FarHindJointToFarHindFoot, Ellipse2D.Builder.Create(new(0.115f, 0.2681f), new(0f, 0.1931f)), Paint("#527f78", 0.045f, "#242a29"), -0.005f),
        new MountedShape("far-hind-paw", FarHindFootUpright, SimplePolygon2D.Builder.Create([
            new(-0.165f, -0.093f), new(0.1842f, -0.093f), new(0.195f, -0.0094f), new(0.1014f, 0.094f), new(-0.075f, 0.105f), new(-0.1542f, 0.0214f)]), Paint("#527f78", 0.045f, "#242a29"), -0.015f),
        new MountedShape("far-hind-toe-0", FarHindFootUpright, Ellipse2D.Builder.Create(new(0.0225f, 0.0325f), new(-0.08f, -0.055f)), Paint("#f6e4bb", 0.012f, "#242a29"), -0.025f),
        new MountedShape("far-hind-toe-1", FarHindFootUpright, Ellipse2D.Builder.Create(new(0.0225f, 0.0325f), new(0.005f, -0.055f)), Paint("#f6e4bb", 0.012f, "#242a29"), -0.025f),
        new MountedShape("far-hind-toe-2", FarHindFootUpright, Ellipse2D.Builder.Create(new(0.0225f, 0.0325f), new(0.09f, -0.055f)), Paint("#f6e4bb", 0.012f, "#242a29"), -0.025f),
        new MountedShape("far-front-upper", FarFrontRootToFarFrontJoint, Ellipse2D.Builder.Create(new(0.125f, 0.2942f), new(0f, 0.1992f)), Paint("#527f78", 0.045f, "#242a29")),
        new MountedShape("far-front-lower", FarFrontJointToFarFrontFoot, Ellipse2D.Builder.Create(new(0.115f, 0.2681f), new(0f, 0.1931f)), Paint("#527f78", 0.045f, "#242a29"), -0.005f),
        new MountedShape("far-front-paw", FarFrontFootUpright, SimplePolygon2D.Builder.Create([
            new(-0.165f, -0.093f), new(0.1842f, -0.093f), new(0.195f, -0.0094f), new(0.1014f, 0.094f), new(-0.075f, 0.105f), new(-0.1542f, 0.0214f)]), Paint("#527f78", 0.045f, "#242a29"), -0.015f),
        new MountedShape("far-front-toe-0", FarFrontFootUpright, Ellipse2D.Builder.Create(new(0.0225f, 0.0325f), new(-0.08f, -0.055f)), Paint("#f6e4bb", 0.012f, "#242a29"), -0.025f),
        new MountedShape("far-front-toe-1", FarFrontFootUpright, Ellipse2D.Builder.Create(new(0.0225f, 0.0325f), new(0.005f, -0.055f)), Paint("#f6e4bb", 0.012f, "#242a29"), -0.025f),
        new MountedShape("far-front-toe-2", FarFrontFootUpright, Ellipse2D.Builder.Create(new(0.0225f, 0.0325f), new(0.09f, -0.055f)), Paint("#f6e4bb", 0.012f, "#242a29"), -0.025f),
        new MountedShape("near-hind-upper", NearHindRootToNearHindJoint, Ellipse2D.Builder.Create(new(0.125f, 0.3012f), new(0f, 0.2062f)), Paint("#79bdb0", 0.045f, "#242a29")),
        new MountedShape("near-hind-lower", NearHindJointToNearHindFoot, Ellipse2D.Builder.Create(new(0.115f, 0.2681f), new(0f, 0.1931f)), Paint("#79bdb0", 0.045f, "#242a29"), -0.005f),
        new MountedShape("near-hind-paw", NearHindFootUpright, SimplePolygon2D.Builder.Create([
            new(-0.165f, -0.093f), new(0.1842f, -0.093f), new(0.195f, -0.0094f), new(0.1014f, 0.094f), new(-0.075f, 0.105f), new(-0.1542f, 0.0214f)]), Paint("#79bdb0", 0.045f, "#242a29"), -0.015f),
        new MountedShape("near-hind-toe-0", NearHindFootUpright, Ellipse2D.Builder.Create(new(0.0225f, 0.0325f), new(-0.08f, -0.055f)), Paint("#f6e4bb", 0.012f, "#242a29"), -0.025f),
        new MountedShape("near-hind-toe-1", NearHindFootUpright, Ellipse2D.Builder.Create(new(0.0225f, 0.0325f), new(0.005f, -0.055f)), Paint("#f6e4bb", 0.012f, "#242a29"), -0.025f),
        new MountedShape("near-hind-toe-2", NearHindFootUpright, Ellipse2D.Builder.Create(new(0.0225f, 0.0325f), new(0.09f, -0.055f)), Paint("#f6e4bb", 0.012f, "#242a29"), -0.025f),
        new MountedShape("near-front-upper", NearFrontRootToNearFrontJoint, Ellipse2D.Builder.Create(new(0.125f, 0.2942f), new(0f, 0.1992f)), Paint("#79bdb0", 0.045f, "#242a29")),
        new MountedShape("near-front-lower", NearFrontJointToNearFrontFoot, Ellipse2D.Builder.Create(new(0.115f, 0.2681f), new(0f, 0.1931f)), Paint("#79bdb0", 0.045f, "#242a29"), -0.005f),
        new MountedShape("near-front-paw", NearFrontFootUpright, SimplePolygon2D.Builder.Create([
            new(-0.165f, -0.093f), new(0.1842f, -0.093f), new(0.195f, -0.0094f), new(0.1014f, 0.094f), new(-0.075f, 0.105f), new(-0.1542f, 0.0214f)]), Paint("#79bdb0", 0.045f, "#242a29"), -0.015f),
        new MountedShape("near-front-toe-0", NearFrontFootUpright, Ellipse2D.Builder.Create(new(0.0225f, 0.0325f), new(-0.08f, -0.055f)), Paint("#f6e4bb", 0.012f, "#242a29"), -0.025f),
        new MountedShape("near-front-toe-1", NearFrontFootUpright, Ellipse2D.Builder.Create(new(0.0225f, 0.0325f), new(0.005f, -0.055f)), Paint("#f6e4bb", 0.012f, "#242a29"), -0.025f),
        new MountedShape("near-front-toe-2", NearFrontFootUpright, Ellipse2D.Builder.Create(new(0.0225f, 0.0325f), new(0.09f, -0.055f)), Paint("#f6e4bb", 0.012f, "#242a29"), -0.025f),
        new MountedShape("body", BodyToBodyUp, Ellipse2D.Builder.Create(new(0.96f, 0.45f), new(-0.05f, 0.04f)), Paint("#79bdb0", 0.045f, "#242a29")) { Patches = [new("#397783", [new(-0.338f, 0.175f), new(-0.3612f, 0.2605f), new(-0.4244f, 0.3231f), new(-0.5108f, 0.346f), new(-0.5972f, 0.3231f), new(-0.6604f, 0.2605f), new(-0.6836f, 0.175f), new(-0.6604f, 0.0895f), new(-0.5972f, 0.0269f), new(-0.5108f, 0.004f), new(-0.4244f, 0.0269f), new(-0.3612f, 0.0895f)]), new("#397783", [new(-0.002f, 0.067f), new(-0.0187f, 0.13f), new(-0.0644f, 0.1761f), new(-0.1268f, 0.193f), new(-0.1892f, 0.1761f), new(-0.2349f, 0.13f), new(-0.2516f, 0.067f), new(-0.2349f, 0.004f), new(-0.1892f, -0.0421f), new(-0.1268f, -0.059f), new(-0.0644f, -0.0421f), new(-0.0187f, 0.004f)])] },
        new MountedShape("head", HeadToHeadUp, SimplePolygon2D.Builder.Create([
            new(-0.365f, -0.1424f), new(-0.1225f, -0.3056f), new(0.3625f, -0.36f), new(0.5856f, -0.1696f), new(0.605f, -0.0064f),
            new(0.3625f, 0.15f), new(-0.0255f, 0.32f), new(-0.3456f, 0.1976f)]), Paint("#79bdb0", 0.045f, "#242a29"), -0.24f),
        new MountedShape("frill", HeadToHeadUp, SimplePolygon2D.Builder.Create([
            new(-0.484f, -0.3124f), new(-0.1496f, -0.34f), new(0.1584f, 0.0326f), new(0.1936f, 0.5432f), new(0.0616f, 1.0124f), new(-0.0528f, 1.04f),
            new(-0.1672f, 0.902f), new(-0.308f, 0.9434f), new(-0.4136f, 0.7778f), new(-0.5544f, 0.764f), new(-0.5984f, 0.5294f),
            new(-0.66f, 0.3776f), new(-0.616f, 0.143f), new(-0.6424f, -0.0364f)]), Paint("#91cebf", 0.045f, "#242a29"), -0.26f),
        new MountedShape("far-brow-horn", HeadToHeadUp, SimplePolygon2D.Builder.Create([
            new(0.095f, 0.29f), new(0.2655f, 0.326f), new(0.405f, 0.65f)]), Paint("#f6e4bb", 0.045f, "#242a29"), -0.255f),
        new MountedShape("near-brow-horn", HeadToHeadUp, SimplePolygon2D.Builder.Create([
            new(-0.13f, 0.3875f), new(0.0032f, 0.295f), new(0.23f, 0.665f), new(0.0932f, 0.628f)]), Paint("#f6e4bb", 0.045f, "#242a29"), -0.29f),
        new MountedShape("nose-horn", HeadToHeadUp, SimplePolygon2D.Builder.Create([
            new(0.43f, 0.12f), new(0.5532f, 0.075f), new(0.65f, 0.325f)]), Paint("#f6e4bb", 0.045f, "#242a29"), -0.29f),
        new MountedShape("eye", HeadToHeadUp, Ellipse2D.Builder.Create(new(0.045f, 0.06f), new(0.34f, 0.01f)), Paint("#242a29"), -0.31f),
        new MountedShape("brow", HeadToHeadUp, SimplePolygon2D.Builder.Create([
            new(0.25f, 0.155f), new(0.41f, 0.1028f), new(0.386f, 0.065f), new(0.25f, 0.119f)]), Paint("#242a29"), -0.31f),
        new MountedShape("mouth", HeadToHeadUp, SimplePolygon2D.Builder.Create([
            new(0.235f, -0.194f), new(0.327f, -0.13f), new(0.465f, -0.198f), new(0.4512f, -0.21f), new(0.327f, -0.162f), new(0.2534f, -0.21f)]), Paint("#242a29"), -0.31f),
    ];

    /// <summary>The baby triceratops, on <see cref="QuadrupedSkeleton.Baby"/>.</summary>
    public static ImmutableArray<Attachment> BabyTriceratops { get; } =
    [
        new MountedShape("tail", TailToTailUp, SimplePolygon2D.Builder.Create([
            new(0.0525f, -0.07f), new(-0.364f, -0.0409f), new(-0.5425f, 0.1375f), new(-0.3164f, 0.0647f), new(0.0525f, 0.1848f)]), Paint("#79bdb0", 0.045f, "#242a29"), 0.014f),
        new MountedShape("far-hind-upper", FarHindRootToFarHindJoint, Ellipse2D.Builder.Create(new(0.0875f, 0.2108f), new(0f, 0.1443f)), Paint("#527f78", 0.045f, "#242a29")),
        new MountedShape("far-hind-lower", FarHindJointToFarHindFoot, Ellipse2D.Builder.Create(new(0.0805f, 0.1877f), new(0f, 0.1352f)), Paint("#527f78", 0.045f, "#242a29"), -0.0035f),
        new MountedShape("far-hind-paw", FarHindFootUpright, SimplePolygon2D.Builder.Create([
            new(-0.1155f, -0.0651f), new(0.1289f, -0.0651f), new(0.1365f, -0.0066f), new(0.071f, 0.0658f), new(-0.0525f, 0.0735f),
            new(-0.1079f, 0.015f)]), Paint("#527f78", 0.045f, "#242a29"), -0.0105f),
        new MountedShape("far-hind-toe-0", FarHindFootUpright, Ellipse2D.Builder.Create(new(0.0158f, 0.0227f), new(-0.056f, -0.0385f)), Paint("#f6e4bb", 0.012f, "#242a29"), -0.0175f),
        new MountedShape("far-hind-toe-1", FarHindFootUpright, Ellipse2D.Builder.Create(new(0.0158f, 0.0227f), new(0.0035f, -0.0385f)), Paint("#f6e4bb", 0.012f, "#242a29"), -0.0175f),
        new MountedShape("far-hind-toe-2", FarHindFootUpright, Ellipse2D.Builder.Create(new(0.0158f, 0.0227f), new(0.063f, -0.0385f)), Paint("#f6e4bb", 0.012f, "#242a29"), -0.0175f),
        new MountedShape("far-front-upper", FarFrontRootToFarFrontJoint, Ellipse2D.Builder.Create(new(0.0875f, 0.206f), new(0f, 0.1395f)), Paint("#527f78", 0.045f, "#242a29")),
        new MountedShape("far-front-lower", FarFrontJointToFarFrontFoot, Ellipse2D.Builder.Create(new(0.0805f, 0.1877f), new(0f, 0.1352f)), Paint("#527f78", 0.045f, "#242a29"), -0.0035f),
        new MountedShape("far-front-paw", FarFrontFootUpright, SimplePolygon2D.Builder.Create([
            new(-0.1155f, -0.0651f), new(0.1289f, -0.0651f), new(0.1365f, -0.0066f), new(0.071f, 0.0658f), new(-0.0525f, 0.0735f),
            new(-0.1079f, 0.015f)]), Paint("#527f78", 0.045f, "#242a29"), -0.0105f),
        new MountedShape("far-front-toe-0", FarFrontFootUpright, Ellipse2D.Builder.Create(new(0.0158f, 0.0227f), new(-0.056f, -0.0385f)), Paint("#f6e4bb", 0.012f, "#242a29"), -0.0175f),
        new MountedShape("far-front-toe-1", FarFrontFootUpright, Ellipse2D.Builder.Create(new(0.0158f, 0.0227f), new(0.0035f, -0.0385f)), Paint("#f6e4bb", 0.012f, "#242a29"), -0.0175f),
        new MountedShape("far-front-toe-2", FarFrontFootUpright, Ellipse2D.Builder.Create(new(0.0158f, 0.0227f), new(0.063f, -0.0385f)), Paint("#f6e4bb", 0.012f, "#242a29"), -0.0175f),
        new MountedShape("near-hind-upper", NearHindRootToNearHindJoint, Ellipse2D.Builder.Create(new(0.0875f, 0.2108f), new(0f, 0.1443f)), Paint("#79bdb0", 0.045f, "#242a29")),
        new MountedShape("near-hind-lower", NearHindJointToNearHindFoot, Ellipse2D.Builder.Create(new(0.0805f, 0.1877f), new(0f, 0.1352f)), Paint("#79bdb0", 0.045f, "#242a29"), -0.0035f),
        new MountedShape("near-hind-paw", NearHindFootUpright, SimplePolygon2D.Builder.Create([
            new(-0.1155f, -0.0651f), new(0.1289f, -0.0651f), new(0.1365f, -0.0066f), new(0.071f, 0.0658f), new(-0.0525f, 0.0735f),
            new(-0.1079f, 0.015f)]), Paint("#79bdb0", 0.045f, "#242a29"), -0.0105f),
        new MountedShape("near-hind-toe-0", NearHindFootUpright, Ellipse2D.Builder.Create(new(0.0158f, 0.0227f), new(-0.056f, -0.0385f)), Paint("#f6e4bb", 0.012f, "#242a29"), -0.0175f),
        new MountedShape("near-hind-toe-1", NearHindFootUpright, Ellipse2D.Builder.Create(new(0.0158f, 0.0227f), new(0.0035f, -0.0385f)), Paint("#f6e4bb", 0.012f, "#242a29"), -0.0175f),
        new MountedShape("near-hind-toe-2", NearHindFootUpright, Ellipse2D.Builder.Create(new(0.0158f, 0.0227f), new(0.063f, -0.0385f)), Paint("#f6e4bb", 0.012f, "#242a29"), -0.0175f),
        new MountedShape("near-front-upper", NearFrontRootToNearFrontJoint, Ellipse2D.Builder.Create(new(0.0875f, 0.206f), new(0f, 0.1395f)), Paint("#79bdb0", 0.045f, "#242a29")),
        new MountedShape("near-front-lower", NearFrontJointToNearFrontFoot, Ellipse2D.Builder.Create(new(0.0805f, 0.1877f), new(0f, 0.1352f)), Paint("#79bdb0", 0.045f, "#242a29"), -0.0035f),
        new MountedShape("near-front-paw", NearFrontFootUpright, SimplePolygon2D.Builder.Create([
            new(-0.1155f, -0.0651f), new(0.1289f, -0.0651f), new(0.1365f, -0.0066f), new(0.071f, 0.0658f), new(-0.0525f, 0.0735f),
            new(-0.1079f, 0.015f)]), Paint("#79bdb0", 0.045f, "#242a29"), -0.0105f),
        new MountedShape("near-front-toe-0", NearFrontFootUpright, Ellipse2D.Builder.Create(new(0.0158f, 0.0227f), new(-0.056f, -0.0385f)), Paint("#f6e4bb", 0.012f, "#242a29"), -0.0175f),
        new MountedShape("near-front-toe-1", NearFrontFootUpright, Ellipse2D.Builder.Create(new(0.0158f, 0.0227f), new(0.0035f, -0.0385f)), Paint("#f6e4bb", 0.012f, "#242a29"), -0.0175f),
        new MountedShape("near-front-toe-2", NearFrontFootUpright, Ellipse2D.Builder.Create(new(0.0158f, 0.0227f), new(0.063f, -0.0385f)), Paint("#f6e4bb", 0.012f, "#242a29"), -0.0175f),
        new MountedShape("body", BodyToBodyUp, Ellipse2D.Builder.Create(new(0.672f, 0.315f), new(-0.035f, 0.028f)), Paint("#79bdb0", 0.045f, "#242a29")) { Patches = [new("#397783", [new(-0.2366f, 0.1225f), new(-0.2528f, 0.1824f), new(-0.2971f, 0.2262f), new(-0.3576f, 0.2422f), new(-0.418f, 0.2262f), new(-0.4623f, 0.1824f), new(-0.4785f, 0.1225f), new(-0.4623f, 0.0626f), new(-0.418f, 0.0188f), new(-0.3576f, 0.0028f), new(-0.2971f, 0.0188f), new(-0.2528f, 0.0626f)]), new("#397783", [new(-0.0014f, 0.0469f), new(-0.0131f, 0.091f), new(-0.0451f, 0.1233f), new(-0.0888f, 0.1351f), new(-0.1324f, 0.1233f), new(-0.1644f, 0.091f), new(-0.1761f, 0.0469f), new(-0.1644f, 0.0028f), new(-0.1324f, -0.0295f), new(-0.0888f, -0.0413f), new(-0.0451f, -0.0295f), new(-0.0131f, 0.0028f)])] },
        new MountedShape("head", HeadToHeadUp, SimplePolygon2D.Builder.Create([
            new(-0.2555f, -0.0997f), new(-0.0858f, -0.2139f), new(0.2537f, -0.252f), new(0.4099f, -0.1187f), new(0.4235f, -0.0045f),
            new(0.2537f, 0.105f), new(-0.0179f, 0.224f), new(-0.2419f, 0.1383f)]), Paint("#79bdb0", 0.045f, "#242a29"), -0.168f),
        new MountedShape("frill", HeadToHeadUp, SimplePolygon2D.Builder.Create([
            new(-0.3388f, -0.2187f), new(-0.1047f, -0.238f), new(0.1109f, 0.0228f), new(0.1355f, 0.3802f), new(0.0431f, 0.7087f),
            new(-0.037f, 0.728f), new(-0.117f, 0.6314f), new(-0.2156f, 0.6604f), new(-0.2895f, 0.5445f), new(-0.3881f, 0.5348f),
            new(-0.4189f, 0.3706f), new(-0.462f, 0.2643f), new(-0.4312f, 0.1001f), new(-0.4497f, -0.0255f)]), Paint("#91cebf", 0.045f, "#242a29"), -0.182f),
        new MountedShape("far-brow-horn", HeadToHeadUp, SimplePolygon2D.Builder.Create([
            new(0.0882f, 0.2282f), new(0.1837f, 0.2484f), new(0.2618f, 0.4298f)]), Paint("#f6e4bb", 0.045f, "#242a29"), -0.1785f),
        new MountedShape("near-brow-horn", HeadToHeadUp, SimplePolygon2D.Builder.Create([
            new(-0.0658f, 0.2842f), new(0.0088f, 0.2324f), new(0.1358f, 0.4396f), new(0.0592f, 0.4189f)]), Paint("#f6e4bb", 0.045f, "#242a29"), -0.203f),
        new MountedShape("nose-horn", HeadToHeadUp, SimplePolygon2D.Builder.Create([
            new(0.3164f, 0.0952f), new(0.3854f, 0.07f), new(0.4396f, 0.21f)]), Paint("#f6e4bb", 0.045f, "#242a29"), -0.203f),
        new MountedShape("eye", HeadToHeadUp, Ellipse2D.Builder.Create(new(0.0378f, 0.0504f), new(0.238f, 0.007f)), Paint("#242a29"), -0.217f),
        new MountedShape("brow", HeadToHeadUp, SimplePolygon2D.Builder.Create([
            new(0.175f, 0.1085f), new(0.287f, 0.072f), new(0.2702f, 0.0455f), new(0.175f, 0.0833f)]), Paint("#242a29"), -0.217f),
        new MountedShape("mouth", HeadToHeadUp, SimplePolygon2D.Builder.Create([
            new(0.1645f, -0.1358f), new(0.2289f, -0.091f), new(0.3255f, -0.1386f), new(0.3158f, -0.147f), new(0.2289f, -0.1134f),
            new(0.1774f, -0.147f)]), Paint("#242a29"), -0.217f),
    ];
}
