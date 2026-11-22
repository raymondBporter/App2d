using App2d.Core.Geometry.Convex;
using System.Text.Json;

namespace App2d.Tests.Mathematics;

public sealed class IntervalTests
{
    [Fact]
    public void FactoriesRepresentEntireOneSidedBoundedAndSingletonIntervals()
    {
        var entire = Interval.Entire<float>();
        Assert.True(entire.IsEntire);
        Assert.False(entire.IsBoundedBelow);
        Assert.False(entire.IsBoundedAbove);
        Assert.Equal(float.PositiveInfinity, entire.Length);

        var lower = Interval.AtLeast(2f);
        Assert.True(lower.IsBoundedBelow);
        Assert.False(lower.IsBoundedAbove);
        Assert.False(lower.IsEntire);
        Assert.Equal(2f, lower.Clamp(-1f));

        var upper = Interval.AtMost(8f);
        Assert.False(upper.IsBoundedBelow);
        Assert.True(upper.IsBoundedAbove);
        Assert.False(upper.IsEntire);
        Assert.Equal(8f, upper.Clamp(12f));

        var bounded = Interval<float>.Builder.Create(2f, 8f);
        Assert.True(bounded.IsBoundedBelow);
        Assert.True(bounded.IsBoundedAbove);
        Assert.False(bounded.IsSingleton);
        Assert.Equal(5f, bounded.Clamp(5f));

        var singleton = Interval<float>.Singleton(4f);
        Assert.True(singleton.IsSingleton);
        Assert.Equal(0f, singleton.Length);
        Assert.Equal(4f, singleton.Clamp(100f));
        Assert.Equal(Interval<int>.Singleton(0), default);
        Assert.True(default(Interval<float>).IsSingleton);

        Assert.Equal(Interval<double>.Builder.CreateUnbounded(2d, double.PositiveInfinity), Interval.AtLeast(2d));
        Assert.Equal(Interval<Half>.Builder.CreateUnbounded(Half.NegativeInfinity, Half.PositiveInfinity), Interval.Entire<Half>());
        Assert.Equal(Interval<int>.Builder.Create(3, 3), Interval<int>.Singleton(3));
        Assert.Equal(Interval<decimal>.Builder.Create(2m, 2m), Interval<decimal>.Singleton(2m));
    }

    [Theory]
    [InlineData(float.NaN, 1f)]
    [InlineData(0f, float.NaN)]
    [InlineData(2f, 1f)]
    [InlineData(float.PositiveInfinity, float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity, float.NegativeInfinity)]
    [InlineData(float.PositiveInfinity, 1f)]
    [InlineData(0f, float.NegativeInfinity)]
    public void ConstructorRejectsInvalidEndpoints(float min, float max)
    {
        Assert.ThrowsAny<ArgumentException>(() => Interval<float>.Builder.Create(min, max));
    }

