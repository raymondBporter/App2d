using System.Numerics;
using App2d.Core.Geometry;
using App2d.Core.Mathematics;

namespace App2d.Core;

/// <summary>
/// A render-agnostic shape placed in world space.
/// </summary>
public class SpatialObject2D(IShape2D shape)
{
    private Bounds2D _worldBounds;
    private int _worldBoundsVersion = -1;
    private Similarity2D _collisionPose;
    private int _collisionPoseVersion = -1;

    public Transform2D Transform { get; } = new();
    public IShape2D Shape { get; } = ArgGuard.RequireNotNull(shape);

    public Bounds2D WorldBounds
    {
        get
        {
            if (_worldBoundsVersion == Transform.Version)
                return _worldBounds;

            _worldBounds = Shape.LocalBounds.TransformedBy(Transform.LocalToWorldMatrix);
            _worldBoundsVersion = Transform.Version;
            return _worldBounds;
        }
    }

    /// <summary>
    /// The validated pose (rotation, uniform scale, mirror, translation) that
    /// collision consumes. Collidable objects must not use non-uniform scale;
    /// render-only objects may, as long as nothing queries them for collision.
    /// </summary>
    public Similarity2D CollisionPose
    {
        get
        {
            if (_collisionPoseVersion == Transform.Version)
                return _collisionPose;

            StateGuard.ThrowIf(
                !Similarity2D.TryFromMatrix(Transform.LocalToWorldMatrix, out _collisionPose),
                "Collision requires a uniform, non-zero scale on the transform.");
            _collisionPoseVersion = Transform.Version;
            return _collisionPose;
        }
    }

    public bool ContainsWorldPoint(Vector2 worldPoint)
    {
        if (!Matrix3x2.Invert(Transform.LocalToWorldMatrix, out var worldToLocal))
            return false;

        return Shape.ContainsPoint(Vector2.Transform(worldPoint, worldToLocal));
    }
}
