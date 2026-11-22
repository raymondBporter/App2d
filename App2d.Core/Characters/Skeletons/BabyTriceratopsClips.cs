using App2d.Core.Animation;
using System.Collections.Immutable;
using static App2d.Core.Characters.Skeletons.QuadrupedSkeleton;

namespace App2d.Core.Characters.Skeletons;

/// <summary>The baby triceratops animations, for <see cref="QuadrupedSkeleton.Baby"/>. Values are changes from rest, in that skeleton's own units.</summary>
public static class BabyTriceratopsClips
{
    private static readonly Interpolator<Vector2f, float> Smooth = Interpolation.Default<Vector2f, float>().WithProgress(Easingf.Smooth);
    private static readonly Interpolator<float, float> SmoothTurn = Interpolation.Linear<float>().WithProgress(Easingf.Smooth);

    public static SkeletonClip2D BabyTriceratopsBrake { get; } = new("baby-triceratops-brake", 0.65f)
    {
        Joints =
        [
            new(Body,
                Move: Track.Of<Vector2f>(new(0f, new(0f, -0.0385f)), new(0.25f, new(0.056f, -0.07f)), new(0.65f, new(0f, 0f))),
                Turn: Track.Linear(new(0f, 0f), new(0.325f, 0.08f), new(0.65f, 0f))),
            new(Head, Turn: Track.Linear(new(0f, -0.32f), new(0.325f, 0.1f), new(0.65f, 0f))),
        ],
        Limbs =
        [
            new(FarHind, Target: Track.Of<Vector2f>(new(0f, new(0.105f, 0f)), new(0.3f, new(0.154f, 0f)), new(0.65f, new(0f, 0f)))),
            new(FarFront, Target: Track.Of<Vector2f>(new(0f, new(0.105f, 0f)), new(0.3f, new(0.154f, 0f)), new(0.65f, new(0f, 0f)))),
            new(NearHind, Target: Track.Of<Vector2f>(new(0f, new(0.105f, 0f)), new(0.3f, new(0.154f, 0f)), new(0.65f, new(0f, 0f)))),
            new(NearFront, Target: Track.Of<Vector2f>(new(0f, new(0.105f, 0f)), new(0.3f, new(0.154f, 0f)), new(0.65f, new(0f, 0f)))),
        ],
        Markers = [new("stop", 0.3f)],
    };

