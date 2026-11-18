using System.Numerics;
using System.Runtime.CompilerServices;

namespace App2d.Tests.Mathematics;

public sealed class NumberTuple2Tests
{
    [Fact]
    public void SharedCalculationsRetainTheConcreteVectorAndItsPrecision()
    {
        Assert.Equal(new Vector2f(6, 8), Move<Vector2f, float>(new(2, 3), new(1, 1), 2));
        Assert.Equal(new Vector2d(33_554_436, 8), Move<Vector2d, double>(new(16_777_217, 3), new(1, 1), 2));
        Assert.Equal(8, Unsafe.SizeOf<Vector2f>());
        Assert.Equal(16, Unsafe.SizeOf<Vector2d>());
    }

    [Fact]
    public void ImplementingThePrimitivesGivesAnotherVectorTheShared2DOperations()
    {
        var value = new TestVector2<float>(3, 4);
        Assert.Equal(5, value.Length());
        Assert.Equal(new TestVector2<float>(-4, 3), value.PerpCcw);
        Assert.Equal(25, value.Cross(value.PerpCcw));
        Assert.Equal(new TestVector2<float>(1, 0), TestVector2<float>.Direction(0));
        Assert.Equal(value, new Matrix2x2<TestVector2<float>, float>(1, 0, 0, 1) * value);
        var polar = value.ToPolar();
        Assert.Equal(5, polar.Radius);
        Assert.InRange(Vector2f.Distance(value.ToVector2f(), polar.ToCartesian<TestVector2<float>>().ToVector2f()), 0, 1e-6f);
        Assert.True(TestVector2<float>.TryRefract(new(0, -1), new(0, 1), 1, 1.5f, out var direction));
        Assert.Equal(new TestVector2<float>(0, -1), direction);
    }

    [Fact]
    public void ConcreteAndSelfTyped2DCalculationsDoNotAllocate()
    {
        for (var i = 0; i < 100; i++) _ = Calculate(i);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var total = 0d;
        for (var i = 0; i < 1000; i++) total += Calculate(i);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.True(double.IsFinite(total));

        static double Calculate(int i)
        {
            var value = TestVector2<double>.FromPolar(3, i * .01);
            var moved = Move<TestVector2<double>, double>(value, value.PerpCcw, 2);
            return moved.Length() + moved.Cross(value) + moved.ToPolar().Radius;
        }
    }

    private static TSelf Move<TSelf, TReal>(TSelf value, TSelf offset, TReal scale)
        where TSelf : unmanaged, IVector2<TSelf, TReal>
        where TReal : unmanaged, IFloatingPointIeee754<TReal> => (value + offset) * scale;
}
