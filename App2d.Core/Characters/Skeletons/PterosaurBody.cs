using App2d.Core.Characters.Attachments;
using App2d.Core.Geometry.Shapes;
using System.Collections.Immutable;
using static App2d.Core.Characters.Attachments.Paints;
using static App2d.Core.Characters.Skeletons.PterosaurSkeleton;

namespace App2d.Core.Characters.Skeletons;

/// <summary>The pterosaur's drawn body, as attachments on <see cref="PterosaurSkeleton"/>.</summary>
public static class PterosaurBody
{
    /// <summary>Body, head, legs, and two wings whose skin stretches between shoulder, bend and tip.</summary>
    public static ImmutableArray<Attachment> Body { get; } =
    [
        new SkinnedShape("far-wing", SimplePolygon2D.Builder.Create([new(0.1f, 0.12f), new(0.31f, -0.02f), new(0.62f, 0.12f), new(0.79f, 0.35f), new(1.08f, 0.85f), new(0.48f, 0.6f)]),
            [
                new(FarShoulderUpright, new(0f, 0f), -0.12f), new(FarShoulderToFarBend, new(0.2515f, 0.0206f), -0.12f),
                new(FarBendToFarTip, new(0.4969f, -0.0554f), -0.12f), new(FarTipToFarBend, new(-0.35f, 0.46f), -0.12f),
                new(FarTipUpright, new(0f, 0f), -0.12f), new(FarBendUpright, new(0f, 0f), -0.12f)],
            Paint("#9e5f7e", 0.028f, "#302632"), 0.12f) { Patches = [new("#80506d", [new(0.15f, 0.15f), new(0.18f, 0.15f), new(0.53f, 0.59f), new(0.48f, 0.6f)])] },
        new JointPath("far-leg", [FarLeg, FarFoot], Paint("#302632", 0.028f, "#302632", stroke: 0.025f), 0.12f),
        new JointPath("far-toe-0", [FarFoot, FarToe0], Paint("#302632", 0.028f, "#302632", stroke: 0.021f), 0.12f),
        new JointPath("far-toe-1", [FarFoot, FarToe1], Paint("#302632", 0.028f, "#302632", stroke: 0.021f), 0.12f),
        new JointPath("far-toe-2", [FarFoot, FarToe2], Paint("#302632", 0.028f, "#302632", stroke: 0.021f), 0.12f),
        new SkinnedShape("near-wing", SimplePolygon2D.Builder.Create([new(-0.1f, 0.12f), new(-0.31f, -0.02f), new(-0.62f, 0.12f), new(-0.79f, 0.35f), new(-1.08f, 0.85f), new(-0.48f, 0.6f)]),
            [
                new(NearShoulderUpright, new(0f, 0f), 0.02f), new(NearShoulderToNearBend, new(-0.2515f, 0.0206f), 0.02f),
                new(NearBendToNearTip, new(-0.4969f, -0.0554f), 0.02f), new(NearTipToNearBend, new(0.35f, 0.46f), 0.02f),
                new(NearTipUpright, new(0f, 0f), 0.02f), new(NearBendUpright, new(0f, 0f), 0.02f)],
            Paint("#b56e8b", 0.028f, "#302632"), -0.02f) { Patches = [new("#80506d", [new(-0.15f, 0.15f), new(-0.18f, 0.15f), new(-0.53f, 0.59f), new(-0.48f, 0.6f)])] },
        new JointPath("near-leg", [NearLeg, NearFoot], Paint("#302632", 0.028f, "#302632", stroke: 0.025f), -0.02f),
        new JointPath("near-toe-0", [NearFoot, NearToe0], Paint("#302632", 0.028f, "#302632", stroke: 0.021f), -0.02f),
        new JointPath("near-toe-1", [NearFoot, NearToe1], Paint("#302632", 0.028f, "#302632", stroke: 0.021f), -0.02f),
        new JointPath("near-toe-2", [NearFoot, NearToe2], Paint("#302632", 0.028f, "#302632", stroke: 0.021f), -0.02f),
        new MountedShape("tail", BodyToBodyUp, SimplePolygon2D.Builder.Create([
            new(-0.42f, -0.125f), new(-0.12f, -0.0875f), new(-0.12f, 0.025f)]), Paint("#805473", 0.028f, "#302632"), -0.04f),
        new MountedShape("body", BodyToBodyUp, Ellipse2D.Builder.Create(new(0.225f, 0.19f), new(0f, -0.01f)), Paint("#805473", 0.028f, "#302632"), -0.07f),
        new MountedShape("crest", HeadToHeadUp, SimplePolygon2D.Builder.Create([
            new(-0.165f, 0.34f), new(0.015f, 0.04f), new(-0.147f, 0.04f)]), Paint("#e4b364", 0.028f, "#302632"), -0.15f),
        new MountedShape("head", HeadToHeadUp, Ellipse2D.Builder.Create(new(0.165f, 0.145f), new(0.015f, 0.015f)), Paint("#ba829d", 0.028f, "#302632"), -0.16f),
        new MountedShape("beak", HeadToHeadUp, SimplePolygon2D.Builder.Create([
            new(0.125f, 0.1f), new(0.475f, -0.13f), new(0.125f, -0.0656f)]), Paint("#edbd71", 0.028f, "#302632"), -0.17f),
        new MountedShape("lower-beak", JawToJawUp, SimplePolygon2D.Builder.Create([
            new(-0.03f, 0.0125f), new(0.23f, -0.055f), new(-0.03f, -0.0625f)]), Paint("#d99a54", 0.028f, "#302632"), -0.18f),
        new MountedShape("eye", HeadToHeadUp, Ellipse2D.Builder.Create(new(0.024f, 0.0345f), new(0.079f, 0.019f)), Paint("#302632"), -0.19f),
        new MountedShape("brow", HeadToHeadUp, SimplePolygon2D.Builder.Create([
            new(0.025f, 0.1095f), new(0.125f, 0.0885f), new(0.115f, 0.0745f), new(0.025f, 0.0962f)]), Paint("#302632"), -0.19f),
    ];
}
