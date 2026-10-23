using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Tests.Mathematics;

public sealed class Vector2iTests
{
    [Fact]
    public void IntegerCoordinatesComposeBeforeExplicitConversionToDrawingCoordinates()
    {
        var cell = new Vector2i(3, -2);
        var offset = new Vector2i(-1, 4);
        Assert.Equal(new Vector2i(2, 2), cell + offset);
        Assert.Equal(new Vector2i(4, -6), cell - offset);
        Assert.Equal(new Vector2i(9, -6), 3 * cell);
        Assert.Equal(cell * 3, 3 * cell);
        Assert.Equal(new Vector2(3, -2), cell.ToVector2());
        Assert.Equal(new Vector2(100, -24), new Vector2(4, 40) + cell.ToVector2() * 32);
    }

    [Fact]
    public void IntegerCoordinateOverflowDoesNotWrapIntoAnUnrelatedCell()
    {
        Assert.Throws<OverflowException>(() => new Vector2i(int.MaxValue, 0) + new Vector2i(1, 0));
        Assert.Throws<OverflowException>(() => new Vector2i(int.MinValue, 0) - new Vector2i(1, 0));
        Assert.Throws<OverflowException>(() => new Vector2i(int.MaxValue, 1) * 2);
    }
}
