using App2d.CharacterStudio.Editor;
using App2d.Core.Characters.Authored;
using App2d.Core.Rendering;
using System.Numerics;

namespace App2d.CharacterStudio;

internal sealed partial class ProofRenders
{
    private bool RenderSpatialProof()
    {
        var scene = new SpatialPreviewScene(Part.Load(Path.Combine(_assetRoot, "authored", "parts", "sword.json")));
        using var renderer = new Renderer3D(GraphicsDevice);
        using var target = RenderCapture2D.CreateTarget(GraphicsDevice, 1200, 800);
        var position = _smokeIndex == 2 ? new Vector3(3.9f, .2f, .15f) : new Vector3(2f, 1.4f, 3.5f);
        var camera = _smokeIndex == 1 ? Camera3D.Orthographic(position, Vector3.Zero, Vector3.UnitY, 4.5f, 3f)
            : Camera3D.Perspective(position, Vector3.Zero, Vector3.UnitY, 1.5f);
        using (GraphicsDevice.UseRenderTarget(target)) scene.Draw(renderer, camera);
        var name = _smokeIndex switch { 0 => "01-perspective", 1 => "02-orthographic", _ => "03-side" };
        target.SavePng(Path.Combine(_smokePath, name + ".png"));
        if (_smokeIndex == 0)
        {
            var cube = scene.Objects[1];
            if (!camera.TryProject(cube.Position, new(1200, 800), out var screen)) throw new InvalidOperationException("Cube is outside the preview camera.");
            var picked = scene.Pick(camera.ScreenRay(new(screen.X, screen.Y), new(1200, 800)), out _);
            if (picked != 1) throw new InvalidOperationException("The camera ray did not select the calibration cube.");
            File.WriteAllText(Path.Combine(_smokePath, "checks.txt"), "PASS: existing sword mesh rendered through Core Renderer3D\nPASS: shared scene depth\nPASS: perspective and orthographic cameras\nPASS: screen ray picked calibration cube\n");
        }
        return _smokeIndex < 2;
    }
}
