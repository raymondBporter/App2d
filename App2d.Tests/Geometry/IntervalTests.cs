using App2d.Core.Geometry.Operations.Convex;
using App2d.Core.Geometry.Primitives;
using System.Numerics;
using System.Text.Json;

namespace App2d.Tests.Geometry;

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

        var bounded = new Interval<float>(2f, 8f);
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

        Assert.Equal(new Interval<double>(2d, double.PositiveInfinity), Interval.AtLeast(2d));
        Assert.Equal(new Interval<Half>(Half.NegativeInfinity, Half.PositiveInfinity), Interval.Entire<Half>());
        Assert.Equal(new Interval<int>(3, 3), Interval<int>.Singleton(3));
        Assert.Equal(new Interval<decimal>(2m, 2m), Interval<decimal>.Singleton(2m));
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
        Assert.ThrowsAny<ArgumentException>(() => new Interval<float>(min, max));
    }

    [Fact]
    public void UnboundedIntersectionsAndHullsRetainTheirBounds()
    {
        var lower = Interval.AtLeast(2f);
        var upper = Interval.AtMost(8f);
        Assert.True(lower.TryIntersect(upper, out var bounded));
        Assert.Equal(new Interval<float>(2f, 8f), bounded);
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
    public void EndpointsRemainValidatedAcrossNumericTypesAndJson()
    {
        Assert.Throws<ArgumentException>(() => new Interval<int>(2, 1));
        Assert.Throws<ArgumentException>(() => new Interval<decimal>(2m, 1m));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Interval<double>(double.NaN, 1d));
        Assert.Throws<ArgumentOutOfRangeException>(() => Interval<double>.Singleton(double.PositiveInfinity));

        var interval = new Interval<float>(2f, 8f);
        var (min, max) = interval;
        Assert.Equal(2f, min);
        Assert.Equal(8f, max);
        Assert.Equal(interval, JsonSerializer.Deserialize<Interval<float>>(JsonSerializer.Serialize(interval)));
        Assert.Throws<ArgumentException>(() => JsonSerializer.Deserialize<Interval<float>>("{\"Min\":8,\"Max\":2}"));
    }

    [Fact]
    public void LengthContainmentAndOverlapIncludeTheEnds()
    {
        var interval = new Interval<float>(-1, 3);
        Assert.Equal(4, interval.Length);
        Assert.True(interval.Contains(-1));
        Assert.True(interval.Contains(3));
        Assert.False(interval.Contains(3.01f));
        Assert.True(interval.Overlaps(new(3, 5)));
        Assert.True(new Interval<float>(3, 5).Overlaps(interval));
        Assert.False(interval.Overlaps(new(3.01f, 5)));
        Assert.True(interval.Overlaps(new(0, 0)));
        Assert.Equal(0, new Interval<float>(2, 2).Length);
    }

    [Fact]
    public void ProjectionsProduceIntervals()
    {
        ReadOnlySpan<Vector2> square = [new(0f, 0f), new(2f, 0f), new(2f, 2f), new(0f, 2f)];
        var polygon = Projection2D.Polygon(square, Vector2.UnitX);
        Assert.Equal(0f, polygon.Min, 3);
        Assert.Equal(2f, polygon.Max, 3);

        var capsule = Projection2D.Capsule(new Vector2(-1f, 0f), new Vector2(3f, 0f), 0.5f, Vector2.UnitX);
        Assert.Equal(-1.5f, capsule.Min, 3);
        Assert.Equal(3.5f, capsule.Max, 3);

        var ellipse = Projection2D.Ellipse(new(1, 1), new(2, 1), Vector2.UnitX);
        Assert.Equal(-1f, ellipse.Min, 5);
        Assert.Equal(3f, ellipse.Max, 5);
        var diagonal = Projection2D.Ellipse(default, new(2, 1), Vector2.Normalize(new(1, 1)));
        Assert.Equal(MathF.Sqrt(2.5f), diagonal.Max, 5);
        Assert.Equal(-diagonal.Max, diagonal.Min, 5);
    }
}
