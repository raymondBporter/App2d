using App2d.Core.Mathematics;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Core.Characters;

/// <summary>Attachment frames and local shape scaling shared by character drawing, picking and editing.</summary>
public static class PartAttachment2D
{


    internal static Vector2 ShapeScale(PuppetPart part, IShape2D shape) => shape is RoundedRectangle2D rounded
        ? new(part.Width / (rounded.Max.X - rounded.Min.X), part.Height / (rounded.Max.Y - rounded.Min.Y))
        : new(part.Width, part.Height);

    public static PartFrame2D FrameOf(PuppetPart part, Func<string, Vector3> world, Func<string, float>? angle = null, Func<string, Matrix3x2>? transform = null)
    {
        if (part.Frame is { } bone)
        {
            if (transform is not null)
            {
                var matrix = Matrix3x2.CreateScale(part.ScaleX, part.ScaleY) * Matrix3x2.CreateRotation(part.Angle) * transform(bone);
                var offset = Vector2.TransformNormal(new(part.OffsetX, part.OffsetY), transform(bone));
                var transformedOrigin = world(part.A) + new Vector3(offset, 0);
                return new(new Frame2D(matrix).WithOrigin(transformedOrigin.XY), transformedOrigin.Z, part.Depth);
            }
            var baseAngle = angle is null ? throw new InvalidOperationException("A bone-attached part needs bone angles.") : angle(bone);
            var origin = world(part.A);
            var parent = Frame2D.FromRotation(baseAngle, origin.XY);
            var attachment = Frame2D.FromRotation(baseAngle + part.Angle,
                parent.TransformPoint(new(part.OffsetX, part.OffsetY)));
            return new(attachment, origin.Z, part.Depth);
        }
        var a = world(part.A); var b = part.B is { } end ? world(end) : a + Vector3.UnitY;
        var direction = new Vector2(b.X - a.X, b.Y - a.Y);
        var up = direction.LengthSquared() > 1e-10f ? Vector2.Normalize(direction) : Vector2.UnitY;
        var frame = Frame2D.FromYAxis(a.XY, up);
        return new(frame.WithOrigin(frame.TransformPoint(new(part.OffsetX, part.OffsetY))), a.Z, part.Depth);
    }

    internal static PartFrame2D BindingFrame(PartPointBinding binding, Func<string, Vector3> world)
    {
        var origin = world(binding.Control);
        var direction = binding.Toward is { } toward ? world(toward) - origin : Vector3.UnitY;
        var up = new Vector2(direction.X, direction.Y);
        up = up.LengthSquared() > 1e-10f ? Vector2.Normalize(up) : Vector2.UnitY;
        return new(Frame2D.FromYAxis(origin.XY, up), origin.Z, 0);
    }
}
