using App2d.Core.Characters;
using App2d.Rendering.Characters;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;

namespace App2d.Rendering;

public sealed partial class Renderer2D
{
    private PointCharacterRenderer? _characterRenderer;
    private readonly Dictionary<PointLibrary, (CharacterGeometry Geometry, EntityPose Pose)> _characterWorkspaces = [];
    private readonly Dictionary<string, Texture2D> _characterFaces = [];
    private PuppetDrawing? _authoredDrawing;

    private void DrawAuthored(WorldObject2D visual, AuthoredCharacterShader shader)
    {
        if (shader.Pose is not { } pose) return;
        _characterRenderer ??= new(_device);
        _authoredDrawing ??= new();
        _authoredDrawing.Build(shader.Entity, pose);
        var m = visual.Transform.LocalToWorldMatrix * _camera.WorldToDeviceMatrix;
        var world = Matrix.CreateScale(shader.Facing, 1, 1) * new Matrix(m.M11, m.M12, 0, 0, m.M21, m.M22, 0, 0, 0, 0, 1, 0, m.M31, m.M32, 0, 1);
        var projection = Matrix.CreateOrthographicOffCenter(0, _device.Viewport.Width, _device.Viewport.Height, 0, -64, 64);
        projection.M33 = 1f / 128; projection.M43 = .5f;
        _device.Clear(ClearOptions.DepthBuffer, Color.Transparent, 1, 0);
        _characterRenderer.Draw(_authoredDrawing.Mesh, projection, world);
    }

    private void DrawCharacter(WorldObject2D visual, PointCharacterShader shader)
    {
        _characterRenderer ??= new(_device);
        var type = shader.Type;
        var library = shader.Catalog.Libraries[type.Library];
        if (!_characterWorkspaces.TryGetValue(library, out var workspace))
            _characterWorkspaces.Add(library, workspace = (new(library), new(library)));
        var action = type.Actions.GetValueOrDefault(shader.Action) ?? type.Actions["idle"];
        workspace.Pose.Evaluate(type, action, shader.Seconds, shader.FacingLeft);
        var look = workspace.Pose.Look;
        if (shader.Weapon is { } weapon) look = look with { Weapons = weapon != "none", Weapon = weapon == "none" ? "sword" : weapon };
        workspace.Geometry.Build(library.Clips[action.Clip], workspace.Pose.ClipTime, look, new(false, false, true, Face: shader.Face), true);
        var m = visual.Transform.LocalToWorldMatrix * _camera.WorldToDeviceMatrix;
        var world = Matrix.CreateTranslation(workspace.Pose.Offset.X, workspace.Pose.Offset.Y, 0) *
            new Matrix(m.M11, m.M12, 0, 0, m.M21, m.M22, 0, 0, 0, 0, 1, 0, m.M31, m.M32, 0, 1);
        var projection = Matrix.CreateOrthographicOffCenter(0, _device.Viewport.Width, _device.Viewport.Height, 0, -64, 64);
        projection.M33 = 1f / 128; projection.M43 = .5f;
        _device.Clear(ClearOptions.DepthBuffer, Color.Transparent, 1, 0);
        _characterRenderer.Draw(workspace.Geometry.Backdrop, projection, world, writeDepth: false);
        _characterRenderer.Draw(workspace.Geometry.Body, projection, world);
        var face = look.CustomHead?.Face ?? look.Face;
        if (face is "happy" or "grumpy")
        {
            var path = Path.Combine(shader.Catalog.Root, "faces", face + ".png");
            if (!_characterFaces.TryGetValue(path, out var texture))
            {
                using var stream = File.OpenRead(path);
                _characterFaces.Add(path, texture = Texture2D.FromStream(_device, stream));
            }
            _characterRenderer.Draw(workspace.Geometry.Face, projection, world, texture, false);
        }
    }

    private void DisposeCharacters()
    {
        _characterRenderer?.Dispose();
        foreach (var texture in _characterFaces.Values) texture.Dispose();
        _characterFaces.Clear();
        _characterWorkspaces.Clear();
    }
}
