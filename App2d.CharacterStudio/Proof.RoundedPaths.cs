using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Curves;
using App2d.Core.Rendering.Characters;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using Color = Microsoft.Xna.Framework.Color;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace App2d.CharacterStudio;

/// <summary>Actual renderer comparison: old segmented artwork versus rounded paths, with identical evaluated poses.</summary>
internal sealed partial class ProofRenders
{
    private static readonly string[] RoundedClips = ["person-walk", "person-run", "player-land", "player-sword-forehand"];
    private bool RenderRoundedPaths()
    {
        if (_smokeIndex >= 24) return false;
        var catalog = ProofCatalog(); var model = catalog.Resolve("person");
        using var target = new RenderTarget2D(GraphicsDevice, 1200, 800, false, SurfaceFormat.Color, DepthFormat.Depth24, 4, RenderTargetUsage.DiscardContents);
        GraphicsDevice.SetRenderTarget(target); GraphicsDevice.Clear(new Color(241, 240, 232));
        var report = new List<string> { "Tiles: walk, run, landing, sword forehand. Within each tile: old on left, rounded on right. Lower figures are game scale. 20 sections, softness 1." };
        for (var tile = 0; tile < RoundedClips.Length; tile++)
        {
            var clip = catalog.Animations[RoundedClips[tile]];
            var seconds = clip.Duration * _smokeIndex / 24f;
            var pose = PoseEvaluator.Sample(model, clip, seconds);
            var row = tile / 2; var col = tile % 2;
            for (var version = 0; version < 2; version++)
            {
                var drawing = new PuppetDrawing();
                if (version == 1) drawing.Build(model, pose);
                else
                {
                    var parts = new List<PuppetPart>();
                    foreach (var part in model.Parts)
                    {
                        if (part.Geometry is not RoundedPolylineCurveDefinition2D || part.Bindings is not { Count: > 1 }) { parts.Add(part); continue; }
                        var controls = part.Bindings.OrderBy(b => b.Point).Select(b => b.Control).ToArray();
                        for (var i = 1; i < controls.Length; i++)
                            parts.Add(part with { A = controls[i - 1], B = controls[i], Bindings = null, Geometry = new LineCurveDefinition2D { Start = Vector2.Zero, End = Vector2.UnitY } });
                    }
                    drawing.Build(model.Base.Ink, model.Base.LineWidth, parts, pose.World,
                        p => pose.Expressions.GetValueOrDefault(p.Id, "none"), angle: id => pose.Angles[id], transform: id => pose.Bones[id], rest: id => model.Rest[id]);
                }
                if (tile == 3)
                {
                    var placed = new ActorPose(pose, Vector2.Zero, 1);
                    foreach (var (prop, socket) in PersonLoadout.Dressed(clip, seconds, PersonGear.Sword, catalog.Entities["hero"]))
                        drawing.AddProp(catalog.Props[prop], placed.Socket(model.Base.Sockets.Single(s => s.Id == socket)));
                }
                var x = col * 600 + version * 300;
                GraphicsDevice.Viewport = new(x, row * 400, 300, 400);
                var facing = tile % 2 == 0 ? 1 : -1;
                var world = Matrix.CreateScale(facing, 1, 1);
                foreach (var (ppu, baseline) in new[] { (100f, 265f), (40f, 387f) })
                {
                    GraphicsDevice.Clear(ClearOptions.DepthBuffer, Color.Transparent, 1, 0);
                    var projection = PointCharacterRenderer.Projection(300, 400, new(150 - facing * pose.Locomotion.X * ppu, baseline), ppu);
                    _renderer.Draw(drawing, projection, world);
                }
                report.Add($"{clip.Id}, {(version == 0 ? "old" : "rounded")}: {drawing.Mesh.TriangleCount} triangles");
            }
        }
        GraphicsDevice.SetRenderTarget(null);
        using (var stream = File.Create(Path.Combine(_smokePath, $"rounded-{_smokeIndex:D2}.png"))) target.SaveAsPng(stream, 1200, 800);
        if (_smokeIndex == 0) File.WriteAllLines(Path.Combine(_smokePath, "report.txt"), report);
        return true;
    }
}
