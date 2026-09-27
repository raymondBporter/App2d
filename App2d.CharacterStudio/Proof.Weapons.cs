using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Rendering.Characters;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using Color = Microsoft.Xna.Framework.Color;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace App2d.CharacterStudio;

internal sealed partial class ProofRenders
{
    private bool RenderWeaponProof()
    {
        var catalog = ProofCatalog();
        using var target = new RenderTarget2D(GraphicsDevice, 1200, 750, false, SurfaceFormat.Color, DepthFormat.Depth24, 4, RenderTargetUsage.DiscardContents);
        GraphicsDevice.SetRenderTarget(target); GraphicsDevice.Clear(new Color(237, 238, 226));
        var props = new[] { "sword", "pistol", "hammer", "spear", "sheath" };
        var angles = new[] { (0f, 0f), (45f, 0f), (90f, 0f), (135f, 0f), (180f, 0f), (30f, 45f) };
        var drawing = new PuppetDrawing();
        for (var row = 0; row < props.Length; row++) for (var column = 0; column < angles.Length; column++)
        {
            drawing.Mesh.Clear(); var (twist, tilt) = angles[column];
            var rotation = Matrix4x4.CreateRotationX(twist * MathF.PI / 180) * Matrix4x4.CreateRotationY(tilt * MathF.PI / 180);
            drawing.AddProp(catalog.Props[props[row]], new(Vector3.Zero, Vector3.Transform(Vector3.UnitX, rotation), Vector3.Transform(Vector3.UnitY, rotation), Vector3.Transform(Vector3.UnitZ, rotation)));
            _renderer.Draw(drawing.Mesh, PointCharacterRenderer.Projection(1200, 750, new(column * 200 + 65, row * 150 + 75), 90), Matrix.Identity);
        }
        GraphicsDevice.SetRenderTarget(null);
        using (var file = File.Create(Path.Combine(_smokePath, "weapon-turntable.png"))) target.SaveAsPng(file, target.Width, target.Height);

        GraphicsDevice.SetRenderTarget(target); GraphicsDevice.Clear(new Color(237, 238, 226));
        var model = catalog.Resolve("person"); var clip = catalog.Animations["player-sword-backhand"];
        for (var row = 0; row < 2; row++) for (var column = 0; column < 6; column++)
        {
            var at = column / 5f * clip.Duration;
            var pose = PoseEvaluator.Sample(model, clip, at); drawing.Build(model, pose);
            var placed = new ActorPose(pose, Vector2.Zero, 1);
            foreach (var (prop, socket) in PersonLoadout.Dressed(clip, at, PersonGear.Sword, catalog.Entities["hero"]))
                drawing.AddProp(catalog.Props[prop], placed.Socket(model.Base.Sockets.First(s => s.Id == socket)));
            _renderer.Draw(drawing.Mesh, PointCharacterRenderer.Projection(1200, 750, new(column * 200 + 100, row * 375 + 325), 105), Matrix.CreateScale(row == 0 ? 1 : -1, 1, 1));
        }
        GraphicsDevice.SetRenderTarget(null);
        using (var file = File.Create(Path.Combine(_smokePath, "sword-swing.png"))) target.SaveAsPng(file, target.Width, target.Height);
        File.WriteAllText(Path.Combine(_smokePath, "weapons.txt"), "Turntable rows: sword, pistol, hammer, spear, sheath. Columns: twist 0, 45, 90, 135, 180 degrees; then twist 30 with tilt 45.\nSwing: 0, .09, .16, .21, .27, .35 seconds; right-facing then left-facing.\n");
        return false;
    }
}
