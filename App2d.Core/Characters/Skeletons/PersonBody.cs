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
    private static readonly RenderMaterialDefinition2D Ink = new() { Fill = InkColor, StrokeWidth = .045f };

    // The torso's outline in a unit box: wider at the hips than the shoulders, with rounded corners.
    private static readonly Vector2f[] Torso =
    [
        new(0.361912f, 0.420588f), new(0.357515f, 0.436081f), new(0.349193f, 0.450978f), new(0.337331f, 0.464707f), new(0.322429f, 0.476741f),
        new(0.305075f, 0.486617f), new(0.285921f, 0.493955f), new(0.265658f, 0.498474f), new(0.245f, 0.5f), new(-0.245f, 0.5f),
        new(-0.265658f, 0.498474f), new(-0.285921f, 0.493955f), new(-0.305075f, 0.486617f), new(-0.322429f, 0.476741f), new(-0.337331f, 0.464707f),
        new(-0.349193f, 0.450978f), new(-0.357515f, 0.436081f), new(-0.361912f, 0.420588f), new(-0.488088f, -0.420588f), new(-0.487585f, -0.436081f),
        new(-0.481396f, -0.450978f), new(-0.469694f, -0.464707f), new(-0.452884f, -0.476741f), new(-0.431596f, -0.486617f),
        new(-0.406664f, -0.493955f), new(-0.37909f, -0.498474f), new(-0.35f, -0.5f), new(0.35f, -0.5f), new(0.37909f, -0.498474f),
        new(0.406664f, -0.493955f), new(0.431596f, -0.486617f), new(0.452884f, -0.476741f), new(0.469694f, -0.464707f), new(0.481397f, -0.450978f),
        new(0.487585f, -0.436081f), new(0.488088f, -0.420588f),
    ];

    /// <summary>The standard body.</summary>
    public static ImmutableArray<Attachment> Standard { get; } = Build();

    /// <summary>
    /// A body for a build of the skeleton; pass the same <paramref name="torso"/> and <paramref name="width"/> as
    /// <see cref="PersonSkeleton.Build"/>. <paramref name="head"/> scales the head alone. Colours are "#rrggbb". Each piece
    /// takes its place in the drawing order from its joint; the head sits in front of its own, and hands and feet just in
    /// front of their limbs.
    /// </summary>
    public static ImmutableArray<Attachment> Build(float torso = 1, float width = 1, float head = 1, string skin = "#fff8e7", string shirt = "#d8e9db")
    {
        // Every filled piece is outlined in ink; hands and feet more finely than the head and torso.
        static RenderMaterialDefinition2D Filled(string fill, float outline) => new() { Fill = fill, Outline = new() { Color = InkColor, Width = outline } };
        Vector2f size = new(.36f * width, .68f * torso), lift = new(0, .32f * torso);
        var hand = Ellipse2D.Builder.Create(new(.043f, .055f), new(0, -.014f));
        var foot = Ellipse2D.Builder.Create(new(.088f, .024f), new(.044f, 0));
        return
        [
            new JointPath("chest-head", [Chest, Head], Ink),
            new JointPath("left-arm", [LeftShoulder, LeftElbow, LeftHand], Ink),
            new JointPath("left-leg", [LeftHip, LeftKnee, LeftFoot], Ink),
            new JointPath("right-arm", [RightShoulder, RightElbow, RightHand], Ink),
            new JointPath("right-leg", [RightHip, RightKnee, RightFoot], Ink),
            new MountedShape("body", BodyArt, SimplePolygon2D.Builder.Create(Torso.Select(corner => corner * size + lift)), Filled(shirt, .045f)),
            new MountedShape("head", HeadArt, Ellipse2D.Builder.Create(new Vector2f(.28f, .29f) * head), Filled(skin, .045f), -.12f),
            new MountedShape("left-hand", LeftHandArt, hand, Filled(skin, .012f), -.004f),
            new MountedShape("right-hand", RightHandArt, hand, Filled(skin, .012f), -.004f),
            new MountedShape("left-foot", LeftFootArt, foot, Filled(skin, .012f), -.004f),
            new MountedShape("right-foot", RightFootArt, foot, Filled(skin, .012f), -.004f),
        ];
    }
}
