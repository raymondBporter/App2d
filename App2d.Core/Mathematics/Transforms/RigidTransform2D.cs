using System.Numerics;
using System.Text.Json.Serialization;

namespace App2d.Core.Mathematics.Transforms;

/// <summary>Translation and rotation in radians. Default is identity; rotation retains full turns. No scale or reflection.</summary>
public readonly record struct RigidTransform2D(Vector2 Translation, float Rotation = 0)
{
    public static RigidTransform2D Identity => default;

    /// <summary>A row-vector matrix: rotate first, then translate.</summary>
    [JsonIgnore]
    public Matrix3x2 Matrix => Matrix3x2.CreateRotation(Rotation) * Matrix3x2.CreateTranslation(Translation);

    public Frame2D ToFrame() => new(Matrix);
    public Vector2 TransformPoint(Vector2 local) => Vector2.Transform(local, Matrix);
    public Vector2 TransformDirection(Vector2 local) => Vector2.TransformNormal(local, Matrix);

    /// <summary>Applies this transform first, then the parent. The accumulated rotation is not wrapped.</summary>
    public RigidTransform2D Then(RigidTransform2D parent) =>
        new(parent.TransformPoint(Translation), Rotation + parent.Rotation);

    public RigidTransform2D Inverse() => new(
        Vector2.TransformNormal(-Translation, Matrix3x2.CreateRotation(-Rotation)), -Rotation);
}
