using App2d.Core.Mathematics;
using App2d.Core.Mathematics.Transforms;
using System.Numerics;

namespace App2d.Core.Characters.Geometry;

/// <summary>
/// The evaluated placement of something inside a character: a planar coordinate frame plus a relative drawing layer
/// (<see cref="CharacterLayer2D"/>: smaller is nearer the viewer). This is the one value controls, artwork, sockets and
/// editor handles place through. Composing local.Then(parent) applies the local frame first and adds both layers.
/// Rotation, scale or reflection in XY never changes a layer, and a plain <see cref="Matrix3x2"/> joins with a zero layer
/// offset. The default value collapses to the origin at layer 0; <see cref="Identity"/> is the placement that leaves
/// everything where it is. The layer is draw order, not the physical Z a 3D prop mesh carries; the prop bridge lifts a
/// placement into that space without its layer. Methods packing XY and layer into Vector3f are planar rig coordinates,
/// not spatial points; drawable contours expose physical positions and layer attributes separately.
/// </summary>
public readonly record struct LayeredFrame2D(Frame2D Frame, float Layer)
{
    /// <summary>Leaves every point and layer where it is.</summary>
    public static LayeredFrame2D Identity { get; } = new(Frame2D.Identity, 0);

    /// <summary>A matrix taking part in placement: its XY frame with no layer offset.</summary>
    public static LayeredFrame2D FromMatrix(Matrix3x2 matrix) => new(new(matrix), 0);

    /// <summary>A rotation about an origin whose Z is the layer.</summary>
    public static LayeredFrame2D FromRotation(float radians, Vector3f origin) => new(Frame2D.FromRotation(radians, origin.XY), origin.Z);

    /// <summary>The frame whose +Y is the given axis (its clockwise perpendicular is +X), at an origin whose Z is the layer. The axis is not normalized.</summary>
    public static LayeredFrame2D FromYAxis(Vector3f origin, Vector2f yAxis) => new(Frame2D.FromYAxis(origin.XY, yAxis), origin.Z);

    /// <summary>
    /// An attachment's own placement on its carrier: scale first, then rotation, then the offset in the carrier's XY, with
    /// a layer offset. This is the one way artwork, sockets and other attachments express offset-and-rotation.
    /// </summary>
    public static LayeredFrame2D Local(Vector2f scale, float radians, Vector2f offset, float layer) =>
        new(new(Affine2D.CreateMatrix(offset, radians, scale)), layer);

    public Vector3f Origin => new(Frame.Origin, Layer);
    public Vector2f XAxis => Frame.XAxis;
    public Vector2f YAxis => Frame.YAxis;

    /// <summary>Applies this placement first, then the parent: frames multiply, layers add.</summary>
    public LayeredFrame2D Then(LayeredFrame2D parent) => new(Frame.Then(parent.Frame), Layer + parent.Layer);

    /// <summary>The same axes at another origin, whose Z replaces the layer.</summary>
    public LayeredFrame2D WithOrigin(Vector3f origin) => new(Frame.WithOrigin(origin.XY), origin.Z);

    /// <summary>A local XY point placed on this placement's layer.</summary>
    public Vector3f At(Vector2f local) => new(Frame.TransformPoint(local), Layer);

    /// <summary>A local point whose Z is a further layer offset.</summary>
    public Vector3f At(Vector3f local) => new(Frame.TransformPoint(local.XY), Layer + local.Z);

    public Vector2f Direction(Vector2f local) => Frame.TransformDirection(local);

    /// <summary>The local XY of a point, ignoring layers. False when the frame is collapsed.</summary>
    public bool TryToLocal(Vector2f point, out Vector2f local) => Frame.TryToLocal(point, out local);

    /// <summary>The local point whose Z is the layer offset from this placement. False when the frame is collapsed.</summary>
    public bool TryToLocal(Vector3f point, out Vector3f local)
    {
        if (!Frame.TryToLocal(point.XY, out var xy))
        {
            local = default;
            return false;
        }

        local = new(xy, point.Z - Layer);
        return true;
    }

    public Vector3f ToLocal(Vector3f point) => TryToLocal(point, out var local)
        ? local : throw new InvalidOperationException("The placement's frame has no finite inverse.");
}
