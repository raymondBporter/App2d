using App2d.Core.Geometry;
using App2d.Core.Shapes;
using System.Numerics;
using System.Text.Json.Nodes;

namespace App2d.Tests.Geometry;

public sealed class ShapeArea2DTests
{
    [Theory]
    [InlineData(1f, 1f, 0f)]
    [InlineData(1f, 1f, 1f)]
    [InlineData(1f, 1f, 2f)]
    [InlineData(1f, 1f, 3f)]
    [InlineData(2f, 1f, .5f)]
    [InlineData(2f, 1f, 1f)]
    [InlineData(2f, 1f, 2f)]
    [InlineData(2f, 1f, 3f)]
    public void CirclePairsMatchTheLensFormula(float firstRadius, float secondRadius, float distance)
    {
        IConvexShape2D[] parts = [new Circle2D(firstRadius), new Circle2D(secondRadius, new(distance, 0))];
        var expected = Math.PI * (firstRadius * firstRadius + secondRadius * secondRadius) - CircleLens(firstRadius, secondRadius, distance);
        Assert.Equal((float)expected, ShapeArea2D.Union(parts), 5);
        Assert.Equal(ShapeArea2D.Union(parts), ShapeArea2D.Union([parts[1], parts[0]]));
    }

    [Fact]
    public void ThreeOverlappingCirclesCountTheTripleIntersectionOnce()
    {
        IConvexShape2D[] parts = [new Circle2D(1), new Circle2D(1, new(1, 0)), new Circle2D(1, new(.5f, MathF.Sqrt(3) / 2))];
        Assert.Equal((float)(1.5 * Math.PI + Math.Sqrt(3)), ShapeArea2D.Union(parts), 5);
        Assert.Equal(ShapeArea2D.Union(parts), ShapeArea2D.Union([parts[2], parts[0], parts[1]]), 5);
    }

    [Fact]
    public void ThreeCirclesCanLeaveAHole()
    {
        const float side = 1.9f;
        IConvexShape2D[] parts = [new Circle2D(1), new Circle2D(1, new(side, 0)), new Circle2D(1, new(side / 2, side * MathF.Sqrt(3) / 2))];
        Assert.Equal((float)(3 * Math.PI - 3 * CircleLens(1, 1, side)), ShapeArea2D.Union(parts), 5);
    }

    [Fact]
    public void DuplicateAndContainedCirclesDoNotContributeExtraArea()
    {
        IConvexShape2D[] parts = [new Circle2D(3), new Circle2D(3), new Circle2D(1), new Circle2D(1, new(1, 0))];
        Assert.Equal(9 * MathF.PI, ShapeArea2D.Union(parts), 5);
    }

    [Fact]
    public void CircleAndPolygonUnionsIntegrateTheExposedCircularArcs()
    {
        var circle = new Circle2D(1);
        Assert.Equal(4 + MathF.PI / 2, ShapeArea2D.Union([circle, new Rectangle2D(new(0, -1), new(2, 1))]), 5);
        Assert.Equal(2 + 3 * MathF.PI / 4, ShapeArea2D.Union([circle, new Triangle2D(new(0, 0), new(2, 0), new(0, 2))]), 5);
        Assert.Equal(8 + MathF.PI / 2, ShapeArea2D.Union([circle, new Triangle2D(new(-2, -2), new(2, 2), new(-2, 2))]), 5);
        Assert.Equal(MathF.PI, ShapeArea2D.Union([circle, new Rectangle2D(new(-.5f, -.5f), new(.5f, .5f))]), 5);
        Assert.Equal(16, ShapeArea2D.Union([circle, new Rectangle2D(new(-2, -2), new(2, 2))]), 5);
        Assert.Equal(4 + MathF.PI, ShapeArea2D.Union([circle, new Rectangle2D(new(1, -1), new(3, 1))]), 5);
    }

    [Fact]
    public void RectanglesAndCoincidentPolygonEdgesCountOnceInEitherWinding()
    {
        var rectangle = new Rectangle2D(new(0, 0), new(2, 2));
        var reversed = new ConvexPolygon2D([new(0, 0), new(0, 2), new(2, 2), new(2, 0)]);
        var partial = new ConvexPolygon2D([new(1, 0), new(3, 0), new(3, 2), new(1, 2)]);
        Assert.Equal(6, ShapeArea2D.Union([rectangle, reversed, partial]));
        Assert.Equal(6, ShapeArea2D.Union([partial, reversed, rectangle]));
        Assert.Equal(6, Area2D.RectangleUnion(rectangle.Min, rectangle.Max, new(1, 0), new(3, 2)));
        Assert.Equal(4, Area2D.RectangleUnion(rectangle.Min, rectangle.Max, rectangle.Min, rectangle.Max));
        Assert.Equal(8, ShapeArea2D.Union([rectangle, new Rectangle2D(new(2, 0), new(4, 2))]));
    }

