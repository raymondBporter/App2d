using App2d.Core.Characters.Attachments;
using App2d.Core.Rendering;
using App2d.Core.Rendering.Characters;
using Microsoft.Xna.Framework;

namespace App2d.Rendering;

public sealed partial class Renderer2D
{
    private PointCharacterRenderer? _characterRenderer;
    private LayeredMesh? _swooshMesh;

    private LayeredMesh? _characterMesh;

    private void DrawCharacter(WorldObject2D visual, CharacterShader shader)
    {
        if (shader.Pose is not { } pose)
        {
            return;
        }

        _characterRenderer ??= new(_device);
        (_characterMesh ??= new()).Clear();
        AttachmentPainter.Paint(_characterMesh, pose.Posed, shader.Drawn);
        var world = Matrix.CreateScale(pose.Facing, 1, 1) * LayeredMeshRenderer.ToXnaMatrix(View.LocalToDevice(visual.Transform.Matrix));
        var projection = LayeredMeshRenderer.DeviceProjection(_device.Viewport.Width, _device.Viewport.Height);
        LayeredMesh? swoosh = null;
        if (shader.Swoosh is { IsVisible: true } visible)
        {
            (_swooshMesh ??= new(4096)).Clear();
            visible.Build(_swooshMesh);
            swoosh = _swooshMesh;
        }
        _characterRenderer.DrawActor(_characterMesh, projection, world, swoosh);
    }

    private void DisposeCharacters()
    {
        _characterRenderer?.Dispose();
    }
}
