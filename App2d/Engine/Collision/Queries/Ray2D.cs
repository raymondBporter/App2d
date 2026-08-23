using System.Numerics;

namespace App2d.Engine.Collision.Queries;

public readonly record struct Ray2D
{
    public Ray2D(Vector2 origin, Vector2 direction)
    {
        ArgGuard.ThrowIfNotFinite(origin);
        ArgGuard.ThrowIfNotFiniteOrZero(direction);

        Origin = origin;
        Direction = Vector2.Normalize(direction);
    }

    public Vector2 Origin { get; }
    public Vector2 Direction { get; }

    public Vector2 GetPoint(float distance) => Origin + Direction * distance;

    public static Ray2D FromPoints(Vector2 origin, Vector2 target) =>
        new(origin, target - origin);
}
