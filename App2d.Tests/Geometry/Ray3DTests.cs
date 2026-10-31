using App2d.Core.Geometry.Meshes;
using App2d.Core.Geometry.Primitives;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class Ray3DTests
{
    [Fact]
    public void PlaneHitsUseWorldDistancesAndRejectParallelOrBackwardHits()
    {
        var ray = new Ray3D(new(0, 0, 3), new(0, 0, -100));
        Assert.True(ray.TryIntersect(new Plane(Vector3.UnitZ * 2, 0), out var distance));
        Assert.Equal(3f, distance);
        Assert.Equal(Vector3.Zero, ray.At(distance));
        Assert.False(ray.TryIntersect(new Plane(Vector3.UnitX, -1), out _));
        Assert.False(ray.TryIntersect(new Plane(Vector3.UnitZ, -4), out _));
        Assert.False(ray.TryIntersect(new Plane(Vector3.Zero, 1), out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Ray3D(Vector3.Zero, Vector3.Zero));
    }

    [Fact]
    public void TriangleHitsAreTwoSidedIncludeEdgesAndIgnoreDegenerateGeometry()
    {
        var triangle = new Triangle3D(new(-1, -1, 0), new(1, -1, 0), new(0, 1, 0));
        var ray = new Ray3D(new(0, 0, 2), -Vector3.UnitZ);
        Assert.True(ray.TryIntersect(triangle, out var distance));
        Assert.Equal(2, distance);
        Assert.True(ray.TryIntersect(new Triangle3D(triangle.C, triangle.B, triangle.A), out _));
        Assert.True(new Ray3D(new(0, 1, 2), -Vector3.UnitZ).TryIntersect(triangle, out _));
        Assert.False(new Ray3D(new(2, 0, 2), -Vector3.UnitZ).TryIntersect(triangle, out _));
        Assert.False(new Ray3D(new(0, 0, -2), -Vector3.UnitZ).TryIntersect(triangle, out _));
        Assert.False(ray.TryIntersect(new Triangle3D(Vector3.Zero, Vector3.One, Vector3.One * 2), out _));
        Assert.False(default(Ray3D).TryIntersect(triangle, out _));
    }
}
