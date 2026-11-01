using App2d.Core.Geometry.Primitives;
using App2d.Core.Mathematics;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Shapes;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Tests.Validation;

public sealed class NumericValidationTests
{
    [Fact]
    public void PredicatesCanBeUsedWithoutThrowing()
    {
        Assert.False(NumericValidation.IsPositive(-0f));
        Assert.False(NumericValidation.IsPositive(float.PositiveInfinity));
        Assert.False(NumericValidation.IsNonNegative(float.NaN));
        Assert.True(NumericValidation.IsNonZero(-1));
        Assert.True(NumericValidation.IsInRange(1d, 0d, 1d));
        Assert.False(NumericValidation.IsInRange(1d, 0d, 1d, bounds: RangeBounds.Open));
        Assert.True(NumericValidation.IsNumber(float.PositiveInfinity));
        Assert.True(NumericValidation.IsNumber(float.NegativeInfinity));
        Assert.False(NumericValidation.IsNumber(float.NaN));
        Assert.True(NumericValidation.IsNegative(-1));
        Assert.False(NumericValidation.IsNegative(-0f));
        Assert.True(NumericValidation.IsNonPositive(-1));
        Assert.True(NumericValidation.IsNonPositive(-0f));
        Assert.False(NumericValidation.IsNonPositive(1));
        foreach (var nonfinite in new[] { float.NaN, float.NegativeInfinity, float.PositiveInfinity })
        {
            Assert.False(NumericValidation.IsNegative(nonfinite));
            Assert.False(NumericValidation.IsNonPositive(nonfinite));
        }
    }

