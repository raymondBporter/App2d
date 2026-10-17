using App2d.Core.Geometry.Curves;
using App2d.Core.Mathematics;
using App2d.Core.Mathematics.Transforms;
using System.Numerics;

namespace App2d.Core.Characters.Geometry;

/// <summary>Local geometry placement shared by character drawing, picking and editing.</summary>
public static class PartAttachment2D
{
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
            var attachment = new Frame2D(Matrix3x2.CreateScale(part.ScaleX, part.ScaleY)
                * Matrix3x2.CreateRotation(baseAngle + part.Angle))
                .WithOrigin(parent.TransformPoint(new(part.OffsetX, part.OffsetY)));
            return new(attachment, origin.Z, part.Depth);
        }
        var a = world(part.A);
        var b = part.B is { } end ? world(end) : a + Vector3.UnitY;
        var direction = new Vector2(b.X - a.X, b.Y - a.Y);
        var up = part.Geometry is ICurve2D ? direction
            : direction.LengthSquared() > 1e-10f ? Vector2.Normalize(direction) : Vector2.UnitY;
        var frame = Frame2D.FromYAxis(a.XY, up);
        var local = new Frame2D(Matrix3x2.CreateScale(part.ScaleX, part.ScaleY) * Matrix3x2.CreateRotation(part.Angle));
        var placed = local.Then(frame).WithOrigin(frame.TransformPoint(new(part.OffsetX, part.OffsetY)));
        return new(placed, a.Z, part.Depth);
    }

    internal static PartFrame2D BindingFrame(PartPointBinding binding, Func<string, Vector3> world)
    {
        var origin = world(binding.Control);
        var direction = binding.Toward is { } toward ? world(toward) - origin : Vector3.UnitY;
        var up = new Vector2(direction.X, direction.Y);
        up = up.LengthSquared() > 1e-10f ? Vector2.Normalize(up) : Vector2.UnitY;
        return new(Frame2D.FromYAxis(origin.XY, up), origin.Z, 0);
    }

    internal static Vector3 BoundPoint(PartPointBinding binding, Vector3 drawnRest, Func<string, Vector3> world, Func<string, Vector3> rest)
    {
        var before = BindingFrame(binding, rest);
        var now = BindingFrame(binding, world);
        var local = before.Coordinates.ToLocal(drawnRest.XY);
        return now.At(local) + new Vector3(0, 0, drawnRest.Z - before.OriginZ);
    }

    /// <summary>Inverse of the same attachment used by rendering, for dragging a point while previewing a pose.</summary>
    public static Vector2 ArtworkPoint(PuppetPart part, int index, Vector2 target, Func<string, Vector3> world, Func<string, Vector3> rest, Func<string, float>? angle = null, Func<string, Matrix3x2>? transform = null, Func<string, Matrix3x2>? restTransform = null)
    {
        var binding = part.Bindings?.FirstOrDefault(b => b.Point == index);
        var art = binding is null ? FrameOf(part, world, angle, transform) : FrameOf(part, rest, transform: restTransform);
        if (binding is not null)
        {
            var now = BindingFrame(binding, world);
            var before = BindingFrame(binding, rest);
            target = before.Coordinates.TransformPoint(now.Coordinates.ToLocal(target));
        }
        if (!art.Coordinates.TryToLocal(target, out var local))
        {
            throw new InvalidOperationException("Cannot edit a cutout through a collapsed frame.");
        }

        return local;
    }
}
