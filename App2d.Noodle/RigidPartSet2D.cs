using App2d.Core.Geometry;
using App2d.Rendering;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Noodle;

internal enum RigidPartShape2D
{
    Segment,
    Circle,
    SplineLimb,
    SplineTorso,
    SplineHead
}

internal enum RigidPartStyle2D
{
    SolidPrototype,
    SplineMan
}

/// <summary>
/// Prototype equivalent of bone -> PNG + draw order. SpriteId becomes a real PNG
/// path later; the solid geometry is deliberately disposable.
/// </summary>
internal sealed record RigidPartAsset2D(
    string Bone,
    string SpriteId,
    RigidPartShape2D Shape,
    float Radius,
    float JointOverlap,
    int DrawOrder,
    XnaColor Color);

internal sealed class RigidPartSet2D
{
    public RigidPartSet2D(
        string name,
        IEnumerable<RigidPartAsset2D> parts,
        XnaColor accent,
        RigidPartStyle2D style = RigidPartStyle2D.SolidPrototype)
    {
        Name = name;
        Parts = parts.OrderBy(part => part.DrawOrder).ToArray();
        Accent = accent;
        Style = style;
    }

    public string Name { get; }
    public IReadOnlyList<RigidPartAsset2D> Parts { get; }
    public XnaColor Accent { get; }
    public RigidPartStyle2D Style { get; }

    public static IReadOnlyList<RigidPartSet2D> PrototypeSets { get; } =
    [
        CreateSplineMan(),
        Create("PLUM KNIGHT", new XnaColor(149, 103, 244), new XnaColor(112, 72, 207), new XnaColor(238, 181, 123)),
        Create("MINT BANDIT", new XnaColor(54, 202, 159), new XnaColor(26, 141, 123), new XnaColor(246, 194, 142))
    ];

    private static RigidPartSet2D CreateSplineMan()
    {
        var body = new XnaColor(74, 171, 255);
        var shadow = new XnaColor(38, 92, 151);
        var skin = new XnaColor(255, 194, 139);

        RigidPartAsset2D Limb(string bone, float radius, int order, XnaColor color) =>
            new(bone, $"spline://splineman/{bone}", RigidPartShape2D.SplineLimb, radius, 8f, order, color);
        RigidPartAsset2D Torso(string bone, int order, XnaColor color) =>
            new(bone, $"spline://splineman/{bone}", RigidPartShape2D.SplineTorso, 0f, 0f, order, color);
        RigidPartAsset2D Head(string bone, float radius, int order, XnaColor color) =>
            new(bone, $"spline://splineman/{bone}", RigidPartShape2D.SplineHead, radius, 0f, order, color);

        return new RigidPartSet2D("SPLINEMAN / PROFILE TEST",
        [
            Limb(StandardBones2D.ThighLeft, 18f, 0, shadow),
            Limb(StandardBones2D.ShinLeft, 14f, 1, shadow),
            Limb(StandardBones2D.FootLeft, 13f, 2, shadow),
            Limb(StandardBones2D.UpperArmLeft, 14f, 3, shadow),
            Limb(StandardBones2D.ForearmLeft, 12f, 4, shadow),
            Limb(StandardBones2D.HandLeft, 11f, 5, skin),
            Torso(StandardBones2D.Torso, 10, body),
            Head(StandardBones2D.Head, StandardSkeleton2D.HeadRadius, 11, skin),
            Limb(StandardBones2D.ThighRight, 20f, 20, body),
            Limb(StandardBones2D.ShinRight, 16f, 21, body),
            Limb(StandardBones2D.FootRight, 14f, 22, body),
            Limb(StandardBones2D.UpperArmRight, 16f, 23, body),
            Limb(StandardBones2D.ForearmRight, 13f, 24, body),
            Limb(StandardBones2D.HandRight, 12f, 25, skin)
        ], new XnaColor(255, 222, 102), RigidPartStyle2D.SplineMan);
    }

