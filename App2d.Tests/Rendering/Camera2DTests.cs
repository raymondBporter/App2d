using App2d.Core.Geometry.Primitives;
using App2d.Core.Rendering;
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
            ReferenceViewportHeight = 1080f,
            Zoom = 1.35f,
            Position = new Vector2f(150f, 70f)
        };
        camera.SetViewport(1920, 1080);
        var originalBounds = camera.VisibleWorldBounds;
        var point = new Vector2f(270f, 120f);
        var originalNormalizedPosition = camera.WorldToDevice(point) / camera.ViewportSize;

        camera.SetViewport(width, height);

        Assert.InRange(Vector2f.Distance(originalBounds.Min, camera.VisibleWorldBounds.Min), 0f, 0.001f);
        Assert.InRange(Vector2f.Distance(originalBounds.Max, camera.VisibleWorldBounds.Max), 0f, 0.001f);
        Assert.InRange(Vector2f.Distance(originalNormalizedPosition,
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
        var origin = camera.WorldToDevice(Vector2f.Zero);
        Assert.Equal(Vector2f.Distance(origin, camera.WorldToDevice(Vector2f.UnitX)),
            Vector2f.Distance(origin, camera.WorldToDevice(Vector2f.UnitY)));
        camera.Zoom = 4f;
        Assert.Equal(size.Y / 2f, camera.VisibleWorldBounds.Size.Y);
    }

    [Fact]
    public void ScaledRotatedCameraPickingAndDragUseTheSameTransform()
    {
        var camera = new Camera2D
        {
            ReferenceViewportHeight = 1080f,
            Zoom = 1.35f,
            Position = new Vector2f(100f, 50f),
            Rotation = 0.3f
        };
        camera.SetViewport(960, 540);
        var point = new Vector2f(170f, -25f);
        var devicePoint = camera.WorldToDevice(point);
        Assert.InRange(Vector2f.Distance(point, camera.DeviceToWorld(devicePoint)), 0f, 0.001f);

        var view = camera.GetFrameView();
        Assert.Equal(Rect2f.Builder.Create(Vector2f.Zero, new(960, 540)), view.TargetBounds);
        Assert.Equal(camera.VisibleWorldBounds, view.VisibleWorldBounds);
        Assert.Equal(devicePoint, view.WorldToDevice(point));
        Assert.InRange(Vector2f.Distance(point, view.DeviceToWorld(devicePoint)), 0f, 0.001f);

        var drag = new Vector2f(75f, -20f);
        camera.Position -= Vector2f.TransformNormal(drag, camera.DeviceToWorldMatrix);
        Assert.InRange(Vector2f.Distance(devicePoint + drag, camera.WorldToDevice(point)), 0f, 0.001f);
        Assert.Equal(devicePoint, view.WorldToDevice(point));
    }

    [Fact]
    public void LocalToDeviceAppliesPlacementBeforeTheRotatedCamera()
    {
        var camera = new Camera2D
        {
            Position = new(25, -60),
            Rotation = .36f,
            Zoom = 2.25f
        };
        camera.SetViewport(960, 540);
        var view = camera.GetFrameView();
        var legacyWorldToDevice = Matrix3x2.CreateTranslation((-camera.Position).ToVector2())
            * Matrix3x2.CreateRotation(-camera.Rotation)
            * Matrix3x2.CreateScale(camera.PixelsPerWorldUnit, -camera.PixelsPerWorldUnit)
            * Matrix3x2.CreateTranslation((camera.ViewportSize / 2f).ToVector2());
        var localToWorld = new Matrix3x2(1, .5f, -.25f, 2, 80, -30);

        Assert.Equal(legacyWorldToDevice, view.WorldToDeviceMatrix);
        Assert.Equal(localToWorld * legacyWorldToDevice, view.LocalToDevice(localToWorld));
    }

    [Fact]
    public void AnchoredViewUsesTheSameProjectionAsTheLayeredPreview()
    {
        var anchor = new Vector2f(157f, 101f);
        var view = FrameView2D.FromAnchor(new(640, 480), anchor, 32f);

        Assert.Equal(anchor, view.WorldToDevice(Vector2f.Zero));
        Assert.Equal(anchor + new Vector2f(32f, -32f), view.WorldToDevice(Vector2f.One));
        Assert.Equal(Vector2f.One, view.DeviceToWorld(view.WorldToDevice(Vector2f.One)));
        Assert.Equal(LayeredMeshRenderer.Projection(640, 480, anchor, 32f), LayeredMeshRenderer.Projection(view));
    }

    [Fact]
    public void VisibleWorldBoundsCentersOnTheCameraPosition()
    {
        var camera = new Camera2D { Position = new Vector2f(100f, 50f) };
        camera.SetViewport(800, 600);

        var bounds = camera.VisibleWorldBounds;

        Assert.Equal(new Vector2f(-300f, -250f), bounds.Min);
        Assert.Equal(new Vector2f(500f, 350f), bounds.Max);
    }

    [Fact]
    public void ZoomShrinksTheVisibleWorldRect()
    {
        var camera = new Camera2D { Zoom = 2f };
        camera.SetViewport(800, 600);

        var bounds = camera.VisibleWorldBounds;

        Assert.Equal(new Vector2f(-200f, -150f), bounds.Min);
        Assert.Equal(new Vector2f(200f, 150f), bounds.Max);
    }

    [Fact]
    public void OptionalWorldBoundsConstrainPositionAfterZoomAndResize()
    {
        var camera = new Camera2D
        {
            WorldBounds = Rect2f.Builder.Create(new(-500f), new(500f)),
            Position = new(1000f),
        };
        Assert.Equal(new Vector2f(100f, 200f), camera.Position);

        camera.Zoom = 2f;
        camera.Position = new(1000f);
        Assert.Equal(new Vector2f(300f, 350f), camera.Position);

        camera.SetViewport(1200, 1000);
        Assert.Equal(new Vector2f(200f, 250f), camera.Position);
        Assert.True(camera.VisibleWorldBounds.Max.X <= 500f);
        Assert.True(camera.VisibleWorldBounds.Max.Y <= 500f);
    }

    [Fact]
    public void PixelAndWorldUnitDistancesUseTheLiveCameraScale()
    {
        var camera = new Camera2D { ReferenceViewportHeight = 1080f, Zoom = 2f };
        camera.SetViewport(960, 540);
        Assert.Equal(15f, camera.WorldUnitsToPixels(15f));
        Assert.Equal(15f, camera.PixelsToWorldUnits(15f));

        camera.Zoom = 4f;
        Assert.Equal(30f, camera.WorldUnitsToPixels(15f));
        Assert.Equal(7.5f, camera.PixelsToWorldUnits(15f));
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
