using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>A linear 2D transform stored as two columns of a two-dimensional vector type.</summary>
/// <remarks>A * B * v applies B to v first, then A.</remarks>
public readonly record struct Matrix2x2<TReal>(Vec2<TReal> X, Vec2<TReal> Y)
    where TReal : unmanaged, IFloatingPointIeee754<TReal>
{
    /// <summary>Constructs a matrix from its entries in row order.</summary>
    public Matrix2x2(TReal m11, TReal m12, TReal m21, TReal m22)
        : this(new(m11, m21), new(m12, m22)) { }

    public TReal M11 => X.X;
    public TReal M12 => Y.X;
    public TReal M21 => X.Y;
    public TReal M22 => Y.Y;

    public static Matrix2x2<TReal> Zero => default;
    public static Matrix2x2<TReal> Identity => new(Vec2<TReal>.UnitX, Vec2<TReal>.UnitY);
    public static Matrix2x2<TReal> FromColumns(Vec2<TReal> xAxis, Vec2<TReal> yAxis) => new(xAxis, yAxis);

    public (Vec2<TReal> X, Vec2<TReal> Y) GetRows() => (new(M11, M12), new(M21, M22));

    public (Vec2<TReal> X, Vec2<TReal> Y) GetColumns() => (X, Y);

    /// <summary>Multiplies the matrix by a point column vector.</summary>
    public Vec2<TReal> TransformPoint(Vec2<TReal> point) => X * point.X + Y * point.Y;

    /// <summary>Applies the same linear map as TransformPoint, following Vector2.TransformNormal.</summary>
    public Vec2<TReal> TransformNormal(Vec2<TReal> normal) => TransformPoint(normal);

    public static Matrix2x2<TReal> operator +(Matrix2x2<TReal> left, Matrix2x2<TReal> right) =>
        new(left.X + right.X, left.Y + right.Y);

    public static Matrix2x2<TReal> operator -(Matrix2x2<TReal> left, Matrix2x2<TReal> right) =>
        new(left.X - right.X, left.Y - right.Y);

    public static Matrix2x2<TReal> operator -(Matrix2x2<TReal> matrix) => new(-matrix.X, -matrix.Y);

    public static Matrix2x2<TReal> operator *(Matrix2x2<TReal> matrix, TReal scale) => new(matrix.X * scale, matrix.Y * scale);

    public static Matrix2x2<TReal> operator *(TReal scale, Matrix2x2<TReal> matrix) => matrix * scale;

    public static Matrix2x2<TReal> operator /(Matrix2x2<TReal> matrix, TReal scale) => new(matrix.X / scale, matrix.Y / scale);

    public static Vec2<TReal> operator *(Matrix2x2<TReal> matrix, Vec2<TReal> vector) => matrix.TransformPoint(vector);

    public static Matrix2x2<TReal> operator *(Matrix2x2<TReal> left, Matrix2x2<TReal> right) =>
        new(left * right.X, left * right.Y);
}
