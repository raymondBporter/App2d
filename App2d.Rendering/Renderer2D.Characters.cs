using App2d.Core.Characters;
using App2d.Rendering.Characters;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;

namespace App2d.Rendering;

public sealed partial class Renderer2D
{
    private PointCharacterRenderer? _characterRenderer;
    private PuppetDrawing? _authoredDrawing;

    private void DrawAuthored(WorldObject2D visual, AuthoredCharacterShader shader)
    {
        if (shader.Pose is not { } pose) return;
        _characterRenderer ??= new(_device);
        _authoredDrawing ??= new();
        _authoredDrawing.Build(shader.Model, pose, shader.Face);
        var local = new ActorPose(pose, System.Numerics.Vector2.Zero, 1);
        foreach (var (prop, socket) in shader.Props) _authoredDrawing.AddProp(prop, local.Socket(socket));
        var m = visual.Transform.LocalToWorldMatrix * _camera.WorldToDeviceMatrix;
        var world = Matrix.CreateScale(shader.Facing, 1, 1) * new Matrix(m.M11, m.M12, 0, 0, m.M21, m.M22, 0, 0, 0, 0, 1, 0, m.M31, m.M32, 0, 1);
        var projection = Matrix.CreateOrthographicOffCenter(0, _device.Viewport.Width, _device.Viewport.Height, 0, -64, 64);
        projection.M33 = 1f / 128; projection.M43 = .5f;
        _device.Clear(ClearOptions.DepthBuffer, Color.Transparent, 1, 0);
        _characterRenderer.Draw(_authoredDrawing.Mesh, projection, world);
    }

    private void DisposeCharacters()
    {
        _characterRenderer?.Dispose();
    }
}
