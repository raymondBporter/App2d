using App2d.Core.Mathematics;
using App2d.Core.Shapes;

namespace App2d.Core.Collision.Contacts;

/// <summary>
/// Contacts for finite convex shapes use the same support query as signed distances. Half-spaces have a direct
/// support-plane query; concave polygons and composites resolve independently over their convex parts.
/// </summary>
public static partial class ShapeCollision2D
{
    /// <summary>Finds the deepest contact between two placed objects.</summary>
    /// <param name="first">The first object; the contact normal moves it out of the second.</param>
    /// <param name="second">The second object.</param>
    /// <param name="contact">The deepest contact, with the normal pointing from the second object toward the first.</param>
    /// <returns>True when the objects penetrate; touching produces no contact.</returns>
    public static bool TryGetContact(SpatialObject2D first, SpatialObject2D second, out CollisionContact2D contact)
    {
        var result = Dispatch(first.Shape, first.CollisionPose, second.Shape, second.CollisionPose);
        contact = result.Contact;
        return result.HasContact;
    }

    private readonly record struct CollisionResult(bool HasContact, CollisionContact2D Contact)
    {
        public static CollisionResult None => default;
        public static CollisionResult From(CollisionContact2D contact) => new(true, contact);
        public CollisionResult Flipped() => HasContact ? From(Contact.Flipped()) : None;
    }

    private static CollisionResult Dispatch(IShape2D first, Similarity2D firstPose, IShape2D second, Similarity2D secondPose)
    {
        if (first is SimplePolygon2D polygon) return CompositeAgainst(polygon.ConvexPieces, firstPose, second, secondPose);
        if (second is SimplePolygon2D otherPolygon) return CompositeAgainst(otherPolygon.ConvexPieces, secondPose, first, firstPose).Flipped();
        if (first is CompositeShape2D composite) return CompositeAgainst(composite, firstPose, second, secondPose);
        if (second is CompositeShape2D otherComposite) return CompositeAgainst(otherComposite, secondPose, first, firstPose).Flipped();
        if (first is HalfSpace2D firstPlane && second is IConvexShape2D secondConvex)
            return ConvexVsHalfSpace(secondConvex, secondPose, firstPlane, firstPose).Flipped();
        if (second is HalfSpace2D secondPlane && first is IConvexShape2D firstConvex)
            return ConvexVsHalfSpace(firstConvex, firstPose, secondPlane, secondPose);
        if (first is not IConvexShape2D convexFirst || second is not IConvexShape2D convexSecond) return CollisionResult.None;

        var query = ShapeConvexQuery2D.Query(convexFirst, firstPose, convexSecond, secondPose);
        return query.SignedDistance < 0f
            ? CollisionResult.From(new CollisionContact2D(query.PointOnSecond, query.Normal, -query.SignedDistance))
            : CollisionResult.None;
    }
}
