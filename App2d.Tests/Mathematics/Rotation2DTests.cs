using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Tests.Mathematics;

public sealed class Rotation2DTests
{
    [Fact]
    public void RotateAroundAndVectorExtensionsUseTheSharedRotation()
    {
        var pivot = new Vector2(1, 1);
        var point = new Vector2(2, 1);
        var turned = Rotation2D.ApplyAround(point, pivot, MathF.PI / 2);
        Assert.InRange(Vector2.Distance(new(1, 2), turned), 0, 1e-6f);
        Assert.Equal(turned, point.RotateAround(pivot, MathF.PI / 2));
        Assert.Equal(Rotation2D.Apply(point, MathF.PI / 2), point.Rotate(MathF.PI / 2));
        Assert.Equal(new Vector3(turned, 7), Rotation2D.ApplyXYAround(new(point, 7), pivot, MathF.PI / 2));
    }

    [Fact]
    public void PivotSolveSatisfiesTheRotationEquation()
    {
        var from = new Vector2(2, 1);
        var to = new Vector2(1, 2);
        Assert.True(Rotation2D.TryFindPivot(from, to, MathF.PI / 2, out var pivot));
        Assert.InRange(Vector2.Distance(new(1, 1), pivot), 0, 1e-6f);
        Assert.InRange(Vector2.Distance(to, Rotation2D.ApplyAround(from, pivot, MathF.PI / 2)), 0, 1e-6f);

        var arbitraryPivot = new Vector2(-3, 4);
        to = Rotation2D.ApplyAround(from, arbitraryPivot, -1.2f);
        Assert.True(Rotation2D.TryFindPivot(from, to, -1.2f, out pivot));
        Assert.InRange(Vector2.Distance(arbitraryPivot, pivot), 0, 2e-6f);
    }

    [Fact]
    public void SingularTurnsDoNotInventAPivot()
    {
        Assert.False(Rotation2D.TryFindPivot(Vector2.Zero, Vector2.One, 0, out _));
        Assert.False(Rotation2D.TryFindPivot(Vector2.Zero, Vector2.One, MathF.Tau, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Rotation2D.TryFindPivot(Vector2.Zero, Vector2.One, float.NaN, out _));
    }

    [Fact]
    public void EndpointArcInterpolationPreservesShortAndLongTurns()
    {
        var shortMiddle = Rotation2D.InterpolateArc(Vector2.UnitX, Vector2.UnitY, MathF.PI / 2, .5f);
        Assert.InRange(Vector2.Distance(new(MathF.Sqrt(.5f)), shortMiddle), 0, 1e-6f);
        var longMiddle = Rotation2D.InterpolateArc(Vector2.UnitX, -Vector2.UnitY, 3 * MathF.PI / 2, .5f);
        Assert.InRange(Vector2.Distance(new(-MathF.Sqrt(.5f), MathF.Sqrt(.5f)), longMiddle), 0, 1e-6f);
        Assert.Equal(Vector2.UnitX, Rotation2D.InterpolateArc(Vector2.UnitX, Vector2.UnitY, MathF.PI / 2, 0));
        Assert.Equal(Vector2.UnitY, Rotation2D.InterpolateArc(Vector2.UnitX, Vector2.UnitY, MathF.PI / 2, 1));
    }

    [Fact]
    public void NearlySingularArcFallsBackToItsChord()
    {
        var from = new Vector2(2, 3);
        var to = new Vector2(8, 9);
        Assert.Equal(new Vector2(5, 6), Rotation2D.InterpolateArc(from, to, 0, .5f));
        Assert.Equal(new Vector2(5, 6), Rotation2D.InterpolateArc(from, to, MathF.Tau, .5f));
    }

    [Fact]
    public void ArcFractionsOutsideTheStepReturnTheNearestEndpoint()
    {
        var from = new Vector2(2, 3);
        var to = new Vector2(8, 9);
        Assert.Equal(from, Rotation2D.InterpolateArc(from, to, MathF.PI / 2, -.25f));
        Assert.Equal(to, Rotation2D.InterpolateArc(from, to, MathF.PI / 2, 1.25f));
        Assert.Throws<ArgumentOutOfRangeException>(() => Rotation2D.InterpolateArc(from, to, 0, float.NaN));
    }

    [Fact]
    public void ArcDisplacementUsesDoublePrecisionBeforeSubtractingLargeEndpoints()
    {
        var middle = Rotation2D.InterpolateArc(new(-2e38f, 0), new(2e38f, 0), MathF.PI, .5f);
        Assert.True(float.IsFinite(middle.X) && float.IsFinite(middle.Y));
        Assert.InRange(MathF.Abs(middle.X), 0f, 1e32f);
        Assert.InRange(middle.Y / 2e38f, -1.001f, -.999f);
    }

    [Fact]
    public void Vector3PlaneProjectionsKeepTheRequestedCoordinates()
    {
        var vector = new Vector3(2, 3, 5);
        Assert.Equal(new Vector2(2, 3), vector.XY);
        Assert.Equal(new Vector2(2, 5), vector.XZ);
        Assert.Equal(new Vector2(3, 5), vector.YZ);
    }
}
