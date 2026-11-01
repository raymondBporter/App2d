using App2d.Core.Mathematics.Transforms;
using System.Numerics;

namespace App2d.Tests.Mathematics;

public sealed class SegmentTurn3DTests
{
    [Fact]
    public void APlanarQuarterTurnKeepsTheTipOnItsArc()
    {
        var turn = SegmentTurn3D.Between(Vector3.Zero, Vector3.UnitX, Vector3.Zero, Vector3.UnitY, SegmentTurn3D.None);
        Assert.True(turn.Planar);
        Assert.Equal(MathF.PI / 2, turn.Angle, 5);
        var (_, tip) = turn.Interpolate(Vector3.Zero, Vector3.UnitX, Vector3.Zero, Vector3.UnitY, .5f);
        Assert.Equal(1, tip.Length(), 5);
        Assert.Equal(MathF.PI / 2, turn.ArcLength(Vector3.UnitX, Vector3.UnitY), 4);
    }

    [Fact]
    public void SweepPicksTheLongWayRoundOnTheFirstStep()
    {
        // 225 degrees clockwise reads as 135 counter-clockwise unless the sweep says otherwise.
        var to = new Vector3(-1, 1, 0);
        var shortest = SegmentTurn3D.Between(Vector3.Zero, Vector3.UnitX, Vector3.Zero, to, SegmentTurn3D.None);
        var clockwise = SegmentTurn3D.Between(Vector3.Zero, Vector3.UnitX, Vector3.Zero, to, SegmentTurn3D.None, sweep: -1);
        Assert.Equal(3 * MathF.PI / 4, shortest.Angle, 5);
        Assert.Equal(-5 * MathF.PI / 4, clockwise.Angle, 5);
    }

    [Fact]
    public void ATurnThroughDepthUsesTheSharedAxis()
    {
        var turn = SegmentTurn3D.Between(Vector3.Zero, Vector3.UnitX, Vector3.Zero, Vector3.UnitZ, SegmentTurn3D.None);
        Assert.False(turn.Planar);
        Assert.Equal(MathF.PI / 2, turn.Angle, 5);
        var (_, tip) = turn.Interpolate(Vector3.Zero, Vector3.UnitX, Vector3.Zero, Vector3.UnitZ, .5f);
        Assert.True(Vector3.Distance(Vector3.Normalize(new(1, 0, 1)), tip) < 1e-5f);
    }
}
