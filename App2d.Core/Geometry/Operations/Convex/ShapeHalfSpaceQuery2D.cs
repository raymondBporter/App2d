using App2d.Core.Geometry.Operations.Transforms;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Shapes;
using App2d.Core.Validation;

namespace App2d.Core.Geometry.Operations.Convex;

/// <summary>Shared signed distance and boundary witnesses for a posed convex shape against a posed half-space.</summary>
public static class ShapeHalfSpaceQuery2D
{
    /// <summary>The normal points out of the half-space; negative distance measures the convex shape's escape depth.</summary>
    public static ConvexQueryResult2D Query(IConvexShape2D shape, Similarity2D pose, HalfSpace2D halfSpace, Similarity2D halfSpacePose)
    {
        ArgGuard.ThrowIfNull(shape);
        ArgGuard.ThrowIfNull(halfSpace);
        var (normal, offset) = ShapeTransforms2D.HalfSpace(halfSpace, halfSpacePose);
        var deepest = pose.TransformPoint(shape.GetSupportPoint(-pose.TransposeTransformDirection(normal)));
        var distance = Distance2D.SignedDistanceToHalfSpace(deepest, normal, offset);
        return new(distance, normal, deepest, deepest - normal * distance, 0f);
    }
}
