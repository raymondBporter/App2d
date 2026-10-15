using App2d.Core.Characters;
using App2d.Core.Constraints;
using App2d.Core.Geometry;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class IIntervalTests
{
    [Fact]
    public void AuthoredAndPhysicsLimitsShareClosedRangeQueries()
    {
        var authored = new Limit(-1, 5).Soft(0, 1);
        var physics = ConstraintLimit1D.Between(1, 7);

        Assert.Equal(6f, IntervalMath<float>.Length(authored));
        Assert.True(authored.Contains(-1f));
        Assert.True(authored.Contains(5f));
        Assert.False(authored.Contains(5.01f));
        Assert.False(authored.Contains(float.NaN));
        Assert.True(physics.Contains(7f));
        Assert.True(IntervalMath<float>.Overlaps(authored, physics));
        Assert.True(IntervalMath<float>.ContainsInterval(ConstraintLimit1D.Free, authored));
        Assert.False(IntervalMath<float>.ContainsInterval(authored, physics));
        Assert.True(IntervalMath<float>.TryIntersect(authored, physics, out var shared));
        Assert.Equal(new Interval<float>(1, 5), shared);
        Assert.Equal(new Interval<float>(-1, 7), IntervalMath<float>.Hull(authored, physics));

        Assert.Equal(0f, authored.SoftMin);
        Assert.Equal(1f, authored.SoftMax);
    }

    [Fact]
    public void ExtensionQueriesDistinguishTouchingFromDisjointIntervals()
    {
        var limits = ConstraintLimit1D.Between(2, 5);

        Assert.True(limits.ContainsInterval(new Interval<float>(3, 4)));
        Assert.True(limits.Overlaps(new Interval<float>(5, 8)));
        Assert.True(limits.TryIntersect(new Interval<float>(5, 8), out var touching));
        Assert.Equal(new Interval<float>(5, 5), touching);
        Assert.False(limits.TryIntersect(new Interval<float>(6, 8), out var disjoint));
        Assert.Equal(default, disjoint);
        Assert.Equal(new Interval<float>(2, 8), limits.Hull(new Interval<float>(6, 8)));
    }

    [Fact]
    public void SharedArithmeticRetainsIntegerAndDoubleEndpointTypes()
    {
        var cells = new Interval<int>(2, 5);
        Assert.Equal(3, cells.Length);
        Assert.Equal(5, cells.Clamp(10));

        var next = double.BitIncrement(1d);
        var precise = new Interval<double>(1d, next);
        Assert.Equal(next - 1d, precise.Length);
        Assert.True(precise.Length > 0d);
        Assert.Equal(next, precise.Clamp(2d));
        Assert.True(precise.TryIntersect(new Interval<double>(next, 2d), out var shared));
        Assert.Equal(new Interval<double>(next, next), shared);
        Assert.False(precise.Overlaps(new Interval<double>(double.BitIncrement(next), 2d)));
    }

    [Fact]
    public void PhysicsLimitsRetainUnboundedAndValidatedClampBehavior()
    {
        Assert.Equal(float.PositiveInfinity, IntervalMath<float>.Length(ConstraintLimit1D.Free));
        Assert.Equal(42f, ConstraintLimit1D.Free.Clamp(42f));
        Assert.Equal(2f, ConstraintLimit1D.AtLeast(2).Clamp(-5));
        Assert.Equal(0f, IntervalMath<float>.Length(ConstraintLimit1D.Locked(4)));
        Assert.Throws<ArgumentOutOfRangeException>(() => ConstraintLimit1D.Free.Clamp(float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => ConstraintLimit1D.Free.Clamp(float.PositiveInfinity));
        Assert.True(float.IsNaN(new Limit(0, 1).Clamp(float.NaN)));
    }

    [Fact]
    public void RectangleAxesExposeIntervalOperations()
    {
        var rectangle = new Rect2D(new Vector2(2, -3), new Vector2(8, 5));

        Assert.Equal(new Interval<float>(2, 8), rectangle.XInterval);
        Assert.Equal(new Interval<float>(-3, 5), rectangle.YInterval);
        Assert.Equal(rectangle.Width, rectangle.XInterval.Length);
        Assert.Equal(rectangle.Height, rectangle.YInterval.Length);
        Assert.Equal(8f, rectangle.XInterval.Clamp(10f));
        Assert.True(rectangle.YInterval.Contains(0));
    }

    [Fact]
    public void MixedStructQueriesDoNotAllocateAfterWarmup()
    {
        var authored = new Limit(-1, 5);
        var physics = ConstraintLimit1D.Between(1, 7);
        RunQueries(authored, physics, 256);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var matches = RunQueries(authored, physics, 4096);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(4096, matches);
        Assert.Equal(0L, allocated);
    }

    private static int RunQueries(Limit authored, ConstraintLimit1D physics, int count)
    {
        var matches = 0;
        for (var i = 0; i < count; i++)
        {
            if (authored.Contains(3f) && IntervalMath<float>.Overlaps(authored, physics) &&
                IntervalMath<float>.TryIntersect(authored, physics, out var shared) && shared.Length == 4f)
                matches++;
        }
        return matches;
    }
}
