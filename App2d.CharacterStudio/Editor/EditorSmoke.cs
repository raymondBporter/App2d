using App2d.Core.Characters;
using App2d.Core.Characters.Editing;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;

namespace App2d.CharacterStudio.Editor;

/// <summary>
/// Scripted walk through the acceptance paths on a scratch copy of the authored assets, rendering the real editor after each
/// step. Phase two: tall variant, build change while walking, shared clip drag and undo, and a headless tripod from Empty.
/// Phase four: several people from builds and looks, Standard and Heavy motion on different sizes, an entity from a template
/// with a masked spear thrust over its walk, one shared walk edit reaching every walker, and the result in Test.
/// Phase five: an imported library walk converted onto Person and compared with its source points on two builds, and a
/// <c>.puppet.json</c> converted into a model with its motion, both saved and reopened with their sources untouched.
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
            ("10-several-people", shell => { shell.Test.Stop(); SeveralPeople(shell.Session); }),
            ("11-heavy-on-short-entity", shell => HeavyAndStandard(shell.Session)),
            ("12-standard-on-tall-entity", shell => { var s = shell.Session; s.Open("ranger-guard"); s.PreviewRoleOf("walk"); s.Seek(.45f); }),
            ("13-masked-thrust-anticipation", shell => Skirmisher(shell.Session)),
            ("14-masked-thrust-active", shell => { var s = shell.Session; s.Seek(s.Entity!.Actions["attack"].Hits[0].Start + .02f);
                var preview = s.EvaluateEntity()!;
                Record(preview.Attacks.Count == 1 && preview.Weight == 1, "the spear's hit region is live inside strike → recover at full layer weight");
                Record(preview.Pose.Local.Contacts.Count > 0, "the walk's feet stay planted under the thrust"); }),
            ("15-masked-thrust-recovery", shell => { var s = shell.Session; s.Seek(s.Entity!.Actions["attack"].Hits[0].Finish + .15f); Record(s.EvaluateEntity()!.Attacks.Count == 0, "no hit region during recovery"); }),
            ("16-shared-walk-once", shell => SharedWalkOnce(shell.Session)),
            ("17-skirmisher-in-test", shell =>
            {
                var s = shell.Session; s.SaveAll();
                Record(!s.Assets.DirtyDocuments.Any(), "Save all wrote every draft: " + s.Message);
                shell.Test.Roster.Clear(); shell.Test.Roster.AddRange(["skirmisher", "stalker-pest"]); shell.Test.Start();
                Record(shell.Test.Problems.Count == 0 && shell.Test.Arena?.Actors.Count == 2, "Test starts the new skirmisher from the drafts");
                var arena = shell.Test.Arena!;
                for (var i = 0; i < 120; i++) shell.Test.Advance(1 / 120f, new(Move: 1));
                // Advance accumulates frame time into fixed steps, so hold the button until the attack has started.
                for (var i = 0; i < 8 && arena.Player.Animator.Action is null; i++) shell.Test.Advance(1 / 120f, new(Move: 1, Attack: true));
                var from = arena.Player.Position.X; var anchors = arena.Player.Animator.Anchors.Count;
                for (var i = 0; i < 36; i++) shell.Test.Advance(1 / 120f, new(Move: 1));
                shell.Test.Paused = true;
                var player = arena.Player;
                Record(player.Animator.Action == "attack" && player.Position.X > from + .1f && anchors > 0 && player.Animator.Anchors.Count > 0,
                    $"the skirmisher keeps walking on planted feet while it thrusts (action {player.Animator.Action ?? "none"}, moved {player.Position.X - from:F2}, anchors {anchors} then {player.Animator.Anchors.Count})");
                var reopened = AuthoringWorkspace.Open(s.Assets.Root);
                Record(new[] { "brute-heavy", "ranger-guard", "skirmisher" }.All(id => reopened.CompileEntity(id) is not null), "the new entities reopen and compile");
            }),
            ("18-imported-walk-on-person", shell => { shell.Test.Stop(); ImportLibraryWalk(shell.Session); }),
            ("19-imported-walk-on-tall", shell => { var s = shell.Session; s.SetSubject("tall-thin"); s.Pin("short-broad"); s.Seek(.9f); }),
            ("20-imported-puppet", shell => ImportPuppet(shell.Session)),
            ("21-imports-reopened", shell => ImportsReopened(shell.Session)),
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

    private void SeveralPeople(EditorSession s)
    {
        foreach (var (id, name, preset, look) in new[] { ("bruiser", "Bruiser", "short-broad", "night"), ("ranger", "Ranger", "tall-thin", "ember"), ("sage", "Sage", "standard", "slate") })
        {
            Check(s.NewVariant(id, name, "person", preset), s); Check(s.ApplyLook(look), s);
        }
        var sage = s.SubjectVariant!;
        Check(s.Edit(sage, () => { sage.Asset.Build["head"] = 1.25f; sage.Asset.Build["torso"] = 1.08f; }), s);
        s.Open("bruiser"); s.SetClip("person-walk"); s.Pin("ranger"); s.Pin("sage"); s.Seek(.3f);
        var people = new[] { "bruiser", "ranger", "sage" }.Select(id => s.Assets.Resolve(id)!).ToArray();
        Record(people.Select(p => p.Parts.Single(x => x.Id == "body").Fill).Distinct().Count() == 3 && people.Select(p => p.Rest["head"].Y).Distinct().Count() == 3,
            "three visibly different people: distinct colors and heights");
        Record(s.Assets.Model("person")!.Dirty == false, "building people never edited the base");
    }

    private void HeavyAndStandard(EditorSession s)
    {
        Check(s.NewEntity("ranger-guard", "Ranger guard", "ranger", EntityAuthoring.Guard), s);
        Check(s.SetEntityRole("attack", "person-thrust"), s);
        Check(s.NewEntity("brute-heavy", "Heavy bruiser", "bruiser", EntityAuthoring.Guard), s);
        Check(s.SetEntityRole("attack", "person-thrust"), s);
        Check(s.Edit(s.EntityDocument, () => s.EntityDocument!.Asset.MotionSet = "heavy"), s);
        var heavy = s.Entity!; var standard = s.Assets.CompileEntity("ranger-guard")!;
        Record(heavy.Clip("walk")!.Id == StarterContent.HeavyWalk && standard.Clip("walk")!.Id == "person-walk", "Heavy on the short bruiser, Standard on the tall ranger");
        s.PreviewRoleOf("walk"); s.Seek(.45f);
    }

    private void Skirmisher(EditorSession s)
    {
        Check(s.DuplicateEntity("ranger-guard", "skirmisher", "Skirmisher"), s);
        var entity = s.EntityDocument!;
        Check(s.Edit(entity, () =>
        {
            entity.Asset.Roles.Clear();
            entity.Asset.Equipment.Add(new() { Prop = "spear", Socket = "right-grip" });
            var attack = entity.Asset.Actions.Single(a => a.Id == "attack");
            attack.Role = null; attack.Clip = "person-thrust"; attack.Mask = StarterContent.Upper; attack.BlendIn = .08f; attack.BlendOut = .12f;
            attack.Hits.Add(new() { Id = "spear-tip", Prop = "spear", Along = -.14f, Width = .42f, Height = .26f, Start = new() { Marker = "strike" }, Finish = new() { Marker = "recover" } });
            attack.Events.Add(new() { Id = "swing", At = new() { Marker = "strike" }, Sound = "swing" });
        }), s);
        Record(s.Assets.Problems(entity).Count == 0, "the skirmisher compiles: " + string.Join("; ", s.Assets.Problems(entity)));
        s.PreviewRoleOf("walk"); s.PreviewActionOf("attack"); s.Selection.Hit = "spear-tip"; s.Seek(.2f);
        Record(s.EvaluateEntity() is { Attacks.Count: 0, Weight: 1 }, "anticipation: layer fully in, no hit region yet");
    }

    private void SharedWalkOnce(EditorSession s)
    {
        var walk = s.Assets.Clip("person-walk")!;
        Check(s.Edit(walk, () => walk.Asset.Markers.Add(new() { Id = "step", Time = .3f })), s);
        var walkers = new[] { "ranger-guard", "skirmisher", "spear-guard", "player" }.Select(id => s.Assets.CompileEntity(id)!).ToArray();
        Record(walkers.All(e => ReferenceEquals(e.Clip("walk"), walk.Asset) && e.Clip("walk")!.Markers.Any(m => m.Id == "step")), "one walk edit reaches every walking entity without copies");
        s.Open("person-walk"); s.Pin("ranger"); s.Pin("bruiser"); s.Seek(.3f);
    }

    private string? _libraryHash, _puppetPath, _puppetHash;
    private static string Hash(string path) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));

    private void ImportLibraryWalk(EditorSession s)
    {
        var library = s.Sources.Get("person"); var manifest = Path.Combine(s.Sources.Root, s.Sources.Entries.First(e => e.Id == "person").Path);
        _libraryHash = Hash(Path.Combine(Path.GetDirectoryName(manifest)!, "points.bin")) + Hash(manifest);
        var mapping = LibraryImport.DefaultMapping(s.Assets.Model("person")!.Asset, library);
        Check(s.ImportLibraryClip("person", "walk", "person", mapping, "imported-walk", "Imported walk"), s);
        var clip = s.ClipDocument!.Asset;
        Record(clip.Source is { Kind: AssetSource.Library, File: "person", Motion: "walk" } && clip.Contacts.Count == 0 && s.ClipDocument.IsNew,
            $"the library walk converts onto Person as an unsaved draft with its source recorded ({clip.Tracks.Count} tracks, {clip.Duration:F2} s)");
        Record(new[] { "person", "tall-thin", "short-broad" }.All(id => s.Assets.CanPlay("imported-walk", id, out _)), "the converted walk plays on every Person build");
        s.Seek(.4f);
    }

    private void ImportPuppet(EditorSession s)
    {
        _puppetPath = Path.Combine(output, "scribble-walk.puppet.json");
        var puppet = PuppetTemplates.StepStudy(); puppet.Name = "Scribble walker"; puppet.Save(_puppetPath); _puppetHash = Hash(_puppetPath);
        Check(s.ImportPuppet(_puppetPath, "scribble", "Scribble walker"), s);
        var model = s.Assets.Model("scribble")!.Asset; var clip = s.ClipDocument!.Asset;
        Record(model.Source?.Kind == AssetSource.Puppet && clip.Model == "scribble" && clip.Contacts.Count == puppet.Motions[0].Contacts.Count,
            $"the puppet converts into model '{model.Id}' ({model.Controls.Count} controls, {model.Chains.Count} chains) and animation '{clip.Id}' with its contacts");
        s.TogglePlay(); s.Tick(.35f); s.TogglePlay();
    }

    private void ImportsReopened(EditorSession s)
    {
        s.SaveAll();
        Record(!s.Assets.DirtyDocuments.Any(), "the imports save: " + s.Message);
        var reopened = AuthoringWorkspace.Open(s.Assets.Root);
        var walk = reopened.Clip("imported-walk")?.Asset; var scribble = reopened.Clips.FirstOrDefault(c => c.Asset.Model == "scribble")?.Asset;
        Record(walk?.Source?.Points?.ContainsKey("hips") == true && scribble?.Source?.Kind == AssetSource.Puppet && reopened.Documents.All(d => reopened.Problems(d).Count == 0),
            "the imported walk and puppet reopen with their sources and need no repair");
        var manifest = Path.Combine(s.Sources.Root, s.Sources.Entries.First(e => e.Id == "person").Path);
        Record(_libraryHash == Hash(Path.Combine(Path.GetDirectoryName(manifest)!, "points.bin")) + Hash(manifest) && _puppetHash == Hash(_puppetPath!), "the library and the puppet file are unchanged");
        s.Open("imported-walk"); s.SetSubject("short-broad"); s.Seek(.2f);
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
