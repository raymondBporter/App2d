using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Drawing;
using App2d.Core.Rendering;
using App2d.Core.Rendering.Characters;
using Microsoft.Xna.Framework;

namespace App2d.Rendering;

public sealed partial class Renderer2D
{
    private PointCharacterRenderer? _characterRenderer;
    private PuppetDrawing? _authoredDrawing;
    private CharacterMesh? _swooshMesh;

    private void DrawAuthored(WorldObject2D visual, AuthoredCharacterShader shader)
    {
        if (shader.Pose is not { } pose)
        {
            return;
        }

        _characterRenderer ??= new(_device);
        _authoredDrawing ??= new();
        _authoredDrawing.Build(shader.Model, pose);
        var local = new ActorPose(pose, System.Numerics.Vector2.Zero, 1);
        foreach (var (prop, socket) in shader.Props)
        {
            _authoredDrawing.AddAttachment(prop, local, socket);
        }

        var m = visual.Transform.Matrix * _camera.WorldToDeviceMatrix;
        var world = Matrix.CreateScale(shader.Facing, 1, 1) * ToXnaMatrix(m);
        var projection = PointCharacterRenderer.DeviceProjection(_device.Viewport.Width, _device.Viewport.Height);
        CharacterMesh? swoosh = null;
        if (shader.Swoosh is { IsVisible: true } visible)
        {
            (_swooshMesh ??= new(4096)).Clear();
            visible.Build(_swooshMesh);
            swoosh = _swooshMesh;
        }
        _characterRenderer.DrawActor(_authoredDrawing, projection, world, swoosh);
    }

    private void DisposeCharacters()
    {
        _characterRenderer?.Dispose();
    }
}
