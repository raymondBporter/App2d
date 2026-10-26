using App2d.Core.Imaging;
using App2d.Core.Mathematics;

namespace App2d.Tests.Imaging;

public sealed class ImageContours2DTests
{
    [Fact]
    public void ARegionTouchingTheImageBorderHasAClosedOrderedPerimeter()
    {
        var source = new Image2D<bool>(4, 3);
        source.Fill(true);
        var loop = Assert.Single(source.TraceBoundaryLoops());
        Assert.Equal(14, loop.Length);
        Assert.Equal(12, Area(loop));
        Assert.Contains(new Vector2i(0, 0), loop);
        Assert.Contains(new Vector2i(4, 3), loop);
        Assert.NotEqual(loop[0], loop[^1]);
    }

    [Fact]
    public void OuterBoundaryHolesAndIslandsHaveTheCorrectWinding()
    {
        var source = Parse("#####", "#...#", "#.#.#", "#...#", "#####");
        var areas = source.TraceBoundaryLoops().Select(Area).Order().ToArray();
        Assert.Equal([-9d, 1d, 25d], areas);
    }

    [Fact]
    public void CornerTouchingPixelsCanBeOneSelectionGroupButHaveTwoPerimeters()
    {
        var source = Parse("#.", ".#");
        Assert.Equal(2, source.LabelComponents().PixelCounts.Length);
        var loops = source.TraceBoundaryLoops();
        Assert.Equal(2, loops.Length);
        Assert.All(loops, loop => Assert.Equal(1, Area(loop)));
    }

    [Fact]
    public void EveryThreeByThreePatternUsesEachExposedCellEdgeExactlyOnce()
    {
        for (var pattern = 0; pattern < 512; pattern++)
        {
            var source = new Image2D<bool>(3, 3);
            for (var bit = 0; bit < 9; bit++)
                source.AsSpan()[bit] = (pattern & (1 << bit)) != 0;

            // Cancel reverse edges between adjacent cells to independently define the perimeter.
            var expected = new HashSet<(Vector2i From, Vector2i To)>();
            for (var y = 0; y < 3; y++)
                for (var x = 0; x < 3; x++)
                    if (source[x, y])
                    {
                        Add(new(x, y), new(x + 1, y));
                        Add(new(x + 1, y), new(x + 1, y + 1));
                        Add(new(x + 1, y + 1), new(x, y + 1));
                        Add(new(x, y + 1), new(x, y));
                    }

            var actual = new HashSet<(Vector2i From, Vector2i To)>();
            var loops = source.TraceBoundaryLoops();
            foreach (var loop in loops)
            {
                Assert.True(loop.Length >= 4);
                for (var i = 0; i < loop.Length; i++)
                {
                    var from = loop[i];
                    var to = loop[(i + 1) % loop.Length];
                    Assert.Equal(1, Math.Abs(from.X - to.X) + Math.Abs(from.Y - to.Y));
                    Assert.True(actual.Add((from, to)));
                }
            }
            Assert.True(expected.SetEquals(actual), $"Boundary mismatch for pattern {pattern}.");
            Assert.Equal(source.AsSpan().ToArray().Count(static p => p), loops.Sum(Area));

            void Add(Vector2i from, Vector2i to)
            {
                if (!expected.Remove((to, from))) expected.Add((from, to));
            }
        }
    }

    [Fact]
    public void EmptyMasksHaveNoPathsAndNullIsRejected()
    {
        Assert.Empty(new Image2D<bool>(0, 7).TraceBoundaryLoops());
        Assert.Empty(new Image2D<bool>(7, 0).TraceBoundaryLoops());
        Assert.Empty(new Image2D<bool>(3, 3).TraceBoundaryLoops());
        Assert.Throws<ArgumentNullException>(() => ImageContours2D.TraceBoundaryLoops(null!));
    }

    private static double Area(Vector2i[] loop)
    {
        long twiceArea = 0;
        for (var i = 0; i < loop.Length; i++)
        {
            var a = loop[i];
            var b = loop[(i + 1) % loop.Length];
            twiceArea += (long)a.X * b.Y - (long)b.X * a.Y;
        }
        return twiceArea / 2d;
    }

    private static Image2D<bool> Parse(params string[] rows) =>
        new(rows[0].Length, rows.Length, rows.SelectMany(row => row.Select(c => c == '#')).ToArray());
}
