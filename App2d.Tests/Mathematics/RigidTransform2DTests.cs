using App2d.Core.Mathematics;
using App2d.Core.Mathematics.Transforms;
using System.Numerics;

namespace App2d.Tests.Mathematics;

public sealed class RigidTransform2DTests
{
    [Fact]
    public void CompositionAndInverseMatchRowVectorMatricesAndRetainFullTurns()
    {
        var local = new RigidTransform2D(new(2, -3), 2 * MathF.Tau + .4f);
        var parent = new RigidTransform2D(new(-5, 7), -.9f);
        var composed = local.Then(parent);
        Assert.Equal(local.Rotation + parent.Rotation, composed.Rotation);
        foreach (var point in new[] { Vector2.Zero, Vector2.UnitX, Vector2.UnitY, new Vector2(3, -4) })
        {
            var expected = Vector2.Transform(point, local.Matrix * parent.Matrix);
            Assert.InRange(Vector2.Distance(expected, composed.TransformPoint(point)), 0, 2e-5f);
            Assert.InRange(Vector2.Distance(point, composed.Inverse().TransformPoint(composed.TransformPoint(point))), 0, 2e-5f);
            Assert.Equal(point, default(RigidTransform2D).TransformPoint(point));
        }
        Assert.Equal(local.TransformDirection(Vector2.UnitX), (local with { Translation = new(20, 30) }).TransformDirection(Vector2.UnitX));
    }

    [Fact]
    public void SignedWrappingPreservesPhaseAndBothHalfTurnRepresentatives()
    {
        for (var turns = -8; turns <= 8; turns++)
        {
            Assert.InRange(MathF.Abs(Angles.WrapSigned(.7f + turns * MathF.Tau) - .7f), 0, 3e-6f);
            Assert.InRange(Math.Abs(Angles.WrapSigned(.7 + turns * Math.Tau) - .7), 0, 5e-15);
        }
        Assert.Equal(MathF.PI, Angles.WrapSigned(MathF.PI));
        Assert.Equal(-MathF.PI, Angles.WrapSigned(-MathF.PI));
        Assert.Equal(Math.PI, Angles.WrapSigned(Math.PI));
        Assert.Equal(-Math.PI, Angles.WrapSigned(-Math.PI));
        Assert.True(float.IsNaN(Angles.WrapSigned(float.PositiveInfinity)));
        Assert.True(double.IsNaN(Angles.WrapSigned(double.NaN)));
    }
}
