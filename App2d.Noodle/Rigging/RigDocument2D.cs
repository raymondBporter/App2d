using System.Numerics;

namespace App2d.Noodle.Rigging;

internal sealed class RigDocument2D
{
    private int _nextBoneId = 1;
    private int _nextShapeId = 1;

    public List<RigBone2D> Bones { get; } = [];
    public List<RigShape2D> Shapes { get; } = [];

    public RigBone2D AddBone(RigBone2D? parent = null, string? name = null)
    {
        var bone = new RigBone2D(_nextBoneId++, name ?? $"bone-{_nextBoneId - 1}", parent);
        if (parent is not null)
            bone.LocalX = parent.Length;
        Bones.Add(bone);
        return bone;
    }

    public RigShape2D AddShape(string kind, RigBone2D attachedBone)
    {
        var id = _nextShapeId++;
        RigShape2D shape = kind switch
        {
            "Rectangle" => new RigRectangleShape2D(id, $"rectangle-{id}", attachedBone),
            "Circle" => new RigCircleShape2D(id, $"circle-{id}", attachedBone),
            "Capsule" => new RigCapsuleShape2D(id, $"capsule-{id}", attachedBone),
            "Polygon" => new RigPolygonShape2D(id, $"polygon-{id}", attachedBone),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        shape.LocalX = attachedBone.Length / 2f;
        Shapes.Add(shape);
        return shape;
    }

    public static Matrix3x2 GetWorldTransform(RigBone2D bone)
    {
        var local = Matrix3x2.CreateRotation(MathF.PI / 180f * bone.AngleDegrees) *
            Matrix3x2.CreateTranslation(bone.LocalX, bone.LocalY);
        return bone.Parent is null ? local : local * GetWorldTransform(bone.Parent);
    }

    public bool Remove(object item)
    {
        if (item is RigShape2D shape)
            return Shapes.Remove(shape);
        if (item is not RigBone2D bone)
            return false;
        if (Bones.Any(candidate => candidate.Parent == bone) || Shapes.Any(shape => shape.AttachedBone == bone))
            throw new InvalidOperationException("Delete or reattach this bone's children and shapes first.");
        return Bones.Remove(bone);
    }

    public static RigDocument2D CreateStarterPerson()
    {
        var document = new RigDocument2D();
        var hips = document.AddBone(name: "hips");
        hips.LocalX = -32f;
        hips.LocalY = -65f;
        hips.Length = 64f;

        var spine = document.AddBone(hips, "spine");
        spine.LocalX = 32f;
        spine.AngleDegrees = 90f;
        spine.Length = 135f;
        var head = document.AddBone(spine, "head");
        head.Length = 42f;

        var leftUpperArm = document.AddBone(spine, "left-upper-arm");
        leftUpperArm.LocalX = 108f;
        leftUpperArm.AngleDegrees = 145f;
        leftUpperArm.Length = 92f;
        var leftForearm = document.AddBone(leftUpperArm, "left-forearm");
        leftForearm.AngleDegrees = 24f;
        leftForearm.Length = 82f;

        var rightUpperArm = document.AddBone(spine, "right-upper-arm");
        rightUpperArm.LocalX = 108f;
        rightUpperArm.AngleDegrees = 35f;
        rightUpperArm.Length = 92f;
        var rightForearm = document.AddBone(rightUpperArm, "right-forearm");
        rightForearm.AngleDegrees = -24f;
        rightForearm.Length = 82f;

        var leftThigh = document.AddBone(hips, "left-thigh");
        leftThigh.LocalX = 17f;
        leftThigh.AngleDegrees = -100f;
        leftThigh.Length = 118f;
        var leftShin = document.AddBone(leftThigh, "left-shin");
        leftShin.AngleDegrees = 12f;
        leftShin.Length = 112f;

        var rightThigh = document.AddBone(hips, "right-thigh");
        rightThigh.LocalX = 47f;
        rightThigh.AngleDegrees = -80f;
        rightThigh.Length = 118f;
        var rightShin = document.AddBone(rightThigh, "right-shin");
        rightShin.AngleDegrees = -12f;
        rightShin.Length = 112f;

        AddCapsule(document, spine, 118f, 34f, 59f, "torso");
        var headShape = document.AddShape("Circle", head);
        headShape.Name = "head-shape";
        headShape.LocalX = 45f;
        ((RigCircleShape2D)headShape).Radius = 39f;
        foreach (var limb in new[] { leftUpperArm, rightUpperArm, leftForearm, rightForearm })
            AddCapsule(document, limb, limb.Length, 17f, limb.Length / 2f, $"{limb.Name}-shape");
        foreach (var limb in new[] { leftThigh, rightThigh, leftShin, rightShin })
            AddCapsule(document, limb, limb.Length, 21f, limb.Length / 2f, $"{limb.Name}-shape");
        return document;
    }

    private static void AddCapsule(
        RigDocument2D document,
        RigBone2D bone,
        float length,
        float radius,
        float localX,
        string name)
    {
        var shape = (RigCapsuleShape2D)document.AddShape("Capsule", bone);
        shape.Name = name;
        shape.Length = length;
        shape.Radius = radius;
        shape.LocalX = localX;
    }
}
