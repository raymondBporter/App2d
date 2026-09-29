using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry;

/// <summary>Three finite 3D points with winding-defined area and normal.</summary>
public readonly record struct Triangle3D
{
    public Triangle3D(Vector3 a, Vector3 b, Vector3 c)
    {
        ArgGuard.ThrowIfNotFinite(a);
        ArgGuard.ThrowIfNotFinite(b);
        ArgGuard.ThrowIfNotFinite(c);
        A = a;
        B = b;
        C = c;
    }

    public Vector3 A { get; }
    public Vector3 B { get; }
    public Vector3 C { get; }

    /// <summary>Magnitude of (B - A) cross (C - A), computed with double intermediates.</summary>
    public double DoubleArea
    {
        get
        {
            var (x, y, z) = CrossEdges();
            return Math.Sqrt(x * x + y * y + z * z);
        }
    }

    public double Area => DoubleArea * 0.5d;

    /// <summary>Returns false and zero for a collinear triangle; never returns a NaN normal.</summary>
    public bool TryGetUnitNormal(out Vector3 normal)
    {
        var (x, y, z) = CrossEdges();
        var length = Math.Sqrt(x * x + y * y + z * z);
        if (!(length > 0d) || !double.IsFinite(length))
        {
            normal = Vector3.Zero;
            return false;
        }
        normal = new((float)(x / length), (float)(y / length), (float)(z / length));
        return true;
    }

    private (double X, double Y, double Z) CrossEdges()
    {
        var ux = (double)B.X - A.X;
        var uy = (double)B.Y - A.Y;
        var uz = (double)B.Z - A.Z;
        var vx = (double)C.X - A.X;
        var vy = (double)C.Y - A.Y;
        var vz = (double)C.Z - A.Z;
        return (uy * vz - uz * vy, uz * vx - ux * vz, ux * vy - uy * vx);
    }
}