    [Fact]
    public void OverlappingRectangleFrameLeavesItsHoleEmpty()
    {
        IConvexShape2D[] parts =
        [
            new Rectangle2D(new(0, 0), new(3, 1)), new Rectangle2D(new(0, 2), new(3, 3)),
            new Rectangle2D(new(0, 0), new(1, 3)), new Rectangle2D(new(2, 0), new(3, 3))
        ];
        Assert.Equal(8, ShapeArea2D.Union(parts));
    }

    [Fact]
    public void RandomPolygonPairsAgreeWithConvexClipping()
    {
        var random = new Random(23147);
        for (var sample = 0; sample < 100; sample++)
        {
            var a = RandomPolygon(random);
            var b = RandomPolygon(random);
            var clipped = PolygonClipping2D.ClipConvex(a.Vertices.ToArray(), b.Vertices.ToArray());
            var expected = a.Area + b.Area - Area2D.Polygon([.. clipped]);
            Assert.InRange(MathF.Abs(ShapeArea2D.Union([a, b]) - expected), 0, 2e-5f);
        }
    }

    [Fact]
    public void RandomMixedUnionsAgreeWithIndependentCrossSectionIntegration()
    {
        var random = new Random(62048);
        for (var sample = 0; sample < 16; sample++)
        {
            IConvexShape2D[] parts = new IConvexShape2D[6];
            for (var i = 0; i < parts.Length; i++)
            {
                var center = new Vector2((float)random.NextDouble() * 4 - 2, (float)random.NextDouble() * 4 - 2);
                parts[i] = i % 2 == 0 ? new Circle2D(.3f + (float)random.NextDouble(), center)
                    : Rectangle2D.FromSize(new Vector2(.5f + (float)random.NextDouble(), .5f + (float)random.NextDouble()), center);
            }
            var expected = CrossSectionArea(parts);
            Assert.InRange(Math.Abs(ShapeArea2D.Union(parts) - expected), 0d, .002d);
        }
    }

    [Fact]
    public void ACommonLargeTranslationDoesNotChangeArea()
    {
        var offset = new Vector2(1 << 20, -(1 << 20));
        var original = ShapeArea2D.Union([new Circle2D(1), new Rectangle2D(new(0, -1), new(2, 1)), new Circle2D(1, new(1, 0))]);
        var moved = ShapeArea2D.Union([new Circle2D(1, offset), new Rectangle2D(offset + new Vector2(0, -1), offset + new Vector2(2, 1)), new Circle2D(1, offset + Vector2.UnitX)]);
        Assert.Equal(original, moved);
        Assert.Equal(2 * MathF.PI, ShapeArea2D.Union([new Circle2D(1), new Circle2D(1, new(1e30f, 0))]), 5);
    }

    [Theory]
    [InlineData(1f, 1f, 0f)]
    [InlineData(1f, 1f, 1f)]
    [InlineData(1f, 1f, 2f)]
    [InlineData(1f, 1f, 3f)]
    [InlineData(2f, 1f, .5f)]
    [InlineData(2f, 1f, 1f)]
    [InlineData(2f, 1f, 2f)]
    public void EllipsePairsAgreeWithAnAffineTransformationOfTheCircleLens(float firstRadius, float secondRadius, float distance)
    {
        var scale = new Vector2(3, .5f);
        IConvexShape2D[] parts = [new Ellipse2D(firstRadius * scale), new Ellipse2D(secondRadius * scale, new(distance * scale.X, 0))];
        var expected = scale.X * scale.Y * (Math.PI * (firstRadius * firstRadius + secondRadius * secondRadius) - CircleLens(firstRadius, secondRadius, distance));
        Assert.Equal((float)expected, ShapeArea2D.Union(parts, 3), 5);
        Assert.Equal(ShapeArea2D.Union(parts), ShapeArea2D.Union([parts[1], parts[0]]), 5);
        Assert.Equal(ShapeArea2D.Union(parts, 3), ShapeArea2D.Union(parts, 128));
    }

