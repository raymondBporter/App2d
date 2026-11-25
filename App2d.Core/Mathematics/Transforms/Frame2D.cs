using App2d.Core.Validation;
using System.Numerics;
using System.Text.Json.Serialization;

namespace App2d.Core.Mathematics.Transforms;

/// <summary>An immutable affine coordinate frame. Axes may be scaled, reflected, sheared or collapsed.</summary>
public readonly record struct Frame2D : ITransform<Vector2f>
{
    public Frame2D(Matrix3x2 matrix) : this(Matrix2x3f.FromNumerics(matrix)) { }

    private Frame2D(Matrix2x3f matrix)
    {
        Ensure.Finite(matrix.ToNumerics(), nameof(matrix));
        ColumnMatrix = matrix;
    }

    public Frame2D(Vector2f origin, Vector2f xAxis, Vector2f yAxis)
        : this(new Matrix2x3f(xAxis, yAxis, origin)) { }

    public static Frame2D Identity { get; } = new(Matrix2x3f.Identity);
    [JsonIgnore]
    public Matrix2x3f ColumnMatrix { get; }

    /// <summary>The System.Numerics representation of this frame.</summary>
    public Matrix3x2 Matrix => ColumnMatrix.ToNumerics();

    public Vector2f Origin => ColumnMatrix.Translation;
    public Vector2f XAxis => ColumnMatrix.XAxis;
    public Vector2f YAxis => ColumnMatrix.YAxis;
    public Vector2f TransformPoint(Vector2f local) => ColumnMatrix.TransformPoint(local);
    public Vector2f TransformDirection(Vector2f local) => ColumnMatrix.TransformDirection(local);

    public static Frame2D FromRotation(float radians, Vector2f origin = default)
    {
        if (radians == 0f)
        {
            return new(origin, Vector2f.UnitX, Vector2f.UnitY);
        }

        var axis = Vector2f.FromAngle(radians);
        return new(origin, axis, axis.PerpCcw);
    }

    /// <summary>A rotation frame authored in degrees, with an optional world-space origin.</summary>
    public static Frame2D FromRotationDegrees(float degrees, Vector2f origin = default) =>
        FromRotation(Angles.DegreesToRadians(degrees), origin);

    /// <summary>Uses the supplied Y axis and its clockwise perpendicular as X; does not normalize or choose a fallback.</summary>
    public static Frame2D FromYAxis(Vector2f origin, Vector2f yAxis) => new(origin, yAxis.PerpCw, yAxis);

    public Frame2D WithOrigin(Vector2f origin) => new(origin, XAxis, YAxis);

    /// <summary>Applies this frame first, then the parent.</summary>
    public Frame2D Then(Frame2D parent) => new(ColumnMatrix.Then(parent.ColumnMatrix));

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

    public bool TryToLocal(Vector2f point, out Vector2f local)
    {
        // Subtract the origin first, so large translations cannot erase small local coordinates.
        var basis = Matrix3x2.Create(XAxis.ToVector2(), YAxis.ToVector2(), Vector2f.Zero.ToVector2());
        if (!Matrix3x2.Invert(basis, out var inverse) || !inverse.IsFinite())
        {
            local = default;
            return false;
        }
        local = Vector2f.TransformNormal(point - Origin, inverse);
        return true;
    }

    public Vector2f ToLocal(Vector2f point) => TryToLocal(point, out var local)
        ? local : throw new InvalidOperationException("The coordinate frame has no finite inverse.");
}
