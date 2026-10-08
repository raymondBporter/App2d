using App2d.Core.Geometry;
using App2d.Core.Meshes;
using App2d.Core.Rendering;
using App2d.Core.Shapes;
using App2d.Rendering;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Tests.Rendering;

[Collection("Graphics")]
public sealed class ShapeRenderingTests
{
    public static TheoryData<IShape2D> FiniteShapes =>
    [
        new Rectangle2D(new(-16, -12), new(16, 12)),
        new Circle2D(16),
        new Ellipse2D(new(20, 12)),
        new Capsule2D(new(-10, 0), new(10, 0), 8),
        new Triangle2D(new(-16, -14), new(16, -14), new(0, 18)),
        new ConvexPolygon2D([new(-15, -12), new(15, -12), new(18, 10), new(-18, 10)]),
        new CompositeShape2D([
            new Rectangle2D(new(-16, -12), new(5, 12)),
            new Rectangle2D(new(-5, -12), new(16, 12)),
        ]),
    ];

    [Theory]
    [MemberData(nameof(FiniteShapes))]
    public void DirectShapesCanBeFilledAndOutlined(IShape2D shape)
    {
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(new Camera2D(), graphics.Device);
        renderer.BeginFrame(128, 128, default);
        renderer.Clear(XnaColor.Transparent);
        renderer.DrawShape(shape, fillColor: XnaColor.Lime, outlineColor: XnaColor.White);
        renderer.EndFrame();

        var pixels = graphics.ReadPixels();
        Assert.Equal(XnaColor.Lime, pixels[64 * 128 + 64]);
        Assert.Contains(XnaColor.White, pixels);
        Assert.Equal(XnaColor.Transparent, pixels[10 * 128 + 10]);
    }

    [Fact]
    public void DirectShapeSupportsOutlineOnlyAndLocalTransform()
    {
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(new Camera2D(), graphics.Device);
        renderer.BeginFrame(128, 128, default);
        renderer.Clear(XnaColor.Transparent);
        renderer.DrawShape(new Rectangle2D(new(-10, -10), new(10, 10)),
            Matrix3x2.CreateTranslation(20, 0), outlineColor: XnaColor.White, screenStrokeWidth: 4);
        renderer.EndFrame();

        var pixels = graphics.ReadPixels();
        Assert.Equal(XnaColor.Transparent, pixels[64 * 128 + 84]);
        Assert.Equal(XnaColor.White, pixels[64 * 128 + 94]);
        Assert.Equal(XnaColor.Transparent, pixels[64 * 128 + 64]);
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
        renderer.Clear(XnaColor.Transparent);
        renderer.DrawTriangleMesh(mesh, fillColor: XnaColor.Lime, outlineColor: XnaColor.White);
        renderer.EndFrame();

        var pixels = graphics.ReadPixels();
        Assert.Equal(XnaColor.Lime, pixels[64 * 128 + 64]);
        Assert.Equal(XnaColor.White, pixels[64 * 128 + 84]);
    }

    [Fact]
    public void DirectHalfSpaceUsesTheVisibleViewForFillAndBoundary()
    {
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(new Camera2D(), graphics.Device);
        renderer.BeginFrame(128, 128, default);
        renderer.Clear(XnaColor.Transparent);
        renderer.DrawShape(new HalfSpace2D(Vector2.UnitY, 0), XnaColor.Blue, XnaColor.White);
        renderer.EndFrame();

        var pixels = graphics.ReadPixels();
        Assert.Equal(XnaColor.Blue, pixels[90 * 128 + 64]);
        Assert.Equal(XnaColor.Transparent, pixels[35 * 128 + 64]);
        Assert.Equal(XnaColor.White, pixels[64 * 128 + 64]);
    }

    [Theory]
    [InlineData(-1000, false)]
    [InlineData(1000, true)]
    public void HalfSpaceOutsideOrCoveringViewHasNoVisibleBoundary(float offset, bool fillsView)
    {
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(new Camera2D(), graphics.Device);
        renderer.BeginFrame(128, 128, default);
        renderer.Clear(XnaColor.Transparent);
        renderer.DrawShape(new HalfSpace2D(Vector2.UnitX, offset), XnaColor.Blue, XnaColor.White);
        renderer.EndFrame();

        var pixels = graphics.ReadPixels();
        Assert.Equal(fillsView ? XnaColor.Blue : XnaColor.Transparent, pixels[64 * 128 + 64]);
        Assert.DoesNotContain(XnaColor.White, pixels);
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
        renderer.Clear(XnaColor.Transparent);
        renderer.DrawWorldSegment(new(-20, 20), new(20, 20), XnaColor.Red, 8, cap);
        renderer.EndFrame();

        var pixels = graphics.ReadPixels();
        Assert.Equal(XnaColor.Red, pixels[44 * 128 + 64]);
        Assert.Equal(extends ? XnaColor.Red : XnaColor.Transparent, pixels[44 * 128 + 41]);
        Assert.Equal(squareCorner ? XnaColor.Red : XnaColor.Transparent, pixels[40 * 128 + 40]);
    }

    [Fact]
    public void InfiniteLinesAndRaysStopAtTheirCorrectBounds()
    {
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(new Camera2D(), graphics.Device);
        renderer.BeginFrame(128, 128, default);
        renderer.Clear(XnaColor.Transparent);
        renderer.DrawWorldLine(new Line2D(Vector2.Zero, Vector2.UnitX), XnaColor.Red, 2);
        renderer.DrawWorldRay(new Ray2D(new(0, -20), Vector2.UnitX), XnaColor.Blue, 2);
        renderer.EndFrame();

        var pixels = graphics.ReadPixels();
        Assert.Equal(XnaColor.Red, pixels[64 * 128 + 10]);
        Assert.Equal(XnaColor.Red, pixels[64 * 128 + 110]);
        Assert.Equal(XnaColor.Transparent, pixels[84 * 128 + 40]);
        Assert.Equal(XnaColor.Blue, pixels[84 * 128 + 90]);
    }
}
