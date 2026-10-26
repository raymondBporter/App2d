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

    [Fact]
    public void RejectionRemovesOnlyTheComponentParallelToAUnitNormal()
    {
        var vector = new Vector2(3, 4);
        Assert.Equal(new Vector2(3, 0), Vector2.Reject(vector, Vector2.UnitY));
        Assert.Equal(new Vector2(0, 4), Vector2.Reject(vector, Vector2.UnitX));
        var normal = Vector2.Normalize(new(2, -3));
        var rejected = Vector2.Reject(vector, normal);
        Assert.InRange(MathF.Abs(Vector2.Dot(rejected, normal)), 0, .000002f);
        Near(vector, rejected + normal * Vector2.Dot(vector, normal));
        Near(rejected, Vector2.Reject(rejected, normal));
    }

    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(90, 0, 1)]
    [InlineData(180, -1, 0)]
    [InlineData(-90, 0, -1)]
    [InlineData(450, 0, 1)]
    public void DegreeConstructionSharesTheExistingVectorDirectionConvention(float degrees, float x, float y)
    {
        Near(new(x, y), Vector2.FromAngleDegrees(degrees));
        Assert.Equal(Vector2.FromAngle(float.DegreesToRadians(degrees)), Vector2.FromAngleDegrees(degrees));
    }

    [Fact]
    public void RefractionUsesTheTransverseComponentAndIsReversible()
    {
        var direction = new Vector2(.6f, -.8f);
        Assert.True(Vector2.TryRefract(direction, Vector2.UnitY, 1, 1.5f, out var transmitted));
        Near(new(.4f, -MathF.Sqrt(.84f)), transmitted);
        Assert.Equal(1, transmitted.Length(), 6);
        Assert.True(Vector2.TryRefract(-transmitted, -Vector2.UnitY, 1.5f, 1, out var reversed));
        Near(-direction, reversed);
    }

    [Fact]
    public void RefractionIncludesCriticalIncidenceAndReportsTotalInternalReflection()
    {
        Assert.True(Vector2.TryRefract(new(.5f, -MathF.Sqrt(.75f)), Vector2.UnitY, 2, 1, out var grazing));
        Near(Vector2.UnitX, grazing);
        Assert.False(Vector2.TryRefract(Vector2.Normalize(new(1, -1)), Vector2.UnitY, 2, 1, out var reflected));
        Assert.Equal(Vector2.Zero, reflected);
        Assert.True(Vector2.TryRefract(Vector2.UnitX, Vector2.UnitY, 1, 2, out var entering));
        Near(new(.5f, -MathF.Sqrt(.75f)), entering);
    }

    [Fact]
    public void ExtremePositiveIndicesDoNotCreateNaNAtNormalIncidence()
    {
        Assert.True(Vector2.TryRefract(-Vector2.UnitY, Vector2.UnitY, float.MaxValue, float.Epsilon, out var normal));
        Assert.Equal(-Vector2.UnitY, normal);
        Assert.False(Vector2.TryRefract(Vector2.Normalize(new(1, -1)), Vector2.UnitY, float.MaxValue, float.Epsilon, out var tir));
        Assert.Equal(Vector2.Zero, tir);
        Assert.ThrowsAny<ArgumentException>(() => Vector2.TryRefract(-Vector2.UnitY, Vector2.UnitY, 0, 1, out _));
        Assert.ThrowsAny<ArgumentException>(() => Vector2.TryRefract(-Vector2.UnitY, Vector2.UnitY, 1, float.NaN, out _));
        Assert.ThrowsAny<ArgumentException>(() => Vector2.FromAngleDegrees(float.PositiveInfinity));
    }

    private static void Near(Vector2 expected, Vector2 actual) => Assert.True(Vector2.Distance(expected, actual) < .000002f, $"Expected {expected}, got {actual}");
}
