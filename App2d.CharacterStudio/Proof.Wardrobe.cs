using App2d.Core.Characters.Authored;
using App2d.Rendering.Characters;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using Color = Microsoft.Xna.Framework.Color;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace App2d.CharacterStudio;

internal sealed partial class ProofRenders
{
    private bool RenderWardrobeProof()
    {
        var catalog = ProofCatalog();
        const int width = 1500, height = 920;
        using var target = new RenderTarget2D(GraphicsDevice, width, height, false, SurfaceFormat.Color, DepthFormat.Depth24, 4, RenderTargetUsage.DiscardContents);
        var drawing = new PuppetDrawing();
        var subjects = new[] { "hero", "maul-brute", "cinder-gunner" };
        foreach (var poseName in new[] { "idle", "run", "attack", "back" })
        {
            GraphicsDevice.SetRenderTarget(target); GraphicsDevice.Clear(new Color(241, 236, 223));
            for (var column = 0; column < subjects.Length; column++)
            {
                var entity = catalog.Entities[subjects[column]];
                var clipId = poseName switch
                {
                    "run" => "person-run",
                    "attack" => column == 0 ? "player-sword-side-cut" : column == 1 ? "person-hammer-slam" : "person-pistol-shot",
                    "back" => column == 0 ? "player-climb" : "person-death",
                    _ => column == 0 ? "player-idle" : "person-idle",
                };
                var clip = catalog.Animations[clipId];
                var at = poseName == "attack" ? (column == 1 ? .78f : column == 2 ? .6f : .12f) : clip.Duration * .3f;
                var pose = PoseEvaluator.Sample(entity.Model, clip, at, false, new() { InPlace = true });
                if (column == 0)
                {
                    drawing.Build(entity.Model, pose);
                    var placed = new ActorPose(pose, Vector2.Zero, 1);
                    foreach (var (prop, socket) in PersonLoadout.Dressed(clip, at, PersonGear.Sword, entity))
                        drawing.AddProp(catalog.Props[prop], placed.Socket(entity.Sockets[socket]));
                }
                else drawing.Build(entity, pose);
                // Large right/left views plus an actual game-size pair along the bottom.
                foreach (var (x, y, ppu, facing) in new[] { (150, 650, 215, 1), (365, 650, 130, -1), (195, 865, 48, 1), (305, 865, 48, -1) })
                {
                    GraphicsDevice.Clear(ClearOptions.DepthBuffer, Color.Transparent, 1, 0);
                    _renderer.Draw(drawing.Mesh, PointCharacterRenderer.Projection(width, height, new(column * 500 + x, y), ppu), Matrix.CreateScale(facing, 1, 1));
                }
            }
            GraphicsDevice.SetRenderTarget(null);
            using var file = File.Create(Path.Combine(_smokePath, "wardrobe-" + poseName + ".png"));
            target.SaveAsPng(file, width, height);
        }
        File.WriteAllText(Path.Combine(_smokePath, "wardrobe.txt"), "Columns: hero, maul brute, cinder gunner. Each shows both facings, then both at 48 pixels/unit. Sheets: idle, run, attack, and climbing/back view (hero) or death recoil (enemies). Uses packaged assets and the shared depth-tested renderer.\n");
        return false;
    }
}
