using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry;

/// <summary>
/// A convex core with an optional round radius and a similarity pose. A core can be caller-owned vertices
/// or an analytic support mapping, so an ellipse needs no polygonal approximation.
/// </summary>
public readonly ref struct ConvexProxy2D
{
    private readonly ReadOnlySpan<Vector2> vertices;
    private readonly IConvexSupport2D? support;
    private readonly Similarity2D pose;

    /// <summary>Creates a world-space vertex core, optionally expanded by a nonnegative radius.</summary>
    public ConvexProxy2D(ReadOnlySpan<Vector2> vertices, float radius = 0f)
        : this(vertices, Similarity2D.Identity, radius) { }

    /// <summary>Creates a local vertex core and pose. Vertices need not have a particular winding.</summary>
    public ConvexProxy2D(ReadOnlySpan<Vector2> vertices, Similarity2D pose, float radius = 0f)
    {
        ArgGuard.ThrowIfTooShort(vertices, 1);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(pose.Scale);
        ArgGuard.ThrowIfNotFiniteOrNegative(radius);
        this.vertices = vertices;
        this.pose = pose;
        support = null;
        Radius = radius * pose.Scale;
    }

    /// <summary>Creates an analytic core and pose, optionally expanded by a local round radius.</summary>
    public ConvexProxy2D(IConvexSupport2D support, Similarity2D pose, float radius = 0f)
    {
        ArgGuard.ThrowIfNull(support);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(pose.Scale);
        ArgGuard.ThrowIfNotFiniteOrNegative(radius);
        this.support = support;
        this.pose = pose;
        vertices = default;
        Radius = radius * pose.Scale;
    }

    internal float Radius { get; }
    internal Vector2 Origin => pose.Translation;
    internal Vector2 ReferencePoint => vertices.IsEmpty ? pose.Translation : pose.TransformPoint(vertices[0]);
    internal int VertexCount => vertices.Length;
    internal Vector2 Vertex(int index, Vector2 origin) => pose.TransformDirection(vertices[index]) + (pose.Translation - origin);

    internal void Validate() => ArgGuard.ThrowIf(support is null && vertices.IsEmpty, "A convex proxy must have a vertex core or support mapping.");

    internal Vector2 Support(Vector2 worldDirection, Vector2 origin)
    {
        var localDirection = pose.TransposeTransformDirection(worldDirection);
        var point = support is null ? SupportPoint2D.Polygon(localDirection, vertices) : support.GetSupportPoint(localDirection);
        // Subtract translations before adding local geometry to retain precision far from the world origin.
        return pose.TransformDirection(point) + (pose.Translation - origin);
    }
}
