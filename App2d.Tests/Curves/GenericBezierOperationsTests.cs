using App2d.Core.Geometry.Curves;
using App2d.Core.Mathematics.Curves;

namespace App2d.Tests.Curves;

/// <summary>Exact subdivision lives on the generic Béziers, so it works for scalars and every dimension.</summary>
public sealed class GenericBezierOperationsTests
{
    private static readonly CubicBezier<double, double> Scalar = new(0d, .2d, .9d, 1d);
    private static readonly CubicBezier<Vector3f, float> Spatial = new(new(0, 1, -2), new(1, 2, 3), new(3, -1, -4), new(4, 2, 1));

    [Fact]
    public void SplitHalvesReproduceAScalarCubic()
    {
        var (left, right) = Scalar.Split(.3d);

        for (var i = 0; i <= 4; i++)
        {
            var u = i / 4d;
            Assert.Equal(Scalar.Evaluate(.3d * u), left.Evaluate(u), 12);
            Assert.Equal(Scalar.Evaluate(.3d + .7d * u), right.Evaluate(u), 12);
        }
    }

    [Fact]
    public void SplitHalvesReproduceASpatialCubicAndAQuadratic()
    {
        var (left, right) = Spatial.Split(.6f);
        var quadratic = new QuadraticBezier<Vector3f, float>(new(0, 1, -2), new(1, 2, 3), new(3, -1, -4));
        var (first, second) = quadratic.Split(.25f);

        for (var i = 0; i <= 4; i++)
        {
            var u = i / 4f;
            Assert.InRange((Spatial.Evaluate(.6f * u) - left.Evaluate(u)).Length(), 0, 1e-5f);
            Assert.InRange((Spatial.Evaluate(.6f + .4f * u) - right.Evaluate(u)).Length(), 0, 1e-5f);
            Assert.InRange((quadratic.Evaluate(.25f * u) - first.Evaluate(u)).Length(), 0, 1e-5f);
            Assert.InRange((quadratic.Evaluate(.25f + .75f * u) - second.Evaluate(u)).Length(), 0, 1e-5f);
        }
    }

    [Fact]
    public void SliceMatchesTheSourceForwardReversedAndEmpty()
    {
        var forward = Scalar.Slice(.2d, .8d);
        var reversed = Scalar.Slice(.8d, .2d);
        var empty = Scalar.Slice(.4d, .4d);
        var quadratic = new QuadraticBezier<double, double>(0d, 0d, 1d).Slice(.5d, 1d);

        for (var i = 0; i <= 4; i++)
        {
            var u = i / 4d;
            Assert.Equal(Scalar.Evaluate(.2d + .6d * u), forward.Evaluate(u), 12);
            Assert.Equal(Scalar.Evaluate(.8d - .6d * u), reversed.Evaluate(u), 12);
            Assert.Equal(Scalar.Evaluate(.4d), empty.Evaluate(u), 12);
            Assert.Equal((.5d + .5d * u) * (.5d + .5d * u), quadratic.Evaluate(u), 12);
        }
    }

    [Fact]
    public void SplitAtTheEndsGivesAPointAndTheWholeCurve()
    {
        var (Left0, Right0) = Scalar.Split(0d);
        var (Left1, Right1) = Scalar.Split(1d);

        Assert.Equal(new CubicBezier<double, double>(0d, 0d, 0d, 0d), Left0);
        Assert.Equal(Scalar, Right0);
        Assert.Equal(Scalar, Left1);
        Assert.Equal(new CubicBezier<double, double>(1d, 1d, 1d, 1d), Right1);
    }

    [Fact]
    public void HermiteFormKeepsItsEndpointsAndTangents()
    {
        var curve = CubicBezier<Vector3f, float>.FromHermite(new(0, 0, 0), new(4, 2, 1), new(3, 0, 6), new(0, 9, -3));

        Assert.Equal(new Vector3f(0, 0, 0), curve.Evaluate(0));
        Assert.Equal(new Vector3f(4, 2, 1), curve.Evaluate(1));
        Assert.InRange((curve.Derivative(0) - new Vector3f(3, 0, 6)).Length(), 0, 1e-5f);
        Assert.InRange((curve.Derivative(1) - new Vector3f(0, 9, -3)).Length(), 0, 1e-5f);
    }

    [Fact]
    public void TheTwoDimensionalCubicUsesTheSameSubdivision()
    {
        var planar = CubicBezier2D.Builder.Create(new(1, 2), new(3, 8), new(7, 5), new(9, -3));
        var generic = new CubicBezier<Vector2f, float>(planar.Start, planar.Control1, planar.Control2, planar.End);

        var (left, right) = planar.Split(.3f);
        var (genericLeft, genericRight) = generic.Split(.3f);

        Assert.Equal(genericLeft.Control2, left.Control2);
        Assert.Equal(genericRight.Control1, right.Control1);
        Assert.Equal(generic.Slice(.2f, .8f).Control1, planar.Slice(.2f, .8f).Control1);
    }
}