    public static SkeletonClip2D BabyTriceratopsCharge { get; } = new("baby-triceratops-charge", 2.7f)
    {
        Joints =
        [
            new(Body,
                Move: new(Smooth,
                    new(0f, new(0f, 0f)), new(0.65f, new(0f, 0.02f)), new(1f, new(0f, -0.04f)), new(1.85f, new(0f, -0.04f)),
                    new(2.15f, new(0.04f, -0.06f)), new(2.7f, new(0f, 0f)))),
            new(Head,
                Move: new(Smooth,
                    new(0f, new(0f, 0f)), new(1f, new(0f, -0.09f)), new(1.85f, new(0f, -0.09f)), new(2.15f, new(0f, -0.04f)), new(2.7f, new(0f, 0f))),
                Turn: new(SmoothTurn, new(0f, -0.1f), new(0.65f, -0.2f), new(1f, -0.55f), new(1.85f, -0.55f), new(2.15f, 0.1f), new(2.7f, 0f))),
            new(Tail, Turn: new(SmoothTurn, new(0f, 0f), new(0.3f, 0.15f), new(0.65f, -0.1f), new(1.85f, -0.1f), new(2.7f, 0f))),
        ],
        Limbs =
        [
            new(FarHind,
                Target: new(Smooth,
                    new(0f, new(0f, 0f)), new(1f, new(0f, 0f)), new(1.0202f, new(0.1348f, 0.0439f)), new(1.0405f, new(0.0923f, 0.0788f)),
                    new(1.0607f, new(0.031f, 0.0978f)), new(1.081f, new(-0.0365f, 0.097f)),
                    new(1.1012f, new(-0.0966f, 0.0765f)), new(1.1214f, new(-0.1372f, 0.0405f)), new(1.1417f, new(-0.1499f, 0f)),
                    new(1.1619f, new(-0.1322f, 0f)), new(1.1821f, new(-0.0878f, 0f)), new(1.2024f, new(-0.0255f, 0f)),
                    new(1.2226f, new(0.0419f, 0f)), new(1.2429f, new(0.1008f, 0f)), new(1.2631f, new(0.1393f, 0f)),
                    new(1.2833f, new(0.1496f, 0.0075f)), new(1.3036f, new(0.1295f, 0.0505f)), new(1.3238f, new(0.0832f, 0.0832f)),
                    new(1.344f, new(0.02f, 0.0991f)), new(1.3643f, new(-0.0473f, 0.0949f)),
                    new(1.3845f, new(-0.1049f, 0.0715f)), new(1.4048f, new(-0.1413f, 0.0335f)), new(1.425f, new(-0.1491f, 0f)),
                    new(1.4452f, new(-0.1266f, 0f)), new(1.4655f, new(-0.0784f, 0f)), new(1.4857f, new(-0.0144f, 0f)),
                    new(1.5059f, new(0.0526f, 0f)), new(1.5262f, new(0.1089f, 0f)), new(1.5464f, new(0.1431f, 0f)),
                    new(1.5667f, new(0.1483f, 0.0149f)), new(1.5869f, new(0.1235f, 0.0568f)), new(1.6071f, new(0.0736f, 0.0871f)),
                    new(1.6274f, new(0.0088f, 0.0998f)), new(1.6476f, new(-0.0578f, 0.0923f)),
                    new(1.6679f, new(-0.1126f, 0.066f)), new(1.6881f, new(-0.1447f, 0.0264f)), new(1.7083f, new(-0.1474f, 0f)),
                    new(1.7286f, new(-0.1202f, 0f)), new(1.7488f, new(-0.0687f, 0f)), new(1.7691f, new(-0.0032f, 0f)),
                    new(1.7893f, new(0.0629f, 0f)), new(1.8095f, new(0.1163f, 0f)), new(1.8298f, new(0.1461f, 0f)),
                    new(1.85f, new(0.1462f, 0.0223f)), new(2.05f, new(0.14f, 0f)), new(2.15f, new(0.1f, 0f)), new(2.7f, new(0f, 0f)))),
            new(FarFront,
                Target: new(Smooth,
                    new(0f, new(0f, 0f)), new(1f, new(0f, 0f)), new(1.0202f, new(-0.1348f, 0f)), new(1.0405f, new(-0.0923f, 0f)),
                    new(1.0607f, new(-0.031f, 0f)), new(1.081f, new(0.0365f, 0f)), new(1.1012f, new(0.0966f, 0f)),
                    new(1.1214f, new(0.1372f, 0f)), new(1.1417f, new(0.1499f, 0.0037f)), new(1.1619f, new(0.1322f, 0.0472f)),
                    new(1.1821f, new(0.0878f, 0.0811f)), new(1.2024f, new(0.0255f, 0.0985f)),
                    new(1.2226f, new(-0.0419f, 0.096f)), new(1.2429f, new(-0.1008f, 0.074f)),
                    new(1.2631f, new(-0.1393f, 0.037f)), new(1.2833f, new(-0.1496f, 0f)), new(1.3036f, new(-0.1295f, 0f)),
                    new(1.3238f, new(-0.0832f, 0f)), new(1.344f, new(-0.02f, 0f)), new(1.3643f, new(0.0473f, 0f)),
                    new(1.3845f, new(0.1049f, 0f)), new(1.4048f, new(0.1413f, 0f)), new(1.425f, new(0.1491f, 0.0112f)),
                    new(1.4452f, new(0.1266f, 0.0537f)), new(1.4655f, new(0.0784f, 0.0852f)), new(1.4857f, new(0.0144f, 0.0995f)),
                    new(1.5059f, new(-0.0526f, 0.0937f)), new(1.5262f, new(-0.1089f, 0.0688f)),
                    new(1.5464f, new(-0.1431f, 0.03f)), new(1.5667f, new(-0.1483f, 0f)), new(1.5869f, new(-0.1235f, 0f)),
                    new(1.6071f, new(-0.0736f, 0f)), new(1.6274f, new(-0.0088f, 0f)), new(1.6476f, new(0.0578f, 0f)),
                    new(1.6679f, new(0.1126f, 0f)), new(1.6881f, new(0.1447f, 0f)), new(1.7083f, new(0.1474f, 0.0186f)),
                    new(1.7286f, new(0.1202f, 0.0598f)), new(1.7488f, new(0.0687f, 0.0889f)),
                    new(1.7691f, new(0.0032f, 0.1f)), new(1.7893f, new(-0.0629f, 0.0908f)),
                    new(1.8095f, new(-0.1163f, 0.0632f)), new(1.8298f, new(-0.1461f, 0.0228f)), new(1.85f, new(-0.1462f, 0f)),
                    new(2.05f, new(0.14f, 0f)), new(2.15f, new(0.1f, 0f)), new(2.7f, new(0f, 0f)))),
            new(NearHind,
                Target: new(Smooth,
                    new(0f, new(0f, 0f)), new(1f, new(0f, 0f)), new(1.0202f, new(-0.1348f, 0f)), new(1.0405f, new(-0.0923f, 0f)),
                    new(1.0607f, new(-0.031f, 0f)), new(1.081f, new(0.0365f, 0f)), new(1.1012f, new(0.0966f, 0f)),
                    new(1.1214f, new(0.1372f, 0f)), new(1.1417f, new(0.1499f, 0.0037f)), new(1.1619f, new(0.1322f, 0.0472f)),
                    new(1.1821f, new(0.0878f, 0.0811f)), new(1.2024f, new(0.0255f, 0.0985f)),
                    new(1.2226f, new(-0.0419f, 0.096f)), new(1.2429f, new(-0.1008f, 0.074f)),
                    new(1.2631f, new(-0.1393f, 0.037f)), new(1.2833f, new(-0.1496f, 0f)), new(1.3036f, new(-0.1295f, 0f)),
                    new(1.3238f, new(-0.0832f, 0f)), new(1.344f, new(-0.02f, 0f)), new(1.3643f, new(0.0473f, 0f)),
                    new(1.3845f, new(0.1049f, 0f)), new(1.4048f, new(0.1413f, 0f)), new(1.425f, new(0.1491f, 0.0112f)),
                    new(1.4452f, new(0.1266f, 0.0537f)), new(1.4655f, new(0.0784f, 0.0852f)), new(1.4857f, new(0.0144f, 0.0995f)),
                    new(1.5059f, new(-0.0526f, 0.0937f)), new(1.5262f, new(-0.1089f, 0.0688f)),
                    new(1.5464f, new(-0.1431f, 0.03f)), new(1.5667f, new(-0.1483f, 0f)), new(1.5869f, new(-0.1235f, 0f)),
                    new(1.6071f, new(-0.0736f, 0f)), new(1.6274f, new(-0.0088f, 0f)), new(1.6476f, new(0.0578f, 0f)),
                    new(1.6679f, new(0.1126f, 0f)), new(1.6881f, new(0.1447f, 0f)), new(1.7083f, new(0.1474f, 0.0186f)),
                    new(1.7286f, new(0.1202f, 0.0598f)), new(1.7488f, new(0.0687f, 0.0889f)),
                    new(1.7691f, new(0.0032f, 0.1f)), new(1.7893f, new(-0.0629f, 0.0908f)),
                    new(1.8095f, new(-0.1163f, 0.0632f)), new(1.8298f, new(-0.1461f, 0.0228f)), new(1.85f, new(-0.1462f, 0f)),
                    new(2.05f, new(0.14f, 0f)), new(2.15f, new(0.1f, 0f)), new(2.7f, new(0f, 0f)))),
            new(NearFront,
                Target: new(Smooth,
                    new(0f, new(0f, 0f)), new(0.2f, new(0.12f, 0.1f)), new(0.4f, new(0.16f, 0f)), new(0.65f, new(-0.12f, 0f)), new(1f, new(0f, 0f)),
                    new(1.0202f, new(0.1348f, 0.0439f)), new(1.0405f, new(0.0923f, 0.0788f)), new(1.0607f, new(0.031f, 0.0978f)),
                    new(1.081f, new(-0.0365f, 0.097f)), new(1.1012f, new(-0.0966f, 0.0765f)),
                    new(1.1214f, new(-0.1372f, 0.0405f)), new(1.1417f, new(-0.1499f, 0f)), new(1.1619f, new(-0.1322f, 0f)),
                    new(1.1821f, new(-0.0878f, 0f)), new(1.2024f, new(-0.0255f, 0f)), new(1.2226f, new(0.0419f, 0f)),
                    new(1.2429f, new(0.1008f, 0f)), new(1.2631f, new(0.1393f, 0f)), new(1.2833f, new(0.1496f, 0.0075f)),
                    new(1.3036f, new(0.1295f, 0.0505f)), new(1.3238f, new(0.0832f, 0.0832f)), new(1.344f, new(0.02f, 0.0991f)),
                    new(1.3643f, new(-0.0473f, 0.0949f)), new(1.3845f, new(-0.1049f, 0.0715f)),
                    new(1.4048f, new(-0.1413f, 0.0335f)), new(1.425f, new(-0.1491f, 0f)), new(1.4452f, new(-0.1266f, 0f)),
                    new(1.4655f, new(-0.0784f, 0f)), new(1.4857f, new(-0.0144f, 0f)), new(1.5059f, new(0.0526f, 0f)),
                    new(1.5262f, new(0.1089f, 0f)), new(1.5464f, new(0.1431f, 0f)), new(1.5667f, new(0.1483f, 0.0149f)),
                    new(1.5869f, new(0.1235f, 0.0568f)), new(1.6071f, new(0.0736f, 0.0871f)),
                    new(1.6274f, new(0.0088f, 0.0998f)), new(1.6476f, new(-0.0578f, 0.0923f)),
                    new(1.6679f, new(-0.1126f, 0.066f)), new(1.6881f, new(-0.1447f, 0.0264f)), new(1.7083f, new(-0.1474f, 0f)),
                    new(1.7286f, new(-0.1202f, 0f)), new(1.7488f, new(-0.0687f, 0f)), new(1.7691f, new(-0.0032f, 0f)),
                    new(1.7893f, new(0.0629f, 0f)), new(1.8095f, new(0.1163f, 0f)), new(1.8298f, new(0.1461f, 0f)),
                    new(1.85f, new(0.1462f, 0.0223f)), new(2.05f, new(0.14f, 0f)), new(2.15f, new(0.1f, 0f)), new(2.7f, new(0f, 0f)))),
        ],
        Contacts = [
            new(FarHind, 0f, 1f, new(0f, 0f)), new(FarHind, 2.15f, 2.7f, new(0f, 0f)), new(FarFront, 0f, 1f, new(0f, 0f)),
            new(FarFront, 2.15f, 2.7f, new(0f, 0f)), new(NearHind, 0f, 1f, new(0f, 0f)), new(NearHind, 2.15f, 2.7f, new(0f, 0f)),
            new(NearFront, 2.15f, 2.7f, new(0f, 0f))],
        Markers = [new("scrape", 0.4f), new("rush", 1f), new("brake", 1.85f), new("recover", 2.15f)],
    };

