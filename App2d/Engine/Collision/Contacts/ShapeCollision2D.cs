using App2d.Engine.Geometry;
using App2d.Engine.Mathematics;

namespace App2d.Engine.Collision.Contacts;

public static partial class ShapeCollision2D
{
    public static bool TryGetContact(WorldObject2D first, WorldObject2D second, out CollisionContact2D contact)
    {
        var result = Dispatch(first.Shape, first.Transform, second.Shape, second.Transform);
        if (result.HasContact)
        {
            contact = result.Contact;
            return true;
        }

        // This makes every implemented row callable in either argument order.
        result = Dispatch(second.Shape, second.Transform, first.Shape, first.Transform);
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
    private static CollisionResult Dispatch(IShape2D first, Transform2D firstTransform, IShape2D second, Transform2D secondTransform) =>
        first switch
        {
            Circle2D circle => CircleAgainst(circle, firstTransform, second, secondTransform),
            Capsule2D capsule => CapsuleAgainst(capsule, firstTransform, second, secondTransform),
            Rectangle2D rectangle => RectangleAgainst(rectangle, firstTransform, second, secondTransform),
            _ => CollisionResult.None
        };

    private static CollisionResult CircleAgainst(        Circle2D circle,        Transform2D circleTransform,        IShape2D other,        Transform2D otherTransform) =>
        other switch
        {
            Circle2D otherCircle => CircleVsCircle(circle, circleTransform, otherCircle, otherTransform),
            ConvexPolygon2D polygon => CircleVsPolygon(circle, circleTransform, polygon.Vertices, otherTransform),
            Rectangle2D rectangle => CircleVsRectangle(circle, circleTransform, rectangle, otherTransform),
            Capsule2D capsule => CircleVsCapsule(circle, circleTransform, capsule, otherTransform),
            HalfSpace2D halfSpace => CircleVsHalfSpace(circle, circleTransform, halfSpace, otherTransform),
            _ => CollisionResult.None
        };

    private static CollisionResult CapsuleAgainst(Capsule2D capsule, Transform2D capsuleTransform, IShape2D other, Transform2D otherTransform)
    {
        return other switch
        {
            Circle2D circle => CircleVsCapsule(circle, otherTransform, capsule, capsuleTransform).Flipped(),
            Capsule2D otherCapsule => CapsuleVsCapsule(capsule, capsuleTransform, otherCapsule, otherTransform),
            Rectangle2D rectangle => RectangleVsCapsule(rectangle, otherTransform, capsule, capsuleTransform).Flipped(),
            _ => CollisionResult.None
        };
    }

    private static CollisionResult RectangleAgainst(Rectangle2D rectangle, Transform2D rectangleTransform, IShape2D other, Transform2D otherTransform)
    {
        return other switch
        {
            Circle2D circle => CircleVsRectangle(circle, otherTransform, rectangle, rectangleTransform).Flipped(),
            Capsule2D capsule => RectangleVsCapsule(rectangle, rectangleTransform, capsule, otherTransform),
            Rectangle2D otherRectangle => RectangleVsRectangle(rectangle, rectangleTransform, otherRectangle, otherTransform),
            _ => CollisionResult.None
        };
    }
}
