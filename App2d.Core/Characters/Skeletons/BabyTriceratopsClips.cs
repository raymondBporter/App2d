// Generated from the authored JSON clips by LegacyClipEmitter in App2d.Tests. While those clips exist, change them and
// regenerate (set APP2D_WRITE_CLIPS=1 and run the tests named GeneratedRigTests) instead of editing this file.
using App2d.Core.Animation;
using System.Collections.Immutable;
using static App2d.Core.Characters.QuadrupedSkeleton;

namespace App2d.Core.Characters;

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
                    new(0f, new(0f, 0f)), new(1f, new(0f, 0f)), new(1.02024f, new(0.134796f, 0.0438691f)), new(1.04048f, new(0.092265f, 0.0788449f)),
                    new(1.06071f, new(0.03103f, 0.0978369f)), new(1.08095f, new(-0.0364956f, 0.096995f)),
                    new(1.10119f, new(-0.0966226f, 0.0764898f)), new(1.12143f, new(-0.137162f, 0.0404783f)), new(1.14167f, new(-0.149895f, 0f)),
                    new(1.1619f, new(-0.132241f, 0f)), new(1.18214f, new(-0.0877783f, 0f)), new(1.20238f, new(-0.0255209f, 0f)),
                    new(1.22262f, new(0.0419102f, 0f)), new(1.24286f, new(0.100845f, 0f)), new(1.2631f, new(0.139336f, 0f)),
                    new(1.28333f, new(0.149581f, 0.00747301f)), new(1.30357f, new(0.129501f, 0.050462f)), new(1.32381f, new(0.0831689f, 0.0832211f)),
                    new(1.34405f, new(0.0199761f, 0.0991093f)), new(1.36429f, new(-0.0472662f, 0.0949056f)),
                    new(1.38452f, new(-0.104927f, 0.0714622f)), new(1.40476f, new(-0.141316f, 0.0335317f)), new(1.425f, new(-0.149057f, 0f)),
                    new(1.44524f, new(-0.12658f, 0f)), new(1.46548f, new(-0.0784431f, 0f)), new(1.48571f, new(-0.0144035f, 0f)),
                    new(1.50595f, new(0.0525561f, 0f)), new(1.52619f, new(0.108861f, 0f)), new(1.54643f, new(0.143098f, 0f)),
                    new(1.56667f, new(0.148325f, 0.0149042f)), new(1.5869f, new(0.123483f, 0.0567726f)), new(1.60714f, new(0.0736076f, 0.0871319f)),
                    new(1.62738f, new(0.00881062f, 0.0998273f)), new(1.64762f, new(-0.0577725f, 0.0922854f)),
                    new(1.66786f, new(-0.112644f, 0.066035f)), new(1.6881f, new(-0.144679f, 0.0263976f)), new(1.70833f, new(-0.147385f, 0f)),
                    new(1.72857f, new(-0.120212f, 0f)), new(1.74881f, new(-0.0686692f, 0f)), new(1.76905f, new(-0.00320546f, 0f)),
                    new(1.78929f, new(0.0629081f, 0f)), new(1.80952f, new(0.116269f, 0f)), new(1.82976f, new(0.146059f, 0f)),
                    new(1.85f, new(0.146239f, 0.0222521f)), new(2.05f, new(0.14f, 0f)), new(2.15f, new(0.1f, 0f)), new(2.7f, new(0f, 0f)))),
            new(FarFront,
                Target: new(Smooth,
                    new(0f, new(0f, 0f)), new(1f, new(0f, 0f)), new(1.02024f, new(-0.134796f, 0f)), new(1.04048f, new(-0.092265f, 0f)),
                    new(1.06071f, new(-0.03103f, 0f)), new(1.08095f, new(0.0364956f, 0f)), new(1.10119f, new(0.0966226f, 0f)),
                    new(1.12143f, new(0.137162f, 0f)), new(1.14167f, new(0.149895f, 0.00373912f)), new(1.1619f, new(0.132241f, 0.0471986f)),
                    new(1.18214f, new(0.0877783f, 0.0810897f)), new(1.20238f, new(0.0255209f, 0.098542f)),
                    new(1.22262f, new(-0.0419102f, 0.0960174f)), new(1.24286f, new(-0.100845f, 0.0740278f)),
                    new(1.2631f, new(-0.139336f, 0.0370309f)), new(1.28333f, new(-0.149581f, 0f)), new(1.30357f, new(-0.129501f, 0f)),
                    new(1.32381f, new(-0.0831689f, 0f)), new(1.34405f, new(-0.0199761f, 0f)), new(1.36429f, new(0.0472662f, 0f)),
                    new(1.38452f, new(0.104927f, 0f)), new(1.40476f, new(0.141316f, 0f)), new(1.425f, new(0.149057f, 0.0111964f)),
                    new(1.44524f, new(0.12658f, 0.0536548f)), new(1.46548f, new(0.0784431f, 0.0852361f)), new(1.48571f, new(0.0144035f, 0.0995379f)),
                    new(1.50595f, new(-0.0525561f, 0.093661f)), new(1.52619f, new(-0.108861f, 0.0687967f)),
                    new(1.54643f, new(-0.143098f, 0.0299856f)), new(1.56667f, new(-0.148325f, 0f)), new(1.5869f, new(-0.123483f, 0f)),
                    new(1.60714f, new(-0.0736076f, 0f)), new(1.62738f, new(-0.00881062f, 0f)), new(1.64762f, new(0.0577725f, 0f)),
                    new(1.66786f, new(0.112644f, 0f)), new(1.6881f, new(0.144679f, 0f)), new(1.70833f, new(0.147385f, 0.0185912f)),
                    new(1.72857f, new(0.120212f, 0.0598111f)), new(1.74881f, new(0.0686692f, 0.0889058f)),
                    new(1.76905f, new(0.00320546f, 0.0999772f)), new(1.78929f, new(-0.0629081f, 0.0907807f)),
                    new(1.80952f, new(-0.116269f, 0.0631808f)), new(1.82976f, new(-0.146059f, 0.0227727f)), new(1.85f, new(-0.146239f, 0f)),
                    new(2.05f, new(0.14f, 0f)), new(2.15f, new(0.1f, 0f)), new(2.7f, new(0f, 0f)))),
            new(NearHind,
                Target: new(Smooth,
                    new(0f, new(0f, 0f)), new(1f, new(0f, 0f)), new(1.02024f, new(-0.134796f, 0f)), new(1.04048f, new(-0.092265f, 0f)),
                    new(1.06071f, new(-0.03103f, 0f)), new(1.08095f, new(0.0364956f, 0f)), new(1.10119f, new(0.0966226f, 0f)),
                    new(1.12143f, new(0.137162f, 0f)), new(1.14167f, new(0.149895f, 0.00373912f)), new(1.1619f, new(0.132241f, 0.0471986f)),
                    new(1.18214f, new(0.0877783f, 0.0810897f)), new(1.20238f, new(0.0255209f, 0.098542f)),
                    new(1.22262f, new(-0.0419102f, 0.0960174f)), new(1.24286f, new(-0.100845f, 0.0740278f)),
                    new(1.2631f, new(-0.139336f, 0.0370309f)), new(1.28333f, new(-0.149581f, 0f)), new(1.30357f, new(-0.129501f, 0f)),
                    new(1.32381f, new(-0.0831689f, 0f)), new(1.34405f, new(-0.0199761f, 0f)), new(1.36429f, new(0.0472662f, 0f)),
                    new(1.38452f, new(0.104927f, 0f)), new(1.40476f, new(0.141316f, 0f)), new(1.425f, new(0.149057f, 0.0111964f)),
                    new(1.44524f, new(0.12658f, 0.0536548f)), new(1.46548f, new(0.0784431f, 0.0852361f)), new(1.48571f, new(0.0144035f, 0.0995379f)),
                    new(1.50595f, new(-0.0525561f, 0.093661f)), new(1.52619f, new(-0.108861f, 0.0687967f)),
                    new(1.54643f, new(-0.143098f, 0.0299856f)), new(1.56667f, new(-0.148325f, 0f)), new(1.5869f, new(-0.123483f, 0f)),
                    new(1.60714f, new(-0.0736076f, 0f)), new(1.62738f, new(-0.00881062f, 0f)), new(1.64762f, new(0.0577725f, 0f)),
                    new(1.66786f, new(0.112644f, 0f)), new(1.6881f, new(0.144679f, 0f)), new(1.70833f, new(0.147385f, 0.0185912f)),
                    new(1.72857f, new(0.120212f, 0.0598111f)), new(1.74881f, new(0.0686692f, 0.0889058f)),
                    new(1.76905f, new(0.00320546f, 0.0999772f)), new(1.78929f, new(-0.0629081f, 0.0907807f)),
                    new(1.80952f, new(-0.116269f, 0.0631808f)), new(1.82976f, new(-0.146059f, 0.0227727f)), new(1.85f, new(-0.146239f, 0f)),
                    new(2.05f, new(0.14f, 0f)), new(2.15f, new(0.1f, 0f)), new(2.7f, new(0f, 0f)))),
            new(NearFront,
                Target: new(Smooth,
                    new(0f, new(0f, 0f)), new(0.2f, new(0.12f, 0.1f)), new(0.4f, new(0.16f, 0f)), new(0.65f, new(-0.12f, 0f)), new(1f, new(0f, 0f)),
                    new(1.02024f, new(0.134796f, 0.0438691f)), new(1.04048f, new(0.092265f, 0.0788449f)), new(1.06071f, new(0.03103f, 0.0978369f)),
                    new(1.08095f, new(-0.0364956f, 0.096995f)), new(1.10119f, new(-0.0966226f, 0.0764898f)),
                    new(1.12143f, new(-0.137162f, 0.0404783f)), new(1.14167f, new(-0.149895f, 0f)), new(1.1619f, new(-0.132241f, 0f)),
                    new(1.18214f, new(-0.0877783f, 0f)), new(1.20238f, new(-0.0255209f, 0f)), new(1.22262f, new(0.0419102f, 0f)),
                    new(1.24286f, new(0.100845f, 0f)), new(1.2631f, new(0.139336f, 0f)), new(1.28333f, new(0.149581f, 0.00747301f)),
                    new(1.30357f, new(0.129501f, 0.050462f)), new(1.32381f, new(0.0831689f, 0.0832211f)), new(1.34405f, new(0.0199761f, 0.0991093f)),
                    new(1.36429f, new(-0.0472662f, 0.0949056f)), new(1.38452f, new(-0.104927f, 0.0714622f)),
                    new(1.40476f, new(-0.141316f, 0.0335317f)), new(1.425f, new(-0.149057f, 0f)), new(1.44524f, new(-0.12658f, 0f)),
                    new(1.46548f, new(-0.0784431f, 0f)), new(1.48571f, new(-0.0144035f, 0f)), new(1.50595f, new(0.0525561f, 0f)),
                    new(1.52619f, new(0.108861f, 0f)), new(1.54643f, new(0.143098f, 0f)), new(1.56667f, new(0.148325f, 0.0149042f)),
                    new(1.5869f, new(0.123483f, 0.0567726f)), new(1.60714f, new(0.0736076f, 0.0871319f)),
                    new(1.62738f, new(0.00881062f, 0.0998273f)), new(1.64762f, new(-0.0577725f, 0.0922854f)),
                    new(1.66786f, new(-0.112644f, 0.066035f)), new(1.6881f, new(-0.144679f, 0.0263976f)), new(1.70833f, new(-0.147385f, 0f)),
                    new(1.72857f, new(-0.120212f, 0f)), new(1.74881f, new(-0.0686692f, 0f)), new(1.76905f, new(-0.00320546f, 0f)),
                    new(1.78929f, new(0.0629081f, 0f)), new(1.80952f, new(0.116269f, 0f)), new(1.82976f, new(0.146059f, 0f)),
                    new(1.85f, new(0.146239f, 0.0222521f)), new(2.05f, new(0.14f, 0f)), new(2.15f, new(0.1f, 0f)), new(2.7f, new(0f, 0f)))),
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
                    new(0f, new(0f, 0f)), new(0.08125f, new(0f, 0.0175f)), new(0.1625f, new(0f, 0f)), new(0.24374999f, new(0f, -0.0175f)),
                    new(0.325f, new(0f, 0f)), new(0.40625f, new(0f, 0.0175f)), new(0.48749998f, new(0f, 0f)), new(0.56874996f, new(0f, -0.0175f)),
                    new(0.65f, new(0f, 0f)))),
            new(Head, Turn: Track.Linear(new(0f, 0f), new(0.325f, -0.04f), new(0.65f, 0f))),
            new(Tail, Turn: Track.Linear(new(0f, 0.05f), new(0.325f, -0.12f), new(0.65f, 0.05f))),
        ],
        Limbs =
        [
            new(FarHind,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.147f, 0f)), new(0.027083334f, new(0.126583f, 0f)), new(0.054166667f, new(0.106167f, 0f)),
                    new(0.08125f, new(0.08575f, 0f)), new(0.108333334f, new(0.0653333f, 0f)), new(0.13541666f, new(0.0449167f, 0f)),
                    new(0.1625f, new(0.0245f, 0f)), new(0.18958332f, new(0.00408335f, 0f)), new(0.21666667f, new(-0.0163333f, 0f)),
                    new(0.24374999f, new(-0.03675f, 0f)), new(0.2708333f, new(-0.0571666f, 0f)), new(0.29791665f, new(-0.0775833f, 0f)),
                    new(0.325f, new(-0.098f, 0f)), new(0.35208333f, new(-0.118417f, 0f)), new(0.37916663f, new(-0.138833f, 0f)),
                    new(0.39000002f, new(-0.147f, 0f)), new(0.40625f, new(-0.128625f, 0.025947f)), new(0.43333334f, new(-0.098f, 0.0665f)),
                    new(0.46041664f, new(-0.067375f, 0.0999947f)), new(0.48749998f, new(-0.03675f, 0.122876f)),
                    new(0.51458335f, new(-0.00612499f, 0.132715f)), new(0.5416666f, new(0.0245f, 0.128468f)),
                    new(0.56874996f, new(0.055125f, 0.110585f)), new(0.5958333f, new(0.08575f, 0.0809653f)),
                    new(0.62291664f, new(0.116375f, 0.0427515f)), new(0.65f, new(0.147f, 0f)))),
            new(FarFront,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.098f, 0f)), new(0.027083334f, new(-0.118417f, 0f)), new(0.054166667f, new(-0.138833f, 0f)),
                    new(0.06500001f, new(-0.147f, 0f)), new(0.08125f, new(-0.128625f, 0.025947f)), new(0.108333334f, new(-0.098f, 0.0665f)),
                    new(0.13541666f, new(-0.067375f, 0.0999947f)), new(0.1625f, new(-0.03675f, 0.122876f)),
                    new(0.18958332f, new(-0.00612504f, 0.132715f)), new(0.21666667f, new(0.0245f, 0.128468f)),
                    new(0.24374999f, new(0.055125f, 0.110585f)), new(0.2708333f, new(0.08575f, 0.0809653f)),
                    new(0.29791665f, new(0.116375f, 0.0427514f)), new(0.325f, new(0.147f, 0f)), new(0.35208333f, new(0.126583f, 0f)),
                    new(0.37916663f, new(0.106167f, 0f)), new(0.40625f, new(0.08575f, 0f)), new(0.43333334f, new(0.0653333f, 0f)),
                    new(0.46041664f, new(0.0449167f, 0f)), new(0.48749998f, new(0.0245f, 0f)), new(0.51458335f, new(0.00408331f, 0f)),
                    new(0.5416666f, new(-0.0163333f, 0f)), new(0.56874996f, new(-0.03675f, 0f)), new(0.5958333f, new(-0.0571667f, 0f)),
                    new(0.62291664f, new(-0.0775833f, 0f)), new(0.65f, new(-0.098f, 0f)))),
            new(NearHind,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.098f, 0f)), new(0.027083334f, new(-0.118417f, 0f)), new(0.054166667f, new(-0.138833f, 0f)),
                    new(0.06500001f, new(-0.147f, 0f)), new(0.08125f, new(-0.128625f, 0.025947f)), new(0.108333334f, new(-0.098f, 0.0665f)),
                    new(0.13541666f, new(-0.067375f, 0.0999947f)), new(0.1625f, new(-0.03675f, 0.122876f)),
                    new(0.18958332f, new(-0.00612504f, 0.132715f)), new(0.21666667f, new(0.0245f, 0.128468f)),
                    new(0.24374999f, new(0.055125f, 0.110585f)), new(0.2708333f, new(0.08575f, 0.0809653f)),
                    new(0.29791665f, new(0.116375f, 0.0427514f)), new(0.325f, new(0.147f, 0f)), new(0.35208333f, new(0.126583f, 0f)),
                    new(0.37916663f, new(0.106167f, 0f)), new(0.40625f, new(0.08575f, 0f)), new(0.43333334f, new(0.0653333f, 0f)),
                    new(0.46041664f, new(0.0449167f, 0f)), new(0.48749998f, new(0.0245f, 0f)), new(0.51458335f, new(0.00408331f, 0f)),
                    new(0.5416666f, new(-0.0163333f, 0f)), new(0.56874996f, new(-0.03675f, 0f)), new(0.5958333f, new(-0.0571667f, 0f)),
                    new(0.62291664f, new(-0.0775833f, 0f)), new(0.65f, new(-0.098f, 0f)))),
            new(NearFront,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.147f, 0f)), new(0.027083334f, new(0.126583f, 0f)), new(0.054166667f, new(0.106167f, 0f)),
                    new(0.08125f, new(0.08575f, 0f)), new(0.108333334f, new(0.0653333f, 0f)), new(0.13541666f, new(0.0449167f, 0f)),
                    new(0.1625f, new(0.0245f, 0f)), new(0.18958332f, new(0.00408335f, 0f)), new(0.21666667f, new(-0.0163333f, 0f)),
                    new(0.24374999f, new(-0.03675f, 0f)), new(0.2708333f, new(-0.0571666f, 0f)), new(0.29791665f, new(-0.0775833f, 0f)),
                    new(0.325f, new(-0.098f, 0f)), new(0.35208333f, new(-0.118417f, 0f)), new(0.37916663f, new(-0.138833f, 0f)),
                    new(0.39000002f, new(-0.147f, 0f)), new(0.40625f, new(-0.128625f, 0.025947f)), new(0.43333334f, new(-0.098f, 0.0665f)),
                    new(0.46041664f, new(-0.067375f, 0.0999947f)), new(0.48749998f, new(-0.03675f, 0.122876f)),
                    new(0.51458335f, new(-0.00612499f, 0.132715f)), new(0.5416666f, new(0.0245f, 0.128468f)),
                    new(0.56874996f, new(0.055125f, 0.110585f)), new(0.5958333f, new(0.08575f, 0.0809653f)),
                    new(0.62291664f, new(0.116375f, 0.0427515f)), new(0.65f, new(0.147f, 0f)))),
        ],
        Contacts = [
            new(FarHind, 0f, 0.39000002f, new(0.147f, 0f)), new(FarFront, 0f, 0.06500001f, new(-0.098f, 0f)),
            new(FarFront, 0.325f, 0.65f, new(0.392f, 0f)), new(NearHind, 0f, 0.06500001f, new(-0.098f, 0f)),
            new(NearHind, 0.325f, 0.65f, new(0.392f, 0f)), new(NearFront, 0f, 0.39000002f, new(0.147f, 0f))],
        Travel = Track.Of<Vector2f>(new(0f, new(0f, 0f)), new(0.65f, new(0.49f, 0f))),
    };

    public static SkeletonClip2D BabyTriceratopsRush { get; } = new("baby-triceratops-rush", 0.48f, loop: true)
    {
        Joints =
        [
            new(Body,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0f, -0.0385f)), new(0.06f, new(0f, -0.021f)), new(0.12f, new(0f, -0.0385f)), new(0.17999999f, new(0f, -0.056f)),
                    new(0.24f, new(0f, -0.0385f)), new(0.29999998f, new(0f, -0.021f)), new(0.35999998f, new(0f, -0.0385f)),
                    new(0.42f, new(0f, -0.056f)), new(0.48f, new(0f, -0.0385f)))),
            new(Head, Turn: Track.Linear(new(0f, -0.32f), new(0.24f, -0.36f), new(0.48f, -0.32f))),
            new(Tail, Turn: Track.Linear(new(0f, 0.05f), new(0.24f, -0.12f), new(0.48f, 0.05f))),
        ],
        Limbs =
        [
            new(FarHind,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.161f, 0f)), new(0.02f, new(0.138639f, 0f)), new(0.04f, new(0.116278f, 0f)), new(0.06f, new(0.0939167f, 0f)),
                    new(0.08f, new(0.0715556f, 0f)), new(0.099999994f, new(0.0491945f, 0f)), new(0.12f, new(0.0268333f, 0f)),
                    new(0.13999999f, new(0.00447223f, 0f)), new(0.16f, new(-0.0178889f, 0f)), new(0.17999999f, new(-0.04025f, 0f)),
                    new(0.19999999f, new(-0.0626111f, 0f)), new(0.22f, new(-0.0849722f, 0f)), new(0.24f, new(-0.107333f, 0f)),
                    new(0.26f, new(-0.129694f, 0f)), new(0.27999997f, new(-0.152056f, 0f)), new(0.28800002f, new(-0.161f, 0f)),
                    new(0.29999998f, new(-0.140875f, 0.0204845f)), new(0.32f, new(-0.107333f, 0.0525f)),
                    new(0.33999997f, new(-0.0737917f, 0.0789432f)), new(0.35999998f, new(-0.04025f, 0.0970073f)),
                    new(0.38f, new(-0.00670833f, 0.104775f)), new(0.39999998f, new(0.0268333f, 0.101422f)), new(0.42f, new(0.060375f, 0.0873043f)),
                    new(0.44f, new(0.0939167f, 0.0639199f)), new(0.45999998f, new(0.127458f, 0.0337512f)), new(0.48f, new(0.161f, 0f)))),
            new(FarFront,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.107333f, 0f)), new(0.02f, new(-0.129694f, 0f)), new(0.04f, new(-0.152056f, 0f)),
                    new(0.04800001f, new(-0.161f, 0f)), new(0.06f, new(-0.140875f, 0.0204845f)), new(0.08f, new(-0.107333f, 0.0525f)),
                    new(0.099999994f, new(-0.0737917f, 0.0789432f)), new(0.12f, new(-0.04025f, 0.0970073f)),
                    new(0.13999999f, new(-0.00670838f, 0.104775f)), new(0.16f, new(0.0268334f, 0.101422f)),
                    new(0.17999999f, new(0.060375f, 0.0873043f)), new(0.19999999f, new(0.0939166f, 0.06392f)),
                    new(0.22f, new(0.127458f, 0.0337511f)), new(0.24f, new(0.161f, 0f)), new(0.26f, new(0.138639f, 0f)),
                    new(0.27999997f, new(0.116278f, 0f)), new(0.29999998f, new(0.0939167f, 0f)), new(0.32f, new(0.0715555f, 0f)),
                    new(0.33999997f, new(0.0491945f, 0f)), new(0.35999998f, new(0.0268333f, 0f)), new(0.38f, new(0.00447219f, 0f)),
                    new(0.39999998f, new(-0.0178888f, 0f)), new(0.42f, new(-0.04025f, 0f)), new(0.44f, new(-0.0626111f, 0f)),
                    new(0.45999998f, new(-0.0849722f, 0f)), new(0.48f, new(-0.107333f, 0f)))),
            new(NearHind,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.107333f, 0f)), new(0.02f, new(-0.129694f, 0f)), new(0.04f, new(-0.152056f, 0f)),
                    new(0.04800001f, new(-0.161f, 0f)), new(0.06f, new(-0.140875f, 0.0204845f)), new(0.08f, new(-0.107333f, 0.0525f)),
                    new(0.099999994f, new(-0.0737917f, 0.0789432f)), new(0.12f, new(-0.04025f, 0.0970073f)),
                    new(0.13999999f, new(-0.00670838f, 0.104775f)), new(0.16f, new(0.0268334f, 0.101422f)),
                    new(0.17999999f, new(0.060375f, 0.0873043f)), new(0.19999999f, new(0.0939166f, 0.06392f)),
                    new(0.22f, new(0.127458f, 0.0337511f)), new(0.24f, new(0.161f, 0f)), new(0.26f, new(0.138639f, 0f)),
                    new(0.27999997f, new(0.116278f, 0f)), new(0.29999998f, new(0.0939167f, 0f)), new(0.32f, new(0.0715555f, 0f)),
                    new(0.33999997f, new(0.0491945f, 0f)), new(0.35999998f, new(0.0268333f, 0f)), new(0.38f, new(0.00447219f, 0f)),
                    new(0.39999998f, new(-0.0178888f, 0f)), new(0.42f, new(-0.04025f, 0f)), new(0.44f, new(-0.0626111f, 0f)),
                    new(0.45999998f, new(-0.0849722f, 0f)), new(0.48f, new(-0.107333f, 0f)))),
            new(NearFront,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.161f, 0f)), new(0.02f, new(0.138639f, 0f)), new(0.04f, new(0.116278f, 0f)), new(0.06f, new(0.0939167f, 0f)),
                    new(0.08f, new(0.0715556f, 0f)), new(0.099999994f, new(0.0491945f, 0f)), new(0.12f, new(0.0268333f, 0f)),
                    new(0.13999999f, new(0.00447223f, 0f)), new(0.16f, new(-0.0178889f, 0f)), new(0.17999999f, new(-0.04025f, 0f)),
                    new(0.19999999f, new(-0.0626111f, 0f)), new(0.22f, new(-0.0849722f, 0f)), new(0.24f, new(-0.107333f, 0f)),
                    new(0.26f, new(-0.129694f, 0f)), new(0.27999997f, new(-0.152056f, 0f)), new(0.28800002f, new(-0.161f, 0f)),
                    new(0.29999998f, new(-0.140875f, 0.0204845f)), new(0.32f, new(-0.107333f, 0.0525f)),
                    new(0.33999997f, new(-0.0737917f, 0.0789432f)), new(0.35999998f, new(-0.04025f, 0.0970073f)),
                    new(0.38f, new(-0.00670833f, 0.104775f)), new(0.39999998f, new(0.0268333f, 0.101422f)), new(0.42f, new(0.060375f, 0.0873043f)),
                    new(0.44f, new(0.0939167f, 0.0639199f)), new(0.45999998f, new(0.127458f, 0.0337512f)), new(0.48f, new(0.161f, 0f)))),
        ],
        Contacts = [
            new(FarHind, 0f, 0.28800002f, new(0.161f, 0f)), new(FarFront, 0f, 0.04800001f, new(-0.107333f, 0f)),
            new(FarFront, 0.24f, 0.48f, new(0.429333f, 0f)), new(NearHind, 0f, 0.04800001f, new(-0.107333f, 0f)),
            new(NearHind, 0.24f, 0.48f, new(0.429333f, 0f)), new(NearFront, 0f, 0.28800002f, new(0.161f, 0f))],
        Travel = Track.Of<Vector2f>(new(0f, new(0f, 0f)), new(0.48f, new(0.536667f, 0f))),
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
                    new(0f, new(0f, 0f)), new(0.15f, new(0f, 0.0175f)), new(0.3f, new(0f, 0f)), new(0.45000002f, new(0f, -0.0175f)),
                    new(0.6f, new(0f, 0f)), new(0.75f, new(0f, 0.0175f)), new(0.90000004f, new(0f, 0f)), new(1.0500001f, new(0f, -0.0175f)),
                    new(1.2f, new(0f, 0f)))),
            new(Head, Turn: Track.Linear(new(0f, 0f), new(0.6f, -0.04f), new(1.2f, 0f))),
            new(Tail, Turn: Track.Linear(new(0f, 0.05f), new(0.6f, -0.12f), new(1.2f, 0.05f))),
        ],
        Limbs =
        [
            new(FarHind,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.105f, 0f)), new(0.050000004f, new(0.0904167f, 0f)), new(0.10000001f, new(0.0758333f, 0f)),
                    new(0.15f, new(0.06125f, 0f)), new(0.20000002f, new(0.0466667f, 0f)), new(0.25f, new(0.0320833f, 0f)),
                    new(0.3f, new(0.0175f, 0f)), new(0.35f, new(0.00291667f, 0f)), new(0.40000004f, new(-0.0116667f, 0f)),
                    new(0.45000002f, new(-0.02625f, 0f)), new(0.5f, new(-0.0408333f, 0f)), new(0.55f, new(-0.0554167f, 0f)),
                    new(0.6f, new(-0.07f, 0f)), new(0.65000004f, new(-0.0845833f, 0f)), new(0.7f, new(-0.0991667f, 0f)),
                    new(0.72f, new(-0.105f, 0f)), new(0.75f, new(-0.091875f, 0.0177532f)), new(0.8000001f, new(-0.07f, 0.0455f)),
                    new(0.85f, new(-0.048125f, 0.0684174f)), new(0.90000004f, new(-0.02625f, 0.084073f)),
                    new(0.95000005f, new(-0.004375f, 0.0908052f)), new(1f, new(0.0175f, 0.0878993f)), new(1.0500001f, new(0.039375f, 0.0756637f)),
                    new(1.1f, new(0.06125f, 0.0553973f)), new(1.15f, new(0.083125f, 0.029251f)), new(1.2f, new(0.105f, 0f)))),
            new(FarFront,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.07f, 0f)), new(0.050000004f, new(-0.0845833f, 0f)), new(0.10000001f, new(-0.0991667f, 0f)),
                    new(0.120000035f, new(-0.105f, 0f)), new(0.15f, new(-0.091875f, 0.0177532f)), new(0.20000002f, new(-0.07f, 0.0455f)),
                    new(0.25f, new(-0.048125f, 0.0684174f)), new(0.3f, new(-0.02625f, 0.084073f)), new(0.35f, new(-0.00437503f, 0.0908052f)),
                    new(0.40000004f, new(0.0175f, 0.0878992f)), new(0.45000002f, new(0.039375f, 0.0756637f)), new(0.5f, new(0.06125f, 0.0553973f)),
                    new(0.55f, new(0.083125f, 0.029251f)), new(0.6f, new(0.105f, 0f)), new(0.65000004f, new(0.0904166f, 0f)),
                    new(0.7f, new(0.0758334f, 0f)), new(0.75f, new(0.06125f, 0f)), new(0.8000001f, new(0.0466666f, 0f)),
                    new(0.85f, new(0.0320834f, 0f)), new(0.90000004f, new(0.0175f, 0f)), new(0.95000005f, new(0.00291664f, 0f)),
                    new(1f, new(-0.0116666f, 0f)), new(1.0500001f, new(-0.02625f, 0f)), new(1.1f, new(-0.0408334f, 0f)),
                    new(1.15f, new(-0.0554166f, 0f)), new(1.2f, new(-0.07f, 0f)))),
            new(NearHind,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.07f, 0f)), new(0.050000004f, new(-0.0845833f, 0f)), new(0.10000001f, new(-0.0991667f, 0f)),
                    new(0.120000035f, new(-0.105f, 0f)), new(0.15f, new(-0.091875f, 0.0177532f)), new(0.20000002f, new(-0.07f, 0.0455f)),
                    new(0.25f, new(-0.048125f, 0.0684174f)), new(0.3f, new(-0.02625f, 0.084073f)), new(0.35f, new(-0.00437503f, 0.0908052f)),
                    new(0.40000004f, new(0.0175f, 0.0878992f)), new(0.45000002f, new(0.039375f, 0.0756637f)), new(0.5f, new(0.06125f, 0.0553973f)),
                    new(0.55f, new(0.083125f, 0.029251f)), new(0.6f, new(0.105f, 0f)), new(0.65000004f, new(0.0904166f, 0f)),
                    new(0.7f, new(0.0758334f, 0f)), new(0.75f, new(0.06125f, 0f)), new(0.8000001f, new(0.0466666f, 0f)),
                    new(0.85f, new(0.0320834f, 0f)), new(0.90000004f, new(0.0175f, 0f)), new(0.95000005f, new(0.00291664f, 0f)),
                    new(1f, new(-0.0116666f, 0f)), new(1.0500001f, new(-0.02625f, 0f)), new(1.1f, new(-0.0408334f, 0f)),
                    new(1.15f, new(-0.0554166f, 0f)), new(1.2f, new(-0.07f, 0f)))),
            new(NearFront,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.105f, 0f)), new(0.050000004f, new(0.0904167f, 0f)), new(0.10000001f, new(0.0758333f, 0f)),
                    new(0.15f, new(0.06125f, 0f)), new(0.20000002f, new(0.0466667f, 0f)), new(0.25f, new(0.0320833f, 0f)),
                    new(0.3f, new(0.0175f, 0f)), new(0.35f, new(0.00291667f, 0f)), new(0.40000004f, new(-0.0116667f, 0f)),
                    new(0.45000002f, new(-0.02625f, 0f)), new(0.5f, new(-0.0408333f, 0f)), new(0.55f, new(-0.0554167f, 0f)),
                    new(0.6f, new(-0.07f, 0f)), new(0.65000004f, new(-0.0845833f, 0f)), new(0.7f, new(-0.0991667f, 0f)),
                    new(0.72f, new(-0.105f, 0f)), new(0.75f, new(-0.091875f, 0.0177532f)), new(0.8000001f, new(-0.07f, 0.0455f)),
                    new(0.85f, new(-0.048125f, 0.0684174f)), new(0.90000004f, new(-0.02625f, 0.084073f)),
                    new(0.95000005f, new(-0.004375f, 0.0908052f)), new(1f, new(0.0175f, 0.0878993f)), new(1.0500001f, new(0.039375f, 0.0756637f)),
                    new(1.1f, new(0.06125f, 0.0553973f)), new(1.15f, new(0.083125f, 0.029251f)), new(1.2f, new(0.105f, 0f)))),
        ],
        Contacts = [
            new(FarHind, 0f, 0.72f, new(0.105f, 0f)), new(FarFront, 0f, 0.120000035f, new(-0.07f, 0f)), new(FarFront, 0.6f, 1.2f, new(0.28f, 0f)),
            new(NearHind, 0f, 0.120000035f, new(-0.07f, 0f)), new(NearHind, 0.6f, 1.2f, new(0.28f, 0f)), new(NearFront, 0f, 0.72f, new(0.105f, 0f))],
        Travel = Track.Of<Vector2f>(new(0f, new(0f, 0f)), new(1.2f, new(0.35f, 0f))),
    };

    public static ImmutableArray<SkeletonClip2D> All { get; } = [
        BabyTriceratopsBrake, BabyTriceratopsCharge, BabyTriceratopsDeath, BabyTriceratopsHeadDown, BabyTriceratopsHit, BabyTriceratopsIdle,
        BabyTriceratopsRecover, BabyTriceratopsRun, BabyTriceratopsRush, BabyTriceratopsScrape, BabyTriceratopsWalk];
}
