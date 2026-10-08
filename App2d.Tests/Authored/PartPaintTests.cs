using App2d.Core.Characters;

namespace App2d.Tests.Authored;

public sealed class PartPaintTests
{
    [Fact]
    public void AcceptsConvexPaintInEitherWindingAndCollinearEdges()
    {
        PuppetPoint[] points = [new(0, 0), new(1, 0), new(2, 0), new(2, 1), new(0, 1)];
        PartPaint.Check([new() { Material = new() { Fill = "#ffffff" }, Points = [.. points] }]);
        PartPaint.Check([new() { Material = new() { Fill = "#ffffff" }, Points = [.. points.Reverse()] }]);
        PartPaint.Check([new() { Material = new() { Fill = "#ffffff" }, Points = [new(0, 0), new(1, 0), new(1, 0), new(0, 1)] }]);
        PartPaint.Check([new() { Material = new() { Fill = "#ffffff" }, Points = [new(0, 0), new(0.0001f, 0), new(0, 0.0002f)] }]);
    }

    [Fact]
    public void RejectsConcaveAndDegeneratePaint()
    {
        Assert.Throws<InvalidDataException>(() => PartPaint.Check(
            [new() { Material = new() { Fill = "#ffffff" }, Points = [new(0, 0), new(2, 0), new(1, 0.25f), new(2, 1), new(0, 1)] }]));
        Assert.Throws<InvalidDataException>(() => PartPaint.Check(
            [new() { Material = new() { Fill = "#ffffff" }, Points = [new(0, 0), new(1, 0), new(2, 0)] }]));
    }

    [Fact]
    public void RejectsPaintWithDepth()
    {
        Assert.Throws<InvalidDataException>(() => PartPaint.Check(
            [new() { Material = new() { Fill = "#ffffff" }, Points = [new(0, 0), new(1, 0, 1), new(0, 1)] }]));
    }
}
