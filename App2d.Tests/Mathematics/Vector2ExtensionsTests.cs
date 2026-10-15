using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Tests.Mathematics;

public sealed class Vector2ExtensionsTests
{
    [Fact]
    public void InfinityConstantsHaveTheirOwnSigns()
    {
        Assert.Equal(new Vector2(float.PositiveInfinity), Vector2Extensions.PositiveInfinity);
        Assert.Equal(new Vector2(float.NegativeInfinity), Vector2Extensions.NegativeInfinity);
    }

    [Fact]
    public void PerpendicularsRotateByQuarterTurns()
    {
        var value = new Vector2(3, 4);
        Assert.Equal(new Vector2(-4, 3), value.PerpCcw);
        Assert.Equal(new Vector2(4, -3), value.PerpCw);
        Assert.Equal(0f, Vector2.Dot(value, value.PerpCcw));
        Assert.Equal(25f, value.Cross(value.PerpCcw));
    }

    [Fact]
    public void FloatAndDoubleCrossProductsKeepTheirOwnPrecision()
    {
        var first = new Vector2(16_777_216, 16_777_215);
        var second = new Vector2(16_777_215, 16_777_214);

        // Float products round to the same value; double products preserve the negative turn.
        Assert.Equal(0f, first.Cross(second));
        Assert.Equal(-1d, new Vector2d(first).Cross(new(second)));
    }
}