    public static SkeletonClip2D BabyTriceratopsDeath { get; } = new("baby-triceratops-death", 0.8f)
    {
        Joints =
        [
            new(Body,
                Move: new(Smooth, new(0f, new(0f, 0f)), new(0.32f, new(-0.08f, -0.35f)), new(0.8f, new(-0.08f, -0.35f))),
                Turn: new(SmoothTurn, new(0f, 0f), new(0.32f, 0.12f), new(0.8f, 0.12f))),
        ],
    };

    public static SkeletonClip2D BabyTriceratopsHeadDown { get; } = new("baby-triceratops-head-down", 0.55f)
    {
        Joints =
        [
            new(Head,
                Move: Track.Of<Vector2f>(new(0f, new(0f, 0f)), new(0.55f, new(0f, -0.126f))),
                Turn: Track.Linear(new(0f, 0f), new(0.275f, -0.4f), new(0.55f, -0.95f))),
        ],
        Contacts = [
            new(FarHind, 0f, 0.55f, new(0f, 0f)), new(FarFront, 0f, 0.55f, new(0f, 0f)), new(NearHind, 0f, 0.55f, new(0f, 0f)),
            new(NearFront, 0f, 0.55f, new(0f, 0f))],
    };

    public static SkeletonClip2D BabyTriceratopsHit { get; } = new("baby-triceratops-hit", 0.3f)
    {
        Joints =
        [
            new(Body,
                Move: new(Smooth, new(0f, new(0f, 0f)), new(0.12f, new(-0.08f, 0f)), new(0.3f, new(0f, 0f))),
                Turn: new(SmoothTurn, new(0f, 0f), new(0.12f, 0.18f), new(0.3f, 0f))),
        ],
    };

