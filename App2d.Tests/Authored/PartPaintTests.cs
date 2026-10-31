using App2d.Core.Characters;
using App2d.Core.Json;
using System.Numerics;
using System.Text.Json;

namespace App2d.Tests.Authored;

public sealed class PartPaintTests
{
    [Fact]
    public void AcceptsConvexPaintInEitherWindingAndCollinearEdges()
    {
        Vector2[] points = [new(0, 0), new(1, 0), new(2, 0), new(2, 1), new(0, 1)];
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

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void RejectsThreeDimensionalPaintCoordinates(int depth)
    {
        var json = $$"""
            {"material":{"fill":"#ffffff"},"points":[{"x":0,"y":0},{"x":1,"y":0,"z":{{depth}}},{"x":0,"y":1}]}
            """;
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PartPaint>(json, AuthoredJson.Options));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TwoDimensionalPaintRoundTrips(bool puppet)
    {
        const string json = """
            {"material":{"fill":"#ffffff"},"points":[{"x":0,"y":0},{"x":1,"y":0},{"x":0,"y":1}]}
            """;
        var options = puppet ? PuppetDefinition.JsonOptions : AuthoredJson.Options;
        var patch = JsonSerializer.Deserialize<PartPaint>(json, options)!;
        PartPaint.Check([patch]);
        Assert.Equal(new[] { Vector2.Zero, Vector2.UnitX, Vector2.UnitY }, patch.Points);

        var saved = JsonSerializer.Serialize(patch, options);
        using var document = JsonDocument.Parse(saved);
        Assert.Equal(["x", "y"], document.RootElement.GetProperty("points")[0].EnumerateObject().Select(p => p.Name));
        Assert.Equal(patch.Points, JsonSerializer.Deserialize<PartPaint>(saved, options)!.Points);
    }
}
