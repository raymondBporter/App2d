using System.Numerics;

namespace App2d.Core.Mathematics;

public readonly record struct TransformState2D(Vector2 Position, float Rotation, Vector2 Scale)
{
    public static TransformState2D Capture(Transform2D transform) =>
        new(transform.Position, transform.Rotation, transform.Scale);

    public void Apply(Transform2D transform)
    {
        transform.Position = Position;
        transform.Rotation = Rotation;
        transform.Scale = Scale;
    }
}
