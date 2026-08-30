using System.Numerics;
using App2d.Core.Geometry;
using App2d.Core.Mathematics;

namespace App2d.Collision;

internal static class CollisionMath2D
{
    public static (Vector2 Center, float Radius) GetWorldCircle(Circle2D circle, Similarity2D pose) =>
        (pose.TransformPoint(circle.Center), circle.Radius * pose.Scale);

    public static (Vector2 Start, Vector2 End, float Radius) GetWorldCapsule(Capsule2D capsule, Similarity2D pose) =>
        (pose.TransformPoint(capsule.Start), pose.TransformPoint(capsule.End), capsule.Radius * pose.Scale);

    public static (Vector2 Normal, float Offset) GetWorldPlane(HalfSpace2D halfSpace, Similarity2D pose)
    {
        // Normals transform by (A⁻¹)ᵀ = A / Scale² for this family, so the
        // direct direction transform is exact (mirror included) once normalized.
        var worldNormal = Vector2.Normalize(pose.TransformDirection(halfSpace.Normal));
        var worldBoundary = pose.TransformPoint(halfSpace.Normal * halfSpace.Offset);
        return (worldNormal, Vector2.Dot(worldBoundary, worldNormal));
    }
}
