using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Mathematics.Transforms;

/// <summary>An immutable affine coordinate frame, including scale, reflection and shear, using row-vector matrices. Default collapses to the origin.</summary>
public readonly record struct Frame3D : ITransform<Vector3f>, ITransform<Vector3d>
{
    public Frame3D(Vector3f origin, Vector3f xAxis, Vector3f yAxis, Vector3f zAxis)
    {
        Ensure.Finite(origin);
        Ensure.Finite(xAxis);
        Ensure.Finite(yAxis);
        Ensure.Finite(zAxis);
        Origin = origin;
        XAxis = xAxis;
        YAxis = yAxis;
        ZAxis = zAxis;
    }

    public Frame3D(Matrix4x4 matrix)
        : this(new(matrix.Translation), new(matrix.M11, matrix.M12, matrix.M13),
            new(matrix.M21, matrix.M22, matrix.M23), new(matrix.M31, matrix.M32, matrix.M33))
    {
        Ensure.Equal(matrix.M14, 0);
        Ensure.Equal(matrix.M24, 0);
        Ensure.Equal(matrix.M34, 0);
        Ensure.Equal(matrix.M44, 1);
    }

    public static Frame3D Identity { get; } = new(Matrix4x4.Identity);

    /// <summary>A planar frame embedded at Z = 0, keeping Z unscaled: its XY axes act in the plane and Z passes through.</summary>
    public static Frame3D FromPlanar(Frame2D frame) => new(new(frame.Origin, 0), new(frame.XAxis, 0), new(frame.YAxis, 0), Vector3f.UnitZ);
    public Matrix4x4 Matrix => new(XAxis.X, XAxis.Y, XAxis.Z, 0,
        YAxis.X, YAxis.Y, YAxis.Z, 0, ZAxis.X, ZAxis.Y, ZAxis.Z, 0,
        Origin.X, Origin.Y, Origin.Z, 1);
    public Vector3f Origin { get; }
    public Vector3f XAxis { get; }
    public Vector3f YAxis { get; }
    public Vector3f ZAxis { get; }

    public Vector3f TransformPoint(Vector3f local) => Origin + XAxis * local.X + YAxis * local.Y + ZAxis * local.Z;
    public Vector3f TransformDirection(Vector3f local) => XAxis * local.X + YAxis * local.Y + ZAxis * local.Z;

    // Double coordinates use widened coefficients, so large positions are never narrowed to float.
    Vector3d ITransform<Vector3d>.TransformPoint(Vector3d local) => Origin.ToVector3d() + Widened(local);
    Vector3d ITransform<Vector3d>.TransformDirection(Vector3d local) => Widened(local);
    private Vector3d Widened(Vector3d local) => XAxis.ToVector3d() * local.X + YAxis.ToVector3d() * local.Y + ZAxis.ToVector3d() * local.Z;

    /// <summary>Rotates about local X, then Y, then Z, in radians, before placing the origin.</summary>
    public static Frame3D FromRotation(Vector3f radians, Vector3f origin = default)
    {
        Ensure.Finite(radians);
        var matrix = Matrix4x4.CreateRotationX(radians.X) * Matrix4x4.CreateRotationY(radians.Y) * Matrix4x4.CreateRotationZ(radians.Z);
        matrix.Translation = origin.ToVector3();
        return new(matrix);
    }

    public Frame3D WithOrigin(Vector3f origin) => new(origin, XAxis, YAxis, ZAxis);

    /// <summary>Applies this frame first, then the parent.</summary>
    public Frame3D Then(Frame3D parent) => new(parent.TransformPoint(Origin),
        parent.TransformDirection(XAxis), parent.TransformDirection(YAxis), parent.TransformDirection(ZAxis));

    public bool TryInverse(out Frame3D inverse)
    {
        if (!Matrix4x4.Invert(Matrix, out var matrix) || !HasFiniteBasis(matrix) || !new Vector3f(matrix.Translation).IsFinite())
        {
            inverse = default;
            return false;
        }
        inverse = new(new(matrix.Translation), new(matrix.M11, matrix.M12, matrix.M13),
            new(matrix.M21, matrix.M22, matrix.M23), new(matrix.M31, matrix.M32, matrix.M33));
        return true;
    }

    public bool TryToLocal(Vector3f point, out Vector3f local)
    {
        // Invert only the basis, then subtract the origin separately to retain small local coordinates.
        var basis = Matrix;
        basis.Translation = Vector3.Zero;
        if (!Matrix4x4.Invert(basis, out var inverse) || !HasFiniteBasis(inverse))
        {
            local = default;
            return false;
        }
        local = Vector3f.TransformNormal(point - Origin, inverse);
        return local.IsFinite();
    }

    public Vector3f ToLocal(Vector3f point) => TryToLocal(point, out var local)
        ? local : throw new InvalidOperationException("The coordinate frame has no finite inverse for this point.");

    private static bool HasFiniteBasis(Matrix4x4 matrix) =>
        new Vector3f(matrix.M11, matrix.M12, matrix.M13).IsFinite() &&
        new Vector3f(matrix.M21, matrix.M22, matrix.M23).IsFinite() &&
        new Vector3f(matrix.M31, matrix.M32, matrix.M33).IsFinite();
}
