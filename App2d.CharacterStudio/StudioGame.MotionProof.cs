using App2d.Core.Characters;
using App2d.Rendering.Characters;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using Color = Microsoft.Xna.Framework.Color;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace App2d.CharacterStudio;

/// <summary>Phase-one visual gate: one shared walk and run on three Person builds, side by side at one world scale, over a game-scale band.</summary>
internal sealed partial class StudioGame
{
    private const int ProofFrames = 24, ProofWidth = 1800, ProofHeight = 900, ProofPanelHeight = 640;
    private const float ProofPpu = 200, GamePpu = 48; // Arena scale is min(width / 26, height / 8): about 49 at 1280x720.
    private static readonly string[] ProofSubjects = ["person", "tall-thin", "short-broad"];
    private static readonly string[] ProofClips = ["person-walk", "person-run", StarterContent.HeavyWalk];
    private readonly bool _motionSmoke;
    private AuthoredCatalog? _proofCatalog;
    private RenderTarget2D? _proofTarget;
    private readonly PuppetDrawing[] _proofDrawings = [new(), new(), new()];
    private readonly CharacterMesh _proofGround = new(8192);

    private AuthoredCatalog ProofCatalog()
    {
        if (_proofCatalog is not null) return _proofCatalog;
        var catalog = AuthoredCatalog.Load(Path.Combine(_assetRoot, "authored"));
        if (catalog.Errors.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, catalog.Errors));
        return _proofCatalog = catalog;
    }

    private bool PrepareMotionProof()
    {
        if (_smokeIndex >= ProofClips.Length * ProofFrames) { WriteMotionProofReport(); return false; }
        var catalog = ProofCatalog();
        _proofTarget ??= new(GraphicsDevice, ProofWidth, ProofHeight, false, SurfaceFormat.Color, DepthFormat.Depth24, 4, RenderTargetUsage.DiscardContents);
        var clipId = ProofClips[_smokeIndex / ProofFrames]; var frame = _smokeIndex % ProofFrames;
        var clip = catalog.Animations[clipId]; var seconds = frame * clip.Duration / ProofFrames;
        var panel = ProofWidth / ProofSubjects.Length; var band = ProofHeight - ProofPanelHeight;
        GraphicsDevice.SetRenderTarget(_proofTarget); GraphicsDevice.Clear(new Color(237, 238, 226));
        for (var i = 0; i < ProofSubjects.Length; i++)
        {
            var model = catalog.Resolve(ProofSubjects[i]);
            var pose = PoseEvaluator.Sample(model, clip, seconds);
            _proofDrawings[i].Build(model, pose);
            // Close-up: the view follows this subject's travel, so ground ticks scroll past a planted foot.
            GraphicsDevice.Viewport = new(i * panel, 0, panel, ProofPanelHeight);
            var close = PointCharacterRenderer.Projection(panel, ProofPanelHeight, new(panel * .5f - pose.Locomotion.X * ProofPpu, ProofPanelHeight * .92f), ProofPpu);
            BuildProofGround(pose, pose.Locomotion.X, ProofPpu);
            _renderer.Draw(_proofGround, close, Matrix.Identity, writeDepth: false);
            _renderer.Draw(_proofDrawings[i].Mesh, close, Matrix.Identity);
            // Game scale: a fixed camera at arena size, so travel and readability show at player size.
            GraphicsDevice.Viewport = new(0, ProofPanelHeight, ProofWidth, band);
            var game = PointCharacterRenderer.Projection(ProofWidth, band, new(ProofWidth * .12f + i * 11 * GamePpu, band * .85f), GamePpu);
            BuildProofGround(pose, 5, GamePpu);
            _renderer.Draw(_proofGround, game, Matrix.Identity, writeDepth: false);
            _renderer.Draw(_proofDrawings[i].Mesh, game, Matrix.Identity);
        }
        GraphicsDevice.SetRenderTarget(null);
        using var stream = File.Create(Path.Combine(_smokePath!, $"{clipId}-{frame:D2}.png"));
        _proofTarget.SaveAsPng(stream, ProofWidth, ProofHeight);
        return true;
    }

    private void BuildProofGround(EvaluatedPose pose, float centerX, float ppu)
    {
        _proofGround.Clear(); var ink = new Color(154, 169, 158); var pixel = 1 / ppu;
        _proofGround.Line(new(centerX - 20, 0, 7), new(centerX + 20, 0, 7), pixel, ink);
        var first = (int)MathF.Floor((centerX - 6) * 4);
        for (var x = first; x <= first + 48; x++) _proofGround.Line(new(x * .25f, 0, 7), new(x * .25f, -(x % 4 == 0 ? 9 : 4) * pixel, 7), pixel, ink);
        foreach (var contact in pose.Contacts)
        {
            var color = contact.Residual < .005f ? new Color(36, 140, 104) : new Color(240, 106, 50); var p = contact.Target with { Z = 6.9f };
            _proofGround.Line(p - new Vector3(8 * pixel, 0, 0), p + new Vector3(8 * pixel, 0, 0), 2 * pixel, color);
            _proofGround.Line(p - new Vector3(0, 8 * pixel, 0), p + new Vector3(0, 8 * pixel, 0), 2 * pixel, color);
        }
    }

    private void WriteMotionProofReport()
    {
        var catalog = ProofCatalog();
        var lines = new List<string> { "Panels, left to right: " + string.Join(", ", ProofSubjects) + $". Close-ups at {ProofPpu} px/unit; bottom band at game scale, {GamePpu} px/unit, fixed camera." };
        foreach (var clipId in ProofClips)
            foreach (var subject in ProofSubjects)
            {
                var model = catalog.Resolve(subject); var clip = catalog.Animations[clipId];
                var poses = Enumerable.Range(0, 481).Select(i => PoseEvaluator.Sample(model, clip, i * clip.Duration / 240.0, repeat: true)).ToArray();
                var stride = PoseEvaluator.Sample(model, clip, clip.Duration, repeat: true).Locomotion.X - poses[0].Locomotion.X;
                var contact = poses.SelectMany(p => p.Contacts).Select(c => c.Residual).DefaultIfEmpty().Max();
                var reach = poses.SelectMany(p => p.Chains).Max(c => c.Residual);
                lines.Add($"{clipId} on {subject}: stride {stride:F3}, max contact residual {contact:E1}, max reach residual {reach:E1}");
            }
        File.WriteAllLines(Path.Combine(_smokePath!, "motion-proof.txt"), lines);
    }
}
