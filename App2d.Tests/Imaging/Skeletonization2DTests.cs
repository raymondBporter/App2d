using App2d.Core.Imaging;

namespace App2d.Tests.Imaging;

public sealed class Skeletonization2DTests
{
    [Fact]
    public void MatchesThePublishedGuoHallThinningExample()
    {
        // Reference fixture: https://scikit-image.org/docs/stable/api/skimage.morphology.html#skimage.morphology.thin
        var source = Parse(
            ".#.....",
            "..###..",
            "..###..",
            "..###..",
            "..###..",
            "..###..",
            ".......");
        AssertImage(Parse(
            ".#.....",
            "..#....",
            "...#...",
            "...#...",
            "...#...",
            ".......",
            "......."), source.Skeletonize());
    }

    [Fact]
    public void ThinRectangleBecomesACenterLine()
    {
        var source = Parse(
            ".......",
            "..###..",
            "..###..",
            "..###..",
            "..###..",
            "..###..",
            ".......");
        var skeleton = source.Skeletonize();
        AssertImage(Parse(
            ".......",
            ".......",
            "...#...",
            "...#...",
            "...#...",
            ".......",
            "......."), skeleton);
        Assert.Equal(15, ForegroundCount(source));
        AssertImage(skeleton, skeleton.Skeletonize());
    }

    [Theory]
    [InlineData(1, 7)]
    [InlineData(7, 1)]
    [InlineData(1, 1)]
    public void SinglePixelWideLinesAndEndpointsSurviveAtImageEdges(int width, int height)
    {
        var source = new Image2D<bool>(width, height);
        source.Fill(true);
        AssertImage(source, source.Skeletonize());
    }

    [Fact]
    public void EveryThreeByThreePatternPreservesComponentsHolesAndExistingPixels()
    {
        for (var pattern = 0; pattern < 512; pattern++)
        {
            var source = new Image2D<bool>(3, 3);
            for (var bit = 0; bit < 9; bit++)
                source.AsSpan()[bit] = (pattern & (1 << bit)) != 0;

            var skeleton = source.Skeletonize();
            Assert.Equal(ComponentCount(source, foreground: true), ComponentCount(skeleton, foreground: true));
            Assert.Equal(ComponentCount(source, foreground: false), ComponentCount(skeleton, foreground: false));
            for (var i = 0; i < source.Count; i++)
                Assert.False(skeleton.AsSpan()[i] && !source.AsSpan()[i]);
            AssertImage(skeleton, skeleton.Skeletonize());
        }
    }

    [Fact]
    public void ThickLoopAndBranchStayConnectedWithoutFillingTheHole()
    {
        var source = Parse(
            "#########..###..",
            "#########..###..",
            "##.....##..###..",
            "##.....##..###..",
            "##.....#########",
            "##.....#########",
            "##.....##..###..",
            "#########..###..",
            "#########..###..");
        var skeleton = source.Skeletonize();
        Assert.True(ForegroundCount(skeleton) < ForegroundCount(source));
        Assert.Equal(ComponentCount(source, foreground: true), ComponentCount(skeleton, foreground: true));
        Assert.Equal(2, ComponentCount(skeleton, foreground: false)); // Outside plus the hole.
        AssertImage(skeleton, skeleton.Skeletonize());
    }

    [Fact]
    public void EmptyImagesKeepTheirDimensionsAndRejectNullInput()
    {
        var empty = new Image2D<bool>(0, 7).Skeletonize();
        Assert.Equal(0, empty.Width);
        Assert.Equal(7, empty.Height);
        Assert.Equal(0, empty.Count);
        AssertImage(new Image2D<bool>(7, 0), new Image2D<bool>(7, 0).Skeletonize());
        AssertImage(new Image2D<bool>(4, 4), new Image2D<bool>(4, 4).Skeletonize());
        Assert.Throws<ArgumentNullException>(() => Skeletonization2D.Skeletonize(null!));
    }

    private static Image2D<bool> Parse(params string[] rows)
    {
        var image = new Image2D<bool>(rows[0].Length, rows.Length);
        for (var y = 0; y < image.Height; y++)
            for (var x = 0; x < image.Width; x++)
                image[x, y] = rows[y][x] == '#';
        return image;
    }

    private static void AssertImage(Image2D<bool> expected, Image2D<bool> actual)
    {
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);
        Assert.Equal(expected.AsSpan().ToArray(), actual.AsSpan().ToArray());
    }

    private static int ForegroundCount(Image2D<bool> image)
    {
        var count = 0;
        foreach (var value in image.AsSpan())
            if (value) count++;
        return count;
    }

    // Foreground uses eight-connectivity; background uses four-connectivity and includes an outer border.
    private static int ComponentCount(Image2D<bool> image, bool foreground)
    {
        var width = image.Width + 2;
        var height = image.Height + 2;
        var visited = new bool[width * height];
        var pending = new Queue<(int X, int Y)>();
        var count = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (visited[y * width + x] || image.GetValueOrDefault(x - 1, y - 1) != foreground)
                    continue;
                count++;
                visited[y * width + x] = true;
                pending.Enqueue((x, y));
                while (pending.TryDequeue(out var pixel))
                {
                    for (var dy = -1; dy <= 1; dy++)
                    {
                        for (var dx = -1; dx <= 1; dx++)
                        {
                            if ((dx == 0 && dy == 0) || (!foreground && dx != 0 && dy != 0))
                                continue;
                            var nx = pixel.X + dx;
                            var ny = pixel.Y + dy;
                            if ((uint)nx >= (uint)width || (uint)ny >= (uint)height
                                || visited[ny * width + nx] || image.GetValueOrDefault(nx - 1, ny - 1) != foreground)
                                continue;
                            visited[ny * width + nx] = true;
                            pending.Enqueue((nx, ny));
                        }
                    }
                }
            }
        }
        return count;
    }
}
