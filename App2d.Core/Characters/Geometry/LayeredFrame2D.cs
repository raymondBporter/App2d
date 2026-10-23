using App2d.Core.Mathematics;
using App2d.Core.Mathematics.Transforms;
using System.Numerics;

namespace App2d.Core.Characters.Geometry;

/// <summary>
/// The evaluated placement of something inside a character: a planar coordinate frame plus a relative drawing layer
/// (<see cref="CharacterLayer2D"/>: smaller is nearer the viewer). This is the one value controls, artwork, sockets and
/// editor handles place through. It composes by multiplying the frames and adding the layers:
/// <c>combined.Frame = local.Frame * parent.Frame</c>, <c>combined.Layer = local.Layer + parent.Layer</c>.
/// Rotation, scale or reflection in XY never changes a layer, and a plain <see cref="Matrix3x2"/> joins with a zero layer
/// offset. The default value collapses to the origin at layer 0; <see cref="Identity"/> is the placement that leaves
/// everything where it is. The layer is draw order, not the physical Z a 3D prop mesh carries; the prop bridge lifts a
/// placement into that space.
/// </summary>
public readonly record struct LayeredFrame2D(Frame2D Frame, float Layer)
{
    /// <summary>Leaves every point and layer where it is.</summary>
    public static LayeredFrame2D Identity { get; } = new(Frame2D.Identity, 0);

    /// <summary>A matrix taking part in placement: its XY frame with no layer offset.</summary>
    public static LayeredFrame2D FromMatrix(Matrix3x2 matrix) => new(new(matrix), 0);

    /// <summary>A rotation about an origin whose Z is the layer.</summary>
    public static LayeredFrame2D FromRotation(float radians, Vector3 origin) => new(Frame2D.FromRotation(radians, origin.XY), origin.Z);

    /// <summary>The frame whose +Y is the given axis (its clockwise perpendicular is +X), at an origin whose Z is the layer. The axis is not normalized.</summary>
    public static LayeredFrame2D FromYAxis(Vector3 origin, Vector2 yAxis) => new(Frame2D.FromYAxis(origin.XY, yAxis), origin.Z);

    /// <summary>
    /// An attachment's own placement on its carrier: scale first, then rotation, then the offset in the carrier's XY, with
    /// a layer offset. This is the one way artwork, sockets and other attachments express offset-and-rotation.
    /// </summary>
    public static LayeredFrame2D Local(Vector2 scale, float radians, Vector2 offset, float layer) =>
        new(new(Matrix3x2.CreateScale(scale) * Matrix3x2.CreateRotation(radians) * Matrix3x2.CreateTranslation(offset)), layer);

    public Vector3 Origin => new(Frame.Origin, Layer);
    public Vector2 XAxis => Frame.XAxis;
    public Vector2 YAxis => Frame.YAxis;

    /// <summary>Applies this placement first, then the parent: frames multiply, layers add.</summary>
    public LayeredFrame2D Then(LayeredFrame2D parent) => new(Frame.Then(parent.Frame), Layer + parent.Layer);

    /// <summary>The same axes at another origin, whose Z replaces the layer.</summary>
    public LayeredFrame2D WithOrigin(Vector3 origin) => new(Frame.WithOrigin(origin.XY), origin.Z);

    /// <summary>A local XY point placed on this placement's layer.</summary>
    public Vector3 At(Vector2 local) => new(Frame.TransformPoint(local), Layer);

    /// <summary>A local point whose Z is a further layer offset.</summary>
    public Vector3 At(Vector3 local) => new(Frame.TransformPoint(local.XY), Layer + local.Z);

    public Vector2 Direction(Vector2 local) => Frame.TransformDirection(local);

    /// <summary>The local XY of a point, ignoring layers. False when the frame is collapsed.</summary>
    public bool TryToLocal(Vector2 point, out Vector2 local) => Frame.TryToLocal(point, out local);

    /// <summary>The local point whose Z is the layer offset from this placement. False when the frame is collapsed.</summary>
    public bool TryToLocal(Vector3 point, out Vector3 local)
    {
        if (!Frame.TryToLocal(point.XY, out var xy))
        {
            local = default;
            return false;
        }

        local = new(xy, point.Z - Layer);
        return true;
    }

    public Vector3 ToLocal(Vector3 point) => TryToLocal(point, out var local)
        ? local : throw new InvalidOperationException("The placement's frame has no finite inverse.");

    /// <summary>The placement that undoes this one: the inverse frame with the negated layer.</summary>
    public bool TryInverse(out LayeredFrame2D inverse)
    {
        if (!Frame.TryInverse(out var frame))
        {
            inverse = default;
            return false;
        }

        inverse = new(frame, -Layer);
        return true;
    }
}
