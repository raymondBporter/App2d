using System.Numerics;
using App2d.Engine.Geometry;
using App2d.Engine.Mathematics;

namespace App2d.Engine.Collision.Contacts;

public static partial class ShapeCollision2D
{
    private static void WriteWorldRectangleVertices(Rectangle2D rectangle, Similarity2D pose, Span<Vector2> vertices)
    {
        rectangle.WriteCorners(vertices);
        for (var i = 0; i < vertices.Length; i++)
            vertices[i] = pose.TransformPoint(vertices[i]);
    }

    private static void AddAxis(Span<Vector2> axes, ref int axisCount, Vector2 candidate)
    {
        if (candidate.LengthSquared() > 0.000001f)
            axes[axisCount++] = candidate;
    }

    private static void ProjectCapsule(Vector2 start, Vector2 end, float radius, Vector2 axis, out float min, out float max)
    {
        var startProjection = Vector2.Dot(start, axis);
        var endProjection = Vector2.Dot(end, axis);
        min = Math.Min(startProjection, endProjection) - radius;
        max = Math.Max(startProjection, endProjection) + radius;
    }
}
