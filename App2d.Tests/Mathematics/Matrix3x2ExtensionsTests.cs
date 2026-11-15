using App2d.Core.Mathematics.Transforms;
using System.Numerics;

namespace App2d.Tests.Mathematics;

public sealed class Matrix3x2ExtensionsTests
{
    [Fact]
    public void RowsAreTransformedAxesAndColumnsMapHomogeneousPoints()
    {
        var matrix = new Matrix3x2(2, 3, 5, 7, 11, 13);
        var (xAxis, yAxis, translation) = matrix.GetRows();
        Assert.Equal(Vector2f.TransformNormal(Vector2f.UnitX, matrix), xAxis);
        Assert.Equal(Vector2f.TransformNormal(Vector2f.UnitY, matrix), yAxis);
        Assert.Equal(Vector2f.Transform(Vector2f.Zero, matrix), translation);

        var point = new Vector2f(17, 19);
        var (xColumn, yColumn) = matrix.GetColumns();
        var homogeneous = new Vector3f(point, 1);
        Assert.Equal(Vector2f.Transform(point, matrix),
            new Vector2f(Vector3f.Dot(homogeneous, xColumn), Vector3f.Dot(homogeneous, yColumn)));
    }
}
