using App2d.Core.Rendering;
using System.Numerics;

namespace App2d.Tests.Rendering;

public sealed class Camera3DTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProjectionAndPickingRayAgreeInAnOrbitingCamera(bool perspective)
    {
        var position = new Vector3(3, 2, 4);
        var camera = perspective ? Camera3D.Perspective(position, Vector3.Zero, Vector3.UnitY, 1.5f)
            : Camera3D.Orthographic(position, Vector3.Zero, Vector3.UnitY, 6, 4);
        var size = new Vector2(900, 600);
        foreach (var point in new[] { Vector3.Zero, new Vector3(.7f, -.4f, .2f), new Vector3(-.2f, .6f, -.3f) })
        {
            Assert.True(camera.TryProject(point, size, out var screen));
            var ray = camera.ScreenRay(new(screen.X, screen.Y), size);
            var plane = new Plane(camera.Forward, -Vector3.Dot(camera.Forward, point));
            Assert.True(ray.TryIntersect(plane, out var distance));
            Assert.InRange(Vector3.Distance(point, ray.At(distance)), 0, .0001f);
            Assert.InRange(ray.Direction.Length(), .99999f, 1.00001f);
        }
        Assert.True(camera.TryProject(Vector3.Zero, size, out var center));
        Assert.Equal(450f, center.X, 3);
        Assert.Equal(300f, center.Y, 3);
    }

    [Fact]
    public void PerspectiveForeshortensWhileOrthographicKeepsSize()
    {
        var perspective = Camera3D.Perspective(new(0, 0, 5), Vector3.Zero, Vector3.UnitY, 1f);
        var orthographic = Camera3D.Orthographic(new(0, 0, 5), Vector3.Zero, Vector3.UnitY, 4f, 4f);
        var size = new Vector2(400);
        Assert.True(perspective.TryProject(new(1, 0, 1), size, out var near));
        Assert.True(perspective.TryProject(new(1, 0, -1), size, out var far));
        Assert.True(near.X > far.X);
        Assert.True(orthographic.TryProject(new(1, 0, 1), size, out near));
        Assert.True(orthographic.TryProject(new(1, 0, -1), size, out far));
        Assert.Equal(near.X, far.X);
        Assert.False(perspective.TryProject(new(0, 0, 6), size, out _));
    }

    [Fact]
    public void PlaneDraggingPreservesTheInitialGrabOffset()
    {
        var camera = Camera3D.Perspective(new(2, 1, 4), Vector3.Zero, Vector3.UnitY, 1f);
        var size = new Vector2(600);
        var hit = new Vector3(.1f, .2f, .1f);
        var origin = new Vector3(.3f, .4f, -.1f);
        Assert.True(camera.TryProject(hit, size, out var screen));
        var plane = new Plane(camera.Forward, -Vector3.Dot(camera.Forward, hit));
        var offset = origin - hit;
        var ray = camera.ScreenRay(new(screen.X + 80, screen.Y - 30), size);
        Assert.True(ray.TryIntersect(plane, out var distance));
        var moved = ray.At(distance) + offset;
        Assert.True(camera.TryProject(moved - offset, size, out var next));
        Assert.Equal(screen.X + 80, next.X, 2);
        Assert.Equal(screen.Y - 30, next.Y, 2);
        Assert.InRange(Math.Abs(Vector3.Dot(camera.Forward, moved - origin)), 0, .0001f);
    }

    [Fact]
    public void InvalidOrSingularCamerasAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new Camera3D(default, Matrix4x4.Identity));
        Assert.Throws<ArgumentOutOfRangeException>(() => Camera3D.Perspective(Vector3.Zero, Vector3.Zero, Vector3.UnitY, 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => Camera3D.Perspective(Vector3.Zero, Vector3.UnitY, Vector3.UnitY, 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => Camera3D.Perspective(Vector3.UnitZ, Vector3.Zero, Vector3.UnitY, 0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => Camera3D.Orthographic(Vector3.UnitZ, Vector3.Zero, Vector3.UnitY, 1, 0));
    }
}
