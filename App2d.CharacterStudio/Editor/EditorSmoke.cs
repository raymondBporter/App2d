using App2d.CharacterSeeds;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Editing;
using App2d.Core.Characters.Geometry;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Rendering;
using App2d.Core.Shapes;
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
        if (Directory.Exists(workspace))
        {
            Directory.Delete(workspace, true);
        }

        foreach (var file in Directory.EnumerateFiles(authoredRoot, "*.json", SearchOption.AllDirectories))
        {
            var target = Path.Combine(workspace, Path.GetRelativePath(authoredRoot, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
        _steps =
        [
            ("01-shared-walk-compare", shell => {
                var s = shell.Session;
                s.Open("person-walk");
                s.Pin("tall-thin");
                s.Pin("short-broad");
                s.Seek(.3f);
            }),
            ("02-new-tall-variant", shell =>
            {
                var s = shell.Session;
                Check(s.NewVariant("tall-guard", "Tall guard", "person", "tall-thin"), s);
                s.SetClip("person-walk");
                s.Selection.Part = Humanoid.Body;
                s.TogglePlay();
            }),
            ("03-build-change-while-walking", shell =>
            {
                var s = shell.Session;
                var variant = s.SubjectVariant!;
                s.Change(variant, () => {
                variant.Asset.Build["legs"] = 1.4f;
                variant.Asset.Build["width"] = .7f;
            });
                s.CommitAll();
                Record(s.Transport.Playing, "still walking after the build change");
                Check(s.Save(variant), s);
            }),
            ("04-shared-clip-drag", shell =>
            {
                var s = shell.Session;
                s.Open("person-walk");
                s.Pin("tall-guard");
                s.Pin("short-broad");
                s.Seek(.2f);
                var chest = s.Evaluate(s.SubjectId!)!.Pose.World(Humanoid.Chest);
                s.Selection.Control = Humanoid.Chest;
                s.BeginDrag();
                for (var i = 1; i <= 8; i++)
                {
                    s.DragControl(Humanoid.Chest, chest + new Vector3(.02f * i, .015f * i, 0));
                }
                s.EndDrag();
                Record(s.ClipDocument!.CanUndo, "the drag is one undo step");
            }),
            ("05-after-undo", shell => {
                var s = shell.Session;
                s.Undo();
                Record(!s.ClipDocument!.CanUndo && !s.ClipDocument.Dirty, "undo restores the saved clip");
            }),
            ("06-tripod-rig-from-empty", shell => BuildTripod(shell.Session)),
            ("07-tripod-animated", shell => AnimateTripod(shell.Session)),
            ("08-reopened", shell =>
            {
                var reopened = AuthoringWorkspace.Open(shell.Session.Assets.Root);
                Record(reopened.Variant("tall-guard")?.Asset.Build["legs"] == 1.4f, "tall-guard reopens with its build");
                Record(reopened.Resolve("tripod") is { } tripod && reopened.CanPlay("tripod-walk", "tripod", out _) && PoseEvaluator.Sample(tripod, reopened.Clip("tripod-walk")!.Asset, .25).Chains.All(c => c.Reached),
                    "tripod and its walk reopen and play");
                Record(reopened.Documents.All(d => reopened.Problems(d).Count == 0), "no asset needs repair");
                shell.Session.Open("tripod-walk");
                shell.Session.TogglePlay();
            }),
            ("09-test-arena", shell =>
            {
                shell.Session.TogglePlay();
                shell.LiveTest = false;
                shell.Test.Roster.Clear();
                shell.Test.Roster.AddRange(["player", "spear-guard", "stalker-pest"]);
                shell.Test.Start();
                Record(shell.Test.Problems.Count == 0 && shell.Test.Arena?.Actors.Count == 3, "Test starts the player, spear guard and stalker from the drafts");
                // The player walks in and thrusts; the guard answers. Fixed steps, so the frame is reproducible.
                for (var i = 0; i < 150; i++)
                {
                    shell.Test.Advance(1 / 120f, new(Move: 1));
                }
                for (var i = 0; i < 90; i++)
                {
                    shell.Test.Advance(1 / 120f, new(Attack: true));
                }
                shell.Test.Paused = true;
                Record(shell.Test.Arena!.Events.Any(e => e.Event.Sound == "swing"), "a thrust played its swing sound on the strike marker");
                Record(shell.Session.Mode == Workspace.Animate && shell.Session.ClipId == "tripod-walk", "testing left the open document and workspace alone");
            }),
            ("10-several-people", shell => {
                shell.Test.Stop();
                SeveralPeople(shell.Session);
            }),
            ("11-heavy-on-short-entity", shell => HeavyAndStandard(shell.Session)),
            ("12-standard-on-tall-entity", shell => {
                var s = shell.Session;
                s.Open("ranger-guard");
                s.PreviewRoleOf("walk");
                s.Seek(.45f);
            }),
            ("13-masked-thrust-anticipation", shell => Skirmisher(shell.Session)),
            ("14-masked-thrust-active", shell => {
var s = shell.Session;
s.Seek(s.Entity!.Actions["attack"].Hits[0].Start + .02f);
                var preview = s.EvaluateEntity()!;
                Record(preview.Attacks.Count == 1 && preview.Weight == 1, "the spear's hit region is live inside strike → recover at full layer weight");
                Record(preview.Pose.Local.Contacts.Count > 0, "the walk's feet stay planted under the thrust");
}),
            ("15-masked-thrust-recovery", shell => {
var s = shell.Session;
s.Seek(s.Entity!.Actions["attack"].Hits[0].Finish + .15f);
Record(s.EvaluateEntity()!.Attacks.Count == 0, "no hit region during recovery");
}),
            ("16-shared-walk-once", shell => SharedWalkOnce(shell.Session)),
            ("17-skirmisher-in-test", shell =>
            {
                var s = shell.Session;
s.SaveAll();
                Record(!s.Assets.DirtyDocuments.Any(), "Save all wrote every draft: " + s.Message);
                shell.Test.Roster.Clear();
shell.Test.Roster.AddRange(["skirmisher", "stalker-pest"]);
shell.Test.Start();
                Record(shell.Test.Problems.Count == 0 && shell.Test.Arena?.Actors.Count == 2, "Test starts the new skirmisher from the drafts");
                var arena = shell.Test.Arena!;
                for (var i = 0; i < 120; i++) {
shell.Test.Advance(1 / 120f, new(Move: 1));
}
                // Advance accumulates frame time into fixed steps, so hold the button until the attack has started.
                for (var i = 0; i < 8 && arena.Player.Animator.Action is null; i++) {
shell.Test.Advance(1 / 120f, new(Move: 1, Attack: true));
} var from = arena.Player.Position.X;
var anchors = arena.Player.Animator.Anchors.Count;
                for (var i = 0; i < 36; i++) {
shell.Test.Advance(1 / 120f, new(Move: 1));
} shell.Test.Paused = true;
                var player = arena.Player;
                Record(player.Animator.Action == "attack" && player.Position.X > from + .1f && anchors > 0 && player.Animator.Anchors.Count > 0,
                    $"the skirmisher keeps walking on planted feet while it thrusts (action {player.Animator.Action ?? "none"}, moved {player.Position.X - from:F2}, anchors {anchors} then {player.Animator.Anchors.Count})");
                var reopened = AuthoringWorkspace.Open(s.Assets.Root);
                Record(reopenAndCompileSourceArray.All(id => reopened.CompileEntity(id) is not null), "the new entities reopen and compile");
            }),
            ("18-imported-walk-on-person", shell => {
                shell.Test.Stop();
                ImportLibraryWalk(shell.Session);
            }),
            ("19-imported-walk-on-tall", shell => {
                var s = shell.Session;
                s.SetSubject("tall-thin");
                s.Pin("short-broad");
                s.Seek(.9f);
            }),
            ("20-imported-puppet", shell => ImportPuppet(shell.Session)),
            ("21-imports-reopened", shell => ImportsReopened(shell.Session)),
            ("22-weapon-orientation", shell =>
            {
                var s = shell.Session;
                s.Open("player-sword-backhand");
                s.SetSubject("person");
                s.Compare.Clear();
                s.PreviewProp = "sword";
                s.PreviewSocket = Humanoid.SwordSocket;
                s.PreviewWeapon = s.EditWeapon = true;
                s.Seek(.16f);
                s.PoseWeapon(new(.8f, -.3f, 0));
                s.CommitAll();
                Record(s.Scene()[0].Pose.SocketAngles[Humanoid.SwordSocket].X == .8f, "weapon rotation reaches the preview");
                Check(s.Save(s.ClipDocument), s);
            }),
            ("23-weapon-edge-on", shell =>
            {
                var s = shell.Session;
                s.PoseWeapon(new(MathF.PI / 2, 0, 0));
                s.CommitAll();
                Record(MathF.Abs(new ActorPose(s.Scene()[0].Pose, Vector2.Zero, 1).Socket(s.Scene()[0].Model.Base.Sockets.First(x => x.Id == Humanoid.SwordSocket)).YAxis.Z) > .99f, "edge-on blade has real depth");
            }),
            ("24-appearance-editor", shell =>
            {
                var s = shell.Session;
                s.Open("maul-brute");
                s.Compare.Clear();
                s.ShowRest = true;
                s.EditRig = false;
                Check(s.DuplicateAppearance("brute-hide-wrap", "test-wrap", "Custom hide wrap"), s);
                var doc = s.AppearanceDocument!;
                Check(s.Edit(doc, () => doc.Asset.Solids[0].Material = doc.Asset.Solids[0].RenderMaterial with { Fill = "#ac7243" }), s);
                Check(s.EquipAppearance("maul-brute", doc.Id, Humanoid.BodySocket), s);
                Check(s.Edit(s.EntityDocument, () => s.EntityDocument!.Asset.Equipment.RemoveAll(e => e.Prop == "brute-hide-wrap")), s);
                s.SaveAll();
                var reopened = AuthoredCatalog.Load(s.Assets.Root);
                Record(reopened.Entities["maul-brute"].Equipment.Any(e => e.Prop.Id == "test-wrap" && e.Prop.Solids[0].RenderMaterial.Fill == "#ac7243"), "custom clothing saves and reaches the runtime entity");
                Record(doc.Asset.Solids[0].Outline is not null, "outline remains editable after saving");
            }),
            ("25-triceratops-cutout", shell =>
            {
                var s = shell.Session;
                s.Compare.Clear();
                s.PreviewWeapon = s.EditWeapon = false;
                s.ShowRest = true;
                Check(s.NewModel("review-triceratops", "Triceratops review", "triceratops"), s);
                s.Selection.Part = "frill";
                shell.Viewport.Fit(s.Evaluate(s.SubjectId!));
                var doc = s.SubjectModel!;
                var before = doc.Serialize();
                Check(s.Edit(doc, () =>
                {
                    var part = doc.Asset.Parts.Single(p => p.Id == "frill");
                    var polygon = (SimplePolygon2D)part.Geometry!;
                    var vertices = new List<Vector2>(polygon.Vertices.ToArray()) { [0] = new(-.32f, -.46f) };
                    ModelAuthoring.SetCutout(part, vertices);
                }), s);
                s.Undo();
                Record(before == doc.Serialize(), "cutout silhouette edit undoes exactly");
                s.Redo();
                s.SaveAll();
                Record(AuthoredCatalog.Load(s.Assets.Root).Animations.ContainsKey("review-triceratops-rush"), "quadruped starter art and clips save together");
            }),
            ("26-quadruped-rig", shell => {
                var s = shell.Session;
                s.EditRig = true;
                s.Selection.Control = "near-front-foot";
            }),
            ("27-quadruped-rush", shell =>
            {
                var s = shell.Session;
                s.EditRig = s.ShowRest = false;
                s.SetMode(Workspace.Animate);
                s.SetClip("review-triceratops-rush");
                s.Seek(.18f);
                s.Selection.Control = "near-front-foot";
                shell.Viewport.Fit(s.Evaluate(s.SubjectId!));
                Record(s.Scene()[0].Pose.Chains.Count == 4, "quadruped IK reaches the animation viewport");
            }),
            ("28-quadruped-head-down", shell => {
                var s = shell.Session;
                s.SetClip("review-triceratops-head-down");
                s.Seek(.55f);
            }),
            ("34-pterosaur-model", shell =>
            {
                var s = shell.Session;
                s.SetMode(Workspace.Model);
                s.ShowRest = true;
                Check(s.NewModel("review-pterosaur", "Pterosaur review", "pterosaur"), s);
                s.Selection.Part = "near-wing";
                shell.Viewport.Fit(s.Evaluate(s.SubjectId!));
                var doc = s.SubjectModel!;
                var before = doc.Serialize();
                Check(s.Edit(doc, () => ModelAuthoring.InsertCutoutPoint(doc.Asset.Parts.Single(p => p.Id == "near-wing"), 2)), s);
                s.Undo();
                Record(doc.Serialize() == before, "bound outline insertion undoes with its attachments");
                s.Redo();
                s.SaveAll();
                var reopened = AuthoredCatalog.Load(s.Assets.Root);
                Record(reopened.Models["review-pterosaur"].Parts.Single(p => p.Id == "near-wing").Bindings!.Count == 7, "wing attachments and clips reopen together");
            }),
            ("35-pterosaur-rig", shell => {
                var s = shell.Session;
                s.EditRig = true;
                s.Selection.Control = "near-tip";
            }),
            ("36-pterosaur-fold", shell =>
            {
                var s = shell.Session;
                s.EditRig = s.ShowRest = false;
                s.SetMode(Workspace.Animate);
                s.SetClip("review-pterosaur-fold");
                s.Seek(.48f);
                s.Selection.Control = "near-tip";
                shell.Viewport.Fit(s.Evaluate(s.SubjectId!));
                Record(s.Scene()[0].Clip!.Tracks.All(t => t.Kind is "rotate" or "translate"), "membranes animate through ordinary control channels");
            }),
            ("37-pterosaur-recovery", shell => {
                var s = shell.Session;
                s.SetClip("review-pterosaur-low-recovery");
                s.Seek(.25f);
            }),
            ("30-native-image-slots", shell =>
            {
                var s = shell.Session;
                BuildImageRig(s);
                s.ShowRest = false;
                s.SetMode(Workspace.Animate);
                s.Seek(.25f);
                shell.Viewport.Fit(s.Evaluate(s.SubjectId!));
                Record(s.Scene()[0].Pose.Slots.Count == 2 && s.SubjectModel!.Asset.Parts.All(p => p.Material?.Texture is not null), "native image slots reach the viewport");
            }),
            ("31-slot-tint-and-curves", shell =>
            {
                var s = shell.Session;
                var model = s.SubjectModel!.Asset;
                var document = s.ClipDocument!;
                var slot = model.Slots[0];
                Check(s.Edit(document, () =>
                {
                    ClipAuthoring.SetColorKey(document.Asset, slot.Id, SlotColorTrack2D.Rgba, 0, new(0, .3f, 1, 1));
                    ClipAuthoring.SetColorKey(document.Asset, slot.Id, SlotColorTrack2D.Rgba, 1, new(0, 1, .3f, .8f));
                    document.Asset.Colors[0].Keys[0].CurveR = new() { Control1 = new(1f / 3, 1), Control2 = new(2f / 3, 1), Absolute = true };
                    document.Asset.Tracks.RemoveAll(t => t.Target == slot.Bone && t.Kind is MotionClip.ScaleKind or MotionClip.ShearKind);
                    ClipAuthoring.SetKey(document.Asset, new(MotionClip.ScaleKind, slot.Bone), 0, new(.1f, .2f, 0));
                    ClipAuthoring.SetKey(document.Asset, new(MotionClip.ScaleKind, slot.Bone), 1, new(.3f, -.1f, 0));
                    ClipAuthoring.SetKey(document.Asset, new(MotionClip.ShearKind, slot.Bone), 0, new(.05f, -.1f, 0));
                    ClipAuthoring.SetKey(document.Asset, new(MotionClip.ShearKind, slot.Bone), 1, new(-.15f, .1f, 0));
                    document.Asset.Attachments.Add(new() { Slot = slot.Id, Keys = [new() { Time = .45f }, new() { Time = .8f, Attachment = slot.Attachment }] });
                    document.Asset.DrawOrder = [new() { Time = .2f, Slots = [.. model.Slots.Select(v => v.Id).Reverse()] }];
                }), s);
                s.Seek(.25f);
                var pose = s.Scene()[0].Pose;
                Record(pose.Slots.Single(v => v.Slot.Id == slot.Id).Color.X > .5f, "equal-endpoint tint curve reaches the viewport");
                Record(pose.Slots[0].Slot.Id == model.Slots[^1].Id, "animated draw order reaches the viewport");
            }),
            ("32-hidden-slot", shell =>
            {
                var s = shell.Session;
                s.Seek(.6f);
                Record(s.Scene()[0].Pose.Slots.Single(v => v.Slot.Id == s.SubjectModel!.Asset.Slots[0].Id).Part is null, "attachment key hides the slot");
            }),
            ("33-slot-timelines-round-trip", shell =>
            {
                var s = shell.Session;
                s.SaveAll();
                var reopened = AuthoringWorkspace.Open(s.Assets.Root);
                var clip = reopened.Clip("image-rig-motion")!.Asset;
                Record(clip.Colors.Count == 1 && clip.Attachments.Count > 0 && clip.DrawOrder.Count > 0,                    "slot color, attachment, and draw order keys survive native save/reopen");
                Record(clip.Tracks.Any(t => t.Kind == MotionClip.ScaleKind) && clip.Tracks.Any(t => t.Kind == MotionClip.ShearKind),                    "scale and shear keys survive native save/reopen");
            }),
            ("34-bone-ik-rest", shell =>
            {
                var s = shell.Session;
                Check(s.NewModel("linkage", "Bone IK linkage", "empty"), s);
                var document = s.SubjectModel!;
                Check(s.Edit(document, () =>
                {
                    var model = document.Asset;
                    ModelAuthoring.AddBone(model, null, "beam");
                    ModelAuthoring.AddBone(model, "beam", "hinge");
                    ModelAuthoring.AddBone(model, "hinge", "tool");
                    ModelAuthoring.AddBoneIk(model, "tool");
                    ModelAuthoring.ResizeBone(model, "beam", .8f);
                    ModelAuthoring.ResizeBone(model, "hinge", .65f);
                    foreach (var (bone, color) in new[] { ("beam", "#e3ac47"), ("hinge", "#57b0b2"), ("tool", "#cf727b") })
                    {
                        var part = ModelAuthoring.AddPartToBone(model, RoundedRectangle2D.Builder.FromSize(.4f, .4f, .05f), bone);
                        part.Material = part.Material! with { Fill = color };
                    }
                }), s);
                s.SetMode(Workspace.Model);
                s.EditRig = true;
                s.Selection.Chain = "tool-chain";
                shell.Viewport.Fit(s.Evaluate("linkage"));
                Record(document.Asset.Constraints.Count == 1 && document.Asset.IkChains.Single().Solver == ModelChain.BoneSolver, "a machine rig declares bone IK as a typed constraint");
            }),
            ("35-bone-ik-target", shell =>
            {
                var s = shell.Session;
                Check(s.NewClip("linkage-move", "Linkage move", 1), s);
                var model = s.Assets.Resolve("linkage")!;
                var clip = s.ClipDocument!;
                Check(s.Edit(clip, () =>
                {
                    ClipAuthoring.Pose(model, clip.Asset, PoseEvaluator.Rest(model), 0, "tool", new(.75f, .65f, 0));
                    ClipAuthoring.SetKey(clip.Asset, new(MotionClip.TargetKind, "tool-chain"), 1, new(-.55f, -.2f, 0));
                }), s);
                s.EditRig = s.ShowRest = false;
                s.SetMode(Workspace.Animate);
                s.Selection.Control = "tool";
                s.Seek(.25f);
                shell.Viewport.Fit(s.Evaluate("linkage"));
                var pose = s.Scene()[0].Pose;
                Record(pose.Chains.Single().Reached && MathF.Abs(pose.Angles["beam"]) > .1f, "keyed bone IK rotates its attachment frames in the viewport");
            }),
            ("36-bone-ik-contact-reopened", shell =>
            {
                var s = shell.Session;
                var subject = s.Evaluate("linkage")!;
                var clip = s.ClipDocument!;
                var target = subject.Pose.World("tool");
                Check(s.Edit(clip, () => ClipAuthoring.Plant(subject.Model, clip.Asset, subject.Pose, "tool-chain", .25f, .5f)), s);
                s.SaveAll();
                s.Seek(.5f);
                var reopened = AuthoredCatalog.Load(s.Assets.Root);
                Record(reopened.Errors.Count == 0, "bone constraints and contact clips save and reopen without resource errors");
                var pose = PoseEvaluator.Sample(reopened.Resolve("linkage"), reopened.Animations["linkage-move"], .5f);
                Record(pose.Contacts.Count == 1 && Vector3.Distance(pose.World("tool"), target) < 1e-4f, "the reopened bone rig holds its contact target");
            }),
            ("37-rounded-path-inspector", shell =>
            {
                var s = shell.Session; s.Open("person"); s.Compare.Clear(); s.EditRig = false;
                s.Selection.Part = "left-arm"; shell.Viewport.Fit(s.Evaluate("person"));
                var part = s.SubjectModel!.Asset.Parts.Single(p => p.Id == "left-arm");
                Record(part.Bindings?.Count == 3 && part.CurveSections == 20, "the Person arm exposes its rounded path in the existing part inspector");
            }),
            ("38-curve-wave-modifier", shell =>
            {
                var s = shell.Session; s.Open("curve-face"); s.Compare.Clear(); s.EditRig = false;
                s.Selection.Part = "hair"; shell.Viewport.Fit(s.Evaluate("curve-face"));
                var doc = s.SubjectModel!;
                PuppetPart Part(string id) => doc.Asset.Parts.Single(p => p.Id == id);
                Record(Part("hair").Wave is { PhaseControl: "hair-wave" } && doc.Asset.Parts.All(p => p.Id == "hair" || p.Wave is null), "only the hair curve carries a wave modifier after reading the version-5 file");
                var before = doc.Serialize();
                Check(s.Edit(doc, () => Part("mouth").Wave = CurveWave.Starter), s);
                Record(Part("mouth").Wave is not null, "a curve part gains a wave modifier");
                s.Undo();
                Record(before == doc.Serialize() && Part("mouth").Wave is null, "adding the wave undoes exactly");
                Record(!s.Edit(doc, () => ModelAuthoring.RemoveControl(doc.Asset, "hair-wave")) && doc.Asset.Controls.Any(c => c.Id == "hair-wave"), "deleting the referenced phase control is refused: " + s.Message);
                Check(s.Edit(doc, () => Part("hair").Wave = Part("hair").Wave! with { Amplitude = .06f }), s);
                s.SaveAll();
                var saved = File.ReadAllText(doc.Path!);
                var reopened = AuthoredCatalog.Load(s.Assets.Root);
                Record(reopened.Errors.Count == 0 && reopened.Models["curve-face"].Parts.Single(p => p.Id == "hair").Wave?.Amplitude == .06f && saved.Contains("\"wave\"") && !saved.Contains("waveAmplitude"),
                    "the wave saves as the typed modifier at version " + CharacterModel.CurrentVersion + " and reopens");
                Check(s.Edit(doc, () => Part("hair").Wave = null), s);
                Record(Part("hair").Wave is null && s.Edit(doc, () => ModelAuthoring.RemoveControl(doc.Asset, "hair-wave")), "removing the wave frees its phase control");
                s.Undo();
                s.Undo();
                Record(Part("hair").Wave?.Amplitude == .06f, "removing the wave undoes");
            }),
            ("39-canonical-bone-editing", shell =>
            {
                var s = shell.Session; s.Open("person"); s.Compare.Clear(); s.SetMode(Workspace.Model); s.EditRig = true;
                s.Selection.Select(control: "left-shoulder"); shell.Viewport.Fit(s.Evaluate("person"));
                var doc = s.SubjectModel!;
                ModelControl Control(string id) => doc.Asset.Controls.Single(c => c.Id == id);
                Vector3 Rest(string id) => s.Assets.Resolve("person")!.Rest[id];
                Record(Control("left-shoulder").Node == RigNodeKinds.Bone && Control("left-elbow").Link is { IsTip: true } && Control("left-hand").Link is { IsTip: true } && doc.Asset.Version == CharacterModel.CurrentVersion,
                    "the Person left arm reads as a bone chain at version " + CharacterModel.CurrentVersion);
                var before = doc.Serialize();
                var hand = Rest("left-hand");
                var elbow = Rest("left-elbow");
                Check(s.Edit(doc, () => ModelAuthoring.ResizeBone(doc.Asset, "left-shoulder", Control("left-shoulder").Length + .2f)), s);
                var stretched = Rest("left-elbow") - elbow;
                Record(MathF.Abs(stretched.Length() - .2f) < 1e-4f && Vector3.Distance(Rest("left-hand") - hand, stretched) < 1e-4f, "lengthening the upper arm moves the bound elbow and hand by the same 0.2 without stretching");
                Check(s.Edit(doc, () => ModelAuthoring.RotateRestBone(doc.Asset, "left-shoulder", -MathF.PI / 2)), s);
                Record(MathF.Abs(Rest("left-elbow").X - Rest("left-shoulder").X) < 1e-4f && Rest("left-elbow").Y < Rest("left-shoulder").Y, "turning the upper arm straight down carries the elbow with it");
                s.BeginDrag();
                s.DragControl("left-elbow", Rest("left-elbow") + new Vector3(.3f, 0, 0));
                s.EndDrag();
                Record(s.MessageIsError && s.Message.Contains("bound to 'left-shoulder'"), "dragging the bound elbow is refused with its reason: " + s.Message);
                var shoulder = Rest("left-shoulder");
                var reach = Rest("left-hand") - shoulder;
                s.BeginDrag();
                s.DragControl("left-shoulder", shoulder + new Vector3(-.1f, 0, 0));
                s.EndDrag();
                Record(Vector3.Distance(Rest("left-shoulder"), shoulder + new Vector3(-.1f, 0, 0)) < 1e-4f && Vector3.Distance(Rest("left-hand") - Rest("left-shoulder"), reach) < 1e-4f,
                    "dragging the shoulder's origin moves it through its local setup and the bound arm comes along");
                while (doc.CanUndo)
                {
                    s.Undo();
                }

                Record(before == doc.Serialize(), "every bone edit undoes exactly");
                Check(s.Edit(doc, () => ModelAuthoring.ConvertToBoneChain(doc.Asset, ["right-shoulder", "right-elbow", "right-hand"])), s);
                var converted = s.Assets.Resolve("person")!;
                var reference = ResolvedModel.From(CharacterModel.FromJson(before));
                Record(converted.Order.All(c => Vector3.Distance(converted.Rest[c.Id], reference.Rest[c.Id]) < 1e-4f) && Control("right-elbow").Link is { IsTip: true }, "converting the right arm to a bone chain keeps every rest position");
                s.Undo();
                Record(before == doc.Serialize() && Control("right-shoulder").Node == RigNodeKinds.Point, "the conversion undoes");
                s.Selection.Select(control: "left-shoulder");
            }),
        ];
        return workspace;
    }

    private static void BuildImageRig(EditorSession s)
    {
        const string texture = "images/image-rig/white.png";
        var path = Path.Combine(s.Assets.Root, texture);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using (var image = new Bitmap(16, 16))
        {
            using (var graphics = Graphics.FromImage(image))
            {
                graphics.Clear(Color.White);
            }

            image.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
        Check(s.NewModel("image-rig", "Image rig", "empty"), s);
        var document = s.SubjectModel!;
        Check(s.Edit(document, () =>
        {
            var model = document.Asset;
            // The image rig is the native affine scenario (scale and shear channels), so its root is a legacy affine node.
            var root = ModelAuthoring.AddBone(model, null, "root");
            root.Node = RigNodeKinds.Affine;
            root.Local = null;
            root.Transform = new();
            model.Validate();
            foreach (var (id, color, offset) in new[] { ("back", "#e3ac47", -.15f), ("front", "#57b0b2", .15f) })
            {
                var part = ModelAuthoring.AddPartToBone(model, RoundedRectangle2D.Builder.FromSize(.4f, .4f, .05f), "root");
                part.Material = new()
                {
                    Texture = texture,
                    Fill = color
                };
                part.OffsetY = offset;
                model.Slots.Add(new()
                {
                    Id = id,
                    Bone = "root",
                    Attachment = "art"
                });
                if (model.Skins.Count == 0)
                {
                    model.Skins.Add(new());
                }

                model.Skins[0].Attachments[id] = new()
                {
                    ["art"] = part.Id
                };
            }
        }), s);
        Check(s.NewClip("image-rig-motion", "Image rig motion", 1), s);
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
                ModelAuthoring.AddPart(model.Asset, new LineSegmentCurve2D { Start = Vector2.Zero, End = Vector2.UnitY }.Validated(), $"hip-{i}", $"knee-{i}");
                ModelAuthoring.AddPart(model.Asset, new LineSegmentCurve2D { Start = Vector2.Zero, End = Vector2.UnitY }.Validated(), $"knee-{i}", $"foot-{i}");
            }
            ModelAuthoring.AddMeasure(model.Asset, "leg", ["hip-1", "knee-1", "foot-1"]);
            foreach (var chain in model.Asset.IkChains)
            {
                chain.Scale = "leg";
            }

            var shell = ModelAuthoring.AddPart(model.Asset, GeometryJson2D.CreateDefault(ShapeKinds2D.Ellipse), "body");
            shell.ScaleX = .95f;
            shell.ScaleY = .38f;
            shell.Material = shell.RenderMaterial with
            {
                Fill = "#c9e0b8"
            };
        }), s);
        Check(s.Save(model), s);
        s.EditRig = true;
        s.Selection.Control = "knee-1";
        Record(!model.Asset.Parts.Any(p => p.Face != "none") && model.Asset.IkChains.Count() == 3, "tripod has three IK legs and no head or face");
    }

    private static void AnimateTripod(EditorSession s)
    {
        s.EditRig = false;
        Check(s.NewClip("tripod-walk", "Tripod walk", 1), s);
        var clip = s.ClipDocument!;
        Check(s.Edit(clip, () => clip.Asset.Travel.Keys = [new() { Time = 0 }, new() { Time = 1, Value = new(.6f, 0, 0) }]), s);
        foreach (var (time, lift) in new[] { (0f, 0f), (.25f, .12f), (.5f, 0f), (.75f, .12f), (1f, 0f) })
        {
            s.Seek(time);
            var pose = s.Evaluate("tripod")!.Pose;
            s.DragControl("body", pose.World("body") + new Vector3(0, lift * .3f, 0));
            s.DragControl("foot-1", pose.World("foot-1") + new Vector3(.1f * MathF.Sin(time * MathF.Tau), lift, 0));
            s.EndDrag();
        }
        s.Seek(0);
        var subject = s.Evaluate("tripod")!;
        Check(s.Edit(clip, () =>
        {
            ClipAuthoring.Plant(subject.Model, clip.Asset, subject.Pose, "foot-0-chain", 0, .5f);
            ClipAuthoring.Plant(subject.Model, clip.Asset, subject.Pose, "foot-2-chain", .5f, .5f);
        }), s);
        Check(s.Save(clip), s);
        s.Seek(.25f);
        s.Selection.Control = "foot-1";
    }

    private void SeveralPeople(EditorSession s)
    {
        foreach (var (id, name, preset, look) in new[] { ("bruiser", "Bruiser", "short-broad", "night"), ("ranger", "Ranger", "tall-thin", "ember"), ("sage", "Sage", "standard", "slate") })
        {
            Check(s.NewVariant(id, name, "person", preset), s);
            Check(s.ApplyLook(look), s);
        }
        var sage = s.SubjectVariant!;
        Check(s.Edit(sage, () =>
        {
            sage.Asset.Build["head"] = 1.25f;
            sage.Asset.Build["torso"] = 1.08f;
        }), s);
        s.Open("bruiser");
        s.SetClip("person-walk");
        s.Pin("ranger");
        s.Pin("sage");
        s.Seek(.3f);
        var people = new[] { "bruiser", "ranger", "sage" }.Select(id => s.Assets.Resolve(id)!).ToArray();
        Record(people.Select(p => p.Parts.Single(x => x.Id == Humanoid.Body).RenderMaterial.Fill).Distinct().Count() == 3 && people.Select(p => p.Rest[Humanoid.Head].Y).Distinct().Count() == 3,
            "three visibly different people: distinct colors and heights");
        Record(!s.Assets.Model("person")!.Dirty, "building people never edited the base");
    }

    private void HeavyAndStandard(EditorSession s)
    {
        Check(s.NewEntity("ranger-guard", "Ranger guard", "ranger", EntityAuthoring.Guard), s);
        Check(s.SetEntityRole("attack", "person-thrust"), s);
        Check(s.NewEntity("brute-heavy", "Heavy bruiser", "bruiser", EntityAuthoring.Guard), s);
        Check(s.SetEntityRole("attack", "person-thrust"), s);
        Check(s.Edit(s.EntityDocument, () => s.EntityDocument!.Asset.MotionSet = "heavy"), s);
        var heavy = s.Entity!;
        var standard = s.Assets.CompileEntity("ranger-guard")!;
        Record(heavy.Clip("walk")!.Id == StarterContent.HeavyWalk && standard.Clip("walk")!.Id == "person-walk", "Heavy on the short bruiser, Standard on the tall ranger");
        s.PreviewRoleOf("walk");
        s.Seek(.45f);
    }

    private void Skirmisher(EditorSession s)
    {
        Check(s.DuplicateEntity("ranger-guard", "skirmisher", "Skirmisher"), s);
        var entity = s.EntityDocument!;
        Check(s.Edit(entity, () =>
        {
            entity.Asset.Roles.Clear();
            entity.Asset.Equipment.Add(new()
            {
                Prop = "spear",
                Socket = Humanoid.RightGripSocket
            });
            var attack = entity.Asset.Actions.Single(a => a.Id == "attack");
            attack.Role = null;
            attack.Clip = "person-thrust";
            attack.Mask = Humanoid.UpperGroup;
            attack.BlendIn = .08f;
            attack.BlendOut = .12f;
            attack.Hits.Add(new()
            {
                Id = "spear-tip",
                Prop = "spear",
                Along = -.14f,
                Shape = Rectangle2D.Builder.FromSize(.42f, .26f),
                Start = new()
                {
                    Marker = "strike"
                },
                Finish = new()
                {
                    Marker = "recover"
                }
            });
            attack.Events.Add(new()
            {
                Id = "swing",
                At = new()
                {
                    Marker = "strike"
                },
                Sound = "swing"
            });
        }), s);
        Record(s.Assets.Problems(entity).Count == 0, "the skirmisher compiles: " + string.Join("; ", s.Assets.Problems(entity)));
        s.PreviewRoleOf("walk");
        s.PreviewActionOf("attack");
        s.Selection.Hit = "spear-tip";
        s.Seek(.2f);
        Record(s.EvaluateEntity() is { Attacks.Count: 0, Weight: 1 }, "anticipation: layer fully in, no hit region yet");
    }

    private void SharedWalkOnce(EditorSession s)
    {
        var walk = s.Assets.Clip("person-walk")!;
        Check(s.Edit(walk, () => walk.Asset.Markers.Add(new() { Id = "step", Time = .3f })), s);
        var walkers = walkersSourceArray.Select(id => s.Assets.CompileEntity(id)!).ToArray();
        Record(walkers.All(e => ReferenceEquals(e.Clip("walk"), walk.Asset) && e.Clip("walk")!.Markers.Any(m => m.Id == "step")), "one walk edit reaches every walking entity without copies");
        s.Open("person-walk");
        s.Pin("ranger");
        s.Pin("bruiser");
        s.Seek(.3f);
    }

    private string? _libraryHash, _puppetPath, _puppetHash;
    internal static readonly string[] reopenAndCompileSourceArray = ["brute-heavy", "ranger-guard", "skirmisher"];
    internal static readonly string[] walkersSourceArray = ["ranger-guard", "skirmisher", "spear-guard", "player"];

    private static string Hash(string path) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));

    private static string HashLibrary(string manifest) => Hash(manifest) + string.Concat(
        Directory.EnumerateFiles(Path.Combine(Path.GetDirectoryName(manifest)!, "points"), "*.json")
            .Order(StringComparer.Ordinal).Select(path => Path.GetFileName(path) + Hash(path)));

    private void ImportLibraryWalk(EditorSession s)
    {
        var library = s.Sources.Get("person");
        var manifest = Path.Combine(s.Sources.Root, s.Sources.Entries.First(e => e.Id == "person").Path);
        _libraryHash = HashLibrary(manifest);
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
        var puppet = PuppetTemplates.StepStudy();
        puppet.Name = "Scribble walker";
        puppet.Save(_puppetPath);
        _puppetHash = Hash(_puppetPath);
        Check(s.ImportPuppet(_puppetPath, "scribble", "Scribble walker"), s);
        var model = s.Assets.Model("scribble")!.Asset;
        var clip = s.ClipDocument!.Asset;
        Record(model.Source?.Kind == AssetSource.Puppet && clip.Model == "scribble" && clip.Contacts.Count == puppet.Motions[0].Contacts.Count,
            $"the puppet converts into model '{model.Id}' ({model.Controls.Count} controls, {model.IkChains.Count()} chains) and animation '{clip.Id}' with its contacts");
        s.TogglePlay();
        s.Tick(.35f);
        s.TogglePlay();
    }

    private void ImportsReopened(EditorSession s)
    {
        s.SaveAll();
        Record(!s.Assets.DirtyDocuments.Any(), "the imports save: " + s.Message);
        var reopened = AuthoringWorkspace.Open(s.Assets.Root);
        var walk = reopened.Clip("imported-walk")?.Asset;
        var scribble = reopened.Clips.FirstOrDefault(c => c.Asset.Model == "scribble")?.Asset;
        Record(walk?.Source?.Points?.ContainsKey(Humanoid.Hips) == true && scribble?.Source?.Kind == AssetSource.Puppet && reopened.Documents.All(d => reopened.Problems(d).Count == 0),
            "the imported walk and puppet reopen with their sources and need no repair");
        var manifest = Path.Combine(s.Sources.Root, s.Sources.Entries.First(e => e.Id == "person").Path);
        Record(_libraryHash == HashLibrary(manifest) && _puppetHash == Hash(_puppetPath!), "the library and the puppet file are unchanged");
        s.Open("imported-walk");
        s.SetSubject("short-broad");
        s.Seek(.2f);
    }

    private static void Check(bool ok, EditorSession session)
    {
        if (!ok)
        {
            throw new InvalidOperationException("Smoke step failed: " + session.Message);
        }
    }
    private void Record(bool ok, string what) => _report.Add((ok ? "PASS " : "FAIL ") + what);

    /// <summary>Runs the next step once the previous one has settled and been captured. Returns false when finished.</summary>
    public bool Step(EditorShell shell)
    {
        if (_wait > 0)
        {
            _wait--;
            shell.Session.Tick(1 / 60f);
            return true;
        }
        if (_pendingName is not null)
        {
            return true;
        }

        if (_step >= _steps.Length)
        {
            File.WriteAllLines(Path.Combine(output, "editor-smoke.txt"), _report);
            return false;
        }
        var (name, run) = _steps[_step++];
        run(shell);
        shell.Session.CommitAll();
        _wait = SettleFrames;
        _pendingName = name;
        return true;
    }

    public void Capture(RenderTarget2D frame)
    {
        if (_pendingName is null || _wait > 0)
        {
            return;
        }

        frame.SavePng(Path.Combine(output, _pendingName + ".png"));
        _report.Add("frame " + _pendingName);
        _pendingName = null;
    }
}
