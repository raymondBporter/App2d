using App2d.Core.Meshes;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class Triangle3DTests
{
    [Fact]
    public void NormalAndAreaFollowWinding()
    {
        var triangle = new Triangle3D(Vector3.Zero, new(2, 0, 0), new(0, 3, 0));
        Assert.Equal(6d, triangle.DoubleArea);
        Assert.Equal(3d, triangle.Area);
        Assert.True(triangle.TryGetUnitNormal(out var normal));
        Assert.Equal(Vector3.UnitZ, normal);

        Assert.True(new Triangle3D(triangle.A, triangle.C, triangle.B).TryGetUnitNormal(out normal));
        Assert.Equal(-Vector3.UnitZ, normal);
    }

    [Theory]
    [InlineData(1e-30f)]
    [InlineData(1e30f)]
    public void UnitNormalHandlesFloatUnderflowAndOverflowInTheCrossProduct(float scale)
    {
        var triangle = new Triangle3D(Vector3.Zero, new(scale, 0, 0), new(0, scale, 0));
        Assert.True(triangle.TryGetUnitNormal(out var normal));
        Assert.Equal(Vector3.UnitZ, normal);
        Assert.Equal((double)scale * scale, triangle.DoubleArea, 12);
    }

    [Fact]
    public void DistantVerticesAreWidenedBeforeSubtractingEdges()
    {
        var triangle = new Triangle3D(new(-float.MaxValue, 0, 0),
            new(float.MaxValue, 0, 0), new(-float.MaxValue, 1, 1));
        Assert.Equal(2 * Math.Sqrt(2) * float.MaxValue, triangle.DoubleArea, 1e24);
        Assert.True(triangle.TryGetUnitNormal(out var normal));
        Assert.Equal(0, normal.X);
        Assert.Equal(-1 / MathF.Sqrt(2), normal.Y);
        Assert.Equal(1 / MathF.Sqrt(2), normal.Z);
    }

    [Theory]
    [InlineData(float.Epsilon)]
    [InlineData(float.MaxValue)]
    public void HandednessHandlesExtremeBasisLengths(float scale)
    {
        var along = new Vector3(scale, 0, 0);
        var across = new Vector3(0, scale, 0);
        var normal = new Vector3(0, 0, scale);
        Assert.True(Orientation3D.TryGetHandedness(along, across, normal, out var right));
        Assert.Equal(1, right);
        Assert.True(Orientation3D.TryGetHandedness(along, across, -normal, out var left));
        Assert.Equal(-1, left);
    }

    [Fact]
    public void DegenerateTriangleReturnsNoNormal()
    {
        var triangle = new Triangle3D(Vector3.Zero, Vector3.One, new(2, 2, 2));
        Assert.Equal(0d, triangle.DoubleArea);
        Assert.False(triangle.TryGetUnitNormal(out var normal));
        Assert.Equal(Vector3.Zero, normal);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Triangle3D(Vector3.Zero,
            new(float.NaN, 0, 0), Vector3.UnitY));
    }

    [Fact]
    public void HandednessDistinguishesReflectionAndFlatFrames()
    {
        Assert.True(Orientation3D.TryGetHandedness(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, out var right));
        Assert.Equal(1, right);
        Assert.True(Orientation3D.TryGetHandedness(-Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, out var left));
        Assert.Equal(-1, left);
        Assert.False(Orientation3D.TryGetHandedness(Vector3.UnitX, Vector3.UnitX, Vector3.UnitZ, out var flat));
        Assert.Equal(0, flat);
        Assert.False(Orientation3D.TryGetHandedness(new(float.NaN, 0, 0), Vector3.UnitY, Vector3.UnitZ, out _));
    }
}
