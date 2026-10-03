using App2d.Core.Mathematics;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Core.Collision.Contacts;

/// <summary>
/// Narrow-phase contact generation as one double-dispatch table. <c>Dispatch</c> picks the row for the first shape and
/// each row switches on the second shape. Circles, capsules, half-spaces and composites have dedicated rows; rectangles,
/// triangles, convex polygons and ellipses share the polygon row through <see cref="WorldShape2D.WritePerimeter"/>.
/// Ellipses are exact against circles and half-spaces and polygonized elsewhere. Composites resolve per part.
/// The contact math lives in the pair files; this file only routes. A new shape needs a row here and a
/// perimeter or dedicated pair functions.
/// </summary>
public static partial class ShapeCollision2D
{
    private const int StackVertexLimit = 64;

    /// <summary>Finds the contact between two placed objects, trying both argument orders so every row works either way.</summary>
    /// <param name="first">The first object; the contact normal moves it out of the second.</param>
    /// <param name="second">The second object.</param>
    /// <param name="contact">The deepest contact found, with the normal pointing from the second object toward the first.</param>
    /// <returns>True when the objects penetrate; touching produces no contact.</returns>
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

    // Rectangle2D rows intentionally also catch the AxisAlignedRectangle2D subtype through the perimeter writer.
    private static CollisionResult Dispatch(IShape2D first, Similarity2D firstPose, IShape2D second, Similarity2D secondPose)
    {
        if (first is SimplePolygon2D polygon) return CompositeAgainst(polygon.ConvexPieces, firstPose, second, secondPose);
        if (second is SimplePolygon2D otherPolygon)
            return CompositeAgainst(otherPolygon.ConvexPieces, secondPose, first, firstPose).Flipped();
        return first switch
        {
            CompositeShape2D composite => CompositeAgainst(composite, firstPose, second, secondPose),
            Circle2D circle => CircleAgainst(circle, firstPose, second, secondPose),
            Capsule2D capsule => CapsuleAgainst(capsule, firstPose, second, secondPose),
            HalfSpace2D halfSpace => HalfSpaceAgainst(halfSpace, firstPose, second, secondPose),
            IConvexShape2D convex when WorldShape2D.PerimeterVertexCount(convex) > 0 => PolygonAgainst(convex, firstPose, second, secondPose),
            _ => CollisionResult.None
        };
    }

    private static CollisionResult CircleAgainst(Circle2D circle, Similarity2D circlePose, IShape2D other, Similarity2D otherPose)
    {
        switch (other)
        {
            case Circle2D otherCircle: return CircleVsCircle(circle, circlePose, otherCircle, otherPose);
            case Capsule2D capsule: return CircleVsCapsule(circle, circlePose, capsule, otherPose);
            case HalfSpace2D halfSpace: return CircleVsHalfSpace(circle, circlePose, halfSpace, otherPose);
            case Ellipse2D ellipse: return CircleVsEllipse(circle, circlePose, ellipse, otherPose);
            default:
                var count = WorldShape2D.PerimeterVertexCount(other);
                if (count == 0) return CollisionResult.None;
                Span<Vector2> vertices = count <= StackVertexLimit ? stackalloc Vector2[count] : new Vector2[count];
                WorldShape2D.WritePerimeter(other, vertices);
                return CircleVsPolygon(circle, circlePose, vertices, otherPose);
        }
    }

    private static CollisionResult CapsuleAgainst(Capsule2D capsule, Similarity2D capsulePose, IShape2D other, Similarity2D otherPose)
    {
        switch (other)
        {
            case Circle2D circle: return CircleVsCapsule(circle, otherPose, capsule, capsulePose).Flipped();
            case Capsule2D otherCapsule: return CapsuleVsCapsule(capsule, capsulePose, otherCapsule, otherPose);
            case HalfSpace2D halfSpace: return ConvexVsHalfSpace(capsule, capsulePose, halfSpace, otherPose);
            default:
                var count = WorldShape2D.PerimeterVertexCount(other);
                if (count == 0) return CollisionResult.None;
                Span<Vector2> vertices = count <= StackVertexLimit ? stackalloc Vector2[count] : new Vector2[count];
                WorldShape2D.WritePerimeter(other, vertices);
                return PolygonVsCapsule(vertices, otherPose, capsule, capsulePose).Flipped();
        }
    }

    private static CollisionResult PolygonAgainst(IConvexShape2D shape, Similarity2D pose, IShape2D other, Similarity2D otherPose)
    {
        var count = WorldShape2D.PerimeterVertexCount(shape);
        Span<Vector2> vertices = count <= StackVertexLimit ? stackalloc Vector2[count] : new Vector2[count];
        WorldShape2D.WritePerimeter(shape, vertices);
        switch (other)
        {
            case Circle2D circle when shape is Ellipse2D ellipse: return CircleVsEllipse(circle, otherPose, ellipse, pose).Flipped();
            case Circle2D circle: return CircleVsPolygon(circle, otherPose, vertices, pose).Flipped();
            case Capsule2D capsule: return PolygonVsCapsule(vertices, pose, capsule, otherPose);
            case HalfSpace2D halfSpace: return ConvexVsHalfSpace(shape, pose, halfSpace, otherPose);
            default:
                var otherCount = WorldShape2D.PerimeterVertexCount(other);
                if (otherCount == 0) return CollisionResult.None;
                Span<Vector2> otherVertices = otherCount <= StackVertexLimit ? stackalloc Vector2[otherCount] : new Vector2[otherCount];
                WorldShape2D.WritePerimeter(other, otherVertices);
                return PolygonVsPolygon(vertices, pose, otherVertices, otherPose);
        }
    }
}
