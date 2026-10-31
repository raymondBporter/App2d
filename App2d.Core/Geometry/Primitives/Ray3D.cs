using App2d.Core.Geometry.Meshes;
using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry.Primitives;

/// <summary>A spatial ray with a unit direction. Hit distances are measured from its origin in world units.</summary>
public readonly record struct Ray3D
{
    public Ray3D(Vector3 origin, Vector3 direction)
    {
        Origin = ArgGuard.Finite(origin);
        ArgGuard.NonZero(direction);
        var wide = direction.ToVector3d();
        Direction = (wide / wide.Length()).ToVector3();
    }

    public Vector3 Origin { get; }
    public Vector3 Direction { get; }
    public Vector3 At(float distance) => Origin + Direction * ArgGuard.NonNegative(distance);

    /// <summary>Intersects a plane Normal dot point + D = 0. Parallel rays and hits behind the origin return false.</summary>
    public bool TryIntersect(Plane plane, out float distance)
    {
        ArgGuard.Finite(plane.Normal);
        ArgGuard.Finite(plane.D);
        distance = 0;
        var normal = plane.Normal.ToVector3d();
        var denominator = normal.Dot(Direction.ToVector3d());
        if (normal.LengthSquared() == 0 || Math.Abs(denominator) <= normal.Length() * 1e-12) return false;
        var hit = -(normal.Dot(Origin.ToVector3d()) + plane.D) / denominator;
        if (!double.IsFinite(hit) || hit < 0 || hit > float.MaxValue) return false;
        distance = (float)hit;
        return true;
    }

    /// <summary>Two-sided triangle hit, including shared edges. Degenerate triangles do not intersect.</summary>
    public bool TryIntersect(Triangle3D triangle, out float distance)
    {
        distance = 0;
        var a = triangle.A.ToVector3d();
        var first = triangle.B.ToVector3d() - a;
        var second = triangle.C.ToVector3d() - a;
        var direction = Direction.ToVector3d();
        var p = direction.Cross(second);
        var determinant = first.Dot(p);
        if (Math.Abs(determinant) <= 1e-12 * first.Length() * second.Length() || determinant == 0) return false;
        var offset = Origin.ToVector3d() - a;
        var u = offset.Dot(p) / determinant;
        if (u < 0 || u > 1) return false;
        var q = offset.Cross(first);
        var v = direction.Dot(q) / determinant;
        if (v < 0 || u + v > 1) return false;
        var hit = second.Dot(q) / determinant;
        if (!double.IsFinite(hit) || hit < 0 || hit > float.MaxValue) return false;
        distance = (float)hit;
        return true;
    }
}
