using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>A linear 2D transform stored as two columns of a two-dimensional vector type.</summary>
/// <remarks>A * B * v applies B to v first, then A.</remarks>
public readonly record struct Matrix2x2<TVector, TReal>(TVector X, TVector Y)
    where TVector : unmanaged, IVector2<TVector, TReal>
    where TReal : unmanaged, IFloatingPointIeee754<TReal>
{
    /// <summary>Constructs a matrix from its entries in row order.</summary>
    public Matrix2x2(TReal m11, TReal m12, TReal m21, TReal m22)
        : this(TVector.Create(m11, m21), TVector.Create(m12, m22)) { }

    public TReal M11 => X.X;
    public TReal M12 => Y.X;
    public TReal M21 => X.Y;
    public TReal M22 => Y.Y;

    public static Matrix2x2<TVector, TReal> Zero => default;
    public static Matrix2x2<TVector, TReal> Identity => new(TVector.Create(TReal.One, TReal.Zero), TVector.Create(TReal.Zero, TReal.One));
    public static Matrix2x2<TVector, TReal> FromColumns(TVector xAxis, TVector yAxis) => new(xAxis, yAxis);

    public (TVector X, TVector Y) GetRows() => (TVector.Create(M11, M12), TVector.Create(M21, M22));

    public (TVector X, TVector Y) GetColumns() => (X, Y);

    /// <summary>Multiplies the matrix by a point column vector.</summary>
    public TVector TransformPoint(TVector point) => X * point.X + Y * point.Y;

    /// <summary>Applies the same linear map as TransformPoint, following Vector2.TransformNormal.</summary>
    public TVector TransformNormal(TVector normal) => TransformPoint(normal);

    public static Matrix2x2<TVector, TReal> operator +(Matrix2x2<TVector, TReal> left, Matrix2x2<TVector, TReal> right) =>
        new(left.X + right.X, left.Y + right.Y);

    public static Matrix2x2<TVector, TReal> operator -(Matrix2x2<TVector, TReal> left, Matrix2x2<TVector, TReal> right) =>
        new(left.X - right.X, left.Y - right.Y);

    public static Matrix2x2<TVector, TReal> operator -(Matrix2x2<TVector, TReal> matrix) => new(-matrix.X, -matrix.Y);

    public static Matrix2x2<TVector, TReal> operator *(Matrix2x2<TVector, TReal> matrix, TReal scale) => new(matrix.X * scale, matrix.Y * scale);

    public static Matrix2x2<TVector, TReal> operator *(TReal scale, Matrix2x2<TVector, TReal> matrix) => matrix * scale;

    public static Matrix2x2<TVector, TReal> operator /(Matrix2x2<TVector, TReal> matrix, TReal scale) => new(matrix.X / scale, matrix.Y / scale);

    public static TVector operator *(Matrix2x2<TVector, TReal> matrix, TVector vector) => matrix.TransformPoint(vector);

    public static Matrix2x2<TVector, TReal> operator *(Matrix2x2<TVector, TReal> left, Matrix2x2<TVector, TReal> right) =>
        new(left * right.X, left * right.Y);
}
