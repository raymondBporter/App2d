using System.Numerics;

namespace App2d.Engine.Mathematics;

public sealed class Transform2D
{
    public Vector2 Position { get; set; }
    public float Rotation { get; set; }
    public Vector2 Scale { get; set; } = Vector2.One;

    // System.Numerics uses row-vector order: scale, then rotate, then translate.
    public Matrix3x2 LocalToWorldMatrix => Matrix3x2.CreateScale(Scale) * Matrix3x2.CreateRotation(Rotation) * Matrix3x2.CreateTranslation(Position);
}
