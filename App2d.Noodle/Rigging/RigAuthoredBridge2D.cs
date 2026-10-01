using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Noodle.Rigging;

/// <summary>
/// Exports the bone editor's current rest pose to the authored runtime model. Bone origins remain
/// animatable controls; helper controls carry each bone's tip and each shape's local attachment frame.
/// </summary>
internal static class RigAuthoredBridge2D
{
    public const float UnitsPerPixel = .01f;

    public static string BoneControl(RigBone2D bone) => $"bone-{bone.Id}";
    public static string ShapeControl(RigShape2D shape) => $"shape-{shape.Id}";

    public static CharacterModel Export(RigDocument2D document, string id, string name)
    {
        var model = new CharacterModel { Id = id, Name = name };
        foreach (var bone in document.Bones)
        {
            var frame = RigDocument2D.GetWorldTransform(bone);
            var control = BoneControl(bone);
            var parent = bone.Parent is null ? null : BoneControl(bone.Parent);
            model.Controls.Add(new ModelControl { Id = control, Parent = parent, Rest = Point(Vector2.Transform(Vector2.Zero, frame)) });
            model.Controls.Add(new ModelControl
            {
                Id = $"{control}-tip", Parent = control,
                Rest = Point(Vector2.Transform(new Vector2(bone.Length, 0), frame))
            });
        }

        foreach (var shape in document.Shapes)
        {
            var frame = RigDocument2D.GetWorldTransform(shape.AttachedBone);
            var origin = Vector2.Transform(new Vector2(shape.LocalX, shape.LocalY), frame);
            var direction = Vector2.TransformNormal(Vector2.UnitY,
                Matrix3x2.CreateRotation(MathF.PI / 180f * shape.AngleDegrees) * frame);
            var control = ShapeControl(shape);
            var parent = BoneControl(shape.AttachedBone);
            model.Controls.Add(new ModelControl { Id = control, Parent = parent, Rest = Point(origin) });
            model.Controls.Add(new ModelControl { Id = $"{control}-up", Parent = control, Rest = Point(origin + direction) });
            model.Parts.Add(Part(shape, control));
        }

        model.Validate();
        return model;
    }

    private static PuppetPoint Point(Vector2 pixels) =>
        new(pixels.X * UnitsPerPixel, pixels.Y * UnitsPerPixel);

    private static PuppetPart Part(RigShape2D shape, string control)
    {
        var part = new PuppetPart
        {
            Id = $"part-{shape.Id}", A = control, B = $"{control}-up",
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
