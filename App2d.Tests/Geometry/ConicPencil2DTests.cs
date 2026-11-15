using App2d.Core.Geometry.Operations.Construction;

namespace App2d.Tests.Geometry;

public sealed class ConicPencil2DTests
{
    [Fact]
    public void TurningTheKnobChangesTheCurveAndKeepsAllFourPoints()
    {
        Vector2f[] points = [new(-1, -1), new(1, -1), new(1, 1), new(-1, 1)];
        var pencil = new ConicPencil2D(points[0], points[1], points[2], points[3]);

        Assert.Equal(pencil.First, pencil.At(0d));
        Assert.Equal(pencil.Second, pencil.At(.5d));
        Assert.Equal(pencil.First, pencil.At(1d));
        Assert.True(pencil.At(.25d).QuadraticDeterminant > 0d);
        Assert.True(pencil.At(.625d).QuadraticDeterminant < 0d);
        Assert.Equal(0d, pencil.At(0d).QuadraticDeterminant);

        foreach (var knob in new[] { 0d, .125d, .25d, .5d, .625d, .75d, 1d })
        {
            var conic = pencil.At(knob);
            Assert.All(points, point => Assert.Equal(0d, conic.Evaluate(point), 12));
        }
    }

    [Fact]
    public void AWeightRatioOrFifthPointCanSelectAParabola()
    {
        // All five points lie on y = x². The two line products give that parabola at weights 1 : 1/8.
        Vector2f[] points = [new(-2, 4), new(-1, 1), new(1, 1), new(2, 4), Vector2f.Zero];
        var pencil = new ConicPencil2D(points[0], points[1], points[2], points[3]);
        var byWeight = pencil.AtWeights(1d, .125d);
        var byPoint = pencil.ThroughPoint(points[4]);

        Assert.Equal(0d, byWeight.QuadraticDeterminant);
        Assert.Equal(0d, byPoint.QuadraticDeterminant);
        Assert.All(points, point =>
        {
            Assert.Equal(0d, byWeight.Evaluate(point));
            Assert.Equal(0d, byPoint.Evaluate(point));
        });
        Assert.Equal(0d, byPoint.Evaluate(new(3, 9)));
        Assert.NotEqual(0d, byPoint.Evaluate(new(3, 8)));
    }

    [Fact]
    public void ATranslatedPencilEvaluatesInItsLocalCoordinates()
    {
        var offset = new Vector2f(1e9f, -1e9f);
        Vector2f[] points = [offset + new Vector2f(-128, -128), offset + new Vector2f(128, -128),
            offset + new Vector2f(128, 128), offset + new Vector2f(-128, 128)];
        var pencil = new ConicPencil2D(points[0], points[1], points[2], points[3]);
        var conic = pencil.AtWeights(double.MaxValue, double.MaxValue);

        Assert.All(points, point => Assert.Equal(0d, conic.Evaluate(point)));
        Assert.True(double.IsFinite(conic.Evaluate(offset)));
        Assert.NotEqual(0d, conic.Evaluate(offset));
    }

    [Fact]
    public void RejectsInputsThatDoNotDefineAUniquePencilOrMember()
    {
        Assert.Throws<ArgumentException>(() => new ConicPencil2D(new(0, 0), new(1, 0), new(2, 0), new(0, 1)));

        var pencil = new ConicPencil2D(new(0, 0), new(1, 0), new(1, 1), new(0, 1));
        Assert.Throws<ArgumentException>(() => pencil.ThroughPoint(new(1, 1)));
        Assert.Throws<ArgumentException>(() => pencil.AtWeights(0d, 0d));
        Assert.Throws<ArgumentOutOfRangeException>(() => pencil.At(double.NaN));
    }
}
