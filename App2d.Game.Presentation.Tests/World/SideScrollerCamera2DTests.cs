using App2d.Core.Geometry;
using App2d.Gameplay.World;
using App2d.Rendering;
using System.Numerics;
using Xunit;

namespace App2d.Gameplay.Tests.World;

public sealed class SideScrollerCamera2DTests
{
    [Fact]
    public void ShakeTemporarilyOffsetsCameraWithoutMovingFollowPosition()
    {
        var (controller, camera) = CreateCamera();
        var restingPosition = camera.Position;

        controller.Shake(6f);
        controller.Update(Vector2.Zero, Vector2.Zero, isGrounded: true, 1f / 60f);

        Assert.InRange(Vector2.Distance(camera.Position, restingPosition), 0.1f, 6f);

        for (var frame = 0; frame < 180; frame++)
            controller.Update(Vector2.Zero, Vector2.Zero, isGrounded: true, 1f / 60f);

        Assert.InRange(Vector2.Distance(camera.Position, restingPosition), 0f, 0.001f);
    }

    [Fact]
    public void ResetImmediatelyClearsActiveShake()
    {
        var (controller, camera) = CreateCamera();

        controller.Shake(6f);
        controller.Update(Vector2.Zero, Vector2.Zero, isGrounded: true, 1f / 60f);
        controller.Reset(Vector2.Zero);

        Assert.Equal(new Vector2(0f, 40f), camera.Position);
    }

    [Fact]
    public void ShakeDoesNotExposeSpaceBelowLevelBounds()
    {
        var levelBounds = new Bounds2D(new Vector2(-5_000f), new Vector2(5_000f));
        var camera = new Camera2D();
        var controller = new SideScrollerCamera2D(
            new Scene2D(),
            camera,
            levelBounds,
            new Vector2(0f, levelBounds.Min.Y),
            _ => -10_000f);

        controller.Shake(20f);
        for (var frame = 0; frame < 60; frame++)
        {
            controller.Update(
                new Vector2(0f, levelBounds.Min.Y),
                Vector2.Zero,
                isGrounded: true,
                1f / 60f);

            Assert.True(camera.VisibleWorldBounds.Min.Y >= levelBounds.Min.Y - 0.001f);
        }
    }

    [Fact]
    public void StabilizedShakeSoftensVerticalFollowDuringImpact()
    {
        var (regularController, regularCamera) = CreateCamera(new Vector2(0f, 1_000f));
        var (stabilizedController, stabilizedCamera) = CreateCamera(new Vector2(0f, 1_000f));

        regularController.Shake(4.5f);
        stabilizedController.Shake(4.5f, stabilizeVerticalFollow: true);
        regularController.Update(Vector2.Zero, Vector2.Zero, isGrounded: true, 1f / 60f);
        stabilizedController.Update(Vector2.Zero, Vector2.Zero, isGrounded: true, 1f / 60f);

        Assert.True(stabilizedCamera.Position.Y > regularCamera.Position.Y);
    }

    private static (SideScrollerCamera2D Controller, Camera2D Camera) CreateCamera(
        Vector2? initialPlayerPosition = null)
    {
        var camera = new Camera2D();
        var controller = new SideScrollerCamera2D(
            new Scene2D(),
            camera,
            new Bounds2D(new Vector2(-5_000f), new Vector2(5_000f)),
            initialPlayerPosition ?? Vector2.Zero,
            _ => -1_000f);
        return (controller, camera);
    }
}
