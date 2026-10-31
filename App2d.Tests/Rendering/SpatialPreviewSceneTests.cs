using App2d.CharacterStudio.Editor;
using App2d.Core.Rendering;
using System.Numerics;

namespace App2d.Tests.Rendering;

public sealed class SpatialPreviewSceneTests
{
    [Fact]
    public void PickingFollowsObjectPlacementAndResetWithoutRebuildingGeometry()
    {
        var scene = new SpatialPreviewScene(null);
        var cube = scene.Objects[0];
        var initial = cube.Position;
        var version = cube.Mesh.Version;
        var camera = Camera3D.Perspective(new(3, 2, 4), Vector3.Zero, Vector3.UnitY, 1.5f);
        var size = new Vector2(900, 600);
        Assert.True(camera.TryProject(initial, size, out var screen));
        var originalRay = camera.ScreenRay(new(screen.X, screen.Y), size);
        Assert.Equal(0, scene.Pick(originalRay, out _));
        cube.Position += new Vector3(1.5f, 1, 0);
        cube.Rotation = new(.2f, .3f, .4f);
        Assert.Null(scene.Pick(originalRay, out _));
        Assert.True(camera.TryProject(cube.Position, size, out screen));
        Assert.Equal(0, scene.Pick(camera.ScreenRay(new(screen.X, screen.Y), size), out _));
        Assert.Equal(version, cube.Mesh.Version);
        cube.Reset();
        Assert.Equal(initial, cube.Position);
        Assert.Equal(Vector3.Zero, cube.Rotation);
        Assert.Equal(0, scene.Pick(originalRay, out _));
    }
}
