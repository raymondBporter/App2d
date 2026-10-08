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

    private static PuppetPart Part(RigShape2D shape, string bone)
    {
        var part = new PuppetPart
        {
            Id = $"part-{shape.Id}",
            A = bone,
            Frame = bone,
            OffsetX = shape.LocalX * UnitsPerPixel,
            OffsetY = shape.LocalY * UnitsPerPixel,
            Angle = MathF.PI / 180f * shape.AngleDegrees,
            Material = new RenderMaterialDefinition2D
            {
                Fill = $"#{shape.Color.R:x2}{shape.Color.G:x2}{shape.Color.B:x2}",
                Outline = new() { Width = 0 }
            },
            Hidden = shape.Purpose == RigShapePurpose.Collision,
            Width = 1,
            Height = 1,
            Geometry = ShapeDefinition2D.FromShape(WorldShape2D.Scaled((IConvexShape2D)shape.CreateGeometry(), UnitsPerPixel))
        };
        if (part.Geometry is RoundedRectangleShapeDefinition2D rounded)
        {
            part.Width = rounded.Max.X - rounded.Min.X;
            part.Height = rounded.Max.Y - rounded.Min.Y;
        }
        PartGeometry.RestoreEditorFields(part);
        return part;
    }
}
