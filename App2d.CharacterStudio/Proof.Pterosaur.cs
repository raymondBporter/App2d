using App2d.Core.Characters.Authored;
using App2d.Core.Rendering;
using App2d.Core.Rendering.Characters;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace App2d.CharacterStudio;

internal sealed partial class ProofRenders
{
    private bool RenderPterosaurProof()
    {
        var catalog = ProofCatalog();
        var model = catalog.Resolve("pterosaur");
        var drawing = new PuppetDrawing();
        using var target = RenderCapture2D.CreateTarget(GraphicsDevice, 1440, 1080);
        var roles = new[] { "hover", "fold", "dive", "brake", "low-recovery", "reset" };
        target.SavePng(Path.Combine(_smokePath, "pterosaur-poses.png"), new Color4b(247, 241, 221), () =>
        {
            for (var row = 0; row < roles.Length; row++)
            {
                for (var column = 0; column < 4; column++)
                {
                    var clip = catalog.Animations["pterosaur-" + roles[row]];
                    var pose = PoseEvaluator.Sample(model, clip, clip.Duration * column / 3f);
                    drawing.Build(model, pose);
                    _renderer.DrawActor(drawing, PointCharacterRenderer.Projection(target.Width, target.Height, new(column * 360 + 180, row * 180 + 180), 75), Matrix.Identity);
                }
            }
        });

        target.SavePng(Path.Combine(_smokePath, "pterosaur-silhouette.png"), new Color4b(247, 241, 221), () =>
        {
            for (var row = 0; row < 2; row++)
            {
                drawing.Build(model, PoseEvaluator.Sample(model, catalog.Animations["pterosaur-hover"], .25f));
                _renderer.DrawActor(drawing, PointCharacterRenderer.Projection(target.Width, target.Height, new(720, row * 540 + 460), 220), Matrix.CreateScale(row == 0 ? 1 : -1, 1, 1));
            }
        });

        File.WriteAllText(Path.Combine(_smokePath, "pterosaur.txt"), "Rows: hover, fold, dive, brake, low-recovery, reset. Columns: 0, 1/3, 2/3, end. Artwork and poses only; entity/controller owns the flight path and attack windows.\n");
        return false;
    }
}
