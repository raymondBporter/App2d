using App2d.Core.Geometry.Primitives;
using App2d.Core.Mathematics.Vectors;
using App2d.Core.Validation;

namespace App2d.Core.Geometry.Meshes;

/// <summary>Three finite 3D points with winding-defined area and normal.</summary>
public readonly record struct Triangle3D : ITriangle<Triangle3D, Vector3f>
{
    public Triangle3D(Vector3f a, Vector3f b, Vector3f c)
    {
        Ensure.Finite(a);
        Ensure.Finite(b);
        Ensure.Finite(c);
        A = a;
        B = b;
        C = c;
    }

    public Vector3f A { get; }
    public Vector3f B { get; }
    public Vector3f C { get; }

    public static Triangle3D Create(Vector3f a, Vector3f b, Vector3f c) => new(a, b, c);

    public void Deconstruct(out Vector3f a, out Vector3f b, out Vector3f c) => (a, b, c) = (A, B, C);

    /// <summary>Magnitude of (B - A) cross (C - A), computed with double intermediates.</summary>
    public double DoubleArea => CrossEdges().Length();

    public double Area => DoubleArea * 0.5d;

    /// <summary>Returns false and zero for a collinear triangle; never returns a NaN normal.</summary>
    public bool TryGetUnitNormal(out Vector3f normal)
    {
        var cross = CrossEdges();
        var length = cross.Length();
        if (!(length > 0d) || !double.IsFinite(length))
        {
            normal = Vector3f.Zero;
            return false;
        }
        normal = (cross / length).ToVector3f();
        return true;
    }

    private Vector3d CrossEdges()
    {
        var origin = A.ToVector3d();
        return (B.ToVector3d() - origin).Cross(C.ToVector3d() - origin);
    }
}
