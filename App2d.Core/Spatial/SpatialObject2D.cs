using App2d.Core.Geometry;
using App2d.Core.Mathematics;
using App2d.Core.Shapes;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core;

/// <summary>
/// A render-agnostic immutable shape placed in world space. Owns local and world bounds caches.
/// </summary>
public class SpatialObject2D(IShape2D shape)
{
    private Rect2D _worldBounds;
    private int _worldBoundsVersion = -1;
    private Similarity2D _collisionPose;
    private int _collisionPoseVersion = -1;

    public Affine2D Transform { get; } = new();
    public IShape2D Shape { get; } = ArgGuard.RequireNotNull(shape);

    /// <summary>Calculated once when the immutable shape is attached; independent of this object's transform.</summary>
    public Rect2D LocalBounds { get; } = ShapeBounds2D.Calculate(shape);

    public Rect2D WorldBounds
    {
        get
        {
            if (_worldBoundsVersion == Transform.Version)
                return _worldBounds;

            _worldBounds = LocalBounds.TransformedBy(Transform.Matrix);
            _worldBoundsVersion = Transform.Version;
            return _worldBounds;
        }
    }

    /// <summary>
    /// The validated pose (rotation, uniform scale, mirror, translation) that
    /// collision consumes. Collidable objects need orthogonal axes with a uniform, nonzero scale;
    /// render-only objects may use general affine transforms.
    /// </summary>
    public Similarity2D CollisionPose
    {
        get
        {
            if (_collisionPoseVersion == Transform.Version)
                return _collisionPose;

            StateGuard.ThrowIf(!Similarity2D.TryFromMatrix(Transform.Matrix, out _collisionPose), "Collision requires a uniform, non-zero scale and orthogonal axes on the transform.");
            _collisionPoseVersion = Transform.Version;
            return _collisionPose;
        }
    }

    public bool ContainsWorldPoint(Vector2 worldPoint)
    {
        if (!Matrix3x2.Invert(Transform.Matrix, out var worldToLocal))
            return false;

        return Shape.ContainsPoint(Vector2.Transform(worldPoint, worldToLocal));
    }
}
