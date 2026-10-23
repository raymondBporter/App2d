using App2d.Core.Geometry.Meshes;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Rendering;
using App2d.Core.Shapes;
using App2d.Rendering;
using System.Numerics;

namespace App2d.Tests.Rendering;

[Collection("Graphics")]
public sealed class ShapeRenderingTests
{
    public static TheoryData<string> FiniteShapes =>
    [
        nameof(Rectangle2D),
        nameof(AxisAlignedRectangle2D),
        nameof(RoundedRectangle2D),
        nameof(Circle2D),
        nameof(Ellipse2D),
        nameof(Capsule2D),
        nameof(Triangle2D),
        nameof(ConvexPolygon2D),
        nameof(SimplePolygon2D),
        nameof(HeightFieldShape2D),
        nameof(CompositeShape2D),
    ];

    private static IShape2D CreateShape(string shapeName) => shapeName switch
    {
        nameof(Rectangle2D) => Rectangle2D.Builder.Create(new(-16, -12), new(16, 12)),
        nameof(AxisAlignedRectangle2D) => AxisAlignedRectangle2D.Builder.Create(new(-16, -12), new(16, 12)),
        nameof(RoundedRectangle2D) => RoundedRectangle2D.Builder.Create(new(-16, -12), new(16, 12), 4),
        nameof(Circle2D) => Circle2D.Builder.Create(16),
        nameof(Ellipse2D) => Ellipse2D.Builder.Create(new(20, 12)),
        nameof(Capsule2D) => Capsule2D.Builder.Create(new(-10, 0), new(10, 0), 8),
        nameof(Triangle2D) => Triangle2D.Builder.Create(new(-16, -14), new(16, -14), new(0, 18)),
        nameof(ConvexPolygon2D) => ConvexPolygon2D.Builder.Create([new(-15, -12), new(15, -12), new(18, 10), new(-18, 10)]),
        nameof(SimplePolygon2D) => SimplePolygon2D.Builder.Create([new(-16, -12), new(16, -12), new(16, 0), new(8, 0), new(8, 12), new(-16, 12)]),
        nameof(HeightFieldShape2D) => HeightFieldShape2D.Builder.Create([new(-16, 12), new(0, 6), new(16, 12)], -12f),
        nameof(CompositeShape2D) => CompositeShape2D.Builder.Create([
            Rectangle2D.Builder.Create(new(-16, -12), new(5, 12)),
            Rectangle2D.Builder.Create(new(-5, -12), new(16, 12)),
        ]),
        _ => throw new ArgumentOutOfRangeException(nameof(shapeName), shapeName, null)
    };

    [Theory]
    [MemberData(nameof(FiniteShapes))]
    public void DirectShapesCanBeFilledAndOutlined(string shapeName)
    {
        var shape = CreateShape(shapeName);
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(new Camera2D(), graphics.Device);
        renderer.BeginFrame(128, 128, default);
        renderer.Clear(Color4b.Transparent);
        renderer.DrawShape(shape, fillColor: Color4b.Lime, outlineColor: Color4b.White);
        renderer.EndFrame();

        var pixels = graphics.ReadPixels();
        Assert.Equal(Color4b.Lime, pixels[64 * 128 + 64]);
        Assert.Contains(Color4b.White, pixels);
        Assert.Equal(Color4b.Transparent, pixels[10 * 128 + 10]);
    }

    [Fact]
    public void DirectShapeSupportsOutlineOnlyAndLocalTransform()
    {
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(new Camera2D(), graphics.Device);
        renderer.BeginFrame(128, 128, default);
        renderer.Clear(Color4b.Transparent);
        renderer.DrawShape(Rectangle2D.Builder.Create(new(-10, -10), new(10, 10)),
            Matrix3x2.CreateTranslation(20, 0), outlineColor: Color4b.White, screenStrokeWidth: 4);
        renderer.EndFrame();

        var pixels = graphics.ReadPixels();
        Assert.Equal(Color4b.Transparent, pixels[64 * 128 + 84]);
        Assert.Equal(Color4b.White, pixels[64 * 128 + 94]);
        Assert.Equal(Color4b.Transparent, pixels[64 * 128 + 64]);
    }

    [Fact]
    public void MeshOutlineSkipsTheSharedDiagonal()
    {
        var mesh = new TriangleMesh2D(
            [new(-20, -20), new(20, -20), new(20, 20), new(-20, 20)],
            [0, 1, 2, 0, 2, 3]);
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(new Camera2D(), graphics.Device);
        renderer.BeginFrame(128, 128, default);
        renderer.Clear(Color4b.Transparent);
        renderer.DrawTriangleMesh(mesh, fillColor: Color4b.Lime, outlineColor: Color4b.White);
        renderer.EndFrame();

        var pixels = graphics.ReadPixels();
        Assert.Equal(Color4b.Lime, pixels[64 * 128 + 64]);
        Assert.Equal(Color4b.White, pixels[64 * 128 + 84]);
    }

