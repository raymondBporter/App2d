using App2d.Core;
using App2d.Core.Geometry;
using App2d.Core.Mathematics;

namespace App2d.Collision.Contacts;

public static partial class ShapeCollision2D
{
    public static bool TryGetContact(SpatialObject2D first, SpatialObject2D second, out CollisionContact2D contact)
    {
        var firstPose = first.CollisionPose;
        var secondPose = second.CollisionPose;
        var result = Dispatch(first.Shape, firstPose, second.Shape, secondPose);
        if (result.HasContact)
        {
            contact = result.Contact;
            return true;
        }

        // This makes every implemented row callable in either argument order.
        result = Dispatch(second.Shape, secondPose, first.Shape, firstPose);
        if (result.HasContact)
        {
            contact = result.Contact.Flipped();
            return true;
        }

        contact = default;
        return false;
    }

    private readonly record struct CollisionResult(bool HasContact, CollisionContact2D Contact)
    {
        public static CollisionResult None => default;
        public static CollisionResult From(CollisionContact2D contact) => new(true, contact);
        public CollisionResult Flipped() => HasContact ? From(Contact.Flipped()) : None;
    }

    // One switch chooses the implemented row; the row switch chooses its pair.
    // Rectangle2D intentionally also catches the AxisAlignedRectangle2D subtype.
    private static CollisionResult Dispatch(IShape2D first, Similarity2D firstPose, IShape2D second, Similarity2D secondPose) =>
        first switch
        {
            CompositeShape2D composite => CompositeAgainst(composite, firstPose, second, secondPose),
            Circle2D circle => CircleAgainst(circle, firstPose, second, secondPose),
            Capsule2D capsule => CapsuleAgainst(capsule, firstPose, second, secondPose),
            Rectangle2D rectangle => RectangleAgainst(rectangle, firstPose, second, secondPose),
            HalfSpace2D halfSpace => HalfSpaceAgainst(halfSpace, firstPose, second, secondPose),
            _ => CollisionResult.None
        };

    private static CollisionResult CircleAgainst(Circle2D circle, Similarity2D circlePose, IShape2D other, Similarity2D otherPose) =>
        other switch
        {
            Circle2D otherCircle => CircleVsCircle(circle, circlePose, otherCircle, otherPose),
            ConvexPolygon2D polygon => CircleVsPolygon(circle, circlePose, polygon.Vertices, otherPose),
            Rectangle2D rectangle => CircleVsRectangle(circle, circlePose, rectangle, otherPose),
            Capsule2D capsule => CircleVsCapsule(circle, circlePose, capsule, otherPose),
            HalfSpace2D halfSpace => CircleVsHalfSpace(circle, circlePose, halfSpace, otherPose),
            _ => CollisionResult.None
        };

    private static CollisionResult CapsuleAgainst(Capsule2D capsule, Similarity2D capsulePose, IShape2D other, Similarity2D otherPose)
    {
        return other switch
        {
            Circle2D circle => CircleVsCapsule(circle, otherPose, capsule, capsulePose).Flipped(),
            Capsule2D otherCapsule => CapsuleVsCapsule(capsule, capsulePose, otherCapsule, otherPose),
            Rectangle2D rectangle => RectangleVsCapsule(rectangle, otherPose, capsule, capsulePose).Flipped(),
            _ => CollisionResult.None
        };
    }

    private static CollisionResult RectangleAgainst(Rectangle2D rectangle, Similarity2D rectanglePose, IShape2D other, Similarity2D otherPose)
    {
        return other switch
        {
            Circle2D circle => CircleVsRectangle(circle, otherPose, rectangle, rectanglePose).Flipped(),
            Capsule2D capsule => RectangleVsCapsule(rectangle, rectanglePose, capsule, otherPose),
            Rectangle2D otherRectangle => RectangleVsRectangle(rectangle, rectanglePose, otherRectangle, otherPose),
            _ => CollisionResult.None
        };
    }
}
