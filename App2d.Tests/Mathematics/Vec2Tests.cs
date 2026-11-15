using App2d.Core.Mathematics;

namespace App2d.Tests.Mathematics;

public sealed class Vec2Tests
{
    [Fact]
    public void InfinityConstantsHaveTheirOwnSigns()
    {
        Assert.Equal(new Vector2f(float.PositiveInfinity), Vector2f.PositiveInfinity);
        Assert.Equal(new Vector2f(float.NegativeInfinity), Vector2f.NegativeInfinity);
        Assert.Equal(new Vector2d(double.PositiveInfinity), Vector2d.PositiveInfinity);
        Assert.Equal(new Vec2<Half>(Half.NegativeInfinity), Vec2<Half>.NegativeInfinity);
    }

    [Fact]
    public void PerpendicularsRotateByQuarterTurns()
    {
        var value = new Vector2f(3, 4);
        Assert.Equal(new Vector2f(-4, 3), value.PerpCcw);
        Assert.Equal(new Vector2f(4, -3), value.PerpCw);
        Assert.Equal(0f, Vector2f.Dot(value, value.PerpCcw));
        Assert.Equal(25f, value.Cross(value.PerpCcw));
    }

    [Fact]
    public void FloatAndDoubleCrossProductsKeepTheirOwnPrecision()
    {
        var first = new Vector2f(16_777_216, 16_777_215);
        var second = new Vector2f(16_777_215, 16_777_214);

        // Float products round to the same value; double products preserve the negative turn.
        Assert.Equal(0f, first.Cross(second));
        Assert.Equal(-1d, new Vector2d(first).Cross(new(second)));
    }

    [Fact]
    public void RejectionRemovesOnlyTheComponentParallelToAUnitNormal()
    {
        var vector = new Vector2f(3, 4);
        Assert.Equal(new Vector2f(3, 0), Vector2f.Reject(vector, Vector2f.UnitY));
        Assert.Equal(new Vector2f(0, 4), Vector2f.Reject(vector, Vector2f.UnitX));
        var normal = Vector2f.Normalize(new(2, -3));
        var rejected = Vector2f.Reject(vector, normal);
        Assert.InRange(MathF.Abs(Vector2f.Dot(rejected, normal)), 0, .000002f);
        Near(vector, rejected + normal * Vector2f.Dot(vector, normal));
        Near(rejected, Vector2f.Reject(rejected, normal));
    }

    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(90, 0, 1)]
    [InlineData(180, -1, 0)]
    [InlineData(-90, 0, -1)]
    [InlineData(450, 0, 1)]
    public void DegreeConstructionSharesTheExistingVectorDirectionConvention(float degrees, float x, float y)
    {
        Near(new(x, y), Vector2f.FromAngleDegrees(degrees));
        Assert.Equal(Vector2f.FromAngle(Angles.DegreesToRadians(degrees)), Vector2f.FromAngleDegrees(degrees));
    }

    [Fact]
    public void RefractionUsesTheTransverseComponentAndIsReversible()
    {
        var direction = new Vector2f(.6f, -.8f);
        Assert.True(Vector2f.TryRefract(direction, Vector2f.UnitY, 1, 1.5f, out var transmitted));
        Near(new(.4f, -MathF.Sqrt(.84f)), transmitted);
        Assert.Equal(1, transmitted.Length(), 6);
        Assert.True(Vector2f.TryRefract(-transmitted, -Vector2f.UnitY, 1.5f, 1, out var reversed));
        Near(-direction, reversed);
    }

    [Fact]
    public void RefractionIncludesCriticalIncidenceAndReportsTotalInternalReflection()
    {
        Assert.True(Vector2f.TryRefract(new(.5f, -MathF.Sqrt(.75f)), Vector2f.UnitY, 2, 1, out var grazing));
        Near(Vector2f.UnitX, grazing);
        Assert.False(Vector2f.TryRefract(Vector2f.Normalize(new(1, -1)), Vector2f.UnitY, 2, 1, out var reflected));
        Assert.Equal(Vector2f.Zero, reflected);
        Assert.True(Vector2f.TryRefract(Vector2f.UnitX, Vector2f.UnitY, 1, 2, out var entering));
        Near(new(.5f, -MathF.Sqrt(.75f)), entering);
    }

    [Fact]
    public void ExtremePositiveIndicesDoNotCreateNaNAtNormalIncidence()
    {
        Assert.True(Vector2f.TryRefract(-Vector2f.UnitY, Vector2f.UnitY, float.MaxValue, float.Epsilon, out var normal));
        Assert.Equal(-Vector2f.UnitY, normal);
        Assert.False(Vector2f.TryRefract(Vector2f.Normalize(new(1, -1)), Vector2f.UnitY, float.MaxValue, float.Epsilon, out var tir));
        Assert.Equal(Vector2f.Zero, tir);
        Assert.ThrowsAny<ArgumentException>(() => Vector2f.TryRefract(-Vector2f.UnitY, Vector2f.UnitY, 0, 1, out _));
        Assert.ThrowsAny<ArgumentException>(() => Vector2f.TryRefract(-Vector2f.UnitY, Vector2f.UnitY, 1, float.NaN, out _));
        Assert.ThrowsAny<ArgumentException>(() => Vector2f.FromAngleDegrees(float.PositiveInfinity));
    }

    [Fact]
    public void DoubleRefractionKeepsNormalAndShallowIncidenceWhenTheRatioOverflows()
    {
        Assert.True(Vector2d.TryRefract(-Vector2d.UnitY, Vector2d.UnitY, double.MaxValue, double.Epsilon, out var normal));
        Assert.Equal(-Vector2d.UnitY, normal);

        Assert.True(Vector2d.TryRefract(new(double.Epsilon, -1), Vector2d.UnitY, 0.5d, double.Epsilon, out var shallow));
        Assert.Equal(new Vector2d(0.5d, -Math.Sqrt(0.75d)), shallow);

        Assert.False(Vector2d.TryRefract(Vector2d.UnitX, Vector2d.UnitY, double.MaxValue, double.Epsilon, out var reflected));
        Assert.Equal(Vector2d.Zero, reflected);
    }

    private static void Near(Vector2f expected, Vector2f actual) => Assert.True(Vector2f.Distance(expected, actual) < .000002f, $"Expected {expected}, got {actual}");
}