    private static RigidPartSet2D Create(string name, XnaColor body, XnaColor shadow, XnaColor skin)
    {
        RigidPartAsset2D Segment(string bone, float radius, int order, XnaColor color) =>
            new(bone, $"solid://{name.ToLowerInvariant().Replace(' ', '-')}/{bone}.png",
                RigidPartShape2D.Segment, radius, 7f, order, color);
        RigidPartAsset2D Circle(string bone, float radius, int order, XnaColor color) =>
            new(bone, $"solid://{name.ToLowerInvariant().Replace(' ', '-')}/{bone}.png",
                RigidPartShape2D.Circle, radius, 0f, order, color);

        return new RigidPartSet2D(name,
        [
            Segment(StandardBones2D.ThighLeft, 18f, 0, shadow),
            Segment(StandardBones2D.ShinLeft, 15f, 1, shadow),
            Segment(StandardBones2D.FootLeft, 14f, 2, shadow),
            Segment(StandardBones2D.UpperArmLeft, 15f, 3, shadow),
            Segment(StandardBones2D.ForearmLeft, 13f, 4, shadow),
            Segment(StandardBones2D.HandLeft, 11f, 5, skin),
            Segment(StandardBones2D.Torso, 37f, 10, body),
            Circle(StandardBones2D.Head, StandardSkeleton2D.HeadRadius, 11, skin),
            Segment(StandardBones2D.ThighRight, 20f, 20, body),
            Segment(StandardBones2D.ShinRight, 17f, 21, body),
            Segment(StandardBones2D.FootRight, 15f, 22, body),
            Segment(StandardBones2D.UpperArmRight, 17f, 23, body),
            Segment(StandardBones2D.ForearmRight, 14f, 24, body),
            Segment(StandardBones2D.HandRight, 12f, 25, skin)
        ], new XnaColor(255, 219, 109));
    }
}

internal sealed class RigidPuppetRenderer2D
{
    private readonly List<PartInstance> _parts = [];
    private RigidPartSet2D _partSet;

    public RigidPuppetRenderer2D(RigidPartSet2D partSet)
    {
        _partSet = partSet;
        RebuildParts();
    }

    public RigidPartSet2D PartSet
    {
        get => _partSet;
        set
        {
            if (ReferenceEquals(_partSet, value))
                return;
            _partSet = value;
            RebuildParts();
        }
    }

    public void Render(
        Renderer2D renderer,
        SolvedRigidPose2D pose,
        double totalSeconds,
        float sideViewAmount = 1f)
    {
        sideViewAmount = Math.Clamp(sideViewAmount, 0f, 1f);
        foreach (var instance in _parts)
        {
            var bone = pose.Bones[instance.Asset.Bone];
            instance.Object.Transform.Position = bone.Start;
            instance.Object.Transform.Rotation = bone.Angle;
            instance.Object.Transform.Scale = GetViewScale(instance.Asset, sideViewAmount);
            renderer.Draw(instance.Object);
            if (_partSet.Style == RigidPartStyle2D.SplineMan)
                renderer.DrawShapeOutline(instance.Object, new XnaColor(24, 32, 52, 225), 2f);
        }

        DrawAttachments(renderer, pose, totalSeconds);
        DrawFace(renderer, pose, sideViewAmount);
    }

    private void RebuildParts()
    {
        _parts.Clear();
        foreach (var asset in _partSet.Parts)
        {
            var nominalLength = GetNominalLength(asset.Bone);
            IShape2D shape = asset.Shape switch
            {
                RigidPartShape2D.Circle => new Circle2D(asset.Radius),
                RigidPartShape2D.SplineLimb => SplineSilhouette2D.CreateLimb(
                    nominalLength,
                    asset.Radius,
                    asset.Radius * 0.78f,
                    asset.JointOverlap),
                RigidPartShape2D.SplineTorso => SplineSilhouette2D.CreateTorso(nominalLength),
                RigidPartShape2D.SplineHead => SplineSilhouette2D.CreateHead(asset.Radius),
                _ => new Capsule2D(
                    new Vector2(-asset.JointOverlap, 0f),
                    new Vector2(nominalLength + asset.JointOverlap, 0f),
                    asset.Radius)
            };
            _parts.Add(new PartInstance(asset, new WorldObject2D(shape, new SolidColorShader(asset.Color))));
        }
    }

    private void DrawAttachments(Renderer2D renderer, SolvedRigidPose2D pose, double totalSeconds)
    {
        var hand = pose.Sockets[StandardBones2D.HandSocketRight];
        var swordAngle = hand.Angle - 0.08f;
        var swordStart = hand.Position - Direction(swordAngle) * 10f;
        var swordEnd = swordStart + Direction(swordAngle) * 112f;
        DrawThickSegment(renderer, swordStart, swordEnd, 6f, new XnaColor(219, 230, 241));
        var guard = Perpendicular(Direction(swordAngle));
        Span<Vector2> guardLine = [hand.Position - guard * 19f, hand.Position + guard * 19f];
        renderer.DrawWorldPolyline(guardLine, _partSet.Accent, 7f);

        // A head_top socket plus one joint curve: this is the stand-in for a hat,
        // ponytail, antenna, cloth tip, or any other cheap secondary motion.
        var head = pose.Sockets[StandardBones2D.HeadTopSocket];
        var wobble = MathF.Sin((float)totalSeconds * 4.1f) * 0.12f;
        var hatDirection = Direction(head.Angle + MathF.PI / 2f + wobble);
        var brimDirection = Perpendicular(hatDirection);
        Span<Vector2> brim = [head.Position - brimDirection * 32f, head.Position + brimDirection * 32f];
        renderer.DrawWorldPolyline(brim, _partSet.Accent, 9f);
        Span<Vector2> plume = [head.Position, head.Position + hatDirection * 48f];
        renderer.DrawWorldPolyline(plume, _partSet.Accent, 7f);
    }

