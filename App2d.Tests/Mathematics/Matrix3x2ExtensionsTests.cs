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
        Assert.Equal(Vector2.TransformNormal(Vector2.UnitX, matrix), xAxis);
        Assert.Equal(Vector2.TransformNormal(Vector2.UnitY, matrix), yAxis);
        Assert.Equal(Vector2.Transform(Vector2.Zero, matrix), translation);

        var point = new Vector2(17, 19);
        var (xColumn, yColumn) = matrix.GetColumns();
        var homogeneous = new Vector3(point, 1);
        Assert.Equal(Vector2.Transform(point, matrix),
            new Vector2(Vector3.Dot(homogeneous, xColumn), Vector3.Dot(homogeneous, yColumn)));
    }
}
