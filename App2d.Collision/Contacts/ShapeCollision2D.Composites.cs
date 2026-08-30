using App2d.Core.Geometry;
using App2d.Core.Mathematics;

namespace App2d.Collision.Contacts;

public static partial class ShapeCollision2D
{
    // Each convex part resolves independently and the deepest contact wins; the
    // iterative physics loop re-collects contacts, so one contact per pair per
    // iteration is enough. Never use a union support map here â€” that would
    // collide against the composite's convex hull and fill its notches.
    private static CollisionResult CompositeAgainst(CompositeShape2D composite, Similarity2D compositePose, IShape2D other, Similarity2D otherPose)
    {
        var best = CollisionResult.None;
        foreach (var part in composite.Parts)
        {
            var result = Dispatch(part, compositePose, other, otherPose);
            if (!result.HasContact)
                result = Dispatch(other, otherPose, part, compositePose).Flipped();
            if (result.HasContact && (!best.HasContact || result.Contact.PenetrationDepth > best.Contact.PenetrationDepth))
                best = result;
        }

        return best;
    }
}