    public static SkeletonClip2D BabyTriceratopsIdle { get; } = new("baby-triceratops-idle", 2f, loop: true)
    {
        Joints =
        [
            new(Body, Move: Track.Of<Vector2f>(new(0f, new(0f, 0f)), new(1f, new(0f, 0.0175f)), new(2f, new(0f, 0f)))),
            new(Head, Turn: Track.Linear(new(0f, 0f), new(1f, -0.025f), new(2f, 0f))),
            new(Tail, Turn: Track.Linear(new(0f, 0f), new(1f, 0.12f), new(2f, 0f))),
        ],
        Contacts = [
            new(FarHind, 0f, 2f, new(0f, 0f)), new(FarFront, 0f, 2f, new(0f, 0f)), new(NearHind, 0f, 2f, new(0f, 0f)),
            new(NearFront, 0f, 2f, new(0f, 0f))],
    };

    public static SkeletonClip2D BabyTriceratopsRecover { get; } = new("baby-triceratops-recover", 1.2f)
    {
        Joints =
        [
            new(Head,
                Move: Track.Of<Vector2f>(new(0f, new(0f, -0.126f)), new(1.2f, new(0f, 0f))),
                Turn: Track.Linear(new(0f, -0.95f), new(0.6f, -0.35f), new(1.2f, 0f))),
            new(Tail, Turn: Track.Linear(new(0f, -0.15f), new(0.6f, 0.16f), new(1.2f, 0f))),
        ],
        Contacts = [
            new(FarHind, 0f, 1.2f, new(0f, 0f)), new(FarFront, 0f, 1.2f, new(0f, 0f)), new(NearHind, 0f, 1.2f, new(0f, 0f)),
            new(NearFront, 0f, 1.2f, new(0f, 0f))],
    };

