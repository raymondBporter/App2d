using System.Numerics;

namespace App2d.Engine.Collision.Queries;

public readonly record struct RayHit2D(
    Vector2 Point,
    Vector2 Normal,
    float Distance);

public readonly record struct RaycastHit2D<T>(
    T Item,
    Vector2 Point,
    Vector2 Normal,
    float Distance)
    where T : class;

public delegate bool RayQueryFilter2D<in T>(T item)
    where T : class;
