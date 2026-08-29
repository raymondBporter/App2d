using System.Numerics;
using App2d.Engine.Geometry;
using App2d.Engine.Mathematics;

namespace App2d.Engine.Collision;

internal static class CollisionMath2D
{
    public static bool TryGetWorldCircle(Circle2D circle, Transform2D transform, out Vector2 center, out float radius)
    {
        var localToWorld = transform.LocalToWorldMatrix;
        center = Vector2.Transform(circle.Center, localToWorld);
        var scaleX = new Vector2(localToWorld.M11, localToWorld.M12).Length();
        var scaleY = new Vector2(localToWorld.M21, localToWorld.M22).Length();
        var largestScale = Math.Max(scaleX, scaleY);

        if (largestScale <= float.Epsilon ||
            MathF.Abs(scaleX - scaleY) > largestScale * 0.001f)
        {
            radius = 0f;
            return false;
        }

        radius = circle.Radius * (scaleX + scaleY) / 2f;
        return true;
    }

    public static bool TryGetUniformScale(Transform2D transform, out float scale)
    {
        var matrix = transform.LocalToWorldMatrix;
        var scaleX = new Vector2(matrix.M11, matrix.M12).Length();
        var scaleY = new Vector2(matrix.M21, matrix.M22).Length();
        var largestScale = Math.Max(scaleX, scaleY);
        scale = (scaleX + scaleY) / 2f;
        return largestScale > float.Epsilon &&
               MathF.Abs(scaleX - scaleY) <= largestScale * 0.001f;
    }

    public static bool TryGetWorldCapsule(Capsule2D capsule, Transform2D transform, out Vector2 start, out Vector2 end, out float radius)
    {
        var localToWorld = transform.LocalToWorldMatrix;
        start = Vector2.Transform(capsule.Start, localToWorld);
        end = Vector2.Transform(capsule.End, localToWorld);
        if (!TryGetUniformScale(transform, out var scale))
        {
            radius = 0f;
            return false;
        }

        radius = capsule.Radius * scale;
        return true;
    }

    public static (Vector2 Normal, float Offset) GetWorldPlane(HalfSpace2D halfSpace, Transform2D transform)
    {
        var localBoundary = halfSpace.Normal * halfSpace.Offset;
        var localTangent = new Vector2(-halfSpace.Normal.Y, halfSpace.Normal.X);
        var localToWorld = transform.LocalToWorldMatrix;
        var worldBoundary = Vector2.Transform(localBoundary, localToWorld);
        var worldTangent = Vector2.Transform(localBoundary + localTangent, localToWorld) - worldBoundary;
        var worldOutward = Vector2.Transform(localBoundary + halfSpace.Normal, localToWorld) - worldBoundary;

        StateGuard.ThrowIf(worldTangent.LengthSquared() <= float.Epsilon || worldOutward.LengthSquared() <= float.Epsilon, "A half-space transform cannot collapse an axis.");

        var worldNormal = Vector2.Normalize(new Vector2(-worldTangent.Y, worldTangent.X));
        if (Vector2.Dot(worldNormal, worldOutward) < 0f)
            worldNormal = -worldNormal;

        return (worldNormal, Vector2.Dot(worldBoundary, worldNormal));
    }
}