    [Fact]
    public void DirectHalfSpaceUsesTheVisibleViewForFillAndBoundary()
    {
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(new Camera2D(), graphics.Device);
        renderer.BeginFrame(128, 128, default);
        renderer.Clear(Color4b.Transparent);
        renderer.DrawShape(HalfSpace2D.Builder.Create(Vector2.UnitY, 0), Color4b.Blue, Color4b.White);
        renderer.EndFrame();

        var pixels = graphics.ReadPixels();
        Assert.Equal(Color4b.Blue, pixels[90 * 128 + 64]);
        Assert.Equal(Color4b.Transparent, pixels[35 * 128 + 64]);
        Assert.Equal(Color4b.White, pixels[64 * 128 + 64]);
    }

    [Theory]
    [InlineData(-1000, false)]
    [InlineData(1000, true)]
    public void HalfSpaceOutsideOrCoveringViewHasNoVisibleBoundary(float offset, bool fillsView)
    {
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(new Camera2D(), graphics.Device);
        renderer.BeginFrame(128, 128, default);
        renderer.Clear(Color4b.Transparent);
        renderer.DrawShape(HalfSpace2D.Builder.Create(Vector2.UnitX, offset), Color4b.Blue, Color4b.White);
        renderer.EndFrame();

        var pixels = graphics.ReadPixels();
        Assert.Equal(fillsView ? Color4b.Blue : Color4b.Transparent, pixels[64 * 128 + 64]);
        Assert.DoesNotContain(Color4b.White, pixels);
    }

    [Theory]
    [InlineData(LineCap2D.Butt, false, false)]
    [InlineData(LineCap2D.Square, true, true)]
    [InlineData(LineCap2D.Round, true, false)]
    public void SegmentCapsHaveTheExpectedShape(LineCap2D cap, bool extends, bool squareCorner)
    {
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(new Camera2D(), graphics.Device);
        renderer.BeginFrame(128, 128, default);
        renderer.Clear(Color4b.Transparent);
        renderer.DrawWorldSegment(new(-20, 20), new(20, 20), Color4b.Red, 8, cap);
        renderer.EndFrame();

        var pixels = graphics.ReadPixels();
        Assert.Equal(Color4b.Red, pixels[44 * 128 + 64]);
        Assert.Equal(extends ? Color4b.Red : Color4b.Transparent, pixels[44 * 128 + 41]);
        Assert.Equal(squareCorner ? Color4b.Red : Color4b.Transparent, pixels[40 * 128 + 40]);
    }

    [Fact]
    public void InfiniteLinesAndRaysStopAtTheirCorrectBounds()
    {
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(new Camera2D(), graphics.Device);
        renderer.BeginFrame(128, 128, default);
        renderer.Clear(Color4b.Transparent);
        renderer.DrawWorldLine(new Line2D(Vector2.Zero, Vector2.UnitX), Color4b.Red, 2);
        renderer.DrawWorldRay(new Ray2D(new(0, -20), Vector2.UnitX), Color4b.Blue, 2);
        renderer.EndFrame();

        var pixels = graphics.ReadPixels();
        Assert.Equal(Color4b.Red, pixels[64 * 128 + 10]);
        Assert.Equal(Color4b.Red, pixels[64 * 128 + 110]);
        Assert.Equal(Color4b.Transparent, pixels[84 * 128 + 40]);
        Assert.Equal(Color4b.Blue, pixels[84 * 128 + 90]);
    }

    [Theory]
    [InlineData(1f, false)]
    [InlineData(2f, true)]
    public void ThickSegmentWidthIsInWorldUnits(float zoom, bool coversThreePixelsOut)
    {
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(new Camera2D { Zoom = zoom }, graphics.Device);
        renderer.BeginFrame(128, 128, default);
        renderer.Clear(Color4b.Transparent);
        renderer.DrawWorldThickSegment(new(-20, 0), new(20, 0), 4, Color4b.Red);
        renderer.EndFrame();

        var pixels = graphics.ReadPixels();
        Assert.Equal(Color4b.Red, pixels[64 * 128 + 64]);
        Assert.Equal(coversThreePixelsOut ? Color4b.Red : Color4b.Transparent, pixels[61 * 128 + 64]);
        Assert.Equal(Color4b.Transparent, pixels[58 * 128 + 64]);
    }

    [Fact]
    public void TaperedThickSegmentWidensTowardsItsEnd()
    {
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(new Camera2D(), graphics.Device);
        renderer.BeginFrame(128, 128, default);
        renderer.Clear(Color4b.Transparent);
        renderer.DrawWorldThickSegment(new(-40, 0), new(40, 0), 2, 10, Color4b.Red);
        renderer.EndFrame();

        var pixels = graphics.ReadPixels();
        Assert.Equal(Color4b.Red, pixels[64 * 128 + 34]);
        Assert.Equal(Color4b.Transparent, pixels[60 * 128 + 34]);
        Assert.Equal(Color4b.Red, pixels[60 * 128 + 94]);
    }

