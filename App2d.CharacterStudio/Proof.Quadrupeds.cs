using App2d.Core.Characters.Authored;
using App2d.Rendering.Characters;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace App2d.CharacterStudio;

internal sealed partial class ProofRenders
{
    private bool RenderQuadrupedProof()
    {
        var catalog = ProofCatalog(); var model = catalog.Resolve("triceratops");
        using var target = new RenderTarget2D(GraphicsDevice, 1440, 960, false, SurfaceFormat.Color, DepthFormat.Depth24, 4, RenderTargetUsage.DiscardContents);
        GraphicsDevice.SetRenderTarget(target); GraphicsDevice.Clear(new Color(247, 241, 221));
        var drawing = new PuppetDrawing(); var roles = new[] { "idle", "scrape", "head-down", "walk", "rush", "brake", "run", "recover" };
        for (var row = 0; row < roles.Length; row++) for (var column = 0; column < 4; column++)
        {
            var clip = catalog.Animations["triceratops-" + roles[row]];
            var pose = PoseEvaluator.Sample(model, clip, clip.Duration * column / 3f, input: new() { InPlace = true });
            drawing.Build(model, pose);
            _renderer.Draw(drawing.Mesh, PointCharacterRenderer.Projection(target.Width, target.Height, new(column * 360 + 180, row * 120 + 112), 55), Matrix.Identity);
        }
        GraphicsDevice.SetRenderTarget(null);
        using (var output = File.Create(Path.Combine(_smokePath, "triceratops-poses.png"))) target.SaveAsPng(output, target.Width, target.Height);
        GraphicsDevice.SetRenderTarget(target); GraphicsDevice.Clear(new Color(247, 241, 221));
        for (var row = 0; row < 2; row++)
        {
            drawing.Build(model, PoseEvaluator.Rest(model));
            _renderer.Draw(drawing.Mesh, PointCharacterRenderer.Projection(target.Width, target.Height, new(720, row * 480 + 420), 200), Matrix.CreateScale(row == 0 ? 1 : -1, 1, 1));
        }
        GraphicsDevice.SetRenderTarget(null);
        using (var output = File.Create(Path.Combine(_smokePath, "triceratops-silhouette.png"))) target.SaveAsPng(output, target.Width, target.Height);
        File.WriteAllText(Path.Combine(_smokePath, "quadrupeds.txt"), "Pose rows: idle, scrape, head-down, walk, rush, brake, run, recover. Columns: 0, 1/3, 2/3, end. Silhouette: both facings.\n");
        return false;
    }
}
