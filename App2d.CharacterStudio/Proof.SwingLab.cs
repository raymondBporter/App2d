using App2d.CharacterStudio.PlayerMoves;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
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
        // The lab and game share the approved cartoon sword.
        var props = PlayerMoves.PlayerMoves.Props().ToDictionary(p => p.Id);
        foreach (var (id, art) in catalog.Props) props.TryAdd(id, art);
        var todaySword = props[PersonLoadout.Sword];
        props[PersonLoadout.Sword] = PlayerMoves.PlayerMoves.CartoonSwordArt();
        var sockets = model.Base.Sockets.ToDictionary(s => s.Id);
        var game = PlayerMoves.PlayerMoves.Clips(model).ToDictionary(c => c.Id);
        var shapes = new Dictionary<MotionClip, List<(float Start, float Finish, Vector2[] Hull)>>();
        List<(float Start, float Finish, Vector2[] Hull)> ShapesOf(MotionClip clip) =>
            shapes.TryGetValue(clip, out var found) ? found : shapes[clip] = HitShapes(model, clip, props, sockets);
        var target = new RenderTarget2D(GraphicsDevice, ReviewWidth, ReviewHeight, false, SurfaceFormat.Color, DepthFormat.Depth24, 4, RenderTargetUsage.DiscardContents);
        var drawing = new PuppetDrawing(); var scenery = new CharacterMesh(8192); var trail = new CharacterMesh(8192);
        var projection = PointCharacterRenderer.Projection(ReviewWidth, ReviewHeight, new(ReviewWidth * .45f, ReviewHeight * LabGround), LabPpu);
        void Frame(string path, MotionClip clip, float seconds, Dictionary<string, PropAsset> worn, BladeSwoosh? swoosh, bool shape, float clock = 0)
        {
            var pose = PoseEvaluator.Sample(model, clip, seconds);
            drawing.Build(model, pose);
            var placed = new ActorPose(pose, Vector2.Zero, 1);
            foreach (var (prop, socket) in PersonLoadout.Dressed(clip, seconds, PersonGear.Sword, catalog.Entities["hero"]))
                drawing.AddProp(worn[prop], placed.Socket(sockets[socket]));
            trail.Clear();
            if (swoosh is not null && LabBlade(model, clip, seconds, worn, sockets) is var (guard, tip))
            {
                // A combo alternates: forehands take the opening swoosh, backhands the follow-up one and turn the other way. A
                // clip holding one swing reads its turn from the clip, as the game does.
                var index = PersonLoadout.SwooshIndex(clip, seconds);
                var backhand = index % 2 == 1 || index == 0 && PersonLoadout.Backhand(model, clip, worn[PersonLoadout.Sword], sockets[PersonLoadout.SwordSocket]);
                swoosh.Style = backhand ? SwooshStyle.FollowUp : SwooshStyle.Primary; swoosh.Sweep = backhand ? 1 : -1;
                swoosh.Record(clock, guard + SwooshDepth, tip + SwooshDepth, PersonLoadout.Swooshing(clip, seconds));
                swoosh.Build(trail);
            }
            if (shape && ShapesOf(clip).FirstOrDefault(w => seconds >= w.Start - 1e-4f && seconds < w.Finish - 1e-4f).Hull is { } hull)
            {
                // The player's hit: one shape fixed to the player for the live window, made from everything the blade covers then.
                var red = new Color(214, 62, 48); var w = 2 / LabPpu;
                for (var k = 0; k < hull.Length; k++) trail.Line(new(hull[k], -2), new(hull[(k + 1) % hull.Length], -2), w, red);
            }
            BuildScenery(scenery, "ground", 0, seconds);
            GraphicsDevice.SetRenderTarget(target); GraphicsDevice.Clear(new Color(241, 240, 232));
            _renderer.Draw(scenery, projection, Matrix.Identity, writeDepth: false);
            if (trail.Count > 0) _renderer.Draw(trail, projection, Matrix.Identity);
            _renderer.Draw(drawing.Mesh, projection, Matrix.Identity);
            GraphicsDevice.SetRenderTarget(null);
            using var stream = File.Create(path);
            target.SaveAsPng(stream, ReviewWidth, ReviewHeight);
        }

        var manifest = new List<object>(); var report = new List<string>();
        var variants = SwingLab.Variants(model, game).ToList();
        foreach (var variant in variants)
        {
            var duration = variant.Duration; var count = (int)MathF.Round(duration * LabFps) + 1;
            (MotionClip Clip, float Local) At(float t)
            {
                var (clip, start) = variant.Timeline.Last(s => s.Start <= t + 1e-4f);
                return (clip, Math.Clamp(t - start, 0, clip.Duration));
            }
            foreach (var mode in new[] { "plain", "swoosh", "box" })
            {
                var folder = Path.Combine(_smokePath!, "frames", variant.Id, mode); Directory.CreateDirectory(folder);
                var swoosh = mode == "plain" ? null : new BladeSwoosh();
                for (var f = 0; f < count; f++)
                {
                    var (clip, local) = At(f / (float)LabFps);
                    Frame(Path.Combine(folder, $"{f:D3}.png"), clip, local, props, swoosh, mode == "box", f / (float)LabFps);
                }
            }
            // Ticks on the card's timeline: each clip's swoosh and strike markers at their place in the row.
            var markers = variant.Timeline.SelectMany(e => e.Clip.Markers
                .Where(k => k.Id.StartsWith("strike", StringComparison.Ordinal) || k.Id.StartsWith(PersonLoadout.SwooshMarker, StringComparison.Ordinal))
                .Select(k => new { id = k.Id, time = e.Start + k.Time })).DistinctBy(k => (k.id, k.time)).ToList();
            manifest.Add(new { id = variant.Id, title = variant.Title, note = variant.Note, duration, frames = count, fps = LabFps, markers });
            report.Add(SwingCheck(model, variant, props, sockets, ShapesOf(variant.Clip)));
        }
        // The sword under review beside today's, in the same held follow-through.
        var still = variants.Single(v => v.Id == "side-cut").Clip;
        var stillAt = still.Markers.Single(m => m.Id == "strike").Time + 6 / (float)LabFps;
        Frame(Path.Combine(_smokePath!, "sword-today.png"), still, stillAt, new(props) { [PersonLoadout.Sword] = todaySword }, null, false);
        Frame(Path.Combine(_smokePath!, "sword-cartoon.png"), still, stillAt, props, null, false);
        File.WriteAllText(Path.Combine(_smokePath!, "manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllLines(Path.Combine(_smokePath!, "checks.txt"), report);
        target.Dispose();
        return false;
    }

    /// <summary>The player's hit rectangles for a clip, one per live window ("strike" to "recover", "strike-2" to "recover-2"); see <see cref="SwingLab.HitRectangle"/>.</summary>
    private static List<(float Start, float Finish, Vector2[] Hull)> HitShapes(ResolvedModel model, MotionClip clip, Dictionary<string, PropAsset> props, Dictionary<string, ModelSocket> sockets)
    {
        var result = new List<(float, float, Vector2[])>();
        foreach (var suffix in Enumerable.Range(1, 8).Select(n => n == 1 ? "" : "-" + n))
        {
            if (clip.Markers.FirstOrDefault(m => m.Id == "strike" + suffix) is not { } strike || clip.Markers.FirstOrDefault(m => m.Id == "recover" + suffix) is not { } recover) continue;
            var from = clip.Markers.FirstOrDefault(m => m.Id == PersonLoadout.SwooshMarker + suffix)?.Time ?? strike.Time;
            if (SwingLab.HitRectangle(model, clip, from, strike.Time, recover.Time, t => LabBlade(model, clip, t, props, sockets)) is var (min, max))
                result.Add((strike.Time, recover.Time, [min, new(max.X, min.Y), max, new(min.X, max.Y)]));
        }
        return result;
    }

    private static (Vector3 Guard, Vector3 Tip)? LabBlade(ResolvedModel model, MotionClip clip, float seconds, Dictionary<string, PropAsset> props, Dictionary<string, ModelSocket> sockets) =>
        SwingLab.Blade(model, clip, seconds, props[PersonLoadout.Sword], sockets[PersonLoadout.SwordSocket]);

    /// <summary>
    /// The study's mechanical checks: when the tip is fastest against the strike, how fast it brakes after, whether the tip
    /// path during the swoosh is one clean curve, whether the arms reach their targets, and whether the held follow-through
    /// crosses the blade over the torso. Whether it looks good is the reviewer's call.
    /// </summary>
    private static string SwingCheck(ResolvedModel model, SwingLab.Variant variant, Dictionary<string, PropAsset> props, Dictionary<string, ModelSocket> sockets, List<(float Start, float Finish, Vector2[] Hull)> shapes)
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
        var lastRecover = clip.Markers.Where(m => m.Id.StartsWith("recover", StringComparison.Ordinal)).Select(m => m.Time).DefaultIfEmpty(clip.Duration).Max();
        var gripTimes = Enumerable.Range(0, (int)MathF.Round(clip.Duration * LabFps) + 1).Select(f => f / (float)LabFps)
            .Where(t => PersonLoadout.SwordInHand(clip, t) && t >= swoosh - 1e-4f && t <= lastRecover + 6 * frame).ToArray();
        var grips = gripTimes.Select(t =>
        {
            var p = PoseEvaluator.Sample(model, clip, t); var blade = LabBlade(model, clip, t, props, sockets)!.Value;
            var forearm = p.Points["right-hand"] - p.Points["right-elbow"]; var along = blade.Tip - blade.Guard;
            return MathF.Abs(MathF.IEEERemainder(MathF.Atan2(along.Y, along.X) - MathF.Atan2(forearm.Y, forearm.X), MathF.Tau)) * 180 / MathF.PI;
        }).ToArray();
        // All out: the elbow's bend (0 = locked straight) at contact and in the held follow-through.
        float Bend(float t)
        {
            var p = PoseEvaluator.Sample(model, clip, MathF.Min(clip.Duration, t));
            var upper = p.Points["right-elbow"] - p.Points["right-shoulder"]; var fore = p.Points["right-hand"] - p.Points["right-elbow"];
            return MathF.Acos(Math.Clamp(Vector3.Dot(Vector3.Normalize(upper), Vector3.Normalize(fore)), -1, 1)) * 180 / MathF.PI;
        }
        // Reach: the first hit shape's extent from the feet (x forward, y up).
        var shape = shapes.FirstOrDefault().Hull;
        var overreach = times.Count(t => PoseEvaluator.Sample(model, clip, t).Chains.Any(c => c.Chain == "right-arm" && !c.Reached && c.Residual > .005f));
        var hold = PoseEvaluator.Sample(model, clip, MathF.Min(clip.Duration, strike + 6 * frame));
        var crossesTorso = LabBlade(model, clip, MathF.Min(clip.Duration, strike + 6 * frame), props, sockets) is var (g, t) &&
            Crosses(new(g.X, g.Y), new(t.X, t.Y), new(hold.Points["hips"].X, hold.Points["hips"].Y), new(hold.Points["chest"].X, hold.Points["chest"].Y));
        return $"{variant.Id}: swoosh {swoosh * 1000:F0}-{swooshEnd * 1000:F0} ms, strike {strike * 1000:F0} ms, tip fastest at {times[peak] * 1000:F0} ms ({(times[peak] - strike) / frame:+0.0;-0.0;0.0} frames from strike), " +
            $"brakes {brake * 100:F0}% within 2 frames, tip path turn reversals {reversals}, sword arm short of target {overreach}/{times.Length} samples, held pose blade over torso {(crossesTorso ? "yes" : "no")}, " +
            $"grip {grips.Min():F0}-{grips.Max():F0} deg across the forearm with {grips.Count(g => g is < 40 or > 140)}/{grips.Length} frames outside 40-140" +
            (grips.Any(g => g is < 40 or > 140) ? $" (at {string.Join(", ", gripTimes.Where((_, i) => grips[i] is < 40 or > 140).Select(t => $"{t * 1000:F0}"))} ms)" : "") + ", " +
            $"elbow bend {Bend(strike):F0} deg at contact and {Bend(strike + 6 * frame):F0} deg held, " +
            (shape is null ? "no hit shape" : $"hit rectangle x {shape.Min(p => p.X):F2} to {shape.Max(p => p.X):F2}, y {shape.Min(p => p.Y):F2} to {shape.Max(p => p.Y):F2} " +
                $"({(shape.Min(p => p.Y) < HalfHeight(model) ? "reaches" : "misses")} an enemy half the player's height, top at {HalfHeight(model):F2})");
    }

    /// <summary>Half the Person's standing height: with no duck, an enemy this tall must be hittable.</summary>
    private static float HalfHeight(ResolvedModel model) => model.DrawnHeight() / 2;

    private static bool Crosses(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        static float Side(Vector2 p, Vector2 q, Vector2 r) => (q.X - p.X) * (r.Y - p.Y) - (q.Y - p.Y) * (r.X - p.X);
        return Side(a, b, c) * Side(a, b, d) < 0 && Side(c, d, a) * Side(c, d, b) < 0;
    }
}