    private void DrawFace(Renderer2D renderer, SolvedRigidPose2D pose, float sideViewAmount)
    {
        var head = pose.Bones[StandardBones2D.Head];
        var rotation = Matrix3x2.CreateRotation(head.Angle);
        Vector2 ToWorld(float forward, float up) =>
            head.Start + Vector2.Transform(new Vector2(forward * pose.Facing, up), rotation);

        if (_partSet.Style != RigidPartStyle2D.SplineMan)
        {
            renderer.DrawWorldCircle(ToWorld(10f, 6f), 4f, new XnaColor(31, 31, 48), 4f);
            return;
        }

        var ink = new XnaColor(24, 32, 52);
        var primaryEye = ToWorld(float.Lerp(7f, 12f, sideViewAmount), 8f);
        renderer.DrawWorldCircle(primaryEye, 4f, ink, 4f);

        var secondEyeVisibility = 1f - sideViewAmount;
        if (secondEyeVisibility > 0.03f)
        {
            var alpha = (byte)(220f * secondEyeVisibility);
            renderer.DrawWorldCircle(ToWorld(-2f, 8f), 3.5f,
                new XnaColor((byte)24, (byte)32, (byte)52, alpha), 3.5f);
        }

        // The nose grows into a readable profile as movement turns the puppet
        // fully sideways. At idle it recedes, leaving a softer three-quarter face.
        var noseAmount = float.Lerp(3f, 11f, sideViewAmount);
        Span<Vector2> nose =
        [
            ToWorld(25f, 5f),
            ToWorld(25f + noseAmount, 1f),
            ToWorld(24f, -3f)
        ];
        renderer.DrawWorldConvexPolygon(nose, new XnaColor(232, 151, 105));

        Span<Vector2> mouth = [ToWorld(19f, -10f), ToWorld(27f, -9f)];
        renderer.DrawWorldPolyline(mouth, ink, 2f);
    }

    private Vector2 GetViewScale(RigidPartAsset2D asset, float sideViewAmount)
    {
        if (_partSet.Style != RigidPartStyle2D.SplineMan)
            return Vector2.One;

        if (asset.Shape == RigidPartShape2D.SplineHead)
            return new Vector2(float.Lerp(1f, 0.88f, sideViewAmount), 1f);

        var transverseScale = asset.Shape switch
        {
            RigidPartShape2D.SplineTorso => float.Lerp(1f, 0.72f, sideViewAmount),
            _ => float.Lerp(1f, 0.92f, sideViewAmount)
        };
        return new Vector2(1f, transverseScale);
    }

    private static void DrawThickSegment(Renderer2D renderer, Vector2 start, Vector2 end, float halfWidth, XnaColor color)
    {
        var direction = Vector2.Normalize(end - start);
        var normal = Perpendicular(direction) * halfWidth;
        Span<Vector2> polygon = [start - normal, end - normal, end + normal, start + normal];
        renderer.DrawWorldConvexPolygon(polygon, color);
    }

    private static float GetNominalLength(string bone) => bone switch
    {
        StandardBones2D.Torso => StandardSkeleton2D.TorsoLength,
        StandardBones2D.UpperArmLeft or StandardBones2D.UpperArmRight => StandardSkeleton2D.UpperArmLength,
        StandardBones2D.ForearmLeft or StandardBones2D.ForearmRight => StandardSkeleton2D.ForearmLength,
        StandardBones2D.ThighLeft or StandardBones2D.ThighRight => StandardSkeleton2D.ThighLength,
        StandardBones2D.ShinLeft or StandardBones2D.ShinRight => StandardSkeleton2D.ShinLength,
        StandardBones2D.HandLeft => 29f,
        StandardBones2D.HandRight => 31f,
        StandardBones2D.FootLeft or StandardBones2D.FootRight => 45f,
        _ => 1f
    };

    private static Vector2 Direction(float angle) => new(MathF.Cos(angle), MathF.Sin(angle));
    private static Vector2 Perpendicular(Vector2 value) => new(-value.Y, value.X);
    private sealed record PartInstance(RigidPartAsset2D Asset, WorldObject2D Object);
}
