using App2d.Core.Geometry;
using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Collision.Contacts;

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

}
