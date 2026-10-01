using App2d.Core.Geometry;
using App2d.Core.Mathematics;
using App2d.Core.Shapes;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// A named convex hit volume derived from an evaluated pose: hurt, attack and movement regions. It is a local shape,
/// the pose that places it in the actor's XY space and an Id; overlap tests, bounds, scaling and outlines all come from
/// the shared Shapes layer. Regions are rebuilt every frame from the pose, so they never join the persistent collision system.
/// </summary>
/// <param name="Id">The hit window, hurt layout or role name the region stands for.</param>
/// <param name="Shape">The region in its own local space, for example a hit window's anchor frame.</param>
/// <param name="Pose">Where that local space sits in the actor's XY space.</param>
public sealed record EntityRegion(string Id, IConvexShape2D Shape, Similarity2D Pose)
{
    /// <summary>A region whose shape is already expressed in the actor's XY space.</summary>
    /// <param name="id">The region name.</param>
    /// <param name="shape">The shape in the actor's XY space.</param>
    public EntityRegion(string id, IConvexShape2D shape) : this(id, shape, Similarity2D.Identity) { }

    /// <summary>The axis-aligned box around the placed region.</summary>
    public Rect2D Bounds => ShapeBounds2D.Calculate(Shape).TransformedBy(Pose.ToMatrix());

    /// <summary>Tests two regions after translating each into a common space. Touching counts as overlap.</summary>
    /// <param name="other">The other region.</param>
    /// <param name="position">Where this region's actor origin sits in the common space.</param>
    /// <param name="otherPosition">Where the other region's actor origin sits in the common space.</param>
    /// <returns>True when the regions share at least a point.</returns>
    public bool Overlaps(EntityRegion other, Vector2 position = default, Vector2 otherPosition = default)
    {
        ArgGuard.ThrowIfNull(other);
        return ShapeDistance2D.SignedDistance(Shape, Pose.Translated(position), other.Shape, other.Pose.Translated(otherPosition)) <= 0f;
    }

    /// <summary>Tests the region against a placed physics shape without requesting contact details.</summary>
    /// <param name="other">The placed object, tested in world space.</param>
    /// <param name="position">Where this region's actor origin sits in world space.</param>
    /// <returns>True when the region and the object share at least a point.</returns>
    public bool Overlaps(SpatialObject2D other, Vector2 position = default)
    {
        ArgGuard.ThrowIfNull(other);
        ArgGuard.ThrowIfNotFinite(position);
        var pose = Pose.Translated(position);
        if (!ShapeBounds2D.Calculate(Shape).TransformedBy(pose.ToMatrix()).Intersects(other.WorldBounds)) return false;
        return ShapeDistance2D.Distance(Shape, pose, other.Shape, other.CollisionPose) == 0f;
    }

    /// <summary>The same region scaled uniformly about the actor origin, for example from authored units into world units.</summary>
    /// <param name="scale">The finite, positive scale factor.</param>
    /// <returns>A new region with a scaled pose.</returns>
    public EntityRegion Scaled(float scale) => this with { Pose = Pose.ScaledBy(scale) };

    /// <summary>A drawable perimeter of the placed region in the actor's XY space.</summary>
    /// <param name="roundSegments">Samples around a circle; a capsule uses half per cap.</param>
    /// <returns>The outline vertices in perimeter order.</returns>
    public Vector2[] Outline(int roundSegments = 24)
    {
        var vertices = new Vector2[WorldShape2D.OutlineVertexCount(Shape, roundSegments)];
        WorldShape2D.WriteOutline(Shape, vertices, roundSegments);
        for (var i = 0; i < vertices.Length; i++) vertices[i] = Pose.TransformPoint(vertices[i]);
        return vertices;
    }

    /// <summary>A placed object carrying this region's shape and pose, for queries that want a spatial object.</summary>
    /// <returns>A new spatial object whose transform reproduces <see cref="Pose"/>.</returns>
    public SpatialObject2D ToSpatialObject()
    {
        var placed = new SpatialObject2D(Shape);
        Pose.ToTransformState().Apply(placed.Transform);
        return placed;
    }

    /// <summary>An axis-aligned box region in the actor's XY space.</summary>
    /// <param name="id">The region name.</param>
    /// <param name="center">The box center.</param>
    /// <param name="size">The finite, positive width and height.</param>
    /// <returns>A rectangle region.</returns>
    public static EntityRegion Box(string id, Vector2 center, Vector2 size) => new(id, Rectangle2D.FromSize(size, center));

    /// <summary>A circular region in the actor's XY space.</summary>
    /// <param name="id">The region name.</param>
    /// <param name="center">The circle center.</param>
    /// <param name="radius">The finite, positive radius.</param>
    /// <returns>A circle region.</returns>
    public static EntityRegion Circle(string id, Vector2 center, float radius) => new(id, new Circle2D(radius, center));

    /// <summary>A capsule region in the actor's XY space; the endpoints may coincide.</summary>
    /// <param name="id">The region name.</param>
    /// <param name="start">One spine endpoint.</param>
    /// <param name="end">The other spine endpoint.</param>
    /// <param name="radius">The finite, positive radius.</param>
    /// <returns>A capsule region.</returns>
    public static EntityRegion Capsule(string id, Vector2 start, Vector2 end, float radius) => new(id, new Capsule2D(start, end, radius));
}
