using App2d.Core.Geometry.Operations.Bounds;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Spatial;

/// <summary>
/// A render-agnostic immutable shape placed in world space. Owns local and world bounds caches.
/// </summary>
public class SpatialObject2D(IShape2D shape)
{
    private Rect2f _worldBounds;
    private int _worldBoundsVersion = -1;
    private Similarity2D _collisionPose;
    private int _collisionPoseVersion = -1;

    public Affine2D Transform { get; } = new();
    public IShape2D Shape { get; } = Ensure.RequireNotNull(shape);

    public Vector2f Position
    {
        get => Transform.Position;
        set => Transform.Position = value;
    }

    /// <summary>Rotation in radians.</summary>
    public float Rotation
    {
        get => Transform.Rotation;
        set => Transform.Rotation = value;
    }

    /// <summary>The positive uniform scale of the pose. Changing it preserves the existing axis signs.</summary>
    public float Scale
    {
        get => CollisionPose.Scale;
        set
        {
            Ensure.Positive(value);
            var scale = Transform.Scale;
            Transform.Scale = new(MathF.CopySign(value, scale.X), MathF.CopySign(value, scale.Y));
        }
    }

    /// <summary>Calculated once when the immutable shape is attached; independent of this object's transform.</summary>
    public Rect2f LocalBounds { get; } = ShapeBounds2D.Calculate(shape);

    public Rect2f WorldBounds
    {
        get
        {
            if (_worldBoundsVersion == Transform.Version)
            {
                return _worldBounds;
            }

            _worldBounds = ShapeBounds2D.CalculateAffineTransformed(Shape, Transform.Matrix, LocalBounds);
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
            {
                return _collisionPose;
            }

            Ensure.ThrowIf(!Similarity2D.TryFromMatrix(Transform.Matrix, out _collisionPose), "Collision requires a uniform, non-zero scale and orthogonal axes on the transform.");
            _collisionPoseVersion = Transform.Version;
            return _collisionPose;
        }
    }

    public bool ContainsWorldPoint(Vector2f worldPoint)
    {
        if (!Matrix3x2.Invert(Transform.Matrix, out var worldToLocal))
        {
            return false;
        }

        return Shape.ContainsPoint(Vector2f.Transform(worldPoint, worldToLocal));
    }
}
