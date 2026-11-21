using App2d.Core.Geometry.Shapes;
using System.Collections.Immutable;
using static App2d.Core.Characters.PersonSkeleton;

namespace App2d.Core.Characters;

/// <summary>
/// The person's own body as attachments on <see cref="PersonSkeleton"/>: a neck and four limbs drawn as lines through their
/// joints, and a torso, head, hands and feet drawn as shapes on mounts. Clothes, hair and held things are further
/// attachments added to this list.
/// </summary>
public static class PersonBody
{
    private const string InkColor = "#222b32";
    private static readonly AttachmentMaterial Ink = new() { Fill = InkColor, StrokeWidth = 0.045f };

    // The torso's outline in a unit box: wider at the hips than the shoulders, with rounded corners.
    private static readonly Vector2f[] Torso =
    [
        new(0.3619f, 0.4206f), new(0.3575f, 0.4361f), new(0.3492f, 0.451f), new(0.3373f, 0.4647f), new(0.3224f, 0.4767f),
        new(0.3051f, 0.4866f), new(0.2859f, 0.494f), new(0.2657f, 0.4985f), new(0.245f, 0.5f), new(-0.245f, 0.5f),
        new(-0.2657f, 0.4985f), new(-0.2859f, 0.494f), new(-0.3051f, 0.4866f), new(-0.3224f, 0.4767f), new(-0.3373f, 0.4647f),
        new(-0.3492f, 0.451f), new(-0.3575f, 0.4361f), new(-0.3619f, 0.4206f), new(-0.4881f, -0.4206f), new(-0.4876f, -0.4361f),
        new(-0.4814f, -0.451f), new(-0.4697f, -0.4647f), new(-0.4529f, -0.4767f), new(-0.4316f, -0.4866f),
        new(-0.4067f, -0.494f), new(-0.3791f, -0.4985f), new(-0.35f, -0.5f), new(0.35f, -0.5f), new(0.3791f, -0.4985f),
        new(0.4067f, -0.494f), new(0.4316f, -0.4866f), new(0.4529f, -0.4767f), new(0.4697f, -0.4647f), new(0.4814f, -0.451f),
        new(0.4876f, -0.4361f), new(0.4881f, -0.4206f),
    ];

    // A hide worn over one shoulder: bare skin above a slanting edge, and three dark spots. In the same unit box as the torso.
    private static readonly PaintPatch[] SpottedHide =
    [
        new("#f3dcc6", [new(-0.35f, 0.65f), new(0.8f, 0.65f), new(0.8f, -0.04f)]),
        new("#765033", [new(-0.165f, 0.2f), new(-0.197f, 0.243f), new(-0.2689f, 0.2536f), new(-0.3266f, 0.2239f), new(-0.3266f, 0.1761f), new(-0.2689f, 0.1464f), new(-0.197f, 0.157f)]),
        new("#765033", [new(0.28f, -0.07f), new(0.2423f, -0.0192f), new(0.1577f, -0.0066f), new(0.0899f, -0.0418f), new(0.0899f, -0.0982f), new(0.1577f, -0.1334f), new(0.2423f, -0.1208f)]),
        new("#765033", [new(-0.16f, -0.3f), new(-0.1864f, -0.2648f), new(-0.2456f, -0.2561f), new(-0.2931f, -0.2805f), new(-0.2931f, -0.3195f), new(-0.2456f, -0.3439f), new(-0.1864f, -0.3352f)]),
    ];

    /// <summary>The standard body.</summary>
    public static ImmutableArray<Attachment> Standard { get; } = Build();

    /// <summary>
    /// A body for a build of the skeleton; pass the same <paramref name="torso"/> and <paramref name="width"/> as
    /// <see cref="PersonSkeleton.Build"/>. <paramref name="head"/> scales the head alone. Colours are "#rrggbb". Each piece
    /// takes its place in the drawing order from its joint; the head sits in front of its own, and hands and feet just in
    /// front of their limbs. <paramref name="spottedHide"/> paints the torso as a spotted hide worn over one shoulder.
    /// </summary>
    public static ImmutableArray<Attachment> Build(float torso = 1, float width = 1, float head = 1, string skin = "#fff8e7", string shirt = "#d8e9db", bool spottedHide = false)
    {
        // Every filled piece is outlined in ink; hands and feet more finely than the head and torso.
        static AttachmentMaterial Filled(string fill, float outline) => new() { Fill = fill, Outline = new() { Color = InkColor, Width = outline } };
        Vector2f size = new(0.36f * width, 0.68f * torso), lift = new(0, 0.32f * torso);
        var hand = Ellipse2D.Builder.Create(new(0.043f, 0.055f), new(0, -0.014f));
        var foot = Ellipse2D.Builder.Create(new(0.088f, 0.024f), new(0.044f, 0));
        return
        [
            new JointPath("chest-head", [Chest, Head], Ink),
            new JointPath("left-arm", [LeftShoulder, LeftElbow, LeftHand], Ink),
            new JointPath("left-leg", [LeftHip, LeftKnee, LeftFoot], Ink),
            new JointPath("right-arm", [RightShoulder, RightElbow, RightHand], Ink),
            new JointPath("right-leg", [RightHip, RightKnee, RightFoot], Ink),
            new MountedShape("body", BodyArt, SimplePolygon2D.Builder.Create(Torso.Select(corner => corner * size + lift)), Filled(shirt, 0.045f))
            {
                Patches = spottedHide ? [.. SpottedHide.Select(patch => patch with { Corners = [.. patch.Corners.Select(corner => corner * size + lift)] })] : [],
            },
            new MountedShape("head", HeadArt, Ellipse2D.Builder.Create(new Vector2f(0.28f, 0.29f) * head), Filled(skin, 0.045f), -0.12f),
            new MountedShape("left-hand", LeftHandArt, hand, Filled(skin, 0.012f), -0.004f),
            new MountedShape("right-hand", RightHandArt, hand, Filled(skin, 0.012f), -0.004f),
            new MountedShape("left-foot", LeftFootArt, foot, Filled(skin, 0.012f), -0.004f),
            new MountedShape("right-foot", RightFootArt, foot, Filled(skin, 0.012f), -0.004f),
        ];
    }
}
