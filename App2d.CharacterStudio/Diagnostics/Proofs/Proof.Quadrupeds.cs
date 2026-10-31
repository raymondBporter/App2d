using App2d.Core.Characters.Authored;
using App2d.Core.Rendering;
using App2d.Core.Rendering.Characters;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace App2d.CharacterStudio;

internal sealed partial class ProofRenders
{
    private bool RenderQuadrupedProof()
    {
        var catalog = ProofCatalog();
        var model = catalog.Resolve("triceratops");
        using var target = RenderCapture2D.CreateTarget(GraphicsDevice, 1440, 960);
        var drawing = new PuppetDrawing();
        var roles = new[] { "idle", "scrape", "head-down", "walk", "rush", "brake", "run", "recover" };
        target.SavePng(Path.Combine(_smokePath, "triceratops-poses.png"), new Color4b(247, 241, 221), () =>
        {
            for (var row = 0; row < roles.Length; row++)
            {
                for (var column = 0; column < 4; column++)
                {
                    var clip = catalog.Animations["triceratops-" + roles[row]];
                    var pose = PoseEvaluator.Sample(model, clip, clip.Duration * column / 3f, input: new()
                    {
                        InPlace = true
                    });
                    drawing.Build(model, pose);
                    _renderer.DrawActor(drawing, PointCharacterRenderer.Projection(target.Width, target.Height, new(column * 360 + 180, row * 120 + 112), 55), Matrix.Identity);
                }
            }
        });

        target.SavePng(Path.Combine(_smokePath, "triceratops-silhouette.png"), new Color4b(247, 241, 221), () =>
        {
            for (var row = 0; row < 2; row++)
            {
                drawing.Build(model, PoseEvaluator.Rest(model));
                _renderer.DrawActor(drawing, PointCharacterRenderer.Projection(target.Width, target.Height, new(720, row * 480 + 420), 200), Matrix.CreateScale(row == 0 ? 1 : -1, 1, 1));
            }
        });

        // P10c: the centaur study composed from the quadruped lower body and the Person upper body; rows: walk, run, slam (walk
        // beneath the hammer slam) and the walk with the sword swing layered over the upper instance; the last row keeps the
        // gait's root motion (not in place) so the travel of the owning module is visible.
        var centaur = catalog.Resolve("centaur");
        var walk = catalog.Animations["centaur-walk"];
        var swing = catalog.Animations["centaur-swing"];
        var mask = centaur.Base.Groups.Single(g => g.Id == "upper-body").Targets.ToHashSet(StringComparer.Ordinal);
        var centaurRows = new (string Label, Func<int, EvaluatedPose> Pose)[]
        {
            ("walk", column => PoseEvaluator.Sample(centaur, walk, walk.Duration * column / 3f, repeat: true, input: new() { InPlace = true })),
            ("run", column => PoseEvaluator.Sample(centaur, catalog.Animations["centaur-run"], catalog.Animations["centaur-run"].Duration * column / 3f, repeat: true, input: new() { InPlace = true })),
            ("slam", column => PoseEvaluator.Sample(centaur, catalog.Animations["centaur-slam"], catalog.Animations["centaur-slam"].Duration * column / 3f, repeat: true, input: new() { InPlace = true })),
            ("walk + swing overlay", column => PoseEvaluator.Sample(centaur, walk, walk.Duration * column / 3f, repeat: true, input: new() { InPlace = true, Overlay = new(swing, swing.Duration * column / 3f, mask, 1) })),
            ("walk travelling", column => PoseEvaluator.Sample(centaur, walk, walk.Duration * column / 3f, repeat: true)),
        };
        target.SavePng(Path.Combine(_smokePath, "centaur-poses.png"), new Color4b(247, 241, 221), () =>
        {
            for (var row = 0; row < centaurRows.Length; row++)
            {
                for (var column = 0; column < 4; column++)
                {
                    drawing.Build(centaur, centaurRows[row].Pose(column));
                    _renderer.DrawActor(drawing, PointCharacterRenderer.Projection(target.Width, target.Height, new(column * 360 + 180, row * 190 + 160), 60), Matrix.Identity);
                }
            }
        });
        File.WriteAllText(Path.Combine(_smokePath, "quadrupeds.txt"), "Pose rows: idle, scrape, head-down, walk, rush, brake, run, recover. Columns: 0, 1/3, 2/3, end. Silhouette: both facings.\n"
            + "Centaur rows (centaur-poses.png): " + string.Join(", ", centaurRows.Select(r => r.Label)) + ". Columns: 0, 1/3, 2/3, end of the wearer clip; the last row is not in place, so the lower module's travel moves the whole figure.\n");
        return false;
    }
}
