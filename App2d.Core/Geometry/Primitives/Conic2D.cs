namespace App2d.Core.Geometry.Primitives;

/// <summary>
/// A quadratic equation in coordinates relative to <see cref="Origin"/>.
/// Its curve consists of the points where the equation evaluates to zero.
/// </summary>
/// <remarks>The mixed term is <c>XY * x * y</c>.</remarks>
public readonly record struct Conic2D(
    Vector2f Origin, double XX, double XY, double YY, Vector2d Linear, double Constant)
{
    /// <summary>
    /// The determinant of the quadratic part. Positive is elliptic, negative is hyperbolic,
    /// and zero is parabolic or degenerate. Degenerate curves can also have a nonzero determinant.
    /// </summary>
    public double QuadraticDeterminant => XX * YY - (XY / 2d) * (XY / 2d);

    /// <summary>Evaluates the equation in double precision at a point in the original coordinate space.</summary>
    public double Evaluate(Vector2f point)
    {
        var p = point.ToVector2d() - Origin.ToVector2d();
        return (XX * p.X + XY * p.Y + Linear.X) * p.X + (YY * p.Y + Linear.Y) * p.Y + Constant;
    }
}
