using App2d.Rendering;
using System.Numerics;

namespace App2d.Tests.Rendering;

public sealed class Camera2DTests
{
    [Theory]
    [InlineData(960, 540)]
    [InlineData(1920, 1080)]
    [InlineData(3840, 2160)]
    [InlineData(1280, 720)]
    public void ReferenceHeightPreservesFramingWhilePixelsScaleWithWindow(int width, int height)
    {
        var camera = new Camera2D
        {
            ReferenceViewportHeight = 1080f, Zoom = 1.35f, Position = new Vector2(150f, 70f)
        };
        camera.SetViewport(1920, 1080);
        var originalBounds = camera.VisibleWorldBounds;
        var point = new Vector2(270f, 120f);
        var originalNormalizedPosition = camera.WorldToDevice(point) / camera.ViewportSize;

        camera.SetViewport(width, height);

        Assert.InRange(Vector2.Distance(originalBounds.Min, camera.VisibleWorldBounds.Min), 0f, 0.001f);
        Assert.InRange(Vector2.Distance(originalBounds.Max, camera.VisibleWorldBounds.Max), 0f, 0.001f);
        Assert.InRange(Vector2.Distance(originalNormalizedPosition,
            camera.WorldToDevice(point) / camera.ViewportSize), 0f, 0.0001f);
        Assert.Equal(1.35f, camera.Zoom);
        Assert.Equal(1.35f * height / 1080f, camera.PixelsPerWorldUnit, 5);
    }

    [Fact]
    public void WiderAspectShowsMoreAtTheSidesWithoutChangingHeightOrStretching()
    {
        var camera = new Camera2D { ReferenceViewportHeight = 1080f, Zoom = 2f };
        camera.SetViewport(960, 540);
        var size = camera.VisibleWorldBounds.Size;
        camera.SetViewport(1440, 540);

        Assert.Equal(size.Y, camera.VisibleWorldBounds.Size.Y);
        Assert.Equal(size.X * 1.5f, camera.VisibleWorldBounds.Size.X);
        var origin = camera.WorldToDevice(Vector2.Zero);
        Assert.Equal(Vector2.Distance(origin, camera.WorldToDevice(Vector2.UnitX)),
            Vector2.Distance(origin, camera.WorldToDevice(Vector2.UnitY)));
        camera.Zoom = 4f;
        Assert.Equal(size.Y / 2f, camera.VisibleWorldBounds.Size.Y);
    }

    [Fact]
    public void ScaledRotatedCameraPickingAndDragUseTheSameTransform()
    {
        var camera = new Camera2D
        {
            ReferenceViewportHeight = 1080f, Zoom = 1.35f,
            Position = new Vector2(100f, 50f), Rotation = 0.3f
        };
        camera.SetViewport(960, 540);
        var point = new Vector2(170f, -25f);
        var devicePoint = camera.WorldToDevice(point);
        Assert.InRange(Vector2.Distance(point, camera.DeviceToWorld(devicePoint)), 0f, 0.001f);

        var drag = new Vector2(75f, -20f);
        camera.Position -= Vector2.TransformNormal(drag, camera.DeviceToWorldMatrix);
        Assert.InRange(Vector2.Distance(devicePoint + drag, camera.WorldToDevice(point)), 0f, 0.001f);
    }

    [Fact]
    public void VisibleWorldBoundsCentersOnTheCameraPosition()
    {
        var camera = new Camera2D { Position = new Vector2(100f, 50f) };
        camera.SetViewport(800, 600);

        var bounds = camera.VisibleWorldBounds;

        Assert.Equal(new Vector2(-300f, -250f), bounds.Min);
        Assert.Equal(new Vector2(500f, 350f), bounds.Max);
    }

    [Fact]
    public void ZoomShrinksTheVisibleWorldRect()
    {
        var camera = new Camera2D { Zoom = 2f };
        camera.SetViewport(800, 600);

        var bounds = camera.VisibleWorldBounds;

        Assert.Equal(new Vector2(-200f, -150f), bounds.Min);
        Assert.Equal(new Vector2(200f, 150f), bounds.Max);
    }

    [Fact]
    public void RotationStillBoundsTheWholeView()
    {
        var camera = new Camera2D { Rotation = MathF.PI / 4f };
        camera.SetViewport(800, 600);

        var bounds = camera.VisibleWorldBounds;

        // A 45°-rotated 800x600 view needs a bounding box wider than either side.
        var expectedHalfExtent = (400f + 300f) / MathF.Sqrt(2f);
        Assert.Equal(-expectedHalfExtent, bounds.Min.X, 2);
        Assert.Equal(expectedHalfExtent, bounds.Max.X, 2);
    }
}
