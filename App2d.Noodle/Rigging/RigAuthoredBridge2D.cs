using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Noodle.Rigging;

/// <summary>
/// Exports the bone editor's current rest pose to the authored runtime model.
/// Each Noodle bone becomes one authored bone control; parts use that bone's local frame.
/// </summary>
internal static class RigAuthoredBridge2D
{
    public const float UnitsPerPixel = .01f;

    public static string BoneControl(RigBone2D bone) => $"bone-{bone.Id}";

    public static CharacterModel Export(RigDocument2D document, string id, string name)
    {
        var model = new CharacterModel { Id = id, Name = name };
        foreach (var bone in document.Bones)
        {
            var frame = BoneFrame2D.FromTransform(RigDocument2D.GetWorldTransform(bone), bone.Length);
            var control = BoneControl(bone);
            var parent = bone.Parent is null ? null : BoneControl(bone.Parent);
            model.Controls.Add(new ModelControl { Id = control, Parent = parent, Rest = Point(frame.Origin),
                RestAngle = frame.Angle, Length = frame.Length * UnitsPerPixel });
        }

        foreach (var shape in document.Shapes)
        {
            model.Parts.Add(Part(shape, BoneControl(shape.AttachedBone)));
        }

        model.Validate();
        return model;
    }

    private static PuppetPoint Point(Vector2 pixels) =>
        new(pixels.X * UnitsPerPixel, pixels.Y * UnitsPerPixel);

    private static PuppetPart Part(RigShape2D shape, string bone)
    {
        var part = new PuppetPart
        {
            Id = $"part-{shape.Id}", A = bone, Frame = bone,
            OffsetX = shape.LocalX * UnitsPerPixel, OffsetY = shape.LocalY * UnitsPerPixel,
            Angle = MathF.PI / 180f * shape.AngleDegrees,
            Fill = $"#{shape.Color.R:x2}{shape.Color.G:x2}{shape.Color.B:x2}",
            OutlineWidth = 0,
            Hidden = shape.Purpose == RigShapePurpose.Collision
        };
        switch (shape)
        {
            case RigCircleShape2D circle:
                part.Kind = PuppetPartKinds.Ellipse;
                part.Width = part.Height = 2 * circle.Radius * UnitsPerPixel;
                break;
            case RigRectangleShape2D rectangle:
                part.Kind = PuppetPartKinds.Box;
                part.Width = rectangle.Width * UnitsPerPixel;
                part.Height = rectangle.Height * UnitsPerPixel;
                part.Roundness = 0;
                break;
            case RigCapsuleShape2D capsule:
                part.Kind = PuppetPartKinds.Box;
                part.Width = (capsule.Length + 2 * capsule.Radius) * UnitsPerPixel;
                part.Height = 2 * capsule.Radius * UnitsPerPixel;
                part.Roundness = 1;
                break;
            case RigPolygonShape2D polygon:
                part.Kind = PuppetPartKinds.Polygon;
                part.Width = part.Height = 1;
                part.Points = [.. ((ConvexPolygon2D)polygon.CreateGeometry()).Vertices.ToArray()
                    .Select(vertex => Point(vertex))];
                break;
            default:
                throw new NotSupportedException($"Cannot export rig shape '{shape.Kind}'.");
        }
        return part;
    }
}