    [Fact]
    public void NonzeroVectorsAllowAxisAlignedDirections()
    {
        Ensure.NonZero(Vector2.UnitX);
        Ensure.NonZero(Vector3.UnitY);
        Ensure.NonZero(Vector4.UnitW);
        Ensure.NonZero(new Vector2(float.Epsilon, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.NonZero(Vector2.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.NonZero(Vector3.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.NonZero(Vector4.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.Positive(Vector2.UnitX));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void EveryVectorComponentIsCheckedAndTheWholeValueIsReported(float bad)
    {
        foreach (var vector in new Vector2[] { new(bad, 1), new(1, bad) })
        {
            Assert.False(vector.IsFinite());
            Assert.False(NumericValidation.IsFinite(vector));
            Assert.False(NumericValidation.IsNonZero(vector));
            Assert.False(NumericValidation.IsPositive(vector));
            var error = Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.NonNegative(vector));
            Assert.Equal(nameof(vector), error.ParamName);
            Assert.Equal(vector, Assert.IsType<Vector2>(error.ActualValue));
            Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.Positive(vector));
        }
        foreach (var vector in new Vector3[] { new(bad, 1, 1), new(1, bad, 1), new(1, 1, bad) })
        {
            Assert.False(vector.IsFinite());
            Assert.False(NumericValidation.IsFinite(vector));
            Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.NonZero(vector));
            Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.Positive(vector));
        }
        foreach (var vector in new Vector4[] { new(bad, 1, 1, 1), new(1, bad, 1, 1), new(1, 1, bad, 1), new(1, 1, 1, bad) })
        {
            Assert.False(vector.IsFinite());
            Assert.False(NumericValidation.IsFinite(vector));
            Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.NonNegative(vector));
            Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.NonZero(vector));
        }
    }

    [Fact]
    public void FinitenessAllowsZeroAndLargeComponentsEvenWhenMagnitudeOrDeterminantOverflows()
    {
        Assert.True(Vector2.Zero.IsFinite());
        Assert.True(Vector3.Zero.IsFinite());
        Assert.True(Vector4.Zero.IsFinite());
        Assert.True(new Vector2(float.MaxValue).IsFinite());
        Assert.True(new Vector3(float.MaxValue).IsFinite());
        Assert.True(new Vector4(float.MaxValue).IsFinite());
        Assert.False(float.IsFinite(new Vector2(float.MaxValue).Length()));

        var singular = new Matrix3x2();
        var large = Matrix3x2.CreateScale(float.MaxValue);
        Assert.True(singular.IsFinite());
        Assert.True(large.IsFinite());
        Assert.False(float.IsFinite(large.GetDeterminant()));
        Ensure.Finite(singular);
        Ensure.Finite(large);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void MatrixGuardChecksAllEntriesAndReportsTheCallersParameter(float bad)
    {
        foreach (var localToWorld in new Matrix3x2[]
        {
            new(bad, 0, 0, 1, 0, 0), new(1, bad, 0, 1, 0, 0),
            new(1, 0, bad, 1, 0, 0), new(1, 0, 0, bad, 0, 0),
            new(1, 0, 0, 1, bad, 0), new(1, 0, 0, 1, 0, bad)
        })
        {
            Assert.False(localToWorld.IsFinite());
            Assert.False(NumericValidation.IsFinite(localToWorld));
            var error = Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.Finite(localToWorld));
            Assert.Equal(nameof(localToWorld), error.ParamName);
            Assert.Equal(localToWorld, Assert.IsType<Matrix3x2>(error.ActualValue));
        }
    }

    [Fact]
    public void ComponentOrderingRequiresEveryAxisAndPreservesInclusiveEdges()
    {
        var mixed = new Vector2(1, 5);
        var limit = new Vector2(3);
        Assert.False(NumericValidation.IsLessThan(mixed, limit));
        Assert.False(NumericValidation.IsGreaterThan(mixed, limit));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.LessThan(mixed, limit));
        Ensure.LessThanOrEqual(new Vector2(1, 3), limit);
        Ensure.GreaterThanOrEqual(limit, new Vector2(1, 3));
        Ensure.LessThan(Vector3.One, new Vector3(2));
        Ensure.GreaterThan(new Vector4(2), Vector4.One);
        Ensure.NonNegative(Vector3.UnitZ);
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.NonNegative(new Vector3(1, -1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.NonNegative(new Vector4(1, 1, 1, -1)));
        Assert.Equal(new Vector3(2), Ensure.Positive(new Vector3(2)));
        Assert.Equal(new Vector4(2), Ensure.Positive(new Vector4(2)));
    }

    [Fact]
    public void VectorRangeGuardsValidateBothBounds()
    {
        Ensure.InRange(Vector2.One, Vector2.Zero, Vector2.One);
        Ensure.InRange(Vector3.One, Vector3.Zero, new Vector3(2), bounds: RangeBounds.Open);
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.InRange(Vector4.One, Vector4.Zero, Vector4.One, bounds: RangeBounds.Open));
        var maximum = new Vector2(1, float.NaN);
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.InRange(Vector2.Zero, Vector2.Zero, maximum));
        Assert.Equal(nameof(maximum), error.ParamName);
        maximum = new(-1, 1);
        error = Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.InRange(Vector2.Zero, Vector2.Zero, maximum));
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
        Assert.Throws<ArgumentOutOfRangeException>(() => Circle2D.Builder.Create(float.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => Rectangle2D.Builder.CreateFromSize(float.PositiveInfinity, 1));
    }

    [Theory]
    [InlineData(RangeBounds.Closed, true, true)]
    [InlineData(RangeBounds.Open, false, false)]
    [InlineData(RangeBounds.ClosedOpen, true, false)]
    [InlineData(RangeBounds.OpenClosed, false, true)]
    public void VectorRangeEndpointsApplyOnEveryAxis(RangeBounds bounds, bool includesMinimum, bool includesMaximum)
    {
        Ensure.InRange(Vector2.One, Vector2.Zero, new Vector2(2), bounds: bounds);
        Ensure.InRange(Vector3.One, Vector3.Zero, new Vector3(2), bounds: bounds);
        Ensure.InRange(Vector4.One, Vector4.Zero, new Vector4(2), bounds: bounds);
        Assert.Equal(includesMinimum, NumericValidation.IsInRange(new Vector2(0, 1), Vector2.Zero, new Vector2(2), bounds));
        Assert.Equal(includesMaximum, NumericValidation.IsInRange(new Vector3(1, 2, 1), Vector3.Zero, new Vector3(2), bounds));
        Assert.False(NumericValidation.IsInRange(new Vector4(1, 1, 3, 1), Vector4.Zero, new Vector4(2), bounds));
        if (!includesMinimum)
            Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.InRange(new Vector2(0, 1), Vector2.Zero, new Vector2(2), bounds: bounds));
        if (!includesMaximum)
            Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.InRange(new Vector3(1, 2, 1), Vector3.Zero, new Vector3(2), bounds: bounds));
    }

    [Fact]
    public void VectorInfinityPoliciesMatchScalarPolicies()
    {
        var unbounded = new Vector2(float.PositiveInfinity);
        Ensure.Number(unbounded);
        Ensure.NonNegative(unbounded, allowInfinity: true);
        Ensure.InRange(Vector2.One, Vector2.Zero, unbounded);
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.LessThanOrEqual(Vector2.One, unbounded));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.NonNegative(unbounded));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.Number(new Vector3(1, float.NaN, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.NonNegative(new Vector4(1, 1, 1, float.NaN), allowInfinity: true));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.InRange(unbounded, Vector2.Zero, unbounded));
    }
}
