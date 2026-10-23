using App2d.Core.Mathematics.Transforms;
using System.Numerics;

namespace App2d.Core.Characters.Geometry;

/// <summary>
/// The one bridge between planar character placement and real 3D prop geometry (migration plan section 3.3). A
/// <see cref="LayeredFrame2D"/> says where an attachment sits in the screen plane and at which drawing layer; a prop's
/// mesh, grip, tip and muzzle need a <see cref="Frame3D"/> whose Z is physical thickness. <see cref="Lift"/> makes that
/// frame from the placement and the clip's orient channel. A planar frame plus a layer cannot hold an arbitrary 3D
/// rotation, so nothing here goes the other way.
/// </summary>
public static class PropPlacement3D
{
    /// <summary>A planar placement as a 3D frame: the same XY axes and origin, the layer as Z, an unchanged Z axis.</summary>
    public static Frame3D Extend(LayeredFrame2D placement) =>
        new(placement.Origin, new(placement.XAxis, 0), new(placement.YAxis, 0), Vector3.UnitZ);

    /// <summary>
    /// Lifts a planar placement into the frame a prop's vertices use. The orientation tilts about local X, twists about
    /// local Y and turns further about Z (radians, applied in that order) before the placement carries the result, so
    /// a mirrored or scaled planar frame mirrors or scales the prop with it and the placement's layer is the frame's Z.
    /// </summary>
    public static Frame3D Lift(LayeredFrame2D placement, Vector3 orientation) =>
        orientation == Vector3.Zero ? Extend(placement) : Frame3D.FromRotation(orientation).Then(Extend(placement));
}
