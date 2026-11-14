using App2d.Core.Geometry.Primitives;
using App2d.Core.Rendering;
using App2d.Presentation.World;
using System.Numerics;

namespace App2d.Tests.Presentation.World;

public sealed class SideScrollerCamera2DTests
{
    [Fact]
    public void FollowAndLevelClampingStayConsistentAcrossWindowSizes()
    {
        var bounds = new Rect2D(new Vector2f(-2000f), new Vector2f(2000f));
        var small = new Camera2D { ReferenceViewportHeight = 1080f };
        var large = new Camera2D { ReferenceViewportHeight = 1080f };
        small.SetViewport(960, 540);
        large.SetViewport(1920, 1080);
        var smallController = new SideScrollerCamera2D(small, bounds, Vector2f.Zero);
        var largeController = new SideScrollerCamera2D(large, bounds, Vector2f.Zero);
        for (var frame = 0; frame < 180; frame++)
        {
            var position = new Vector2f(frame * 10f, -frame * 10f);
            smallController.Update(position, new Vector2f(600f, -600f), false, 1f / 60f);
            largeController.Update(position, new Vector2f(600f, -600f), false, 1f / 60f);
            Assert.InRange(Vector2f.Distance(small.Position, large.Position), 0f, 0.001f);
            Assert.True(small.VisibleWorldBounds.Min.Y >= bounds.Min.Y - 0.001f);
            Assert.True(small.VisibleWorldBounds.Max.X <= bounds.Max.X + 0.001f);
        }
        small.SetViewport(1920, 1080);
        smallController.Reset(new Vector2f(1950f, -1950f));
        largeController.Reset(new Vector2f(1950f, -1950f));
        Assert.InRange(Vector2f.Distance(small.Position, large.Position), 0f, 0.001f);
    }

    [Fact]
    public void ShakeTemporarilyOffsetsCameraWithoutMovingFollowPosition()
    {
        var (controller, camera) = CreateCamera();
        var restingPosition = camera.Position;

        controller.Shake(6f);
        controller.Update(Vector2f.Zero, Vector2f.Zero, isGrounded: true, 1f / 60f);

        Assert.InRange(Vector2f.Distance(camera.Position, restingPosition), 0.1f, 6f);

        for (var frame = 0; frame < 180; frame++)
        {
            controller.Update(Vector2f.Zero, Vector2f.Zero, isGrounded: true, 1f / 60f);
        }

        Assert.InRange(Vector2f.Distance(camera.Position, restingPosition), 0f, 0.001f);
    }

    [Fact]
    public void ResetImmediatelyClearsActiveShake()
    {
        var (controller, camera) = CreateCamera();

        controller.Shake(6f);
        controller.Update(Vector2f.Zero, Vector2f.Zero, isGrounded: true, 1f / 60f);
        controller.Reset(Vector2f.Zero);

        Assert.Equal(new Vector2f(0f, 40f), camera.Position);
    }

    [Fact]
    public void ShakeDoesNotExposeSpaceBelowLevelBounds()
    {
        var levelBounds = new Rect2D(new Vector2f(-5_000f), new Vector2f(5_000f));
        var camera = new Camera2D();
        var controller = new SideScrollerCamera2D(
            camera,
            levelBounds,
            new Vector2f(0f, levelBounds.Min.Y));

        controller.Shake(20f);
        for (var frame = 0; frame < 60; frame++)
        {
            controller.Update(
                new Vector2f(0f, levelBounds.Min.Y),
                Vector2f.Zero,
                isGrounded: true,
                1f / 60f);

            Assert.True(camera.VisibleWorldBounds.Min.Y >= levelBounds.Min.Y - 0.001f);
        }
    }

    [Fact]
    public void StabilizedShakeSoftensVerticalFollowDuringImpact()
    {
        var (regularController, regularCamera) = CreateCamera(new Vector2f(0f, 1_000f));
        var (stabilizedController, stabilizedCamera) = CreateCamera(new Vector2f(0f, 1_000f));

        regularController.Shake(4.5f);
        stabilizedController.Shake(4.5f, stabilizeVerticalFollow: true);
        regularController.Update(Vector2f.Zero, Vector2f.Zero, isGrounded: true, 1f / 60f);
        stabilizedController.Update(Vector2f.Zero, Vector2f.Zero, isGrounded: true, 1f / 60f);

        Assert.True(stabilizedCamera.Position.Y > regularCamera.Position.Y);
    }

    [Fact]
    public void LastGroundedPositionHoldsFramingUntilARealFall()
    {
        var (controller, camera) = CreateCamera();
        var restingY = camera.Position.Y;

        controller.Update(new Vector2f(0f, -50f), new Vector2f(0f, -200f), false, 1f / 60f);
        Assert.Equal(restingY, camera.Position.Y);

        for (var frame = 0; frame < 120; frame++)
        {
            controller.Update(new Vector2f(0f, -300f), new Vector2f(0f, -400f), false, 1f / 60f);
        }

        Assert.True(camera.Position.Y < restingY);
    }

    [Fact]
    public void LooksAheadInTravelDirection()
    {
        var (controller, camera) = CreateCamera();
        for (var frame = 0; frame < 120; frame++)
        {
            controller.Update(Vector2f.Zero, new Vector2f(600f, 0f), true, 1f / 60f);
        }

        Assert.True(camera.Position.X > 0f);

        for (var frame = 0; frame < 120; frame++)
        {
            controller.Update(Vector2f.Zero, new Vector2f(-600f, 0f), true, 1f / 60f);
        }

        Assert.True(camera.Position.X < 0f);
    }

    private static (SideScrollerCamera2D Controller, Camera2D Camera) CreateCamera(
        Vector2f? initialPlayerPosition = null)
    {
        var camera = new Camera2D();
        var controller = new SideScrollerCamera2D(
            camera,
            new Rect2D(new Vector2f(-5_000f), new Vector2f(5_000f)),
            initialPlayerPosition ?? Vector2f.Zero);
        return (controller, camera);
    }
}
