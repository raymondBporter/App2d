using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Rendering;
using App2d.Core.Rendering.Characters;
using System.Numerics;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace App2d.CharacterStudio;

/// <summary>Actual renderer comparison: segmented strokes versus rounded paths, with identical evaluated poses.</summary>
internal sealed partial class ProofRenders
{
    private static readonly string[] RoundedClips = ["person-walk", "person-run", "player-land", "player-sword-forehand"];

    private bool RenderRoundedPaths()
    {
        if (_smokeIndex >= 24)
        {
            return false;
        }

        var catalog = ProofCatalog();
        var model = catalog.Resolve("person");
        const int width = 1200, height = 800;
        using var target = RenderCapture2D.CreateTarget(GraphicsDevice, width, height);
        var report = new List<string> { "Tiles: walk, run, landing, sword forehand. Within each tile: segmented on left, rounded on right. Lower figures are game scale. 20 sections, softness 1." };
        using (GraphicsDevice.UseRenderTarget(target, new Color4b(241, 240, 232)))
        {
            for (var tile = 0; tile < RoundedClips.Length; tile++)
            {
                var clip = catalog.Animations[RoundedClips[tile]];
                var seconds = clip.Duration * _smokeIndex / 24f;
                var pose = PoseEvaluator.Sample(model, clip, seconds);
                var row = tile / 2;
                var col = tile % 2;
                for (var version = 0; version < 2; version++)
                {
                    var drawing = new PuppetDrawing();
                    if (version == 1)
                    {
                        drawing.Build(model, pose);
                    }
                    else
                    {
                        // The segmented version draws each rounded path as straight strokes between its controls.
                        var parts = new List<PartGeometry>();
                        foreach (var part in model.Parts)
                        {
                            if (part.Geometry is not RoundedPolylineCurve2D || part.Bindings is not { Count: > 1 })
                            {
                                parts.Add(part);
                                continue;
                            }

                            var controls = part.Bindings.OrderBy(b => b.Point).Select(b => b.Control).ToArray();
                            for (var i = 1; i < controls.Length; i++)
                            {
                                parts.Add(part with { A = controls[i - 1], B = controls[i], Bindings = null, Geometry = GeometryJson2D.CreateDefault(CurveKinds2D.Line) });
                            }
                        }
                        drawing.Build(model.Base.Ink, model.Base.LineWidth, parts, pose.World, bone: pose.Placement, rest: id => model.Rest[id]);
                    }
                    if (tile == 3)
                    {
                        var placed = new ActorPose(pose, Vector2.Zero, 1);
                        var hero = catalog.Entities["hero"];
                        foreach (var (prop, socket) in PersonLoadout.Dressed(clip, seconds, PersonGear.Sword, hero))
                        {
                            drawing.AddAttachment(catalog.Parts[prop], placed, hero.Sockets[socket]);
                        }
                    }
                    var facing = tile % 2 == 0 ? 1 : -1;
                    var x = col * 600 + version * 300 + 150;
                    foreach (var (ppu, baseline) in new[] { (100f, 265f), (40f, 387f) })
                    {
                        var projection = PointCharacterRenderer.Projection(width, height, new(x - facing * pose.Locomotion.X * ppu, row * 400 + baseline), ppu);
                        _renderer.DrawActor(drawing, projection, Matrix.CreateScale(facing, 1, 1));
                    }
                    report.Add($"{clip.Id}, {(version == 0 ? "segmented" : "rounded")}: {drawing.Mesh.TriangleCount} triangles");
                }
            }
        }
        target.SavePng(Path.Combine(_smokePath, $"rounded-{_smokeIndex:D2}.png"));
        if (_smokeIndex == 0)
        {
            File.WriteAllLines(Path.Combine(_smokePath, "report.txt"), report);
        }

        return true;
    }
}
