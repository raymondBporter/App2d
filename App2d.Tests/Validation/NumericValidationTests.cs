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
    }

    [Fact]
    public void NonzeroVectorsAllowAxisAlignedDirections()
    {
        ArgGuard.NonZero(Vector2.UnitX);
        ArgGuard.NonZero(Vector3.UnitY);
        ArgGuard.NonZero(Vector4.UnitW);
        ArgGuard.NonZero(new Vector2(float.Epsilon, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.NonZero(Vector2.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.NonZero(Vector3.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.NonZero(Vector4.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.Positive(Vector2.UnitX));
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
            var error = Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.NonNegative(vector));
            Assert.Equal(nameof(vector), error.ParamName);
            Assert.Equal(vector, Assert.IsType<Vector2>(error.ActualValue));
            Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.Positive(vector));
        }
        foreach (var vector in new Vector3[] { new(bad, 1, 1), new(1, bad, 1), new(1, 1, bad) })
        {
            Assert.False(vector.IsFinite());
            Assert.False(NumericValidation.IsFinite(vector));
            Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.NonZero(vector));
            Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.Positive(vector));
        }
        foreach (var vector in new Vector4[] { new(bad, 1, 1, 1), new(1, bad, 1, 1), new(1, 1, bad, 1), new(1, 1, 1, bad) })
        {
            Assert.False(vector.IsFinite());
            Assert.False(NumericValidation.IsFinite(vector));
            Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.NonNegative(vector));
            Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.NonZero(vector));
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
        ArgGuard.Finite(singular);
        ArgGuard.Finite(large);
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
            var error = Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.Finite(localToWorld));
            Assert.Equal(nameof(localToWorld), error.ParamName);
            Assert.Equal(localToWorld, Assert.IsType<Matrix3x2>(error.ActualValue));
        }
    }

    [Fact]
    public void ComponentOrderingRequiresEveryAxisAndPreservesInclusiveEdges()
    {
        var mixed = new Vector2(1, 5);
        var limit = new Vector2(3);
        Assert.False(NumericValidation.IsComponentWiseLessThan(mixed, limit));
        Assert.False(NumericValidation.IsComponentWiseGreaterThan(mixed, limit));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.LessThan(mixed, limit));
        ArgGuard.AtMost(new Vector2(1, 3), limit);
        ArgGuard.AtLeast(limit, new Vector2(1, 3));
        ArgGuard.LessThan(Vector3.One, new Vector3(2));
        ArgGuard.GreaterThan(new Vector4(2), Vector4.One);
        ArgGuard.NonNegative(Vector3.UnitZ);
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.NonNegative(new Vector3(1, -1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.NonNegative(new Vector4(1, 1, 1, -1)));
        Assert.Equal(new Vector3(2), ArgGuard.Positive(new Vector3(2)));
        Assert.Equal(new Vector4(2), ArgGuard.Positive(new Vector4(2)));
    }

    [Fact]
    public void VectorRangeGuardsValidateBothBounds()
    {
        ArgGuard.InRange(Vector2.One, Vector2.Zero, Vector2.One);
        ArgGuard.InRange(Vector3.One, Vector3.Zero, new Vector3(2), bounds: RangeBounds.Open);
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.InRange(Vector4.One, Vector4.Zero, Vector4.One, bounds: RangeBounds.Open));
        var maximum = new Vector2(1, float.NaN);
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.InRange(Vector2.Zero, Vector2.Zero, maximum));
        Assert.Equal(nameof(maximum), error.ParamName);
        maximum = new(-1, 1);
        error = Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.InRange(Vector2.Zero, Vector2.Zero, maximum));
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
        ArgGuard.InRange(Vector2.One, Vector2.Zero, new Vector2(2), bounds: bounds);
        ArgGuard.InRange(Vector3.One, Vector3.Zero, new Vector3(2), bounds: bounds);
        ArgGuard.InRange(Vector4.One, Vector4.Zero, new Vector4(2), bounds: bounds);
        Assert.Equal(includesMinimum, NumericValidation.IsInRange(new Vector2(0, 1), Vector2.Zero, new Vector2(2), bounds));
        Assert.Equal(includesMaximum, NumericValidation.IsInRange(new Vector3(1, 2, 1), Vector3.Zero, new Vector3(2), bounds));
        Assert.False(NumericValidation.IsInRange(new Vector4(1, 1, 3, 1), Vector4.Zero, new Vector4(2), bounds));
        if (!includesMinimum)
            Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.InRange(new Vector2(0, 1), Vector2.Zero, new Vector2(2), bounds: bounds));
        if (!includesMaximum)
            Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.InRange(new Vector3(1, 2, 1), Vector3.Zero, new Vector3(2), bounds: bounds));
    }

    [Fact]
    public void VectorInfinityPoliciesMatchScalarPolicies()
    {
        var unbounded = new Vector2(float.PositiveInfinity);
        ArgGuard.Number(unbounded);
        ArgGuard.NonNegative(unbounded, allowInfinity: true);
        ArgGuard.InRange(Vector2.One, Vector2.Zero, unbounded);
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.AtMost(Vector2.One, unbounded));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.NonNegative(unbounded));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.Number(new Vector3(1, float.NaN, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.NonNegative(new Vector4(1, 1, 1, float.NaN), allowInfinity: true));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.InRange(unbounded, Vector2.Zero, unbounded));
    }
}
