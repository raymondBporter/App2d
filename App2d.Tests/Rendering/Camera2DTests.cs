using App2d.Rendering;
using System.Numerics;

namespace App2d.Tests.Rendering;

public sealed class Camera2DTests
{
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
