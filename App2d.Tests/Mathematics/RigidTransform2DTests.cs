using App2d.Core.Mathematics.Transforms;

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
        foreach (var point in new[] { Vector2f.Zero, Vector2f.UnitX, Vector2f.UnitY, new Vector2f(3, -4) })
        {
            var expected = Vector2f.Transform(point, local.Matrix * parent.Matrix);
            Assert.InRange(Vector2f.Distance(expected, composed.TransformPoint(point)), 0, 2e-5f);
            Assert.InRange(Vector2f.Distance(point, composed.Inverse().TransformPoint(composed.TransformPoint(point))), 0, 2e-5f);
            Assert.Equal(point, default(RigidTransform2D).TransformPoint(point));
        }
        Assert.Equal(local.TransformDirection(Vector2f.UnitX), (local with { Translation = new(20, 30) }).TransformDirection(Vector2f.UnitX));
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

    [Fact]
    public void FractionalDegreeConversionsUseTheRequestedPrecision()
    {
        Assert.Equal(0.5f * Angles.RadiansPerDegree, Angles.DegreesToRadians(0.5f), 7);
        Assert.Equal(Math.PI / 360, Angles.DegreesToRadians(0.5d), 15);
        Assert.Equal(0.5d, Angles.RadiansToDegrees(Math.PI / 360), 14);
    }

    [Fact]
    public void PositiveWrappingAndExactTrigPreserveTheFullTurnSeam()
    {
        Assert.Equal(3f * MathF.PI / 2, Angles.WrapPositive(-MathF.PI / 2));
        Assert.Equal(0d, Angles.WrapPositive(Math.Tau));
        Assert.Equal(Math.Tau, Angles.WrapPositive(-double.Epsilon));
        Assert.Equal((0d, 1d), Angles.SinCosAtTurnEndpoint(0d));
        Assert.Equal((0d, 1d), Angles.SinCosAtTurnEndpoint(Math.Tau));
        Assert.Equal((0f, 1f), Angles.SinCosAtTurnEndpoint(MathF.Tau));
        Assert.Equal(Math.SinCos(0.5d), Angles.SinCosAtTurnEndpoint(0.5d));
    }
}
