using System.Numerics;
using System.Text.Json.Serialization;

namespace App2d.Core.Mathematics.Transforms;

/// <summary>Translation and rotation in radians. Default is identity; rotation retains full turns. No scale or reflection.</summary>
public readonly record struct RigidTransform2D(Vector2f Translation, float Rotation = 0)
{
    public static RigidTransform2D Identity => default;

    /// <summary>A row-vector matrix: rotate first, then translate.</summary>
    [JsonIgnore]
    public Matrix3x2 Matrix => Matrix3x2.CreateRotation(Rotation).Then(Matrix3x2.CreateTranslation(Translation.ToVector2()));

    public Frame2D ToFrame() => new(Matrix);
    public Vector2f TransformPoint(Vector2f local) => Vector2f.Transform(local, Matrix);
    public Vector2f TransformDirection(Vector2f local) => Vector2f.TransformNormal(local, Matrix);

    /// <summary>Applies this transform first, then the parent. The accumulated rotation is not wrapped.</summary>
    public RigidTransform2D Then(RigidTransform2D parent) => new(parent.TransformPoint(Translation), Rotation + parent.Rotation);

    public RigidTransform2D Inverse() => new(Vector2f.TransformNormal(-Translation, Matrix3x2.CreateRotation(-Rotation)), -Rotation);
}
