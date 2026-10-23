using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Mathematics.Transforms;

/// <summary>An immutable affine coordinate frame, including scale, reflection and shear, using row-vector matrices. Default collapses to the origin.</summary>
public readonly record struct Frame3D
{
    public Frame3D(Vector3 origin, Vector3 xAxis, Vector3 yAxis, Vector3 zAxis)
    {
        ArgGuard.Finite(origin);
        ArgGuard.Finite(xAxis);
        ArgGuard.Finite(yAxis);
        ArgGuard.Finite(zAxis);
        Origin = origin;
        XAxis = xAxis;
        YAxis = yAxis;
        ZAxis = zAxis;
    }

    public Frame3D(Matrix4x4 matrix)
        : this(matrix.Translation, new(matrix.M11, matrix.M12, matrix.M13),
            new(matrix.M21, matrix.M22, matrix.M23), new(matrix.M31, matrix.M32, matrix.M33))
    {
        ArgGuard.ThrowIf(matrix.M14 != 0 || matrix.M24 != 0 || matrix.M34 != 0 || matrix.M44 != 1,
            "A coordinate frame needs an affine matrix without perspective.", nameof(matrix));
    }

    public static Frame3D Identity { get; } = new(Matrix4x4.Identity);
    public Matrix4x4 Matrix => new(XAxis.X, XAxis.Y, XAxis.Z, 0,
        YAxis.X, YAxis.Y, YAxis.Z, 0, ZAxis.X, ZAxis.Y, ZAxis.Z, 0,
        Origin.X, Origin.Y, Origin.Z, 1);
    public Vector3 Origin { get; }
    public Vector3 XAxis { get; }
    public Vector3 YAxis { get; }
    public Vector3 ZAxis { get; }

    public Vector3 TransformPoint(Vector3 local) => Origin + XAxis * local.X + YAxis * local.Y + ZAxis * local.Z;
    public Vector3 TransformDirection(Vector3 local) => XAxis * local.X + YAxis * local.Y + ZAxis * local.Z;

    /// <summary>Rotates about local X, then Y, then Z, in radians, before placing the origin.</summary>
    public static Frame3D FromRotation(Vector3 radians, Vector3 origin = default)
    {
        ArgGuard.Finite(radians);
        var matrix = Matrix4x4.CreateRotationX(radians.X) * Matrix4x4.CreateRotationY(radians.Y) * Matrix4x4.CreateRotationZ(radians.Z);
        matrix.Translation = origin;
        return new(matrix);
    }

    public Frame3D WithOrigin(Vector3 origin) => new(origin, XAxis, YAxis, ZAxis);

    /// <summary>Applies this frame first, then the parent.</summary>
    public Frame3D Then(Frame3D parent) => new(parent.TransformPoint(Origin),
        parent.TransformDirection(XAxis), parent.TransformDirection(YAxis), parent.TransformDirection(ZAxis));

    public bool TryInverse(out Frame3D inverse)
    {
        if (!Matrix4x4.Invert(Matrix, out var matrix) || !HasFiniteBasis(matrix) || !matrix.Translation.IsFinite())
        {
            inverse = default;
            return false;
        }
        inverse = new(matrix.Translation, new(matrix.M11, matrix.M12, matrix.M13),
            new(matrix.M21, matrix.M22, matrix.M23), new(matrix.M31, matrix.M32, matrix.M33));
        return true;
    }

    public bool TryToLocal(Vector3 point, out Vector3 local)
    {
        // Invert only the basis, then subtract the origin separately to retain small local coordinates.
        var basis = Matrix;
        basis.Translation = Vector3.Zero;
        if (!Matrix4x4.Invert(basis, out var inverse) || !HasFiniteBasis(inverse))
        {
            local = default;
            return false;
        }
        local = Vector3.TransformNormal(point - Origin, inverse);
        return local.IsFinite();
    }

    public Vector3 ToLocal(Vector3 point) => TryToLocal(point, out var local)
        ? local : throw new InvalidOperationException("The coordinate frame has no finite inverse for this point.");

    private static bool HasFiniteBasis(Matrix4x4 matrix) =>
        new Vector3(matrix.M11, matrix.M12, matrix.M13).IsFinite() &&
        new Vector3(matrix.M21, matrix.M22, matrix.M23).IsFinite() &&
        new Vector3(matrix.M31, matrix.M32, matrix.M33).IsFinite();
}
