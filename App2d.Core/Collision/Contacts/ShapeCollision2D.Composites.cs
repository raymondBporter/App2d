using App2d.Core.Geometry.Operations.Bounds;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Shapes;

namespace App2d.Core.Collision.Contacts;

public static partial class ShapeCollision2D
{
    // Each convex part resolves independently and the deepest contact wins; the iterative physics loop
    // re-collects contacts, so one contact per pair per iteration is enough. Never use a union support map
    // here: that would collide against the composite's convex hull and fill its notches.
    private static CollisionResult CompositeAgainst(CompositeShape2D composite, Similarity2D compositePose, IShape2D other, Similarity2D otherPose)
    {
        if (composite.Parts.Length > 32)
            return IndexedCompositeAgainst(composite, compositePose, other, otherPose);
        var best = CollisionResult.None;
        var otherBounds = ShapeBounds2D.CalculateTransformed(other, otherPose.ToMatrix());
        foreach (var part in composite.Parts)
        {
            if (!ShapeBounds2D.CalculateTransformed(part, compositePose.ToMatrix()).Intersects(otherBounds)) continue;
            var result = Dispatch(part, compositePose, other, otherPose);
            if (result.HasContact && (!best.HasContact || result.Contact.PenetrationDepth > best.Contact.PenetrationDepth))
            {
                best = result;
            }
        }
        return best;
    }
}
