using App2d.Rendering;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Noodle.Rigging;

internal static class RigDocumentRenderer2D
{
    private static readonly XnaColor BoneColor = new(235, 239, 248, 220);
    private static readonly XnaColor SelectedColor = new(117, 255, 178);
    private static readonly XnaColor CollisionColor = new(255, 165, 72, 225);

    public static void Render(Renderer2D renderer, RigDocument2D document, object? selection)
    {
        foreach (var shape in document.Shapes)
            DrawShape(renderer, document, shape, ReferenceEquals(shape, selection));
        foreach (var bone in document.Bones)
            DrawBone(renderer, document, bone, ReferenceEquals(bone, selection));
    }

    private static void DrawShape(
        Renderer2D renderer,
        RigDocument2D document,
        RigShape2D shape,
        bool isSelected)
    {
        var geometry = shape.CreateGeometry();
        var drawingColor = shape.Color;
        var color = XnaColor.FromNonPremultiplied(
            drawingColor.R, drawingColor.G, drawingColor.B, drawingColor.A);
        if (shape.Purpose == RigShapePurpose.Collision)
            color = new XnaColor(CollisionColor.R, CollisionColor.G, CollisionColor.B, (byte)60);

        var item = new WorldObject2D(geometry, new SolidColorShader(color));
        var boneTransform = RigDocument2D.GetWorldTransform(shape.AttachedBone);
        item.Transform.Position = Vector2.Transform(new Vector2(shape.LocalX, shape.LocalY), boneTransform);
        item.Transform.Rotation = MathF.Atan2(boneTransform.M12, boneTransform.M11) +
            MathF.PI / 180f * shape.AngleDegrees;
        renderer.Draw(item);

        if (shape.Purpose != RigShapePurpose.Visual)
            renderer.DrawShapeOutline(item, CollisionColor, 2f);
        if (isSelected)
            renderer.DrawShapeOutline(item, SelectedColor, 4f);
    }

    private static void DrawBone(
        Renderer2D renderer,
        RigDocument2D document,
        RigBone2D bone,
        bool isSelected)
    {
        var transform = RigDocument2D.GetWorldTransform(bone);
        var start = Vector2.Transform(Vector2.Zero, transform);
        var end = Vector2.Transform(new Vector2(bone.Length, 0f), transform);
        Span<Vector2> line = [start, end];
        var color = isSelected ? SelectedColor : BoneColor;
        renderer.DrawWorldPolyline(line, color, isSelected ? 7f : 4f);
        renderer.DrawWorldCircle(start, isSelected ? 9f : 6f, color, isSelected ? 4f : 3f);
        renderer.DrawWorldCircle(end, 4f, color, 2f);
    }
}
