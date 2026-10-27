using App2d.Core.Characters.Authored;
using App2d.Core.Geometry.Curves;
using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Core.Characters.Geometry;

/// <summary>
/// Local geometry placement shared by character drawing, picking and editing. A part is one <see cref="LayeredFrame2D"/>
/// attachment (scale, rotation, offset, depth) composed onto the carrier its controls define; nothing here composes
/// offsets or rotations by hand.
/// </summary>
public static class PartAttachment2D
{
    /// <summary>The part's own placement on its carrier: scale, then rotation, then offset in the carrier's XY, then depth as a layer offset.</summary>
    public static LayeredFrame2D Attachment(PartGeometry part) =>
        LayeredFrame2D.Local(new(part.ScaleX, part.ScaleY), part.Angle, new(part.OffsetX, part.OffsetY), part.Depth);

    /// <summary>
    /// The carrier a part attaches to: its bone's frame placed at control A, or the frame whose +Y runs from A toward B
    /// (unit length for shapes, the full A to B span for curves, screen up without B or when A and B coincide).
    /// </summary>
    public static LayeredFrame2D Carrier(PartGeometry part, Func<string, Vector3> world, Func<string, LayeredFrame2D>? bone = null)
    {
        var origin = world(part.A);
        var direction = part.B is { } end ? (world(end) - origin).XY : Vector2.Zero;
        if (part.Frame is { } frame && (part.B is null || direction.LengthSquared() <= 1e-10f))
        {
            return bone is null
                ? throw new InvalidOperationException("A bone-attached part needs bone placements.")
                : bone(frame).WithOrigin(origin);
        }

        if (part.B is null) direction = Vector2.UnitY;
        var up = part.Geometry is ICurve2D ? direction
            : direction.LengthSquared() > 1e-10f ? Vector2.Normalize(direction) : Vector2.UnitY;
        return LayeredFrame2D.FromYAxis(origin, up);
    }

    /// <summary>Where a part's geometry lands: its attachment on its carrier.</summary>
    public static LayeredFrame2D FrameOf(PartGeometry part, Func<string, Vector3> world, Func<string, LayeredFrame2D>? bone = null, Func<string, Vector3>? rest = null)
    {
        var stretch = 1f;
        if (part.Stretch && part.B is { } toward && rest is not null)
        {
            var span = Vector2.Distance(rest(part.A).XY, rest(toward).XY);
            if (span > 1e-6f) stretch = Vector2.Distance(world(part.A).XY, world(toward).XY) / span;
        }
        return LayeredFrame2D.Local(new(part.ScaleX * stretch, part.ScaleY * stretch), part.Angle,
            new(part.OffsetX + part.Along, part.OffsetY), part.Depth).Then(Carrier(part, world, bone));
    }

    public static LayeredFrame2D FrameOf(PartGeometry part, ResolvedModel model, EvaluatedPose pose) => FrameOf(part, pose.World, pose.Placement, id => model.Rest[id]);

    internal static LayeredFrame2D BindingFrame(PartPointBinding binding, Func<string, Vector3> world)
    {
        var origin = world(binding.Control);
        var direction = binding.Toward is { } toward ? world(toward) - origin : Vector3.UnitY;
        var up = new Vector2(direction.X, direction.Y);
        up = up.LengthSquared() > 1e-10f ? Vector2.Normalize(up) : Vector2.UnitY;
        return LayeredFrame2D.FromYAxis(origin, up);
    }

    /// <summary>A drawn rest point carried by its bound control, keeping its layer offset from that control.</summary>
    internal static Vector3 BoundPoint(PartPointBinding binding, Vector3 drawnRest, Func<string, Vector3> world, Func<string, Vector3> rest)
    {
        var before = BindingFrame(binding, rest);
        var now = BindingFrame(binding, world);
        return now.At(before.ToLocal(drawnRest));
    }

    /// <summary>Inverse of the same attachment used by rendering, for dragging a point while previewing a pose.</summary>
    public static Vector2 ArtworkPoint(PartGeometry part, int index, Vector2 target, Func<string, Vector3> world, Func<string, Vector3> rest, Func<string, LayeredFrame2D>? bone = null, Func<string, LayeredFrame2D>? restBone = null)
    {
        var binding = part.Bindings?.FirstOrDefault(b => b.Point == index);
        var art = binding is null ? FrameOf(part, world, bone) : FrameOf(part, rest, restBone);
        if (binding is not null)
        {
            var now = BindingFrame(binding, world);
            var before = BindingFrame(binding, rest);
            target = before.At(now.Frame.ToLocal(target)).XY;
        }
        if (!art.TryToLocal(target, out var local))
        {
            throw new InvalidOperationException("Cannot edit a cutout through a collapsed frame.");
        }

        return local;
    }

    public static Vector2 ArtworkPoint(PartGeometry part, int index, Vector2 target, ResolvedModel model, EvaluatedPose pose) =>
        ArtworkPoint(part, index, target, pose.World, id => model.Rest[id], pose.Placement, model.RestPlacement);
}
