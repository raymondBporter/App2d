using App2d.Core.Characters.Authored;
using App2d.Core.Mathematics;
using App2d.Core.Mathematics.Transforms;
using System.Numerics;

namespace App2d.Core.Characters.Geometry;

/// <summary>
/// The one bridge between planar character placement and real 3D prop geometry (migration plan section 3.3). A
/// <see cref="LayeredFrame2D"/> says where an attachment sits in the screen plane and at which drawing layer; a prop's
/// mesh, grip, tip and muzzle need a <see cref="Frame3D"/> whose Z is physical thickness. <see cref="Lift"/> makes that
/// frame from the placement and the clip's orient channel; <see cref="ProjectAxis"/> is the explicit way gameplay comes
/// back to XY. A planar frame plus a layer cannot hold an arbitrary 3D rotation, so nothing here goes the other way.
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

    /// <summary>
    /// A lifted frame's X axis back in gameplay XY: the unit projection, or the facing when the axis points straight
    /// at or away from the viewer. Foreshortening never changes a projectile's speed or a region's axis length.
    /// </summary>
    public static Vector2 ProjectAxis(Frame3D frame, int facing)
    {
        var projected = frame.XAxis.XY;
        return projected.LengthSquared() > 1e-8f ? Vector2.Normalize(projected) : new Vector2(facing, 0);
    }

    /// <summary>A prop's local point in the world, with its grip on the frame's origin. The one transform art, hits and muzzles share.</summary>
    public static Vector3 PropPoint(Frame3D frame, PropAsset prop, PuppetPoint local) =>
        frame.TransformPoint((local.XYZ - prop.Grip.XYZ) * prop.Scale);

    /// <summary>
    /// The screen-plane frame of a prop's local XY axes at one of its points: the projection editor drags invert to move
    /// that point. Its determinant is near zero when the prop is seen edge-on.
    /// </summary>
    public static Frame2D PropPlane(Frame3D frame, PropAsset prop, PuppetPoint at) =>
        new(PropPoint(frame, prop, at).XY, frame.XAxis.XY * prop.Scale, frame.YAxis.XY * prop.Scale);
}