    [Fact]
    public void UnboundedIntersectionsAndHullsRetainTheirBounds()
    {
        var lower = Interval.AtLeast(2f);
        var upper = Interval.AtMost(8f);
        Assert.True(lower.TryIntersect(upper, out var bounded));
        Assert.Equal(Interval<float>.Builder.Create(2f, 8f), bounded);
        Assert.True(lower.Hull(upper).IsEntire);
        Assert.True(Interval.Entire<float>().TryIntersect(lower, out var ray));
        Assert.Equal(lower, ray);
        Assert.True(lower.TryIntersect(Interval.AtMost(2f), out var touching));
        Assert.Equal(Interval<float>.Singleton(2f), touching);
        Assert.False(lower.TryIntersect(Interval.AtMost(1f), out _));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(float.PositiveInfinity)]
    public void FactoriesRequireFiniteEndpoints(float value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Interval.AtLeast(value));
        Assert.Throws<ArgumentOutOfRangeException>(() => Interval.AtMost(value));
        Assert.Throws<ArgumentOutOfRangeException>(() => Interval<float>.Singleton(value));
    }

    [Fact]
    public void BuildersValidateAcrossNumericTypesAndJsonDefersValidation()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Interval<int>.Builder.Create(2, 1));
        Assert.ThrowsAny<ArgumentException>(() => Interval<decimal>.Builder.Create(2m, 1m));
        Assert.Throws<ArgumentOutOfRangeException>(() => Interval<double>.Builder.Create(double.NaN, 1d));
        Assert.Throws<ArgumentOutOfRangeException>(() => Interval<double>.Singleton(double.PositiveInfinity));

        var interval = Interval<float>.Builder.Create(2f, 8f);
        var (min, max) = interval;
        Assert.Equal(2f, min);
        Assert.Equal(8f, max);
        Assert.Equal(interval, JsonSerializer.Deserialize<Interval<float>>(JsonSerializer.Serialize(interval)));
        var reversed = JsonSerializer.Deserialize<Interval<float>>("{\"Min\":8,\"Max\":2}");
        Assert.Equal((8f, 2f), (reversed.Min, reversed.Max));
        Assert.ThrowsAny<ArgumentException>(() => reversed.Validate());
    }

    [Fact]
    public void GenericIntervalsRoundTripJsonWithoutNumericSpecificIntervalConverters()
    {
        var cells = Interval<int>.Builder.Create(2, 8);
        var precise = Interval<double>.Builder.Create(1d, double.BitIncrement(1d));
        var decimals = Interval<decimal>.Builder.Create(1m, 1.0000000000000000000000000001m);

        Assert.Equal(cells, JsonSerializer.Deserialize<Interval<int>>(JsonSerializer.Serialize(cells)));
        Assert.Equal(precise, JsonSerializer.Deserialize<Interval<double>>(JsonSerializer.Serialize(precise)));
        Assert.Equal(decimals, JsonSerializer.Deserialize<Interval<decimal>>(JsonSerializer.Serialize(decimals)));
    }

    [Fact]
    public void LengthContainmentAndOverlapIncludeTheEnds()
    {
        var interval = Interval<float>.Builder.Create(-1, 3);
        Assert.Equal(4, interval.Length);
        Assert.True(interval.Contains(-1));
        Assert.True(interval.Contains(3));
        Assert.False(interval.Contains(3.01f));
        Assert.True(interval.Overlaps(Interval<float>.Builder.Create(3, 5)));
        Assert.True(Interval<float>.Builder.Create(3, 5).Overlaps(interval));
        Assert.False(interval.Overlaps(Interval<float>.Builder.Create(3.01f, 5)));
        Assert.True(interval.Overlaps(Interval<float>.Builder.Create(0, 0)));
        Assert.Equal(0, Interval<float>.Builder.Create(2, 2).Length);
    }

    [Fact]
    public void ProjectionsProduceIntervals()
    {
        ReadOnlySpan<Vector2f> square = [new(0f, 0f), new(2f, 0f), new(2f, 2f), new(0f, 2f)];
        var polygon = Projection2D.Polygon(square, Vector2f.UnitX);
        Assert.Equal(0f, polygon.Min, 3);
        Assert.Equal(2f, polygon.Max, 3);

        var capsule = Projection2D.Capsule(new Vector2f(-1f, 0f), new Vector2f(3f, 0f), 0.5f, Vector2f.UnitX);
        Assert.Equal(-1.5f, capsule.Min, 3);
        Assert.Equal(3.5f, capsule.Max, 3);

        var ellipse = Projection2D.Ellipse(new(1, 1), new(2, 1), Vector2f.UnitX);
        Assert.Equal(-1f, ellipse.Min, 5);
        Assert.Equal(3f, ellipse.Max, 5);
        var diagonal = Projection2D.Ellipse(default, new(2, 1), Vector2f.Normalize(new(1, 1)));
        Assert.Equal(MathF.Sqrt(2.5f), diagonal.Max, 5);
        Assert.Equal(-diagonal.Max, diagonal.Min, 5);
    }
}