    [Theory]
    [InlineData(2f, 1f)]
    [InlineData(50f, .01f)]
    [InlineData(1.00001f, 1f)]
    public void CrossingEllipsesWithFourIntersectionsKeepEveryExposedArc(float a, float b)
    {
        var first = new Ellipse2D(new(a, b));
        var second = new Ellipse2D(new(b, a));
        var expected = (float)(4d * a * b * Math.Atan2(a, b));
        Assert.Equal(expected, ShapeArea2D.Union([first, second]), 5);
        Assert.Equal(expected, ShapeArea2D.Union([second, first]), 5);
    }

    [Fact]
    public void EllipseAndPolygonUnionsUseExactSegmentIntersections()
    {
        var ellipse = new Ellipse2D(new(2, 1));
        Assert.Equal(8 + MathF.PI, ShapeArea2D.Union([ellipse, new Rectangle2D(new(0, -1), new(4, 1))]), 5);
        Assert.Equal(4 + 1.5f * MathF.PI, ShapeArea2D.Union([ellipse, new Triangle2D(new(0, 0), new(4, 0), new(0, 2))]), 5);
        Assert.Equal(2 * MathF.PI, ShapeArea2D.Union([ellipse, new Rectangle2D(new(-.5f, -.5f), new(.5f, .5f))]), 5);
        Assert.Equal(32, ShapeArea2D.Union([ellipse, new Rectangle2D(new(-4, -2), new(4, 2))]));
        Assert.Equal(8 + 2 * MathF.PI, ShapeArea2D.Union([ellipse, new Rectangle2D(new(2, -1), new(6, 1))]), 5);
        // These polygon edges' infinite lines reach the ellipse; their finite segments do not.
        var separate = new Rectangle2D(new(1.8f, .8f), new(3, 2));
        Assert.Equal(ellipse.Area + separate.Area, ShapeArea2D.Union([ellipse, separate]), 5);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.9f)]
    public void EllipseTriplesPreserveTripleCoverageAndHoles(float side)
    {
        var radii = new Vector2(2, .5f);
        IConvexShape2D[] parts =
        [
            new Ellipse2D(radii), new Ellipse2D(radii, new(2 * side, 0)),
            new Ellipse2D(radii, new(side, side * MathF.Sqrt(3) / 4))
        ];
        var expected = side == 1 ? 1.5 * Math.PI + Math.Sqrt(3) : 3 * Math.PI - 3 * CircleLens(1, 1, side);
        Assert.Equal((float)expected, ShapeArea2D.Union(parts), 5);
        Assert.Equal(ShapeArea2D.Union(parts), ShapeArea2D.Union([parts[2], parts[0], parts[1]]), 5);
    }

    [Fact]
    public void EllipseContainmentTangencyAndCoincidentBoundariesCountOnce()
    {
        var ellipse = new Ellipse2D(new(2, 1));
        Assert.Equal(ellipse.Area, ShapeArea2D.Union([ellipse, new Ellipse2D(new(2, 1)), new Ellipse2D(new(1, .5f)), new Circle2D(.25f)]), 5);
        Assert.Equal(ellipse.Area, ShapeArea2D.Union([ellipse, new Ellipse2D(new(1, .5f), new(1, 0))]), 5);
        Assert.Equal(ellipse.Area, ShapeArea2D.Union([ellipse, new Circle2D(1)]), 5);
        Assert.Equal(MathF.PI, ShapeArea2D.Union([new Circle2D(1), new Ellipse2D(Vector2.One)]), 5);
        Assert.Equal(ellipse.Area, ShapeArea2D.Union([ellipse, new Ellipse2D(new(2, 1), new(1e-25f, 0))]), 5);
        Assert.Equal(ellipse.Area, ShapeArea2D.Union([ellipse, new Ellipse2D(new(2, 1), new(0, 1e-25f))]), 5);
        Assert.Equal(ellipse.Area, ShapeArea2D.Union([ellipse, new Ellipse2D(new(2, 1), new(1e-25f, 1e-25f))]), 5);
        Assert.Equal(2 * ellipse.Area, ShapeArea2D.Union([ellipse, new Ellipse2D(new(2, 1), new(0, 2))]), 5);
        var translated = new Ellipse2D(new(2, 1), new(1e20f, -1e20f));
        Assert.Equal(ellipse.Area, ShapeArea2D.Union([translated, translated]), 5);
    }

    [Fact]
    public void EllipseUnionIsInvariantUnderTranslationAndUniformScaling()
    {
        var offset = new Vector2(1 << 20, -(1 << 20));
        var expected = ShapeArea2D.Union([new Ellipse2D(new(2, 1)), new Circle2D(1, new(1, 0)), new Rectangle2D(new(0, -1), new(2, 1))]);
        Assert.Equal(expected, ShapeArea2D.Union([new Ellipse2D(new(2, 1), offset), new Circle2D(1, offset + Vector2.UnitX), new Rectangle2D(offset + new Vector2(0, -1), offset + new Vector2(2, 1))]));
        foreach (var scale in new[] { 1e-8f, 1e8f })
        {
            var actual = ShapeArea2D.Union([new Ellipse2D(scale * new Vector2(2, 1)), new Circle2D(scale, new(scale, 0)), new Rectangle2D(scale * new Vector2(0, -1), scale * new Vector2(2, 1))]);
            Assert.InRange(MathF.Abs(actual / scale / scale - expected), 0, 2e-6f);
        }
    }

    [Fact]
    public void MixedEllipseUnionsAgreeWithIndependentCrossSectionIntegration()
    {
        var random = new Random(77613);
        // Includes four circle/ellipse intersections, very thin ellipses, and multiple overlaps.
        Check([new Ellipse2D(new(2, .5f)), new Circle2D(1)]);
        Check([new Ellipse2D(new(10, .02f)), new Circle2D(.25f, new(3, 0))]);
        for (var sample = 0; sample < 32; sample++)
        {
            IConvexShape2D[] parts = new IConvexShape2D[6];
            for (var i = 0; i < parts.Length; i++)
            {
                var center = new Vector2((float)random.NextDouble() * 4 - 2, (float)random.NextDouble() * 4 - 2);
                parts[i] = (i % 3) switch
                {
                    0 => new Ellipse2D(new(.1f + (float)random.NextDouble() * 2, .1f + (float)random.NextDouble() * 2), center),
                    1 => new Circle2D(.3f + (float)random.NextDouble(), center),
                    _ => Rectangle2D.FromSize(new Vector2(.5f + (float)random.NextDouble(), .5f + (float)random.NextDouble()), center)
                };
            }
            Check(parts);
        }

        static void Check(IConvexShape2D[] parts)
        {
            var expected = CrossSectionArea(parts);
            var actual = ShapeArea2D.Union(parts, 3);
            Assert.InRange(Math.Abs(actual - expected), 0d, .0002d);
            Assert.Equal(actual, ShapeArea2D.Union(parts.Reverse().ToArray(), 128), 5);
        }
    }

    [Theory]
    [InlineData("Capsule")]
    [InlineData("RoundedRectangle")]
    public void PolygonizedCurvesConvergeAsTheSampleCountIncreases(string kind)
    {
        IConvexShape2D shape = kind switch
        {
            "Capsule" => new Capsule2D(new(-1, 0), new(1, 0), 1),
            _ => new RoundedRectangle2D(new(-2, -1), new(2, 1), .5f)
        };
        var coarse = ShapeArea2D.Union([shape, shape], 8);
        var fine = ShapeArea2D.Union([shape, shape], 128);
        Assert.True(coarse < fine);
        Assert.InRange(fine, shape.Area * .999f, shape.Area);
        Assert.Equal(shape.Area, ShapeArea2D.Union([shape]));
    }

    [Fact]
    public void CompositeCachesTheChosenAreaAndPreservesItsModeInJson()
    {
        IConvexShape2D[] parts = [new Circle2D(1), new Rectangle2D(new(0, -1), new(2, 1))];
        Assert.Equal(MathF.PI + 4, new CompositeShape2D(parts).Area);
        var composite = new CompositeShape2D(parts, includeOverlap: false, areaOutlineSegments: 96);
        Assert.Equal(MathF.PI / 2 + 4, composite.Area, 5);
        var json = ShapeDefinition2D.FromShape(composite).ToJson();
        var restored = Assert.IsType<CompositeShape2D>(ShapeDefinition2D.FromJson(json).Build());
        Assert.False(restored.IncludeOverlap);
        Assert.Equal(96, restored.AreaOutlineSegments);
        Assert.Equal(composite.Area, restored.Area);
        var legacy = JsonNode.Parse(json)!.AsObject();
        legacy.Remove("includeOverlap");
        legacy.Remove("areaOutlineSegments");
        var legacyShape = Assert.IsType<CompositeShape2D>(ShapeDefinition2D.FromJson(legacy.ToJsonString()).Build());
        Assert.True(legacyShape.IncludeOverlap);
        Assert.Equal(MathF.PI + 4, legacyShape.Area);
    }

    [Fact]
    public void CustomSupportMappingsAreSampledOnceAndDegeneratePartsHaveZeroArea()
    {
        var shape = new CountingCircle();
        var composite = new CompositeShape2D([shape, new Circle2D(.1f)], includeOverlap: false);
        Assert.Equal(64, shape.SupportReads);
        for (var i = 0; i < 100; i++) Assert.True(composite.Area > 3);
        Assert.Equal(64, shape.SupportReads);
        Assert.Equal(MathF.PI, ShapeArea2D.Union([new PointShape(), new Circle2D(1)]), 5);
        Assert.Throws<ArgumentException>(() => new CompositeShape2D([new Circle2D(1)], areaOutlineSegments: 2));
        Assert.Throws<ArgumentException>(() => new CompositeShape2D([null!]));
    }

    private static double CircleLens(double a, double b, double distance)
    {
        if (distance >= a + b) return 0d;
        if (distance <= Math.Abs(a - b)) return Math.PI * Math.Min(a, b) * Math.Min(a, b);
        var triangle = Math.Sqrt((-distance + a + b) * (distance + a - b) * (distance - a + b) * (distance + a + b));
        return a * a * Math.Acos((distance * distance + a * a - b * b) / (2 * distance * a))
            + b * b * Math.Acos((distance * distance + b * b - a * a) / (2 * distance * b)) - triangle / 2;
    }

    private static ConvexPolygon2D RandomPolygon(Random random)
    {
        var angle = (float)random.NextDouble() * MathF.Tau;
        var transform = Matrix3x2.CreateRotation(angle) * Matrix3x2.CreateTranslation((float)random.NextDouble() * 4 - 2, (float)random.NextDouble() * 4 - 2);
        Vector2[] points = [new(-1, -.7f), new(1, -.7f), new(1, .7f), new(-1, .7f)];
        for (var i = 0; i < points.Length; i++) points[i] = Vector2.Transform(points[i], transform);
        if (random.Next(2) == 0) Array.Reverse(points);
        return new(points);
    }

    private static double CrossSectionArea(IConvexShape2D[] parts)
    {
        var bounds = parts.Select(ShapeBounds2D.Calculate).ToArray();
        var minimum = bounds.Min(b => b.Min.X);
        var maximum = bounds.Max(b => b.Max.X);
        const int steps = 32768;
        var width = ((double)maximum - minimum) / steps;
        var intervals = new List<(double Low, double High)>();
        var area = 0d;
        for (var i = 0; i < steps; i++)
        {
            var x = minimum + (i + .5) * width;
            intervals.Clear();
            foreach (var part in parts)
            {
                if (part is Circle2D circle)
                {
                    var squared = (double)circle.Radius * circle.Radius - (x - circle.Center.X) * (x - circle.Center.X);
                    if (squared <= 0d) continue;
                    var half = Math.Sqrt(squared);
                    intervals.Add((circle.Center.Y - half, circle.Center.Y + half));
                }
                else if (part is Ellipse2D ellipse)
                {
                    var normalizedX = (x - ellipse.Center.X) / ellipse.Radii.X;
                    var squared = 1d - normalizedX * normalizedX;
                    if (squared <= 0d) continue;
                    var half = ellipse.Radii.Y * Math.Sqrt(squared);
                    intervals.Add((ellipse.Center.Y - half, ellipse.Center.Y + half));
                }
                else if (part is Rectangle2D rectangle && x > rectangle.Min.X && x < rectangle.Max.X)
                    intervals.Add((rectangle.Min.Y, rectangle.Max.Y));
            }
            intervals.Sort(static (a, b) => a.Low.CompareTo(b.Low));
            var end = double.NegativeInfinity;
            foreach (var (low, high) in intervals)
            {
                area += Math.Max(0d, high - Math.Max(low, end)) * width;
                end = Math.Max(end, high);
            }
        }
        return area;
    }

    private sealed class CountingCircle : IConvexShape2D
    {
        public string Kind => "CountingCircle";
        public float Area => MathF.PI;
        public int SupportReads { get; private set; }
        public bool ContainsPoint(Vector2 point) => point.LengthSquared() <= 1;
        public Vector2 GetSupportPoint(Vector2 direction)
        {
            SupportReads++;
            return Vector2.Normalize(direction);
        }
    }

    private sealed class PointShape : IConvexShape2D
    {
        public string Kind => "Point";
        public float Area => 0;
        public bool ContainsPoint(Vector2 point) => point == Vector2.Zero;
        public Vector2 GetSupportPoint(Vector2 direction) => Vector2.Zero;
    }
}
