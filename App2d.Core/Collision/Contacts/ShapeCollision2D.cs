using App2d.Core.Geometry.Operations.Convex;
using App2d.Core.Geometry.Operations.Distance;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Shapes;
using App2d.Core.Spatial;
using App2d.Core.Validation;

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
        => TryGetContact(first.Shape, first.CollisionPose, second.Shape, second.CollisionPose, out contact);

    /// <summary>Finds the deepest penetrating contact directly from shapes and their placements, without registering objects.</summary>
    public static bool TryGetContact(IShape2D first, Similarity2D firstPose, IShape2D second, Similarity2D secondPose, out CollisionContact2D contact)
    {
        ArgGuard.ThrowIfNull(first);
        ArgGuard.ThrowIfNull(second);
        var result = Dispatch(first, firstPose, second, secondPose);
        contact = result.Contact;
        return result.HasContact;
    }

    /// <summary>Tests filled shapes in a common space. Touching counts as overlap; no contact details or object wrappers are needed.</summary>
    public static bool Overlaps(IShape2D first, Similarity2D firstPose, IShape2D second, Similarity2D secondPose) =>
        ShapeDistance2D.Distance(first, firstPose, second, secondPose) == 0f;

    private readonly record struct CollisionResult(bool HasContact, CollisionContact2D Contact)
    {
        public static CollisionResult None => default;
        public static CollisionResult From(CollisionContact2D contact) => new(true, contact);
        public CollisionResult Flipped() => HasContact ? From(Contact.Flipped()) : None;
    }

    private static CollisionResult Dispatch(IShape2D first, Similarity2D firstPose, IShape2D second, Similarity2D secondPose)
    {
        if (first is HeightFieldShape2D heightField)
            return HeightFieldAgainst(heightField, firstPose, second, secondPose);
        if (second is HeightFieldShape2D otherHeightField)
            return HeightFieldAgainst(otherHeightField, secondPose, first, firstPose).Flipped();

        // Query the actual perimeter for circles, rather than the internal decomposition seams.
        if (first is Circle2D circle && second is SimplePolygon2D circlePolygon)
            return CircleVsSimplePolygon(circle, firstPose, circlePolygon, secondPose);
        if (second is Circle2D otherCircle && first is SimplePolygon2D otherCirclePolygon)
            return CircleVsSimplePolygon(otherCircle, secondPose, otherCirclePolygon, firstPose).Flipped();

        if (first is SimplePolygon2D polygon)
        {
            return CompositeAgainst(polygon.ConvexPieces, firstPose, second, secondPose);
        }

        if (second is SimplePolygon2D otherPolygon)
        {
            return CompositeAgainst(otherPolygon.ConvexPieces, secondPose, first, firstPose).Flipped();
        }

        if (first is CompositeShape2D composite)
        {
            return CompositeAgainst(composite, firstPose, second, secondPose);
        }

        if (second is CompositeShape2D otherComposite)
        {
            return CompositeAgainst(otherComposite, secondPose, first, firstPose).Flipped();
        }

        if (first is HalfSpace2D firstPlane && second is IConvexShape2D secondConvex)
        {
            return ConvexVsHalfSpace(secondConvex, secondPose, firstPlane, firstPose).Flipped();
        }

        if (second is HalfSpace2D secondPlane && first is IConvexShape2D firstConvex)
        {
            return ConvexVsHalfSpace(firstConvex, firstPose, secondPlane, secondPose);
        }

        if (first is not IConvexShape2D convexFirst || second is not IConvexShape2D convexSecond)
        {
            return CollisionResult.None;
        }

        var query = ShapeConvexQuery2D.Query(convexFirst, firstPose, convexSecond, secondPose);
        return query.SignedDistance < 0f
            ? CollisionResult.From(new CollisionContact2D(query.PointOnSecond, query.Normal, -query.SignedDistance))
            : CollisionResult.None;
    }

    private static CollisionResult HeightFieldAgainst(HeightFieldShape2D field, Similarity2D fieldPose, IShape2D other, Similarity2D otherPose) =>
        other is Circle2D circle
            ? CircleVsSimplePolygon(circle, otherPose, field.Polygon, fieldPose).Flipped()
            : CompositeAgainst(field.ConvexPieces, fieldPose, other, otherPose);
}
