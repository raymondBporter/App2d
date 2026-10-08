using App2d.Core.Characters.Authored;
using App2d.Core.Rendering;
using App2d.Core.Rendering.Characters;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;

namespace App2d.Rendering;

public sealed partial class Renderer2D
{
    private PointCharacterRenderer? _characterRenderer;
    private PuppetDrawing? _authoredDrawing;
    private CharacterMesh? _swooshMesh;

    private void DrawAuthored(WorldObject2D visual, AuthoredCharacterShader shader)
    {
        if (shader.Pose is not { } pose) return;
        _characterRenderer ??= new(_device);
        _authoredDrawing ??= new();
        _authoredDrawing.Build(shader.Model, pose, shader.Face);
        var local = new ActorPose(pose, System.Numerics.Vector2.Zero, 1);
        foreach (var (prop, socket) in shader.Props) _authoredDrawing.AddProp(prop, local.Socket(socket));
        var m = visual.Transform.Matrix * _camera.WorldToDeviceMatrix;
        var world = Matrix.CreateScale(shader.Facing, 1, 1) * new Matrix(m.M11, m.M12, 0, 0, m.M21, m.M22, 0, 0, 0, 0, 1, 0, m.M31, m.M32, 0, 1);
        var projection = Matrix.CreateOrthographicOffCenter(0, _device.Viewport.Width, _device.Viewport.Height, 0, -64, 64);
        projection.M33 = 1f / 128; projection.M43 = .5f;
        _device.Clear(ClearOptions.DepthBuffer, Color.Transparent, 1, 0);
        if (shader.Swoosh is { IsVisible: true } swoosh)
        {
            (_swooshMesh ??= new(4096)).Clear(); swoosh.Build(_swooshMesh);
            if (_swooshMesh.Count > 0) _characterRenderer.Draw(_swooshMesh, projection, world);
        }
        _characterRenderer.Draw(_authoredDrawing, projection, world);
    }

    private void DisposeCharacters()
    {
        _characterRenderer?.Dispose();
    }
}
