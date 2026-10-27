using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Geometry.Operations.Transforms;
using App2d.Core.Geometry.Shapes;
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
            model.Controls.Add(new ModelControl
            {
                Id = control,
                Parent = parent,
                Rest = Point(frame.Origin),
                RestAngle = frame.Angle,
                Length = frame.Length * UnitsPerPixel
            });
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

    private static PartGeometry Part(RigShape2D shape, string bone)
    {
        var part = new PartGeometry
        {
            Id = $"part-{shape.Id}",
            A = bone,
            Frame = bone,
            OffsetX = shape.LocalX * UnitsPerPixel,
            OffsetY = shape.LocalY * UnitsPerPixel,
            Angle = float.DegreesToRadians(shape.AngleDegrees),
            Material = new RenderMaterialDefinition2D
            {
                Fill = $"#{shape.Color.R:x2}{shape.Color.G:x2}{shape.Color.B:x2}",
                Outline = new() { Width = 0 }
            },
            Hidden = shape.Purpose == RigShapePurpose.Collision,
            ScaleX = 1,
            ScaleY = 1,
            Geometry = ShapeTransforms2D.Scaled((IConvexShape2D)shape.CreateGeometry(), UnitsPerPixel)
        };
        if (part.Geometry is RoundedRectangle2D rounded)
        {
            part.ScaleX = rounded.Size.X;
            part.ScaleY = rounded.Size.Y;
        }
        return part;
    }
}
