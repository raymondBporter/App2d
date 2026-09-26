using App2d.Core.Characters;
using App2d.Core.Characters.Editing;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;

namespace App2d.CharacterStudio.Editor;

/// <summary>
/// Scripted walk through the phase-two acceptance path on a scratch copy of the authored assets, rendering the real editor
/// after each step: tall variant, build change while walking, shared clip drag and undo, and a headless tripod from Empty.
/// </summary>
internal sealed class EditorSmoke(string output)
{
    private const int SettleFrames = 4;
    private readonly List<string> _report = [];
    private int _step, _wait;
    private string? _pendingName;
    private (string Name, Action<EditorShell> Run)[] _steps = [];

    public string PrepareWorkspace(string authoredRoot)
    {
        var workspace = Path.Combine(output, "workspace");
        if (Directory.Exists(workspace)) Directory.Delete(workspace, true);
        foreach (var file in Directory.EnumerateFiles(authoredRoot, "*.json", SearchOption.AllDirectories))
        {
            var target = Path.Combine(workspace, Path.GetRelativePath(authoredRoot, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file, target);
        }
        _steps =
        [
            ("01-shared-walk-compare", shell => { var s = shell.Session; s.Open("person-walk"); s.Pin("tall-thin"); s.Pin("short-broad"); s.Seek(.3f); }),
            ("02-new-tall-variant", shell =>
            {
                var s = shell.Session; Check(s.NewVariant("tall-guard", "Tall guard", "person", "tall-thin"), s);
                s.SetClip("person-walk"); s.Selection.Part = "body"; s.TogglePlay();
            }),
            ("03-build-change-while-walking", shell =>
            {
                var s = shell.Session; var variant = s.SubjectVariant!;
                s.Change(variant, () => { variant.Asset.Build["legs"] = 1.4f; variant.Asset.Build["width"] = .7f; }); s.CommitAll();
                Record(s.Transport.Playing, "still walking after the build change");
                Check(s.Save(variant), s);
            }),
            ("04-shared-clip-drag", shell =>
            {
                var s = shell.Session; s.Open("person-walk"); s.Pin("tall-guard"); s.Pin("short-broad"); s.Seek(.2f);
                var chest = s.Evaluate(s.SubjectId!)!.Pose.World("chest"); s.Selection.Control = "chest";
                s.BeginDrag(); for (var i = 1; i <= 8; i++) s.DragControl("chest", chest + new Vector3(.02f * i, .015f * i, 0)); s.EndDrag();
                Record(s.ClipDocument!.CanUndo, "the drag is one undo step");
            }),
            ("05-after-undo", shell => { var s = shell.Session; s.Undo(); Record(!s.ClipDocument!.CanUndo && !s.ClipDocument.Dirty, "undo restores the saved clip"); }),
            ("06-tripod-rig-from-empty", shell => BuildTripod(shell.Session)),
            ("07-tripod-animated", shell => AnimateTripod(shell.Session)),
            ("08-reopened", shell =>
            {
                var reopened = AuthoringWorkspace.Open(shell.Session.Assets.Root);
                Record(reopened.Variant("tall-guard")?.Asset.Build["legs"] == 1.4f, "tall-guard reopens with its build");
                Record(reopened.Resolve("tripod") is { } tripod && reopened.CanPlay("tripod-walk", "tripod", out _) && PoseEvaluator.Sample(tripod, reopened.Clip("tripod-walk")!.Asset, .25).Chains.All(c => c.Reached),
                    "tripod and its walk reopen and play");
                Record(reopened.Documents.All(d => reopened.Problems(d).Count == 0), "no asset needs repair");
                shell.Session.Open("tripod-walk"); shell.Session.TogglePlay();
            }),
            ("09-test-arena", shell =>
            {
                shell.Session.TogglePlay(); shell.LiveTest = false;
                shell.Test.Roster.Clear(); shell.Test.Roster.AddRange(["player", "spear-guard", "stalker-pest"]);
                shell.Test.Start();
                Record(shell.Test.Problems.Count == 0 && shell.Test.Arena?.Actors.Count == 3, "Test starts the player, spear guard and stalker from the drafts");
                // The player walks in and thrusts; the guard answers. Fixed steps, so the frame is reproducible.
                for (var i = 0; i < 150; i++) shell.Test.Advance(1 / 120f, new(Move: 1));
                for (var i = 0; i < 90; i++) shell.Test.Advance(1 / 120f, new(Attack: true));
                shell.Test.Paused = true;
                Record(shell.Test.Arena!.Events.Any(e => e.Event.Sound == "swing"), "a thrust played its swing sound on the strike marker");
                Record(shell.Session.Mode == Workspace.Animate && shell.Session.ClipId == "tripod-walk", "testing left the open document and workspace alone");
            }),
        ];
        return workspace;
    }

    private void BuildTripod(EditorSession s)
    {
        Check(s.NewModel("tripod", "Tripod", "empty"), s);
        var model = s.SubjectModel!;
        Check(s.Edit(model, () =>
        {
            ModelAuthoring.AddControl(model.Asset, null, new(0, .9f, 0), "body");
            for (var i = 0; i < 3; i++)
            {
                var x = (i - 1) * .35f;
                ModelAuthoring.AddControl(model.Asset, "body", new(x, .8f, (i - 1) * .1f), $"hip-{i}");
                ModelAuthoring.AddControl(model.Asset, $"hip-{i}", new(x + .15f, .4f, (i - 1) * .1f), $"knee-{i}");
                ModelAuthoring.AddControl(model.Asset, $"knee-{i}", new(x, 0, (i - 1) * .1f), $"foot-{i}");
                ModelAuthoring.AddChain(model.Asset, $"foot-{i}");
                ModelAuthoring.AddPart(model.Asset, "stroke", $"hip-{i}", $"knee-{i}"); ModelAuthoring.AddPart(model.Asset, "stroke", $"knee-{i}", $"foot-{i}");
            }
            ModelAuthoring.AddMeasure(model.Asset, "leg", ["hip-1", "knee-1", "foot-1"]);
            foreach (var chain in model.Asset.Chains) chain.Scale = "leg";
            var shell = ModelAuthoring.AddPart(model.Asset, "ellipse", "body"); shell.Width = .95f; shell.Height = .38f; shell.Fill = "#c9e0b8";
        }), s);
        Check(s.Save(model), s);
        s.EditRig = true; s.Selection.Control = "knee-1";
        Record(!model.Asset.Parts.Any(p => p.Face != "none") && model.Asset.Chains.Count == 3, "tripod has three IK legs and no head or face");
    }

    private void AnimateTripod(EditorSession s)
    {
        s.EditRig = false;
        Check(s.NewClip("tripod-walk", "Tripod walk", 1), s);
        var clip = s.ClipDocument!;
        Check(s.Edit(clip, () => clip.Asset.Travel.Keys = [new() { Time = 0 }, new() { Time = 1, X = .6f }]), s);
        foreach (var (time, lift) in new[] { (0f, 0f), (.25f, .12f), (.5f, 0f), (.75f, .12f), (1f, 0f) })
        {
            s.Seek(time); var pose = s.Evaluate("tripod")!.Pose;
            s.DragControl("body", pose.World("body") + new Vector3(0, lift * .3f, 0));
            s.DragControl("foot-1", pose.World("foot-1") + new Vector3(.1f * MathF.Sin(time * MathF.Tau), lift, 0));
            s.EndDrag();
        }
        s.Seek(0);
        var subject = s.Evaluate("tripod")!;
        Check(s.Edit(clip, () => { ClipAuthoring.Plant(subject.Model, clip.Asset, subject.Pose, "foot-0-chain", 0, .5f); ClipAuthoring.Plant(subject.Model, clip.Asset, subject.Pose, "foot-2-chain", .5f, .5f); }), s);
        Check(s.Save(clip), s);
        s.Seek(.25f); s.Selection.Control = "foot-1";
    }

    private static void Check(bool ok, EditorSession session) { if (!ok) throw new InvalidOperationException("Smoke step failed: " + session.Message); }
    private void Record(bool ok, string what) => _report.Add((ok ? "PASS " : "FAIL ") + what);

    /// <summary>Runs the next step once the previous one has settled and been captured. Returns false when finished.</summary>
    public bool Step(EditorShell shell)
    {
        if (_wait > 0) { _wait--; shell.Session.Tick(1 / 60f); return true; }
        if (_pendingName is not null) return true;
        if (_step >= _steps.Length) { File.WriteAllLines(Path.Combine(output, "editor-smoke.txt"), _report); return false; }
        var (name, run) = _steps[_step++];
        run(shell); shell.Session.CommitAll();
        _wait = SettleFrames; _pendingName = name;
        return true;
    }

    public void Capture(RenderTarget2D frame)
    {
        if (_pendingName is null || _wait > 0) return;
        using var stream = File.Create(Path.Combine(output, _pendingName + ".png"));
        frame.SaveAsPng(stream, frame.Width, frame.Height);
        _report.Add("frame " + _pendingName); _pendingName = null;
    }
}