    [Fact]
    public void ThinWorldStrokesKeepAtLeastOnePixel()
    {
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(new Camera2D(), graphics.Device);
        renderer.BeginFrame(128, 128, default);
        renderer.Clear(Color4b.Transparent);
        renderer.DrawWorldThickSegment(new(-20, 0.3f), new(20, 0.3f), 0.1f, Color4b.Red);
        renderer.EndFrame();

        var pixels = graphics.ReadPixels();
        Assert.Equal(Color4b.Red, pixels[63 * 128 + 64]);
    }

    [Theory]
    [InlineData(1f, false)]
    [InlineData(2f, true)]
    public void ThickPolylineWidthIsInWorldUnits(float zoom, bool coversThreePixelsOut)
    {
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(new Camera2D { Zoom = zoom }, graphics.Device);
        renderer.BeginFrame(128, 128, default);
        renderer.Clear(Color4b.Transparent);
        renderer.DrawWorldThickPolyline([new(-20, 0), new(0, 0), new(0, 20)], 4, Color4b.Red);
        renderer.EndFrame();

        var pixels = graphics.ReadPixels();
        Assert.Equal(Color4b.Red, pixels[64 * 128 + 56]);
        Assert.Equal(coversThreePixelsOut ? Color4b.Red : Color4b.Transparent, pixels[61 * 128 + 56]);
        Assert.Equal(Color4b.Transparent, pixels[58 * 128 + 56]);
        Assert.Equal(Color4b.Red, pixels[54 * 128 + 64]);
        Assert.Equal(coversThreePixelsOut ? Color4b.Red : Color4b.Transparent, pixels[54 * 128 + 67]);
        Assert.Equal(Color4b.Transparent, pixels[54 * 128 + 70]);
    }

    [Theory]
    [InlineData(1f, false)]
    [InlineData(2f, true)]
    public void ThickCircleStrokeIsInWorldUnits(float zoom, bool coversThreePixelsIn)
    {
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(new Camera2D { Zoom = zoom }, graphics.Device);
        renderer.BeginFrame(128, 128, default);
        renderer.Clear(Color4b.Transparent);
        renderer.DrawWorldThickCircle(Vector2.Zero, 20, Color4b.Red, 4);
        renderer.EndFrame();

        var pixels = graphics.ReadPixels();
        var radius = (int)(20 * zoom);
        Assert.Equal(Color4b.Red, pixels[64 * 128 + 64 + radius - 1]);
        Assert.Equal(coversThreePixelsIn ? Color4b.Red : Color4b.Transparent, pixels[64 * 128 + 64 + radius - 3]);
        Assert.Equal(Color4b.Transparent, pixels[64 * 128 + 64 + radius - 6]);
    }

    [Theory]
    [InlineData(1f, false)]
    [InlineData(2f, true)]
    public void ConvexPolygonOutlineIsInWorldUnits(float zoom, bool coversThreePixelsIn)
    {
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(new Camera2D { Zoom = zoom }, graphics.Device);
        renderer.BeginFrame(128, 128, default);
        renderer.Clear(Color4b.Transparent);
        renderer.DrawWorldConvexPolygon([new(-20, -20), new(20, -20), new(20, 20), new(-20, 20)], Color4b.Lime, Color4b.Red, 4);
        renderer.EndFrame();

        var pixels = graphics.ReadPixels();
        var edge = 64 + (int)(20 * zoom);
        Assert.Equal(Color4b.Lime, pixels[64 * 128 + 64]);
        Assert.Equal(Color4b.Red, pixels[64 * 128 + edge - 1]);
        Assert.Equal(coversThreePixelsIn ? Color4b.Red : Color4b.Lime, pixels[64 * 128 + edge - 3]);
        Assert.Equal(Color4b.Lime, pixels[64 * 128 + edge - 6]);
        Assert.Equal(Color4b.Transparent, pixels[64 * 128 + edge + 6]);
    }

    [Fact]
    public void WorldStrokesRejectEmptyWidthsAndStyles()
    {
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(new Camera2D(), graphics.Device);
        renderer.BeginFrame(128, 128, default);
        Assert.Throws<ArgumentOutOfRangeException>(() => renderer.DrawWorldThickSegment(Vector2.Zero, Vector2.UnitX, 0, Color4b.Red));
        Assert.Throws<ArgumentOutOfRangeException>(() => renderer.DrawWorldThickPolyline([Vector2.Zero, Vector2.UnitX], -1, Color4b.Red));
        Assert.Throws<ArgumentOutOfRangeException>(() => renderer.DrawWorldThickCircle(Vector2.Zero, 5, Color4b.Red, 0));
        Assert.Throws<ArgumentException>(() => renderer.DrawWorldConvexPolygon([Vector2.Zero, Vector2.UnitX, Vector2.UnitY], null));
        renderer.EndFrame();
    }
}
