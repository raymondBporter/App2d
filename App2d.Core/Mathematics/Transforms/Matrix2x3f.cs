using System.Numerics;

namespace App2d.Core.Mathematics.Transforms;

/// <summary>A 2D affine matrix with an implicit third row (0, 0, 1), backed by a System.Numerics.Matrix3x2.</summary>
/// <remarks>A * B * p applies B to the column vector p first, then A.</remarks>
public readonly record struct Matrix2x3f
{
    private readonly Matrix3x2 _value;

    private Matrix2x3f(Matrix3x2 value) => _value = value;

    public Matrix2x3f(Vector2f XAxis, Vector2f YAxis, Vector2f Translation)
        : this(Matrix3x2.Create(XAxis.ToVector2(), YAxis.ToVector2(), Translation.ToVector2())) { }

    public Vector2f XAxis { get => new(_value.X); init => _value.X = value.ToVector2(); }
    public Vector2f YAxis { get => new(_value.Y); init => _value.Y = value.ToVector2(); }
    public Vector2f Translation { get => new(_value.Translation); init => _value.Translation = value.ToVector2(); }

    public void Deconstruct(out Vector2f xAxis, out Vector2f yAxis, out Vector2f translation) =>
        (xAxis, yAxis, translation) = (XAxis, YAxis, Translation);

    public static Matrix2x3f Identity => new(Matrix3x2.Identity);
    public static Matrix2x3f CreateTranslation(Vector2f translation) => new(Matrix3x2.CreateTranslation(translation.ToVector2()));
    public static Matrix2x3f CreateScale(Vector2f scale) => new(Matrix3x2.CreateScale(scale.ToVector2()));
    public static Matrix2x3f CreateRotation(float radians) => new(Matrix3x2.CreateRotation(radians));

    /// <summary>Reads the System.Numerics representation of the same affine map.</summary>
    public static Matrix2x3f FromNumerics(Matrix3x2 matrix) => new(matrix);

    /// <summary>Writes the System.Numerics representation, whose three rows represent these columns.</summary>
    public Matrix3x2 ToNumerics() => _value;

    public Vector2f TransformPoint(Vector2f point) => Vector2f.Transform(point, _value);
    public Vector2f TransformDirection(Vector2f direction) => Vector2f.TransformNormal(direction, _value);

    /// <summary>Applies this transform first, then <paramref name="next"/>.</summary>
    public Matrix2x3f Then(Matrix2x3f next) => next * this;

    public static Vector2f operator *(Matrix2x3f matrix, Vector2f point) => matrix.TransformPoint(point);

    // System.Numerics uses row vectors, so its multiplication order is reversed.
    public static Matrix2x3f operator *(Matrix2x3f left, Matrix2x3f right) => new(right._value * left._value);
}
