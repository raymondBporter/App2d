using App2d.CharacterStudio.PlayerMoves;
using App2d.Core.Characters;
using App2d.Rendering.Characters;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using System.Text.Json;
using Color = Microsoft.Xna.Framework.Color;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace App2d.CharacterStudio;

/// <summary>
/// Swing lab renders: each sword swing experiment at 60 fps through the real drawing path, once plain and once with the
/// live <see cref="BladeSwoosh"/> fed one blade per frame, as the game would. Writes frames, a manifest and a report of
/// the study's mechanical checks; the lab page is assembled from those files.
/// </summary>
internal sealed partial class ProofRenders
{
    private const int LabFps = 60;
    /// <summary>A little further out than the move review, so an overhead swoosh stays in frame.</summary>
    private const float LabPpu = 120, LabGround = .9f;
    /// <summary>The swoosh sits just behind the blade so the sword always draws over it.</summary>
    private static readonly Vector3 SwooshDepth = new(0, 0, .01f);

    private bool RenderSwingLab()
    {
        var catalog = ProofCatalog();
        var model = catalog.Resolve("person");
        var props = PlayerMoves.PlayerMoves.Props().ToDictionary(p => p.Id);
        var sockets = model.Base.Sockets.ToDictionary(s => s.Id);
        var today = PlayerMoves.PlayerMoves.Clips(model).Single(c => c.Id == "player-sword-slash");
        var target = new RenderTarget2D(GraphicsDevice, ReviewWidth, ReviewHeight, false, SurfaceFormat.Color, DepthFormat.Depth24, 4, RenderTargetUsage.DiscardContents);
        var drawing = new PuppetDrawing(); var scenery = new CharacterMesh(8192); var trail = new CharacterMesh(8192);
        var manifest = new List<object>(); var report = new List<string>();
        foreach (var variant in SwingLab.Variants(model, today))
        {
            var clip = variant.Clip; var count = (int)MathF.Round(clip.Duration * LabFps) + 1;
            foreach (var withSwoosh in new[] { false, true })
            {
                var folder = Path.Combine(_smokePath!, "frames", variant.Id, withSwoosh ? "swoosh" : "plain"); Directory.CreateDirectory(folder);
                var swoosh = new BladeSwoosh();
                for (var f = 0; f < count; f++)
                {
                    var seconds = Math.Min(clip.Duration, f / (float)LabFps);
                    var pose = PoseEvaluator.Sample(model, clip, seconds);
                    drawing.Build(model, pose);
                    var placed = new ActorPose(pose, Vector2.Zero, 1);
                    foreach (var (prop, socket) in PersonLoadout.Worn(clip, seconds, PersonGear.Sword))
                        drawing.AddProp(props[prop], placed.Socket(sockets[socket]));
                    trail.Clear();
                    if (withSwoosh && LabBlade(model, clip, seconds, props, sockets) is var (guard, tip))
                    {
                        // Combo swings take the follow-up style from their second swoosh on.
                        swoosh.Style = PersonLoadout.SwooshIndex(clip, seconds) > 0 ? SwooshStyle.FollowUp : SwooshStyle.Primary;
                        swoosh.Record(seconds, guard + SwooshDepth, tip + SwooshDepth, PersonLoadout.Swooshing(clip, seconds));
                        swoosh.Build(trail);
                    }
                    BuildScenery(scenery, "ground", 0, seconds);
                    GraphicsDevice.SetRenderTarget(target); GraphicsDevice.Clear(new Color(241, 240, 232));
                    var projection = PointCharacterRenderer.Projection(ReviewWidth, ReviewHeight, new(ReviewWidth * .45f, ReviewHeight * LabGround), LabPpu);
                    _renderer.Draw(scenery, projection, Matrix.Identity, writeDepth: false);
                    if (trail.Count > 0) _renderer.Draw(trail, projection, Matrix.Identity);
                    _renderer.Draw(drawing.Mesh, projection, Matrix.Identity);
                    GraphicsDevice.SetRenderTarget(null);
                    using var stream = File.Create(Path.Combine(folder, $"{f:D3}.png"));
                    target.SaveAsPng(stream, ReviewWidth, ReviewHeight);
                }
            }
            manifest.Add(new { id = variant.Id, title = variant.Title, note = variant.Note, duration = clip.Duration, frames = count, fps = LabFps,
                markers = clip.Markers.Where(k => k.Id.StartsWith("strike", StringComparison.Ordinal) || k.Id.StartsWith(PersonLoadout.SwooshMarker, StringComparison.Ordinal)).Select(k => new { id = k.Id, time = k.Time }) });
            report.Add(SwingCheck(model, variant, props, sockets));
        }
        File.WriteAllText(Path.Combine(_smokePath!, "manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllLines(Path.Combine(_smokePath!, "checks.txt"), report);
        target.Dispose();
        return false;
    }

    /// <summary>The held sword's blade, guard to tip, in actor space; null while it is sheathed.</summary>
    private static (Vector3 Guard, Vector3 Tip)? LabBlade(ResolvedModel model, MotionClip clip, float seconds, Dictionary<string, PropAsset> props, Dictionary<string, ModelSocket> sockets)
    {
        if (!PersonLoadout.SwordInHand(clip, seconds)) return null;
        var sword = props[PersonLoadout.Sword];
        var frame = new ActorPose(PoseEvaluator.Sample(model, clip, seconds), Vector2.Zero, 1).Socket(sockets[PersonLoadout.SwordSocket]);
        var depth = sword.Shapes.Max(s => s.Points.Max(p => p.Z));
        return (ActorPose.PropPoint(frame, sword, new(.07f, 0, depth)), ActorPose.PropPoint(frame, sword, new(sword.Tip.X, sword.Tip.Y, depth)));
    }

    /// <summary>
    /// The study's mechanical checks: when the tip is fastest against the strike, how fast it brakes after, whether the tip
    /// path during the swoosh is one clean curve, whether the arms reach their targets, and whether the held follow-through
    /// crosses the blade over the torso. Whether it looks good is the reviewer's call.
    /// </summary>
    private static string SwingCheck(ResolvedModel model, SwingLab.Variant variant, Dictionary<string, PropAsset> props, Dictionary<string, ModelSocket> sockets)
    {
        var clip = variant.Clip; const int rate = 240; var frame = 1f / LabFps;
        var times = Enumerable.Range(0, (int)(clip.Duration * rate) + 1).Select(i => i / (float)rate).ToArray();
        var tips = times.Select(t => LabBlade(model, clip, t, props, sockets)?.Tip).ToArray();
        var speed = new float[times.Length];
        for (var i = 1; i < times.Length; i++) speed[i] = tips[i] is { } b && tips[i - 1] is { } a ? Vector3.Distance(a, b) * rate : 0;
        var strike = clip.Markers.FirstOrDefault(m => m.Id == "strike")?.Time ?? 0;
        var swoosh = clip.Markers.FirstOrDefault(m => m.Id == PersonLoadout.SwooshMarker)?.Time ?? 0;
        var swooshEnd = clip.Markers.FirstOrDefault(m => m.Id == PersonLoadout.SwooshEndMarker)?.Time ?? clip.Duration;
        // First swing only: the steps after the swoosh opens, up to its end (a step's speed is its travel since the last sample).
        var window = Enumerable.Range(0, times.Length).Where(i => times[i] > swoosh + 1e-4f && times[i] <= swooshEnd + 1e-4f).ToArray();
        var peak = window.MaxBy(i => speed[i]);
        var after = Math.Min(times.Length - 1, peak + (int)(2 * frame * rate));
        var brake = speed[peak] > 0 ? 1 - speed[after] / speed[peak] : 0;
        var reversals = 0; var lastTurn = 0;
        for (var k = 2; k < window.Length; k++)
        {
            if (tips[window[k]] is not { } c || tips[window[k - 1]] is not { } b || tips[window[k - 2]] is not { } a) continue;
            var d0 = b - a; var d1 = c - b; var cross = d0.X * d1.Y - d0.Y * d1.X;
            if (MathF.Abs(cross) < 1e-6f) continue;
            var turn = MathF.Sign(cross); if (lastTurn != 0 && turn != lastTurn) reversals++; lastTurn = turn;
        }
        // Grip: the blade's angle across the forearm on every rendered frame with the sword in hand (0 = along the arm).
        var grips = Enumerable.Range(0, (int)MathF.Round(clip.Duration * LabFps) + 1).Select(f => f / (float)LabFps).Where(t => PersonLoadout.SwordInHand(clip, t)).Select(t =>
        {
            var p = PoseEvaluator.Sample(model, clip, t); var blade = LabBlade(model, clip, t, props, sockets)!.Value;
            var forearm = p.Points["right-hand"] - p.Points["right-elbow"]; var along = blade.Tip - blade.Guard;
            return MathF.Abs(MathF.IEEERemainder(MathF.Atan2(along.Y, along.X) - MathF.Atan2(forearm.Y, forearm.X), MathF.Tau)) * 180 / MathF.PI;
        }).ToArray();
        var overreach = times.Count(t => PoseEvaluator.Sample(model, clip, t).Chains.Any(c => c.Chain == "right-arm" && !c.Reached && c.Residual > .005f));
        var hold = PoseEvaluator.Sample(model, clip, MathF.Min(clip.Duration, strike + 6 * frame));
        var crossesTorso = LabBlade(model, clip, MathF.Min(clip.Duration, strike + 6 * frame), props, sockets) is var (g, t) &&
            Crosses(new(g.X, g.Y), new(t.X, t.Y), new(hold.Points["hips"].X, hold.Points["hips"].Y), new(hold.Points["chest"].X, hold.Points["chest"].Y));
        return $"{variant.Id}: swoosh {swoosh * 1000:F0}-{swooshEnd * 1000:F0} ms, strike {strike * 1000:F0} ms, tip fastest at {times[peak] * 1000:F0} ms ({(times[peak] - strike) / frame:+0.0;-0.0;0.0} frames from strike), " +
            $"brakes {brake * 100:F0}% within 2 frames, tip path turn reversals {reversals}, sword arm short of target {overreach}/{times.Length} samples, held pose blade over torso {(crossesTorso ? "yes" : "no")}, " +
            $"grip {grips.Min():F0}-{grips.Max():F0} deg across the forearm with {grips.Count(g => g is < 40 or > 140)}/{grips.Length} frames outside 40-140";
    }

    private static bool Crosses(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        static float Side(Vector2 p, Vector2 q, Vector2 r) => (q.X - p.X) * (r.Y - p.Y) - (q.Y - p.Y) * (r.X - p.X);
        return Side(a, b, c) * Side(a, b, d) < 0 && Side(c, d, a) * Side(c, d, b) < 0;
    }
}
