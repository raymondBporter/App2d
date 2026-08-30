using System.Numerics;
using App2d.Engine.Geometry;
using App2d.Engine.Mathematics;

namespace App2d.Engine.Collision.Contacts;

public static partial class ShapeCollision2D
{
    private static void WriteWorldRectangleVertices(Rectangle2D rectangle, Transform2D transform, Span<Vector2> vertices)
    {
        rectangle.WriteCorners(vertices);
        var localToWorld = transform.LocalToWorldMatrix;
        for (var i = 0; i < vertices.Length; i++)
            vertices[i] = Vector2.Transform(vertices[i], localToWorld);
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

    private static void WriteCircleBoundary(Circle2D circle, Span<Vector2> vertices)
    {
        var angleStep = MathF.Tau / vertices.Length;
        for (var i = 0; i < vertices.Length; i++)
        {
            var angle = i * angleStep;
            vertices[i] = circle.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * circle.Radius;
        }
    }
}