    public static SkeletonClip2D BabyTriceratopsRun { get; } = new("baby-triceratops-run", 0.65f, loop: true)
    {
        Joints =
        [
            new(Body,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0f, 0f)), new(0.0813f, new(0f, 0.0175f)), new(0.2437f, new(0f, -0.0175f)), new(0.4062f, new(0f, 0.0175f)),
                    new(0.5687f, new(0f, -0.0175f)), new(0.65f, new(0f, 0f)))),
            new(Head, Turn: Track.Linear(new(0f, 0f), new(0.325f, -0.04f), new(0.65f, 0f))),
            new(Tail, Turn: Track.Linear(new(0f, 0.05f), new(0.325f, -0.12f), new(0.65f, 0.05f))),
        ],
        Limbs =
        [
            new(FarHind,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.147f, 0f)), new(0.39f, new(-0.147f, 0f)), new(0.4062f, new(-0.1286f, 0.0259f)), new(0.4333f, new(-0.098f, 0.0665f)),
                    new(0.4604f, new(-0.0674f, 0.1f)), new(0.4875f, new(-0.0367f, 0.1229f)), new(0.5146f, new(-0.0061f, 0.1327f)),
                    new(0.5417f, new(0.0245f, 0.1285f)), new(0.5687f, new(0.0551f, 0.1106f)), new(0.5958f, new(0.0858f, 0.081f)),
                    new(0.6229f, new(0.1164f, 0.0428f)), new(0.65f, new(0.147f, 0f)))),
            new(FarFront,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.098f, 0f)), new(0.065f, new(-0.147f, 0f)), new(0.0813f, new(-0.1286f, 0.0259f)),
                    new(0.1083f, new(-0.098f, 0.0665f)), new(0.1354f, new(-0.0674f, 0.1f)), new(0.1625f, new(-0.0367f, 0.1229f)),
                    new(0.1896f, new(-0.0061f, 0.1327f)), new(0.2167f, new(0.0245f, 0.1285f)), new(0.2437f, new(0.0551f, 0.1106f)),
                    new(0.2708f, new(0.0858f, 0.081f)), new(0.2979f, new(0.1164f, 0.0428f)), new(0.325f, new(0.147f, 0f)),
                    new(0.65f, new(-0.098f, 0f)))),
            new(NearHind,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.098f, 0f)), new(0.065f, new(-0.147f, 0f)), new(0.0813f, new(-0.1286f, 0.0259f)),
                    new(0.1083f, new(-0.098f, 0.0665f)), new(0.1354f, new(-0.0674f, 0.1f)), new(0.1625f, new(-0.0367f, 0.1229f)),
                    new(0.1896f, new(-0.0061f, 0.1327f)), new(0.2167f, new(0.0245f, 0.1285f)), new(0.2437f, new(0.0551f, 0.1106f)),
                    new(0.2708f, new(0.0858f, 0.081f)), new(0.2979f, new(0.1164f, 0.0428f)), new(0.325f, new(0.147f, 0f)),
                    new(0.65f, new(-0.098f, 0f)))),
            new(NearFront,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.147f, 0f)), new(0.39f, new(-0.147f, 0f)), new(0.4062f, new(-0.1286f, 0.0259f)), new(0.4333f, new(-0.098f, 0.0665f)),
                    new(0.4604f, new(-0.0674f, 0.1f)), new(0.4875f, new(-0.0367f, 0.1229f)), new(0.5146f, new(-0.0061f, 0.1327f)),
                    new(0.5417f, new(0.0245f, 0.1285f)), new(0.5687f, new(0.0551f, 0.1106f)), new(0.5958f, new(0.0858f, 0.081f)),
                    new(0.6229f, new(0.1164f, 0.0428f)), new(0.65f, new(0.147f, 0f)))),
        ],
        Contacts = [
            new(FarHind, 0f, 0.39f, new(0.147f, 0f)), new(FarFront, 0f, 0.065f, new(-0.098f, 0f)),
            new(FarFront, 0.325f, 0.65f, new(0.392f, 0f)), new(NearHind, 0f, 0.065f, new(-0.098f, 0f)),
            new(NearHind, 0.325f, 0.65f, new(0.392f, 0f)), new(NearFront, 0f, 0.39f, new(0.147f, 0f))],
        Travel = Track.Of<Vector2f>(new(0f, new(0f, 0f)), new(0.65f, new(0.49f, 0f))),
    };

    public static SkeletonClip2D BabyTriceratopsRush { get; } = new("baby-triceratops-rush", 0.48f, loop: true)
    {
        Joints =
        [
            new(Body,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0f, -0.0385f)), new(0.06f, new(0f, -0.021f)), new(0.18f, new(0f, -0.056f)), new(0.3f, new(0f, -0.021f)),
                    new(0.42f, new(0f, -0.056f)), new(0.48f, new(0f, -0.0385f)))),
            new(Head, Turn: Track.Linear(new(0f, -0.32f), new(0.24f, -0.36f), new(0.48f, -0.32f))),
            new(Tail, Turn: Track.Linear(new(0f, 0.05f), new(0.24f, -0.12f), new(0.48f, 0.05f))),
        ],
        Limbs =
        [
            new(FarHind,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.161f, 0f)), new(0.288f, new(-0.161f, 0f)), new(0.3f, new(-0.1409f, 0.0205f)), new(0.32f, new(-0.1073f, 0.0525f)),
                    new(0.34f, new(-0.0738f, 0.0789f)), new(0.36f, new(-0.0403f, 0.097f)), new(0.38f, new(-0.0067f, 0.1048f)),
                    new(0.4f, new(0.0268f, 0.1014f)), new(0.42f, new(0.0604f, 0.0873f)), new(0.44f, new(0.0939f, 0.0639f)),
                    new(0.46f, new(0.1275f, 0.0338f)), new(0.48f, new(0.161f, 0f)))),
            new(FarFront,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.1073f, 0f)), new(0.048f, new(-0.161f, 0f)), new(0.06f, new(-0.1409f, 0.0205f)), new(0.08f, new(-0.1073f, 0.0525f)),
                    new(0.1f, new(-0.0738f, 0.0789f)), new(0.12f, new(-0.0403f, 0.097f)), new(0.14f, new(-0.0067f, 0.1048f)),
                    new(0.16f, new(0.0268f, 0.1014f)), new(0.18f, new(0.0604f, 0.0873f)), new(0.2f, new(0.0939f, 0.0639f)),
                    new(0.22f, new(0.1275f, 0.0338f)), new(0.24f, new(0.161f, 0f)), new(0.48f, new(-0.1073f, 0f)))),
            new(NearHind,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.1073f, 0f)), new(0.048f, new(-0.161f, 0f)), new(0.06f, new(-0.1409f, 0.0205f)), new(0.08f, new(-0.1073f, 0.0525f)),
                    new(0.1f, new(-0.0738f, 0.0789f)), new(0.12f, new(-0.0403f, 0.097f)), new(0.14f, new(-0.0067f, 0.1048f)),
                    new(0.16f, new(0.0268f, 0.1014f)), new(0.18f, new(0.0604f, 0.0873f)), new(0.2f, new(0.0939f, 0.0639f)),
                    new(0.22f, new(0.1275f, 0.0338f)), new(0.24f, new(0.161f, 0f)), new(0.48f, new(-0.1073f, 0f)))),
            new(NearFront,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.161f, 0f)), new(0.288f, new(-0.161f, 0f)), new(0.3f, new(-0.1409f, 0.0205f)), new(0.32f, new(-0.1073f, 0.0525f)),
                    new(0.34f, new(-0.0738f, 0.0789f)), new(0.36f, new(-0.0403f, 0.097f)), new(0.38f, new(-0.0067f, 0.1048f)),
                    new(0.4f, new(0.0268f, 0.1014f)), new(0.42f, new(0.0604f, 0.0873f)), new(0.44f, new(0.0939f, 0.0639f)),
                    new(0.46f, new(0.1275f, 0.0338f)), new(0.48f, new(0.161f, 0f)))),
        ],
        Contacts = [
            new(FarHind, 0f, 0.288f, new(0.161f, 0f)), new(FarFront, 0f, 0.048f, new(-0.1073f, 0f)),
            new(FarFront, 0.24f, 0.48f, new(0.4293f, 0f)), new(NearHind, 0f, 0.048f, new(-0.1073f, 0f)),
            new(NearHind, 0.24f, 0.48f, new(0.4293f, 0f)), new(NearFront, 0f, 0.288f, new(0.161f, 0f))],
        Travel = Track.Of<Vector2f>(new(0f, new(0f, 0f)), new(0.48f, new(0.5367f, 0f))),
    };

    public static SkeletonClip2D BabyTriceratopsScrape { get; } = new("baby-triceratops-scrape", 1.1f, loop: true)
    {
        Joints =
        [
            new(Head, Turn: Track.Linear(new(0f, -0.12f), new(0.55f, -0.18f), new(1.1f, -0.12f))),
        ],
        Limbs =
        [
            new(NearFront,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0f, 0f)), new(0.25f, new(0.112f, 0.084f)), new(0.45f, new(0.154f, 0f)), new(0.78f, new(-0.126f, 0f)),
                    new(1.1f, new(0f, 0f)))),
        ],
        Contacts = [new(FarHind, 0f, 1.1f, new(0f, 0f)), new(FarFront, 0f, 1.1f, new(0f, 0f)), new(NearHind, 0f, 1.1f, new(0f, 0f))],
        Markers = [new("scrape", 0.45f)],
    };

    public static SkeletonClip2D BabyTriceratopsWalk { get; } = new("baby-triceratops-walk", 1.2f, loop: true)
    {
        Joints =
        [
            new(Body,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0f, 0f)), new(0.15f, new(0f, 0.0175f)), new(0.45f, new(0f, -0.0175f)), new(0.75f, new(0f, 0.0175f)),
                    new(1.05f, new(0f, -0.0175f)), new(1.2f, new(0f, 0f)))),
            new(Head, Turn: Track.Linear(new(0f, 0f), new(0.6f, -0.04f), new(1.2f, 0f))),
            new(Tail, Turn: Track.Linear(new(0f, 0.05f), new(0.6f, -0.12f), new(1.2f, 0.05f))),
        ],
        Limbs =
        [
            new(FarHind,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.105f, 0f)), new(0.72f, new(-0.105f, 0f)), new(0.75f, new(-0.0919f, 0.0178f)), new(0.8f, new(-0.07f, 0.0455f)),
                    new(0.85f, new(-0.0481f, 0.0684f)), new(0.9f, new(-0.0262f, 0.0841f)), new(0.95f, new(-0.0044f, 0.0908f)),
                    new(1f, new(0.0175f, 0.0879f)), new(1.05f, new(0.0394f, 0.0757f)), new(1.1f, new(0.0612f, 0.0554f)),
                    new(1.15f, new(0.0831f, 0.0293f)), new(1.2f, new(0.105f, 0f)))),
            new(FarFront,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.07f, 0f)), new(0.12f, new(-0.105f, 0f)), new(0.15f, new(-0.0919f, 0.0178f)), new(0.2f, new(-0.07f, 0.0455f)),
                    new(0.25f, new(-0.0481f, 0.0684f)), new(0.3f, new(-0.0262f, 0.0841f)), new(0.35f, new(-0.0044f, 0.0908f)),
                    new(0.4f, new(0.0175f, 0.0879f)), new(0.45f, new(0.0394f, 0.0757f)), new(0.5f, new(0.0612f, 0.0554f)),
                    new(0.55f, new(0.0831f, 0.0293f)), new(0.6f, new(0.105f, 0f)), new(1.2f, new(-0.07f, 0f)))),
            new(NearHind,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.07f, 0f)), new(0.12f, new(-0.105f, 0f)), new(0.15f, new(-0.0919f, 0.0178f)), new(0.2f, new(-0.07f, 0.0455f)),
                    new(0.25f, new(-0.0481f, 0.0684f)), new(0.3f, new(-0.0262f, 0.0841f)), new(0.35f, new(-0.0044f, 0.0908f)),
                    new(0.4f, new(0.0175f, 0.0879f)), new(0.45f, new(0.0394f, 0.0757f)), new(0.5f, new(0.0612f, 0.0554f)),
                    new(0.55f, new(0.0831f, 0.0293f)), new(0.6f, new(0.105f, 0f)), new(1.2f, new(-0.07f, 0f)))),
            new(NearFront,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.105f, 0f)), new(0.72f, new(-0.105f, 0f)), new(0.75f, new(-0.0919f, 0.0178f)), new(0.8f, new(-0.07f, 0.0455f)),
                    new(0.85f, new(-0.0481f, 0.0684f)), new(0.9f, new(-0.0262f, 0.0841f)), new(0.95f, new(-0.0044f, 0.0908f)),
                    new(1f, new(0.0175f, 0.0879f)), new(1.05f, new(0.0394f, 0.0757f)), new(1.1f, new(0.0612f, 0.0554f)),
                    new(1.15f, new(0.0831f, 0.0293f)), new(1.2f, new(0.105f, 0f)))),
        ],
        Contacts = [
            new(FarHind, 0f, 0.72f, new(0.105f, 0f)), new(FarFront, 0f, 0.12f, new(-0.07f, 0f)), new(FarFront, 0.6f, 1.2f, new(0.28f, 0f)),
            new(NearHind, 0f, 0.12f, new(-0.07f, 0f)), new(NearHind, 0.6f, 1.2f, new(0.28f, 0f)), new(NearFront, 0f, 0.72f, new(0.105f, 0f))],
        Travel = Track.Of<Vector2f>(new(0f, new(0f, 0f)), new(1.2f, new(0.35f, 0f))),
    };

    public static ImmutableArray<SkeletonClip2D> All { get; } = [
        BabyTriceratopsBrake, BabyTriceratopsCharge, BabyTriceratopsDeath, BabyTriceratopsHeadDown, BabyTriceratopsHit, BabyTriceratopsIdle,
        BabyTriceratopsRecover, BabyTriceratopsRun, BabyTriceratopsRush, BabyTriceratopsScrape, BabyTriceratopsWalk];
}
