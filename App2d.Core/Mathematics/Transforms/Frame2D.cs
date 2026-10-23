using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Mathematics.Transforms;

/// <summary>An immutable affine coordinate frame. Axes may be scaled, reflected, sheared or collapsed.</summary>
public readonly record struct Frame2D
{
    public Frame2D(Matrix3x2 matrix)
    {
        ArgGuard.Finite(matrix);
        Matrix = matrix;
    }

    public Frame2D(Vector2 origin, Vector2 xAxis, Vector2 yAxis)
        : this(new Matrix3x2(xAxis.X, xAxis.Y, yAxis.X, yAxis.Y, origin.X, origin.Y)) { }

    public static Frame2D Identity { get; } = new(Matrix3x2.Identity);
    public Matrix3x2 Matrix { get; }
    public Vector2 Origin => Matrix.Translation;
    public Vector2 XAxis => Matrix.X;
    public Vector2 YAxis => Matrix.Y;
    public Vector2 TransformPoint(Vector2 local) => Vector2.Transform(local, Matrix);
    public Vector2 TransformDirection(Vector2 local) => Vector2.TransformNormal(local, Matrix);

    public static Frame2D FromRotation(float radians, Vector2 origin = default)
    {
        if (radians == 0f)
        {
            return new(origin, Vector2.UnitX, Vector2.UnitY);
        }

        var axis = Vector2.FromAngle(radians);
        return new(origin, axis, axis.PerpCcw);
    }

    /// <summary>A rotation frame authored in degrees, with an optional world-space origin.</summary>
    public static Frame2D FromRotationDegrees(float degrees, Vector2 origin = default) =>
        FromRotation(float.DegreesToRadians(degrees), origin);

    /// <summary>Uses the supplied Y axis and its clockwise perpendicular as X; does not normalize or choose a fallback.</summary>
    public static Frame2D FromYAxis(Vector2 origin, Vector2 yAxis) => new(origin, yAxis.PerpCw, yAxis);

    public Frame2D WithOrigin(Vector2 origin) => new(origin, XAxis, YAxis);

    /// <summary>Applies this frame first, then the parent, using Matrix3x2's row-vector convention.</summary>
    public Frame2D Then(Frame2D parent) => new(Matrix * parent.Matrix);

    public bool TryInverse(out Frame2D inverse)
    {
        if (!Matrix3x2.Invert(Matrix, out var matrix) || !matrix.IsFinite())
        {
            inverse = default;
            return false;
        }
        inverse = new(matrix);
        return true;
    }

    public bool TryToLocal(Vector2 point, out Vector2 local)
    {
        // Subtract the origin first, so large translations cannot erase small local coordinates.
        var basis = new Matrix3x2(Matrix.M11, Matrix.M12, Matrix.M21, Matrix.M22, 0, 0);
        if (!Matrix3x2.Invert(basis, out var inverse) || !inverse.IsFinite())
        {
            local = default;
            return false;
        }
        local = Vector2.TransformNormal(point - Origin, inverse);
        return true;
    }

    public Vector2 ToLocal(Vector2 point) => TryToLocal(point, out var local)
        ? local : throw new InvalidOperationException("The coordinate frame has no finite inverse.");
}
