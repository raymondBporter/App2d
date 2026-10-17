using App2d.Core.Mathematics;
using App2d.Core.Validation;
using App2d.Core.Geometry.Primitives;
using System.Numerics;

namespace App2d.Core.Geometry.Meshes;

/// <summary>Three finite 3D points with winding-defined area and normal.</summary>
public readonly record struct Triangle3D : ITriangle<Triangle3D, Vector3>
{
    public Triangle3D(Vector3 a, Vector3 b, Vector3 c)
    {
        ArgGuard.Finite(a);
        ArgGuard.Finite(b);
        ArgGuard.Finite(c);
        A = a;
        B = b;
        C = c;
    }

    public Vector3 A { get; }
    public Vector3 B { get; }
    public Vector3 C { get; }

    public static Triangle3D Create(Vector3 a, Vector3 b, Vector3 c) => new(a, b, c);

    public void Deconstruct(out Vector3 a, out Vector3 b, out Vector3 c) => (a, b, c) = (A, B, C);

    /// <summary>Magnitude of (B - A) cross (C - A), computed with double intermediates.</summary>
    public double DoubleArea => CrossEdges().Length();

    public double Area => DoubleArea * 0.5d;

    /// <summary>Returns false and zero for a collinear triangle; never returns a NaN normal.</summary>
    public bool TryGetUnitNormal(out Vector3 normal)
    {
        var cross = CrossEdges();
        var length = cross.Length();
        if (!(length > 0d) || !double.IsFinite(length))
        {
            normal = Vector3.Zero;
            return false;
        }
        normal = (cross / length).ToVector3();
        return true;
    }

    private Vector3d CrossEdges()
    {
        var origin = A.ToVector3d();
        return (B.ToVector3d() - origin).Cross(C.ToVector3d() - origin);
    }
}
