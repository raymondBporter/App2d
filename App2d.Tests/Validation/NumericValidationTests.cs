using App2d.Core.Geometry.Functions;
using App2d.Core.Geometry;
using App2d.Core.Shapes;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Tests.Validation;

public sealed class NumericValidationTests
{
    [Fact]
    public void PredicatesCanBeUsedWithoutThrowing()
    {
        Assert.True(NumericValidation.IsLessThan(1, 2));
        Assert.True(NumericValidation.IsLessThanOrEqual(2d, 2d));
        Assert.True(NumericValidation.IsGreaterThan(3L, 2L));
        Assert.True(NumericValidation.IsGreaterThanOrEqual(2m, 2m));
        Assert.False(NumericValidation.IsPositive(-0f));
        Assert.False(NumericValidation.IsFiniteAndPositive(float.PositiveInfinity));
        Assert.False(NumericValidation.IsFiniteAndNonNegative(float.NaN));
        Assert.True(NumericValidation.IsFiniteAndNonZero(-1));
        Assert.True(NumericValidation.IsInClosedRange(1d, 0d, 1d));
        Assert.False(NumericValidation.IsInOpenRange(1d, 0d, 1d));
    }

    [Fact]
    public void NonzeroVectorsAllowAxisAlignedDirections()
    {
        ArgGuard.ThrowIfNotFiniteOrZero(Vector2.UnitX);
        ArgGuard.ThrowIfNotFiniteOrZero(Vector3.UnitY);
        ArgGuard.ThrowIfNotFiniteOrZero(Vector4.UnitW);
        ArgGuard.ThrowIfNotFiniteOrZero(new Vector2(float.Epsilon, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfNotFiniteOrZero(Vector2.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfNotFiniteOrZero(Vector3.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfNotFiniteOrZero(Vector4.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfNotFiniteOrNotPositive(Vector2.UnitX));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void EveryVectorComponentIsCheckedAndTheWholeValueIsReported(float bad)
    {
        foreach (var vector in new Vector2[] { new(bad, 1), new(1, bad) })
        {
            Assert.False(NumericValidation.IsFinite(vector));
            Assert.False(NumericValidation.IsFiniteAndNonZero(vector));
            Assert.False(NumericValidation.IsFiniteAndPositive(vector));
            var error = Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfNotFiniteOrNegative(vector));
            Assert.Equal(nameof(vector), error.ParamName);
            Assert.Equal(vector, Assert.IsType<Vector2>(error.ActualValue));
            Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.RequireFinitePositive(vector));
        }
        foreach (var vector in new Vector3[] { new(bad, 1, 1), new(1, bad, 1), new(1, 1, bad) })
        {
            Assert.False(NumericValidation.IsFinite(vector));
            Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfNotFiniteOrZero(vector));
            Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfNotFiniteOrNotPositive(vector));
        }
        foreach (var vector in new Vector4[] { new(bad, 1, 1, 1), new(1, bad, 1, 1), new(1, 1, bad, 1), new(1, 1, 1, bad) })
        {
            Assert.False(NumericValidation.IsFinite(vector));
            Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfNotFiniteOrNegative(vector));
            Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfNotFiniteOrZero(vector));
        }
    }

    [Fact]
    public void ComponentOrderingRequiresEveryAxisAndPreservesInclusiveEdges()
    {
        var mixed = new Vector2(1, 5);
        var limit = new Vector2(3);
        Assert.False(NumericValidation.IsComponentWiseLessThan(mixed, limit));
        Assert.False(NumericValidation.IsComponentWiseGreaterThan(mixed, limit));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfNotFiniteOrNotComponentWiseLessThan(mixed, limit));
        ArgGuard.ThrowIfNotFiniteOrNotComponentWiseLessThanOrEqual(new Vector2(1, 3), limit);
        ArgGuard.ThrowIfNotFiniteOrNotComponentWiseGreaterThanOrEqual(limit, new Vector2(1, 3));
        ArgGuard.ThrowIfNotFiniteOrNotComponentWiseLessThan(Vector3.One, new Vector3(2));
        ArgGuard.ThrowIfNotFiniteOrNotComponentWiseGreaterThan(new Vector4(2), Vector4.One);
        ArgGuard.ThrowIfNotFiniteOrNegative(Vector3.UnitZ);
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfNegative(new Vector3(1, -1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfNegative(new Vector4(1, 1, 1, -1)));
        Assert.Equal(new Vector3(2), ArgGuard.RequireFinitePositive(new Vector3(2)));
        Assert.Equal(new Vector4(2), ArgGuard.RequireFinitePositive(new Vector4(2)));
    }

    [Fact]
    public void VectorRangeGuardsValidateBothBounds()
    {
        ArgGuard.ThrowIfNotFiniteOrNotInClosedRange(Vector2.One, Vector2.Zero, Vector2.One);
        ArgGuard.ThrowIfNotFiniteOrNotInOpenRange(Vector3.One, Vector3.Zero, new Vector3(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfNotFiniteOrNotInOpenRange(Vector4.One, Vector4.Zero, Vector4.One));
        var maximum = new Vector2(1, float.NaN);
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfNotFiniteOrNotInClosedRange(Vector2.Zero, Vector2.Zero, maximum));
        Assert.Equal(nameof(maximum), error.ParamName);
        maximum = new(-1, 1);
        error = Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfNotFiniteOrNotInClosedRange(Vector2.Zero, Vector2.Zero, maximum));
        Assert.Equal(nameof(maximum), error.ParamName);
    }

    [Fact]
    public void InfiniteBoundsAndZeroSizedRectanglesKeepTheirExistingPolicies()
    {
        var unbounded = new Rect2D(new(float.NegativeInfinity), new(float.PositiveInfinity));
        Assert.False(unbounded.IsFinite);
        Assert.True(unbounded.Contains(Vector2.Zero));
        Assert.Equal(default, Rect2D.FromSize(Vector2.Zero));
        Assert.Throws<ArgumentException>(() => new Rect2D(new(float.NaN, 0), Vector2.One));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Circle2D(float.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => Rectangle2D.FromSize(new(float.PositiveInfinity, 1)));
    }
}
