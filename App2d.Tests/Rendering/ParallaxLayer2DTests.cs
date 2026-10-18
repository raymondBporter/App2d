using App2d.Core.Rendering;
using System.Numerics;

namespace App2d.Tests.Rendering;

public sealed class ParallaxLayer2DTests
{
    [Theory]
    [InlineData(0f, 1f)]
    [InlineData(1f, 0.5f)]
    [InlineData(4f, 0.2f)]
    public void DepthScalesBothCameraAxesWhilePreservingZoomRotationAndViewport(float depth, float factor)
    {
        var camera = new Camera2D
        {
            ReferenceViewportHeight = 720f,
            Zoom = 1.35f,
            Rotation = 0.3f,
            WorldBounds = new(new(-2000f), new(2000f)),
            Position = new(800f, -400f)
        };
        camera.SetViewport(1280, 720);
        var originalPosition = camera.Position;
        var layer = new ParallaxLayer2D(depth);

        var view = layer.GetView(camera);

        Assert.Equal(factor, layer.ScrollFactor, 5);
        Assert.Equal(camera.Position * factor, view.Position);
        Assert.Equal(camera.Zoom, view.Zoom);
        Assert.Equal(camera.Rotation, view.Rotation);
        Assert.Equal(camera.ViewportSize, view.ViewportSize);
        Assert.Equal(camera.PixelsPerWorldUnit, view.PixelsPerWorldUnit);
        Assert.Null(view.WorldBounds);
        Assert.Equal(originalPosition, camera.Position);

        camera.SetViewport(2560, 1440);
        camera.Position += new Vector2(50f, 20f); // Also handles editor panning without a simulation tick.
        view = layer.GetView(camera);
        Assert.Equal(camera.Position * factor, view.Position);
        Assert.Equal(camera.ViewportSize, view.ViewportSize);
        Assert.Equal(camera.PixelsPerWorldUnit, view.PixelsPerWorldUnit);
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void InvalidDepthCannotEnterTheLayer(float value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ParallaxLayer2D(value));
        var layer = new ParallaxLayer2D(1f);
        Assert.Throws<ArgumentOutOfRangeException>(() => layer.Depth = value);
        Assert.Equal(1f, layer.Depth);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void InvalidRepeatWidthIsRejected(float value)
    {
        var layer = new ParallaxLayer2D(1f);
        Assert.Throws<ArgumentOutOfRangeException>(() => layer.RepeatWidth = value);
        Assert.Null(layer.RepeatWidth);
    }

    [Fact]
    public void LayersSortByDepthAndKeepInsertionOrderAtEqualDepths()
    {
        var near = new ParallaxLayer2D(1f);
        var far = new ParallaxLayer2D(4f);
        var sameDepth = new ParallaxLayer2D(4f);
        ParallaxBackground2D background = [near, far, sameDepth];
        Assert.Equal([far, sameDepth, near], background.GetDrawOrder());
        near.Depth = 8f;
        Assert.Equal([near, far, sameDepth], background.GetDrawOrder());
    }
}
