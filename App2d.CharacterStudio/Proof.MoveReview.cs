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
/// Review renders for the player move set: every clip at 30 fps through the real drawing path, with the sheathed sword,
/// held props and simple scenery (wall, ladder, ledge), plus layered previews that put upper-body clips on other legs.
/// Writes frames, a manifest and a mechanical report; the review page is assembled from those files.
/// </summary>
internal sealed partial class ProofRenders
{
    private const int ReviewWidth = 440, ReviewHeight = 460, ReviewFps = 30;
    private const float ReviewPpu = 150;
    private static readonly string[] UpperTargets = ["chest", "head", "left-shoulder", "right-shoulder", "left-arm", "right-arm"];

    private sealed record ReviewItem(string Id, string Title, MotionClip Clip, string Scene, bool Gun, string Note, float ViewX = 0);

    private bool RenderMoveReview()
    {
        var catalog = ProofCatalog();
        var model = catalog.Resolve("person");
        var props = PlayerMoves.PlayerMoves.Props().ToDictionary(p => p.Id);
        var sockets = model.Base.Sockets.ToDictionary(s => s.Id);
        var target = new RenderTarget2D(GraphicsDevice, ReviewWidth, ReviewHeight, false, SurfaceFormat.Color, DepthFormat.Depth24, 4, RenderTargetUsage.DiscardContents);
        var drawing = new PuppetDrawing(); var scenery = new CharacterMesh(8192);
        var items = ReviewItems(catalog, model);
        var manifest = new List<object>(); var report = new List<string>();
        foreach (var item in items)
        {
            var folder = Path.Combine(_smokePath!, "frames", item.Id); Directory.CreateDirectory(folder);
            var clip = item.Clip; var count = Math.Max(2, (int)MathF.Round(clip.Duration * ReviewFps) + (clip.Loop ? 0 : 1));
            for (var f = 0; f < count; f++)
            {
                var seconds = Math.Min(clip.Duration, f / (float)ReviewFps);
                var pose = PoseEvaluator.Sample(model, clip, seconds);
                drawing.Build(model, pose);
                var placed = new ActorPose(pose, Vector2.Zero, 1);
                foreach (var (prop, socket) in PersonLoadout.Worn(clip, seconds, item.Gun ? PersonGear.Gun : PersonGear.Sword))
                    drawing.AddProp(props[prop], placed.Socket(sockets[socket]));
                var centerX = pose.Locomotion.X + item.ViewX;
                BuildScenery(scenery, item.Scene, centerX, seconds);
                GraphicsDevice.SetRenderTarget(target); GraphicsDevice.Clear(new Color(241, 240, 232));
                var projection = PointCharacterRenderer.Projection(ReviewWidth, ReviewHeight, new(ReviewWidth * .5f - centerX * ReviewPpu, ReviewHeight * .84f), ReviewPpu);
                _renderer.Draw(scenery, projection, Matrix.Identity, writeDepth: false);
                _renderer.Draw(drawing.Mesh, projection, Matrix.Identity);
                GraphicsDevice.SetRenderTarget(null);
                using var stream = File.Create(Path.Combine(folder, $"{f:D3}.png"));
                target.SaveAsPng(stream, ReviewWidth, ReviewHeight);
            }
            manifest.Add(new { id = item.Id, title = item.Title, clip = clip.Id, duration = clip.Duration, loop = clip.Loop, frames = count, fps = ReviewFps, note = item.Note,
                markers = clip.Markers.Select(k => new { id = k.Id, time = k.Time }) });
            report.Add(Check(model, item));
        }
        File.WriteAllText(Path.Combine(_smokePath!, "manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllLines(Path.Combine(_smokePath!, "checks.txt"), report);
        target.Dispose();
        return false;
    }

    private static List<ReviewItem> ReviewItems(AuthoredCatalog catalog, ResolvedModel model)
    {
        var clips = PlayerMoves.PlayerMoves.Clips(model).ToDictionary(c => c.Id);
        ReviewItem Of(string id, string scene = "ground", bool gun = false, string note = "") => new(id, clips[id].Name, clips[id], scene, gun, note);
        var run = catalog.Animations["person-run"]; var walk = catalog.Animations["person-walk"];
        return
        [
            new("person-walk", "Walk (existing)", walk, "ground", false, "Existing walk, now wearing the sheath."),
            new("person-run", "Run (existing)", run, "ground", false, "Existing run, now wearing the sheath."),
            Of("player-idle"), Of("player-jump", note: "Opens on the push; gameplay leaves the ground on frame one."),
            Of("player-fall", "air"), Of("player-land"), Of("player-dash", note: "Gameplay dash is 0.16 s (marker dash-end); the rest is recovery."),
            Of("player-climb-on", "ladder", note: "The girdles sweep round with no perspective: limbs slide across and swap depth, then reach for the rails."),
            Of("player-climb", "ladder"), Of("player-climb-off", "ladder", note: "The turn onto the ladder played backward."), Of("player-wall-grip", "wall"),
            Of("player-balance-forward", "ledge-ahead"), Of("player-balance-backward", "ledge-behind"),
            Of("player-hit"), Of("player-death") with { ViewX = -.3f }, Of("player-celebrate"),
            Of("player-sword-draw-slash", note: "Damage window 0.10–0.27 s."), Of("player-sword-slash", note: "Follow-up with the sword out."),
            Of("player-sword-sheathe"), Of("player-sword-down-attack", "air", note: "Damage from frame one to 0.083 s."),
            Of("player-gun-aim", gun: true), Of("player-gun-shot", gun: true, note: "Upper body only; the legs here are unkeyed rest. See the layered previews."),
            Of("player-gun-wall-shot", "wall", gun: true),
            new("layer-aim-shot", "Layered: aim + shot", Layer(clips["player-gun-aim"], clips["player-gun-shot"], 2, .5f), "ground", true, "Shot arms on the aim stance, firing every 0.5 s."),
            new("layer-walk-shot", "Layered: walk + shot", Layer(walk, clips["player-gun-shot"], 2, .4f), "ground", true, "Walk legs, shot arms. No new animation."),
            new("layer-run-shot", "Layered: run + shot", Layer(run, clips["player-gun-shot"], 3, .36f), "ground", true, "Run legs, shot arms. No new animation."),
        ];
    }

    /// <summary>
    /// Legs, hips, travel and contacts from <paramref name="legs"/> repeated <paramref name="cycles"/> times; chest, head,
    /// shoulders and arms from <paramref name="arms"/> restarted every <paramref name="period"/> and holding its last key between.
    /// </summary>
    private static MotionClip Layer(MotionClip legs, MotionClip arms, int cycles, float period)
    {
        var duration = legs.Duration * cycles;
        List<ClipKey> Repeat(List<ClipKey> keys, float every, int count, float limit, Func<ClipKey, int, ClipKey>? shift = null)
        {
            var result = new List<ClipKey>();
            for (var n = 0; n < count; n++)
                foreach (var key in keys)
                {
                    var time = key.Time + n * every;
                    if (time > limit + 1e-5f || result.Count > 0 && time <= result[^1].Time + 1e-5f) continue;
                    result.Add((shift?.Invoke(key, n) ?? key) with { Time = MathF.Min(time, limit) });
                }
            return result;
        }
        var stride = legs.Travel.Keys.Count > 0 ? legs.Travel.Keys[^1].X - legs.Travel.Keys[0].X : 0;
        var upper = UpperTargets.ToHashSet();
        var fires = (int)MathF.Ceiling(duration / period);
        return new MotionClip
        {
            Id = legs.Id + "-" + arms.Id, Name = "Layered", Model = legs.Model, StructureRevision = legs.StructureRevision, Duration = duration, Loop = true,
            Reference = legs.Reference,
            Travel = new() { Scale = legs.Travel.Scale, Keys = Repeat(legs.Travel.Keys, legs.Duration, cycles, duration, (k, n) => k with { X = k.X + n * stride }) },
            Tracks =
            [
                .. legs.Tracks.Where(t => !upper.Contains(t.Target)).Select(t => t with { Keys = Repeat(t.Keys, legs.Duration, cycles, duration) }),
                .. arms.Tracks.Where(t => upper.Contains(t.Target)).Select(t => t with { Keys = Repeat(t.Keys, period, fires, duration) }),
            ],
            Contacts = [.. Enumerable.Range(0, cycles).SelectMany(n => legs.Contacts.Select(c => c with
            {
                Start = c.Start + n * legs.Duration, Finish = MathF.Min(duration, c.Finish + n * legs.Duration), Target = c.Target with { X = c.Target.X + n * stride },
            }))],
            Markers = [], Faces = arms.Faces,
        };
    }

    private static void BuildScenery(CharacterMesh mesh, string scene, float centerX, float seconds)
    {
        mesh.Clear(); var ink = new Color(150, 162, 152); var faint = new Color(196, 204, 194); var pixel = 1 / ReviewPpu;
        void Line(float x0, float y0, float x1, float y1, float width, Color color) => mesh.Line(new(x0, y0, 7), new(x1, y1, 7), width * pixel, color);
        switch (scene)
        {
            case "air": break;
            case "ledge-ahead": Line(centerX - 3, 0, .16f, 0, 2, ink); Line(.16f, 0, .16f, -2, 2, ink); break;
            case "ledge-behind": Line(-.14f, 0, centerX + 3, 0, 2, ink); Line(-.14f, 0, -.14f, -2, 2, ink); break;
            default:
                Line(centerX - 3, 0, centerX + 3, 0, 2, ink);
                var first = (int)MathF.Floor((centerX - 2) * 4);
                for (var x = first; x <= first + 16; x++) Line(x * .25f, 0, x * .25f, -(x % 4 == 0 ? 9 : 4) * pixel, 1, ink);
                break;
        }
        if (scene == "wall") { Line(-.3f, -1, -.3f, 3, 3, ink); for (var y = -1f; y < 3; y += .2f) Line(-.3f, y, -.42f, y - .12f, 1, faint); }
        if (scene == "ladder")
        {
            const float left = -.36f, right = .36f, spacing = .275f; var scroll = -(seconds * 1.375f % spacing);
            Line(left, -1, left, 3.2f, 3, faint); Line(right, -1, right, 3.2f, 3, faint);
            for (var y = -1f + scroll; y < 3.2f; y += spacing) Line(left, y, right, y, 3, faint);
        }
    }

    /// <summary>Mechanical checks only: planted feet, limbs reaching, loop seams and the shared hip/shoulder turn. Art judgment is the reviewer's.</summary>
    private static string Check(ResolvedModel model, ReviewItem item)
    {
        var clip = item.Clip; var samples = Math.Max(2, (int)(clip.Duration * 240));
        float contact = 0, turnSum = 0, swing = 0; var overreach = 0;
        static float Yaw(EvaluatedPose p, string left, string right, string frame)
        {
            var d = PoseEvaluator.RotateXY(p.Points[right] - p.Points[left], -p.Angles[frame]);
            return MathF.Atan2(d.Z, d.X);
        }
        var rest = PoseEvaluator.Rest(model);
        var restShoulders = Yaw(rest, "left-shoulder", "right-shoulder", "chest"); var restHips = Yaw(rest, "left-hip", "right-hip", "hips");
        for (var i = 0; i <= samples; i++)
        {
            var pose = PoseEvaluator.Sample(model, clip, i * clip.Duration / samples);
            contact = pose.Contacts.Select(c => c.Residual).Append(contact).Max();
            if (pose.Chains.Any(c => !c.Reached && c.Residual > .005f)) overreach++;
            var shoulders = Yaw(pose, "left-shoulder", "right-shoulder", "chest") - restShoulders;
            var hips = Yaw(pose, "left-hip", "right-hip", "hips") - restHips;
            var offset = MathF.IEEERemainder(shoulders - hips, MathF.Tau); // back views sit on the +-180 degree seam
            turnSum += offset; swing = MathF.Max(swing, MathF.Abs(offset));
        }
        var seam = 0f;
        if (clip.Loop)
        {
            var a = PoseEvaluator.Sample(model, clip, 0); var b = PoseEvaluator.Sample(model, clip, clip.Duration);
            seam = a.Points.Keys.Max(k => Vector3.Distance(a.Points[k] - a.Locomotion, b.Points[k] - b.Locomotion));
        }
        return $"{item.Id}: contact residual {contact:F4}, frames overreaching {overreach}/{samples + 1}, shared turn offset (mean) {turnSum / (samples + 1) * 180 / MathF.PI:F2} deg, peak counter-rotation {swing * 180 / MathF.PI:F1} deg" +
            (clip.Loop ? $", loop seam {seam:F4}" : "");
    }
}
