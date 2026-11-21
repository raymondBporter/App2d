using System.Numerics;
using System.Text.Json.Serialization;

namespace App2d.Core.Mathematics.Transforms;

/// <summary>Translation and rotation in radians. Default is identity; rotation retains full turns. No scale or reflection.</summary>
public readonly record struct RigidTransform2D(Vector2f Translation, float Rotation = 0) : IInterpolatable<RigidTransform2D, float>
{
    public static RigidTransform2D Identity => default;

    /// <summary>Interpolates translation and the unwrapped angle, retaining full turns and rigidity.</summary>
    public static RigidTransform2D Lerp(RigidTransform2D start, RigidTransform2D end, float amount) =>
        new(Vector2f.Lerp(start.Translation, end.Translation, amount), Interpolation.Lerp(start.Rotation, end.Rotation, amount));

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
