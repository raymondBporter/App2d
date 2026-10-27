using App2d.CharacterSeeds;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Editing;
using App2d.Core.Characters.Geometry;
using App2d.Core.Constraints;
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
internal sealed partial class EditorSmoke(string output, bool acceptance = false)
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
                variant.Asset.Parameters["legs"] = 1.4f;
                variant.Asset.Parameters["width"] = .7f;
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
                Record(reopened.Variant("tall-guard")?.Asset.Parameters["legs"] == 1.4f, "tall-guard reopens with its build");
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
                    document.Asset.Tracks.RemoveAll(t => t.Target == slot.Bone && t.Kind == MotionClip.ScaleKind);
                    ClipAuthoring.SetKey(document.Asset, new(MotionClip.ScaleKind, slot.Bone), 0, new(.1f, .1f, 0));
                    ClipAuthoring.SetKey(document.Asset, new(MotionClip.ScaleKind, slot.Bone), 1, new(.3f, .3f, 0));
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
                Record(clip.Tracks.Any(t => t.Kind == MotionClip.ScaleKind), "uniform scale keys survive native save/reopen");
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
                Record(document.Asset.Constraints.Count == 1, "a machine rig declares bone IK as a typed constraint");
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
            ("39-canonical-bone-editing", shell =>
            {
                var s = shell.Session; s.Open("person"); s.Compare.Clear(); s.SetMode(Workspace.Model); s.EditRig = true;
                s.Selection.Select(control: "left-shoulder"); shell.Viewport.Fit(s.Evaluate("person"));
                var doc = s.SubjectModel!;
                ModelControl Control(string id) => doc.Asset.Controls.Single(c => c.Id == id);
                Vector3 Rest(string id) => s.Assets.Resolve("person")!.Rest[id];
                // P11a: every limb is a bone-solver constraint; the shoulder is the authored pivot, the upper arm the solved bone beneath it.
                Record(Control("left-shoulder").Node == RigNodeKinds.Point && Control(Humanoid.LeftUpperArmBone).Node == RigNodeKinds.Bone && Control("left-elbow").Link is { IsTip: true }
                    && Control("left-hand").Link?.Orientation == RigLinkOrientations.Character && doc.Asset.Version == CharacterModel.CurrentVersion,
                    "the Person's limbs read as bone constraints beneath their pivots at version " + CharacterModel.CurrentVersion);
                var before = doc.Serialize();
                var hand = Rest("left-hand");
                var elbow = Rest("left-elbow");
                Check(s.Edit(doc, () => ModelAuthoring.ResizeBone(doc.Asset, Humanoid.LeftUpperArmBone, Control(Humanoid.LeftUpperArmBone).Length + .2f)), s);
                var stretched = Rest("left-elbow") - elbow;
                Record(MathF.Abs(stretched.Length() - .2f) < 1e-4f && Vector3.Distance(Rest("left-hand") - hand, stretched) < 1e-4f, "lengthening the upper arm moves the bound elbow and hand by the same 0.2 without stretching");
                Check(s.Edit(doc, () => ModelAuthoring.RotateRestBone(doc.Asset, Humanoid.LeftUpperArmBone, -MathF.PI / 2)), s);
                Record(MathF.Abs(Rest("left-elbow").X - Rest("left-shoulder").X) < 1e-4f && Rest("left-elbow").Y < Rest("left-shoulder").Y, "turning the upper arm straight down carries the elbow with it");
                s.BeginDrag();
                s.DragControl("left-elbow", Rest("left-elbow") + new Vector3(.3f, 0, 0));
                s.EndDrag();
                Record(s.MessageIsError && s.Message.Contains("bound to '" + Humanoid.LeftUpperArmBone + "'"), "dragging the bound elbow is refused with its reason: " + s.Message);
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
                // The pivot keeps the grips' frame and the clips' turns: the sword socket frames on the right shoulder, not on the solved bone.
                Record(doc.Asset.Sockets.Single(k => k.Id == Humanoid.SwordSocket).Frame == Humanoid.RightShoulder && doc.Asset.IkChains.Single(c => c.Id == Humanoid.RightArm).Frame == Humanoid.RightShoulder,
                    "the sword socket and the right arm's target keep the shoulder pivot as their frame");
                s.Selection.Select(control: "left-shoulder");
            }),
            ("40-parameter-bindings", shell =>
            {
                // P4a: the Person's proportions are declared parameters with bindings; a variant's values and a base's
                // declarations both re-resolve the preview, undo exactly and survive save/reopen; t = .5 follows a bound length.
                var s = shell.Session; s.Open("person"); s.Compare.Clear(); s.SetMode(Workspace.Model); s.EditRig = true;
                var doc = s.SubjectModel!;
                var person = s.Assets.Resolve("person")!;
                Record(doc.Asset.Version == CharacterModel.CurrentVersion && doc.Asset.Parameters.Select(p => p.Id).SequenceEqual(["legs", "torso", "arms", "width", "head"]) && doc.Asset.Parameters.Sum(p => p.Bindings.Count) == 34 && doc.Asset.Presets.Count == 5,
                    $"the Person declares its five proportion parameters with 34 bindings (the limbs' eight bone lengths since P11a, plus the face instance's two fit bindings, P7c) and 5 presets (the P4c proof builds declared since P11b) at version {CharacterModel.CurrentVersion} ({doc.Asset.Parameters.Sum(p => p.Bindings.Count)} bindings)");
                Record(person.Parameters.BindingOfControl(Humanoid.LeftUpperArmBone, RigProperties.Length)?.Parameter.Id == "arms" && person.Parameters.BindingOfPart("body", RigProperties.ScaleX)?.Parameter.Id == "width" && MathF.Abs((person.Parameters.BindingOfControl("hips", RigProperties.LocalY)?.Binding.About ?? 0) - .025f) < 1e-5f,
                    "the upper-arm length, body width and ground-anchored hip height read their bindings");
                s.Open("tall-thin");
                var variant = s.SubjectVariant!;
                var variantBefore = variant.Serialize();
                var tall = s.Assets.Resolve("tall-thin")!;
                s.Change(variant, () => variant.Asset.Parameters["legs"] = 1.4f);
                s.CommitAll();
                var taller = s.Assets.Resolve("tall-thin")!;
                Record(taller.Rest["hips"].Y > tall.Rest["hips"].Y + .1f && MathF.Abs(taller.Rest["left-foot"].Y - tall.Rest["left-foot"].Y) < 1e-5f && MathF.Abs(taller.Measure("torso") - tall.Measure("torso")) < 1e-5f,
                    "raising the variant's legs re-resolves the preview: the hips rise, the feet stay on the ground and the torso keeps its length");
                s.Undo();
                Record(variantBefore == variant.Serialize(), "the parameter value edit undoes exactly");
                s.Open("person");
                doc = s.SubjectModel!;
                var baseBefore = doc.Serialize();
                Record(!s.Edit(doc, () => ModelAuthoring.AddBinding(doc.Asset, "arms", new() { Target = PropertyRef.OfControl(Humanoid.LeftUpperArmBone, RigProperties.Length) })) && s.Message.Contains("one binding"),
                    "a second binding on a bound property is refused: " + s.Message);
                Check(s.Edit(doc, () => ModelAuthoring.AddParameter(doc.Asset, "hand-span", "Hand span", ParameterUnits.Length, 0, 0, 1)), s);
                Check(s.Edit(doc, () => ModelAuthoring.AddBinding(doc.Asset, "hand-span", new() { Kind = BindingKinds.Set, Target = PropertyRef.OfControl("left-hand", RigProperties.Length) })), s);
                s.Change(doc, () => { doc.Asset.Parameters.Last().Default = .12f; doc.Asset.Validate(); });
                s.CommitAll();
                Record(MathF.Abs(s.Assets.Resolve("person")!.Nodes["left-hand"].Length - .12f) < 1e-6f && s.Assets.Resolve("tall-thin")!.Nodes["left-hand"].Length == s.Assets.Resolve("person")!.Nodes["left-hand"].Length,
                    "a new parameter's set binding re-resolves the hand bone's length on the base and its variants");
                Check(s.Save(doc), s);
                var reopened = AuthoringWorkspace.Open(s.Assets.Root);
                Record(reopened.Model("person")!.Asset.Parameters.Any(p => p.Id == "hand-span" && p.Default == .12f) && reopened.Resolve("person")!.Nodes["left-hand"].Length == .12f && reopened.Problems(reopened.Model("person")!).Count == 0,
                    "the declared parameter and its binding save and reopen");
                while (doc.CanUndo)
                {
                    s.Undo();
                }

                Record(baseBefore == doc.Serialize(), "every declaration edit undoes exactly");
                Check(s.Save(doc), s);
                Check(s.NewModel("lever", "Lever", "empty"), s);
                var lever = s.SubjectModel!;
                Check(s.Edit(lever, () =>
                {
                    ModelAuthoring.AddControl(lever.Asset, null, new(0, 1, 0), "post");
                    var beam = ModelAuthoring.AddBone(lever.Asset, "post", "beam");
                    beam.Length = .6f;
                    var weight = ModelAuthoring.AddBone(lever.Asset, "beam", "weight");
                    weight.Link!.T = .5f;
                    weight.Length = .1f;
                    ModelAuthoring.AddPartToBone(lever.Asset, RoundedRectangle2D.Builder.FromSize(.6f, .08f, .02f), "beam");
                    ModelAuthoring.AddPartToBone(lever.Asset, RoundedRectangle2D.Builder.FromSize(.12f, .12f, .03f), "weight");
                    ModelAuthoring.AddParameter(lever.Asset, "beam-length", "Beam length", ParameterUnits.Length, .6f, .1f, 2);
                    ModelAuthoring.AddBinding(lever.Asset, "beam-length", new() { Kind = BindingKinds.Set, Target = PropertyRef.OfControl("beam", RigProperties.Length) });
                }), s);
                s.Selection.Select(control: "weight");
                shell.Viewport.Fit(s.Evaluate("lever"));
                Vector3 Weight() => s.Assets.Resolve("lever")!.Rest["weight"];
                var half = Weight();
                s.Change(lever, () => { lever.Asset.Parameters[0].Default = 1.2f; lever.Asset.Validate(); });
                s.CommitAll();
                Record(MathF.Abs(Vector3.Distance(Weight(), s.Assets.Resolve("lever")!.Rest["beam"]) - .6f) < 1e-5f && MathF.Abs(Vector3.Distance(half, s.Assets.Resolve("lever")!.Rest["beam"]) - .3f) < 1e-5f,
                    "t = .5 follows the changing midpoint when the bound beam length doubles");
                Check(s.Save(lever), s);
                Record(AuthoringWorkspace.Open(s.Assets.Root).Resolve("lever")!.Nodes["beam"].Length == 1.2f, "the lever's parameter default saves and reopens");
            }),
            ("41-library-parts", shell =>
            {
                // P5c: one library part on both feet as two instances, swapped on one variant only, edited once for every
                // wearer, found through uses / used by, undone in order and saved with its wearers.
                var s = shell.Session;
                var library = s.Assets.Parts.Select(p => p.Id).Order(StringComparer.Ordinal).ToArray();
                Record(library.SequenceEqual(["boot", "face-person", "person-lower", "person-upper", "quadruped-head", "quadruped-lower", "sandal"]) && s.Assets.Part("boot")!.Asset.Solids.Count == 3, "the parts folder loads boot, the baked Person face, the four rig modules and sandal as library parts");
                s.Open("person");
                Check(s.NewPart("mitt", "Mitt", "hair-short"), s);
                var mitt = s.PartDocument!;
                Record(s.Mode == Workspace.Appearance && mitt.Asset.Solids.Count > 0 && mitt.IsNew, "a library part converted from a hair prop opens in Appearance as a new draft");
                s.PreviewSocket = Humanoid.LeftHandSocket;
                Record(s.PreviewAttachmentOn(s.Assets.Resolve("person")!) is { Instance.Socket: Humanoid.LeftHandSocket }, "the open part previews on the wearer's chosen socket before it is attached");
                Check(s.AttachPart("mitt", Humanoid.LeftHandSocket), s);
                Check(s.AttachPart("mitt", Humanoid.RightHandSocket, mirror: true), s);
                var person = s.Assets.Model("person")!;
                Record(person.Asset.Attachments.Count == 3 && person.Asset.Attachments.Skip(1).All(a => a.Asset == "mitt") && person.Asset.Attachments[^1].Mirror && s.PreviewAttachmentOn(s.Assets.Resolve("person")!) is null,
                    "one asset occupies both hands as two instances (the right one mirrored) and the preview instance withdraws");
                var right = person.Asset.Attachments[^1].Id;
                s.Open("tall-thin");
                var tall = s.SubjectVariant!;
                Check(s.Edit(tall, () => ModelAuthoring.EditAttachmentOverride(tall.Asset, s.Assets.Resolve("tall-thin")!, right, i => i.Asset = "boot")), s);
                Record(s.Assets.Resolve("tall-thin")!.Attachment(right)!.Asset.Id == "boot" && s.Assets.Resolve("person")!.Attachment(right)!.Asset.Id == "mitt" && s.Assets.Resolve("short-broad")!.Attachment(right)!.Asset.Id == "mitt",
                    "swapping the right instance on tall-thin leaves the base and short-broad wearing the mitt");
                Record(s.Assets.UsedBy("mitt").Any(r => r.Id == "person") && s.Assets.Wearers("mitt").Contains("short-broad") && s.Assets.UsedBy("boot").Any(r => r.Id == "tall-thin"),
                    "uses / used by follow the instances and the override");
                var fillBefore = s.Assets.Resolve("short-broad")!.Attachment(right)!.Solids[0].RenderMaterial.Fill;
                s.Open("mitt");
                Check(s.Edit(mitt, () => { var solid = mitt.Asset.Solids[0]; solid.Material = solid.RenderMaterial with { Fill = "#13579b" }; mitt.Asset.Validate(); }), s);
                Record(s.Assets.Resolve("short-broad")!.Attachment(right)!.Solids[0].RenderMaterial.Fill == "#13579b" && s.Assets.Resolve("person")!.Attachments[^2].Solids[0].RenderMaterial.Fill == "#13579b",
                    "one shared edit reaches every wearer's preview without copying the asset");
                s.Undo();
                Record(s.Assets.Resolve("short-broad")!.Attachment(right)!.Solids[0].RenderMaterial.Fill == fillBefore && mitt.CanRedo, "the shared edit undoes in order from the part editor");
                s.Redo();
                s.SaveAll();
                Record(!s.MessageIsError && File.Exists(Path.Combine(s.Assets.Root, "parts", "mitt.json")), "save all writes the part before the characters that wear it: " + s.Message);
                var reopened = AuthoringWorkspace.Open(s.Assets.Root);
                Record(reopened.LoadErrors.Count == 0 && reopened.Resolve("person")!.Attachments.Count == 3 && reopened.Resolve("tall-thin")!.Attachment(right)!.Asset.Id == "boot"
                    && reopened.Resolve("person")!.Attachments[^2].Solids[0].RenderMaterial.Fill == "#13579b" && reopened.Documents.All(d => reopened.Problems(d).Count == 0),
                    "the part, its instances and the variant's swap reopen together");
                s.Open("person");
                s.Selection.Select(attachment: right);
                Check(s.Edit(person, () => ModelAuthoring.RemoveAttachment(person.Asset, right)), s);
                Record(s.Assets.Problems(tall).Any(p => p.Contains(right)), "removing an instance the variant overrides reports the dangling override by name instead of dropping it");
                s.Undo();
                Record(person.Asset.Attachments.Count == 3 && s.Assets.Problems(tall).Count == 0, "undo restores the instance and the variant resolves again");
                s.Selection.Select(attachment: right);
                shell.Viewport.Fit(s.Evaluate("person"));
            }),
            ("42-variant-recipe-overrides", shell =>
            {
                // P6: a variant's recipe overrides are typed, addressed by stable IDs and shown with their provenance. Lengthening
                // a variant forearm and swapping its glove leaves the base alone, an unoverridden base edit propagates, resetting
                // one property restores inheritance, a bone tip drags on the variant, a point tip is refused with its reason, a
                // role rebinds, a duplicate is a sibling and making the variant independent flattens the recipe.
                var s = shell.Session; s.Open("tall-thin"); s.Compare.Clear(); s.SetMode(Workspace.Model); s.EditRig = true;
                var tall = s.SubjectVariant!;
                var person = s.Assets.Model("person")!;
                var right = person.Asset.Attachments[^1].Id;
                ResolvedModel Tall() => s.Assets.Resolve("tall-thin")!;
                Record(tall.Asset.Version == ModelVariant.CurrentVersion && tall.Asset.Overrides.Count == 2 && tall.Asset.Override(PropertyRef.OfAttachment(right, RigProperties.Asset))?.Selection == "boot",
                    $"tall-thin reads at variant version {ModelVariant.CurrentVersion} with its body material and glove swap as typed overrides");
                var baseBefore = person.Serialize();
                var broadBefore = s.Assets.Resolve("short-broad")!.Nodes["left-elbow"].Length;
                var forearm = Tall().Nodes["left-elbow"].Length;
                var hand = Tall().Rest["left-hand"];
                s.Selection.Select(control: "left-elbow");
                s.BeginDrag();
                s.DragBoneTip("left-elbow", new Vector2(hand.X, hand.Y) + Vector2.Normalize(new Vector2(hand.X, hand.Y) - new Vector2(Tall().Rest["left-elbow"].X, Tall().Rest["left-elbow"].Y)) * .2f);
                s.EndDrag();
                Record(!s.MessageIsError && MathF.Abs(Tall().Nodes["left-elbow"].Length - forearm - .2f) < 1e-3f && Tall().Provenance(PropertyRef.OfControl("left-elbow", RigProperties.Length)) == PropertySource.Override && Tall().Masks(PropertyRef.OfControl("left-elbow", RigProperties.Length)),
                    "dragging the variant's forearm tip lengthens it by 0.2 as an override that masks the arms binding");
                Record(person.Serialize() == baseBefore && !person.Dirty && s.Assets.Resolve("short-broad")!.Nodes["left-elbow"].Length == broadBefore && s.Assets.Resolve("short-broad")!.Attachment(right)!.Asset.Id == "mitt",
                    "the longer forearm and the swapped glove leave the base and short-broad unchanged");
                s.BeginDrag();
                s.DragBoneTip("right-shoulder", new Vector2(0, 0));
                s.EndDrag();
                Record(s.MessageIsError && s.Message.Contains("only drag a bone node") && !tall.Asset.HasOverride(PropertyRef.OfControl("right-shoulder", RigProperties.Length)),
                    "dragging the shoulder pivot's tip on the variant (a point control) is refused with its reason, not ignored: " + s.Message);
                var shoulder = Tall().Nodes[Humanoid.LeftUpperArmBone].Length;
                Check(s.Edit(person, () => ModelAuthoring.ResizeBone(person.Asset, Humanoid.LeftUpperArmBone, person.Asset.Controls.Single(c => c.Id == Humanoid.LeftUpperArmBone).Length + .1f)), s);
                Record(MathF.Abs(Tall().Nodes[Humanoid.LeftUpperArmBone].Length - shoulder - .1f * tall.Asset.Parameters["arms"]) < 1e-4f && MathF.Abs(Tall().Nodes["left-elbow"].Length - forearm - .2f) < 1e-3f,
                    "an unoverridden base edit (the upper arm's length) propagates through the arms binding while the forearm override holds");
                s.Undo();
                Check(s.Edit(tall, () => ModelAuthoring.ResetOverride(tall.Asset, PropertyRef.OfControl("left-elbow", RigProperties.Length))), s);
                Record(MathF.Abs(Tall().Nodes["left-elbow"].Length - forearm) < 1e-5f && Tall().Provenance(PropertyRef.OfControl("left-elbow", RigProperties.Length)) == PropertySource.ParameterBinding && tall.Asset.Overrides.Count == 2,
                    "resetting the length override restores inheritance from the arms binding without writing a copy of the value");
                Check(s.Edit(tall, () => ModelAuthoring.SetOverride(tall.Asset, person.Asset, PropertyOverride.Of(PropertyRef.OfRole("walk"), "person-heavy-walk"))), s);
                Record(s.Assets.CompileEntity("spear-guard")?.Roles["walk"] is { Clip.Id: "person-heavy-walk", Source: "variant:tall-thin" } && s.Assets.UsedBy("person-heavy-walk").Any(r => r.Id == "tall-thin"),
                    "an animation-role override on the variant reaches the entity standing on it and the reference graph");
                Check(s.DuplicateVariant("tall-thin", "tall-twin", "Tall twin"), s);
                Record(s.SubjectVariant?.Asset.Base == "person" && s.Assets.Resolve("tall-twin")!.RoleClips["walk"] == "person-heavy-walk", "duplicating makes a sibling of the same base with the same overrides");
                Check(s.MakeIndependent("tall-thin", "tall-flat", "Tall flat"), s);
                var flat = s.Assets.Resolve("tall-flat")!;
                Record(s.SubjectModel?.Id == "tall-flat" && flat.Base.Parameters.Count == 0 && flat.Base.MotionSets[0].Roles["walk"] == "person-heavy-walk" && flat.Base.Attachments[^1].Asset == "boot"
                    && Tall().Order.All(c => Vector3.Distance(Tall().Rest[c.Id], flat.Rest[c.Id]) < 1e-5f),
                    "making the variant independent flattens its resolved recipe: lengths, the swapped glove and the role binding become authored");
                s.Open("tall-thin");
                Check(s.Edit(tall, () => ModelAuthoring.SetOverride(tall.Asset, person.Asset, PropertyOverride.Of(PropertyRef.OfControl("left-elbow", RigProperties.Length), forearm + .15f))), s);
                s.SetMode(Workspace.Model);
                s.EditRig = true;
                s.Selection.Select(control: "left-elbow");
                shell.Viewport.Fit(s.Evaluate("tall-thin"));
            }),
            ("43-face-instance", shell =>
            {
                // P7c: the Person's face is the baked face part (parts/face-person.json) instanced on the head socket; the head
                // part carries no payload, the clips select expressions, variants and looks override the instance's, a
                // gameplay request wins, and the climb's back view hides it through the ordinary selection channel.
                var s = shell.Session; s.Open("person"); s.Compare.Clear(); s.SetMode(Workspace.Model); s.EditRig = false;
                s.Selection.Select(attachment: PersonTemplate.FaceInstance); shell.Viewport.Fit(s.Evaluate("person"));
                var person = s.Assets.Resolve("person")!;
                var face = person.Attachment(PersonTemplate.FaceInstance);
                Record(face is { IsAnimated: true, Asset.Id: ProceduralFace.AssetId, Socket.Id: Humanoid.HeadSocket } && face.Asset.Expressions.Count == ProceduralFace.Names.Count
                    && person.Parameters.BindingOfAttachment(PersonTemplate.FaceInstance, RigProperties.FitX)?.Parameter.Id == "head" && s.SubjectModel!.Asset.Version == CharacterModel.CurrentVersion,
                    $"the Person wears the baked face part on its head socket, its fit bound to the head parameter, at model version {CharacterModel.CurrentVersion}");
                Record(PoseEvaluator.Rest(person).Expressions[PersonTemplate.FaceInstance] == "relaxed", "at rest the instance shows its default expression");
                s.Open("person-hit"); s.Seek(.1f);
                Record(s.Evaluate("person")!.Pose.Expressions[PersonTemplate.FaceInstance] == "hurt" && s.Assets.Clip("person-hit")!.Asset.Plays.Single().Instance == PersonTemplate.FaceInstance,
                    "the hit clip's converted selection key makes the face hurt");
                s.Open("player-climb"); s.Seek(.4f);
                Record(s.Evaluate("person")!.Pose.Attachments[PersonTemplate.FaceInstance].Hidden, "the climb's back view hides the face through the selection channel ('none')");
                s.Expression = "surprised";
                Record(s.Evaluate("person")!.Pose.Expressions[PersonTemplate.FaceInstance] == "surprised", "a gameplay request the asset binds wins over the clip");
                s.Expression = null;
                var brute = s.Assets.Resolve("brute")!;
                Record(brute.Provenance(PropertyRef.OfAttachment(PersonTemplate.FaceInstance, RigProperties.Expression)) == PropertySource.Override && PoseEvaluator.Rest(brute).Expressions[PersonTemplate.FaceInstance] == "angry",
                    "the brute variant's converted face override is the instance's expression with Override provenance");
                var ember = s.SubjectModel!.Asset.Looks.First(l => l.Id == "ember");
                Record(ember.Expressions[PersonTemplate.FaceInstance] == "determined", "the ember look carries its face word as the instance's expression");
                var saved = File.ReadAllText(s.SubjectModel!.Path!);
                Record(!saved.Contains("\"face\": {") && !saved.Contains("faceX") && saved.Contains("\"attachments\""), "the shipped model file carries no face payload or offset field, only the instance (and the looks' words on it)");
                s.Open("person-walk"); s.Seek(.3f);
                s.Selection.Select(attachment: PersonTemplate.FaceInstance);
                shell.Viewport.Fit(s.Scene().FirstOrDefault());
            }),
            ("44-joint-limits", shell =>
            {
                // P4b: joint limits on bone nodes, the limit-aware solve with an honest residual, the normalized bend and a variant's limit override.
                var s = shell.Session; s.Compare.Clear();
                Check(s.NewModel("limb", "Limited limb", "empty"), s);
                var limb = s.SubjectModel!;
                Check(s.Edit(limb, () =>
                {
                    var m = limb.Asset;
                    ModelAuthoring.AddControl(m, null, new(0, 1.2f, 0), "shoulder");
                    var upper = ModelAuthoring.AddBone(m, "shoulder", "upper");
                    upper.Local = new() { Rotation = -MathF.PI / 2 };
                    upper.Length = .5f;
                    var fore = ModelAuthoring.AddBone(m, "upper", "fore");
                    fore.Local = new() { Rotation = .3f };
                    fore.Length = .4f;
                    ModelAuthoring.AddBone(m, "fore", "hand");
                    var tail = ModelAuthoring.AddBone(m, "shoulder", "tail");
                    tail.Local = new() { Rotation = MathF.PI / 2 };
                    tail.Length = .3f;
                    m.Validate();
                    ModelAuthoring.AddPartToBone(m, RoundedRectangle2D.Builder.FromSize(.5f, .08f, .02f), "upper");
                    ModelAuthoring.AddPartToBone(m, RoundedRectangle2D.Builder.FromSize(.4f, .07f, .02f), "fore");
                    ModelAuthoring.AddPartToBone(m, RoundedRectangle2D.Builder.FromSize(.3f, .06f, .02f), "tail");
                    // Bend -1 puts the elbow on the side where its local rotation is positive, the side its limits allow.
                    ModelAuthoring.AddBoneIk(m, "hand").Bend = -1;
                    ModelAuthoring.SetLimit(m, "fore", new() { Min = 0, Max = 2.4f });
                    ModelAuthoring.SetLimit(m, "tail", new() { Min = 0, Max = MathF.PI });
                }), s);
                ModelControl Node(string id) => limb.Asset.Controls.Single(c => c.Id == id);
                var chain = limb.Asset.IkChains.Single().Id;
                Record(RigProperties.Declarations(Node("fore")).Any(d => d.Name == RigProperties.LimitMax) && RigProperties.Unsupported(Node("upper")).Single().Reason.Contains("no joint limits declared"),
                    "the limited elbow exposes limit.min/max; the unlimited upper arm says why it exposes none");
                Record(!s.Edit(limb, () => ModelAuthoring.SetLimit(limb.Asset, "shoulder", RigLimit.Starter)) && Node("shoulder").Limit is null, "joint limits on a point control are refused: " + s.Message);
                Check(s.NewClip("limb-reach", "Limb reach"), s);
                var clip = s.ClipDocument!;
                var resolved = s.Assets.Resolve("limb")!;
                var shoulder = resolved.Rest["upper"];
                Vector3 Target(float x, float y) => shoulder + new Vector3(x, y, 0) - resolved.Rest["hand"];
                Check(s.Edit(clip, () => ClipAuthoring.SetKey(clip.Asset, new(MotionClip.TargetKind, chain), 0, Target(.05f, -.1f))), s);
                s.Seek(0);
                var pose = s.Evaluate("limb")!.Pose;
                var result = pose.Chains.Single();
                Record(result.Limited && !result.Reached && result.Residual > .02f && pose.Limits["fore"].State == ConstraintLimitState1D.Upper,
                    $"a target that needs the elbow folded past its upper limit is held at the bound and reported unreached by {result.Residual:F3}");
                Check(s.Edit(clip, () => ClipAuthoring.SetKey(clip.Asset, new(MotionClip.TargetKind, chain), 0, Target(.25f, -.6f))), s);
                result = s.Evaluate("limb")!.Pose.Chains.Single();
                Record(result.Reached && !result.Limited, "a target inside the elbow's range is reached exactly");
                Check(s.Edit(clip, () => ClipAuthoring.SetKey(clip.Asset, new(MotionClip.BendKind, "tail"), 0, new(1, 0, 0))), s);
                pose = s.Evaluate("limb")!.Pose;
                Record(MathF.Abs(pose.Angles["tail"] - MathF.PI) < 1e-4f && pose.Limits["tail"].State == ConstraintLimitState1D.Upper, "a normalized bend of 1 lands exactly on the tail's upper limit");
                var before = clip.Serialize();
                Check(s.Edit(clip, () => ClipAuthoring.SetKey(clip.Asset, new(MotionClip.RotateKind, "tail"), 0, default, .2f)), s);
                Record(s.Assets.Problems(clip).Any(p => p.Contains("not both")), "keying an angle on the node the bend owns is reported: " + string.Join("; ", s.Assets.Problems(clip)));
                s.Undo();
                Record(before == clip.Serialize() && s.Assets.Problems(clip).Count == 0, "the conflicting angle key undoes");
                s.SaveAll();
                var reopened = AuthoredCatalog.Load(s.Assets.Root);
                Record(reopened.Errors.Count == 0 && reopened.Models["limb"].Controls.Single(c => c.Id == "fore").Limit is { Min: 0, Max: 2.4f } && reopened.Animations["limb-reach"].Tracks.Any(t => t.Kind == MotionClip.BendKind),
                    "joint limits and the bend track save and reopen");
                Check(s.NewVariant("limb-stiff", "Stiff limb", "limb"), s);
                var stiff = s.SubjectVariant!;
                var limitMax = PropertyRef.OfControl("fore", RigProperties.LimitMax);
                Check(s.Edit(stiff, () => ModelAuthoring.SetOverride(stiff.Asset, limb.Asset, PropertyOverride.Of(limitMax, 1f))), s);
                Record(s.Assets.Resolve("limb-stiff")!.Nodes["fore"].Limit!.Max == 1 && s.Assets.Resolve("limb-stiff")!.Provenance(limitMax) == PropertySource.Override && s.Assets.Resolve("limb")!.Nodes["fore"].Limit!.Max == 2.4f,
                    "a variant overrides the elbow's upper limit with provenance while the base keeps its range");
                Record(!s.Edit(stiff, () => ModelAuthoring.SetOverride(stiff.Asset, limb.Asset, PropertyOverride.Of(PropertyRef.OfControl("upper", RigProperties.LimitMin), -1f))), "overriding a limit the base never declared is refused: " + s.Message);
                s.Undo();
                Record(!stiff.Asset.HasOverride(limitMax), "the limit override undoes");
                s.Open("limb");
                s.SetMode(Workspace.Model);
                s.EditRig = true;
                s.Selection.Select(control: "fore");
                shell.Viewport.Fit(s.Evaluate("limb"));
            }),
            ("45-proportion-extremes", shell =>
            {
                // P4c: a deliberately extreme build at the hard range plays the shared walk, run and attack keys; measure-scaled contacts and travel follow its leg measure.
                var s = shell.Session; s.Compare.Clear();
                Check(s.NewVariant("person-stilted", "Stilted person", "person"), s);
                var stilted = s.SubjectVariant!;
                Check(s.Edit(stilted, () => { foreach (var (id, value) in PersonBuild.Stilted.Values()) stilted.Asset.Parameters[id] = value; }), s);
                var model = s.Assets.Resolve("person-stilted")!;
                var standard = s.Assets.Resolve("person")!;
                Record(model.ParameterValues["legs"] == 2 && model.ParameterValues["torso"] == .5f && MathF.Abs(model.Nodes["left-shoulder"].Length - 2 * standard.Nodes["left-shoulder"].Length) < 1e-5f
                    && model.ParameterValues["legs"] > model.Parameters.ById["legs"].Range.SoftMax && model.ParameterValues["legs"] == model.Parameters.ById["legs"].Max,
                    "the stilted build sits at the hard range beyond the slider's suggested span and resolves with doubled legs and arms");
                var walk = s.Assets.Clip("person-walk")!.Asset;
                var ratio = model.Measure(walk.Travel.Scale) / walk.Reference[walk.Travel.Scale];
                var standardRatio = standard.Measure(walk.Travel.Scale) / walk.Reference[walk.Travel.Scale];
                var misses = new List<string>();
                foreach (var clipId in new[] { "person-walk", "person-run", "player-sword-side-cut", "person-hammer-slam" })
                {
                    var clip = s.Assets.Clip(clipId)!.Asset;
                    for (var i = 0; i <= 24; i++)
                    {
                        var pose = PoseEvaluator.Sample(model, clip, i * clip.Duration / 24.0, repeat: true);
                        misses.AddRange(pose.Chains.Where(c => !c.Reached).Select(c => $"{clipId}@{i * clip.Duration / 24.0:0.00} {c.Chain} short by {c.Residual:F3}"));
                        if (clipId is "person-walk" or "person-run")
                        {
                            misses.AddRange(pose.Contacts.Where(c => c.Residual > 1e-4f).Select(c => $"{clipId} contact {c.Chain} residual {c.Residual:E1}"));
                        }
                    }
                }
                Record(misses.All(m => !m.StartsWith("person-walk") && !m.StartsWith("person-run")), "the shared walk and run plant and reach on the stilted build: " + (misses.Count == 0 ? "no chain runs short in any clip" : string.Join("; ", misses.Take(4))));
                Record(MathF.Abs(PoseEvaluator.CycleTravel(model, walk).X - PoseEvaluator.CycleTravel(standard, walk).X * ratio / standardRatio) < 1e-4f && ratio / standardRatio > 1.3f,
                    $"travel and planted targets scale by the leg measure ratio {ratio / standardRatio:F3} of the stilted build");
                s.SaveAll();
                var reopened = AuthoringWorkspace.Open(s.Assets.Root);
                Record(reopened.Resolve("person-stilted")!.ParameterValues["legs"] == 2 && reopened.Problems(reopened.Variant("person-stilted")!).Count == 0, "the extreme build saves and reopens");
                s.Open("person-walk");
                s.SetSubject("person-stilted");
                s.Compare.Add("person"); s.Compare.Add("short-broad");
                s.Seek(.3f);
                s.Selection.Select(control: "left-knee");
                shell.Viewport.Fit(s.Scene().FirstOrDefault());
            }),
            ("46-animated-part-instance", shell =>
            {
                // P7a: an animated library part (the tail, not a face) instanced on the Person with its own rig, layers and
                // clocks; the walk keys its exposed tip through a namespaced channel, selects an expression, a gameplay
                // request hides it, an unexposed target is refused by name, and everything undoes, saves and reopens.
                var s = shell.Session; s.Compare.Clear();
                var tailDocument = s.Assets.Create(PartSeeds.Tail());
                s.Open("person");
                Check(s.AttachPart(PartSeeds.TailId, Humanoid.BodySocket), s);
                var person = s.Assets.Resolve("person")!;
                var tail = person.Attachment(PartSeeds.TailId);
                Record(tail is { IsAnimated: true, Socket.Id: Humanoid.BodySocket } && tail.Rig!.Order.Count == 3, "the tail attaches to the body socket as an animated instance carrying its own three-control rig");
                s.Open("person-walk");
                s.Seek(.3f);
                var subject = s.Evaluate("person")!;
                var evaluated = subject.Pose.Attachments[PartSeeds.TailId];
                var wag = PoseEvaluator.Sample(tail!.Rig!, tail.Asset.Clips[0], .3, repeat: true);
                Record(evaluated.Expression == "wag" && MathF.Abs(evaluated.Pose.Angles["root"] - wag.Angles["root"]) < 1e-5f && !evaluated.Hidden,
                    $"at 0.3 s of the walk the tail wags on the shared clock (root {float.RadiansToDegrees(evaluated.Pose.Angles["root"]):0.#} degrees)");
                var walk = s.ClipDocument!;
                var tipBefore = evaluated.World("tip");
                s.Selection.Select(control: "tail/tip");
                s.BeginDrag();
                s.DragControl("tail/tip", tipBefore + new Vector3(.08f, .03f, 0));
                s.EndDrag();
                var tipAfter = s.Evaluate("person")!.Pose.Attachments[PartSeeds.TailId].World("tip");
                Record(walk.Asset.Tracks.Any(t => t.Target == "tail/tip" && t.Kind == MotionClip.TranslateKind) && Vector3.Distance(tipAfter, tipBefore + new Vector3(.08f, .03f, 0)) < 1e-3f,
                    "dragging the exposed tip keys the walk's namespaced 'tail/tip' channel and the tip lands where it was dragged");
                Check(s.Edit(walk, () => ClipAuthoring.SetPlay(walk.Asset, PartSeeds.TailId, .3f, "flick")), s);
                var flicked = s.Evaluate("person")!.Pose.Attachments[PartSeeds.TailId];
                Record(flicked.Expression == "flick" && MathF.Abs(flicked.Pose.Angles["root"] - wag.Angles["root"]) < 1e-5f, "an expression key selects the flick on its tip-only layer while the wag goes on beneath");
                s.Expression = "tucked";
                Record(s.Evaluate("person")!.Pose.Attachments[PartSeeds.TailId].Hidden, "the game-face request 'tucked' hides the tail through the asset's binding");
                s.Expression = null;
                Check(s.Edit(walk, () => ClipAuthoring.SetKey(walk.Asset, new(MotionClip.TranslateKind, "tail/root"), 0, Vector3.UnitX)), s);
                var problems = s.Assets.Problems(walk);
                Record(problems.Any(p => p.Contains("does not expose 'root'") && p.Contains("path: person > attachments > tail > part tail > exposed")), "keying the unexposed root is reported by name with the reference path: " + string.Join("; ", problems));
                s.Undo();
                Record(s.Assets.Problems(walk).Count == 0 && walk.Asset.Plays.Count == 1, "undo removes the refused key and keeps the expression key");
                s.SaveAll();
                var reopened = AuthoringWorkspace.Open(s.Assets.Root);
                var reopenedTail = reopened.Part(PartSeeds.TailId)?.Asset;
                var reopenedWalk = reopened.Clip("person-walk")!.Asset;
                Record(reopenedTail is { IsAnimated: true, Clips.Count: 2, Layers.Count: 2 } && reopened.Resolve("person")!.Attachment(PartSeeds.TailId) is { IsAnimated: true }
                    && reopenedWalk.Tracks.Any(t => t.Target == "tail/tip") && reopenedWalk.Plays.Single().Keys.Single().Expression == "flick" && reopened.Documents.All(d => reopened.Problems(d).Count == 0),
                    "the tail part, the instance and the walk's nested keys save and reopen without problems");
                s.Open("person-walk");
                s.Pin("tall-thin");
                s.Seek(.4f);
                s.Selection.Select(control: "tail/tip");
                shell.Viewport.Fit(s.Scene().FirstOrDefault());
            }),
            ("48-collider-attachments", shell =>
            {
                // P8c: the converted spear guard's colliders are editable and shown as views of what the game registers; an
                // unconverted entity converts explicitly as one undo step, keeping every attack placement; the Test arena's
                // overlays read the registered colliders and the guard's reach trigger reports its contacts.
                var s = shell.Session; s.Compare.Clear();
                s.Open("spear-guard");
                var guard = s.EntityDocument!;
                Record(s.Entity is not null, "the converted spear guard compiles in the workspace: " + (s.EntityError ?? "ok"));
                Record(guard.Asset.Version == EntityAsset.CurrentVersion
                    && guard.Asset.Colliders.Select(c => c.Id).SequenceEqual(["spear-tip", "body", "head", "legs", "reach"]) && guard.Asset.Actions[0].Hits[0].Collider == "spear-tip",
                    "the spear guard declares its attack, hurt and trigger colliders at entity version " + EntityAsset.CurrentVersion + " and its window names the attack collider");
                s.PreviewActionOf("attack");
                var strike = s.Entity!.Actions["attack"].Hits[0];
                s.Seek(strike.Start + .02f);
                var preview = s.EvaluateEntity()!;
                var spear = preview.Colliders.First(c => c.Id == "spear-tip");
                var reach = preview.Colliders.First(c => c.Id == "reach");
                Record(spear.Enabled && reach.Enabled && preview.Colliders.Count(c => c.Use == ColliderUses.Hurt && c.Enabled) == 3
                    && spear.Region.Pose == preview.Attacks.Single().Region.Pose,
                    "inside the window the preview shows the enabled attack collider (the window's region), three hurt colliders and the trigger");
                s.Seek(strike.Finish + .1f);
                Record(!s.EvaluateEntity()!.Colliders.First(c => c.Id == "spear-tip").Enabled, "after the window closes the attack collider is shown disabled");
                s.Selection.Collider = "reach";
                var radius = ((Circle2D)guard.Asset.Colliders.First(c => c.Id == "reach").Shape).Radius;
                Check(s.Edit(guard, () => guard.Asset.Colliders.First(c => c.Id == "reach").Shape = Circle2D.Builder.Create(radius + .2f)), s);
                Record(((Circle2D)s.Entity!.Collider("reach")!.Shape).Radius > radius + .19f, "editing the trigger's shape re-resolves the entity");
                s.Undo();
                Record(((Circle2D)s.Entity!.Collider("reach")!.Shape).Radius == radius, "and undoes exactly");
                Check(s.Edit(guard, () => guard.Asset.Colliders.First(c => c.Id == "head").Shape = Capsule2D.Builder.Create(new(0, -.2f), new(0, .2f), .3f)), s);
                Record(s.EvaluateEntity()!.Colliders.First(c => c.Id == "head").Region.Shape is Capsule2D, "a hurt collider takes any convex shape: the head as a capsule, shown as registered");
                s.Undo();
                Check(s.AddCollider(new() { Id = "shin", Use = ColliderUses.Hurt, Control = Humanoid.LeftKnee, Toward = Humanoid.LeftFoot, Stretch = true, Shape = Capsule2D.Builder.Create(Vector2.Zero, new(0, .39f), .06f) }), s);
                Record(s.Entity!.Collider("shin") is { Binding: ColliderBindings.Control, RestSpan: > .3f } && s.EvaluateEntity()!.Colliders.Any(c => c.Id == "shin" && c.Enabled),
                    "a stretched capsule along the shin is added, resolved against the rig and shown");
                Record(!s.RemoveCollider("spear-tip") && s.Message.Contains("re-point the window first"), "removing the collider a window names is refused by name");
                s.Undo();
                // P11a converted every shipped entity: the maul brute's tilted hammer registers as an attack collider with its three hurt boxes.
                s.Open("maul-brute");
                var brute = s.EntityDocument!;
                Record(brute.Asset.Version == EntityAsset.CurrentVersion && brute.Asset.Colliders.Select(c => c.Id).SequenceEqual(["hammer-head", "body", "head", "legs"]) && s.Entity!.Colliders.Count(c => c.Use == ColliderUses.Hurt) == 3,
                    "the maul brute declares its hammer-head attack collider and three hurt colliders at version " + EntityAsset.CurrentVersion);
                // Test mode: the arena registers every actor's colliders once and the overlays read them; the reach trigger reports the player.
                shell.LiveTest = false;
                shell.Test.Roster.Clear();
                shell.Test.Roster.AddRange(["player", "spear-guard"]);
                shell.Test.Start();
                var arena = shell.Test.Arena!;
                arena.Teleport(0, new(0, 0));
                arena.Teleport(1, new(2.05f, 0));
                var registered = arena.Collision.Colliders.Count;
                for (var i = 0; i < 60; i++)
                {
                    shell.Test.Advance(1 / 120f, default);
                }
                shell.Test.Paused = true;
                var frames = ArenaActorFrame.Capture(arena);
                Record(registered == 11 && arena.Collision.Colliders.Count == 11 && frames[1].Colliders.Count(c => c.Use == ColliderUses.Hurt) == 3 && frames[1].Colliders.Any(c => c.Use == ColliderUses.Trigger)
                    && arena.Hits.Count == 1 && arena.Hits[0].Tick == 48 && arena.Triggers.Count > 0 && arena.Triggers[0].Entered,
                    $"the Test arena registers 11 colliders for the pair once, lands the thrust at tick 48 as the reference records and the guard's reach trigger reports entering the player (first at tick {arena.Triggers.FirstOrDefault().Tick})");
                shell.Test.Stop();
                s.Open("spear-guard");
                s.PreviewActionOf("attack");
                s.Seek(strike.Start + .02f);
                s.Selection.Collider = "reach";
                shell.Viewport.Fit(s.Scene().FirstOrDefault());
            }),
            ("49-behavior-inspectors", shell =>
            {
                // P9a/P9c: the converted spear guard's typed behaviors are edited through the schema fields; the legacy maul brute converts
                // explicitly as one undo step; the game profile binds the player's roles and the placement spawns as data.
                var s = shell.Session; s.Compare.Clear();
                s.Open("spear-guard");
                var guard = s.EntityDocument!;
                Record(guard.Asset.Version == EntityAsset.CurrentVersion && guard.Asset.Behaviors.Strikes.ContainsKey("spear-tip") && guard.Asset.Behaviors.Damage.Health == 3,
                    "the spear guard declares its strike and receiving-damage behaviors at entity version " + EntityAsset.CurrentVersion);
                Check(s.Edit(guard, () => guard.Asset.Behaviors.Strikes["spear-tip"].Damage = 2), s);
                Record(s.Entity!.Actions["attack"].Hits[0].Strike.Damage == 2, "a strike's damage edit re-resolves the window's strike");
                s.Undo();
                Record(s.Entity!.Actions["attack"].Hits[0].Strike.Damage == 1, "and undoes exactly");
                Check(s.Edit(guard, () => guard.Asset.Behaviors.Properties["patrol-note"] = "holds the gate"), s);
                Record(s.Entity!.Properties["patrol-note"] == "holds the gate", "a custom property reaches the resolved entity by name");
                s.Undo();
                // A strike for no hit group is a resolution problem reported on the entity by name (the inspector only offers real groups).
                Check(s.Edit(guard, () => guard.Asset.Behaviors.Strikes["spear-butt"] = new()), s);
                Record(s.Entity is null && s.EntityError?.Contains("behaviors.strikes.spear-butt: no hit window or projectile action") == true, "a strike for no hit group is reported on the entity by name: " + s.EntityError);
                s.Undo();
                Record(s.Entity is not null, "and undoing it restores the compiling entity");
                s.Open("maul-brute");
                var brute = s.EntityDocument!;
                Record(brute.Asset.Behaviors.Strikes["hammer-head"] == new StrikeDef { Damage = 5, Sound = "heavy" } && brute.Asset.Behaviors.Damage.Health == 22,
                    "the maul brute's strike (damage 5 / heavy) and health 22 are typed behaviors since P11a");
                var profile = AuthoredCatalog.Load(s.Assets.Root).Profiles.GetValueOrDefault(GameProfile.GameId);
                Record(profile is { HasPlayer: true, Entity: "hero" } && profile.Roles[PersonRoles.GunShot] == "player-gun-shot" && profile.Spawns["BoilerBrute"] == "maul-brute",
                    "the game profile binds the hero entity, the gun-shot role and the brute placement as data");
                s.Open("spear-guard");
                shell.EntityInspector.ScrollTo = "Behaviors";
            }),
            ("50-template-contracts", shell =>
            {
                // P10a: the anatomy templates are published resources; Studio creates from them and from the empty rig only. The
                // quadruped's published contract holds on the shipped model, a creation starts from a published preset, and a
                // contract that does not hold is reported with its reference path instead of creating a broken bundle.
                var s = shell.Session; s.Compare.Clear();
                var published = s.Assets.ModelTemplates.Values.Where(t => t.Publishes).Select(t => t.Id).Order(StringComparer.Ordinal).ToArray();
                // The Person's contract is checked against the smoke's own edited Person (earlier steps swap its face), so it is reported, not required here.
                var contracts = s.Assets.ModelTemplates.Values.ToDictionary(t => t.Id, t => s.Assets.TemplateProblems(t));
                Record(published.SequenceEqual(["centaur", "person", "pterosaur", "quadruped", "triceratops"]) && contracts.Where(c => c.Key != "person").All(c => c.Value.Count == 0),
                    "the Person, quadruped, triceratops, pterosaur and centaur templates publish their contracts (slots, dimensions, presets, library) and every creature contract holds on its shipped model (" + string.Join(", ", published) + "); the edited Person reports: " + (contracts["person"].Count == 0 ? "nothing" : string.Join(" | ", contracts["person"])));
                var quadruped = s.Assets.ModelTemplates["quadruped"];
                Record(quadruped.Slots.Select(x => x.Id + ":" + x.Usage).SequenceEqual(["head-art:headgear", "body-art:saddle"]) && quadruped.Dimensions.SequenceEqual(["legs", "body", "head"]) && quadruped.Library.Masks.SequenceEqual(["head", "legs"]) && quadruped.Library.Mappings["leg"] == "legs",
                    "the quadruped template publishes two part slots, three dimensions with three presets, two masks and the leg and body measure mappings");
                Check(s.NewModel("long-quadruped", "Long-legged quadruped", "quadruped", "long-legged"), s);
                var created = s.Assets.Resolve("long-quadruped")!;
                var shipped = s.Assets.Resolve("quadruped")!;
                Record(created.Base.Parameters.Single(p => p.Id == "legs").Default == 1.35f && created.Rest["body"].Y > shipped.Rest["body"].Y + .2f && created.Base.Presets.Count == 3 && s.Assets.Clips.Count(c => c.Asset.Model == "long-quadruped") == 8,
                    $"creating from the quadruped template with the long-legged preset writes its values as the new model's defaults (body at {created.Rest["body"].Y:0.##} over {shipped.Rest["body"].Y:0.##}) and copies the presets and eight clips");
                Record(s.Assets.ModelTemplates["quadruped"].ContractProblems(created.Base, s.Assets.PartOf).Count == 0, "the published contract holds on the created copy (roles remapped to its own clips)");
                Record(!s.NewModel("giant-quadruped", "Giant", "quadruped", "giant") && s.Message!.Contains("publishes no preset 'giant'"), "a preset the template does not publish is refused by name: " + s.Message);
                Record(!s.NewModel("centipede", "Centipede", "centipede") && s.Message!.Contains("one of empty, centaur, person, pterosaur, quadruped, stalker, triceratops"), "an unknown template lists the resources; no anatomy is named in code: " + s.Message);
                var broken = ModelTemplate.FromJson(quadruped.ToJson());
                broken.Slots.Add(new() { Id = "horn-art", Usage = "horn" });
                broken.Dimensions.Add("neck");
                var problems = s.Assets.TemplateProblems(broken);
                Record(problems.Count == 2 && problems[0].StartsWith("templates > quadruped > slots > horn-art:", StringComparison.Ordinal) && problems[1].StartsWith("templates > quadruped > dimensions > neck:", StringComparison.Ordinal),
                    "a contract that does not hold is reported with its reference paths: " + string.Join(" | ", problems));
                s.Open("long-quadruped");
                s.SetMode(Workspace.Model);
                s.Seek(.3f);
                shell.Viewport.Fit(s.Scene().FirstOrDefault());
            }),
            ("51-module-instances", shell =>
            {
                // P10b: the Person's upper body extracted as a rig module (parts/person-upper.json) worn twice on a stand through the
                // P7 instancing mechanism: each instance keeps its own namespaced targets beside the wearer's, one instance sets the
                // module's arms input, a socket usage refuses the wrong module by name, and the inspector shows the contract.
                var s = shell.Session; s.Compare.Clear();
                var upper = s.Assets.Part(ModulePlans.PersonUpper)!.Asset;
                var lower = s.Assets.Part(ModulePlans.QuadrupedLower)!.Asset;
                Record(upper is { IsAnimated: true, Connection: Humanoid.Chest, Usage: ModulePlans.UpperBody } && upper.Parameters.Select(p => p.Id).SequenceEqual(["torso", "arms", "width", "head"]) && upper.Clips.Count == 41 && lower.Locations.Single().Id == ModulePlans.Withers && lower.Clip("quadruped-walk")!.Contacts.Count == 6,
                    "the parts folder loads the Person upper body (connection chest, inputs torso/arms/width/head, 41 clips) and the quadruped lower body (the withers point, the walk's six contacts and its travel) as rig modules");
                var stand = new CharacterModel { Id = "rider-stand", Name = "Rider stand" };
                stand.Controls.Add(new() { Id = "root", Rest = new(0, 1, 0) });
                stand.Controls.Add(new() { Id = "head", Parent = "root", Rest = new(0, 1.3f, 0) });
                stand.Parts.Add(new() { Id = "beam", A = "root", B = "head", Geometry = App2d.Core.Shapes.Ellipse2D.Builder.Create(new(.05f, .15f)), Material = new() { Fill = "#888888" } });
                stand.Sockets.Add(new() { Id = "left-seat", Control = "root", OffsetX = -.6f, Frame = CharacterModel.Locomotion, Usage = ModulePlans.UpperBody });
                stand.Sockets.Add(new() { Id = "right-seat", Control = "root", OffsetX = .6f, Frame = CharacterModel.Locomotion, Usage = ModulePlans.UpperBody });
                stand.Attachments.Add(new() { Id = "a", Asset = ModulePlans.PersonUpper, Socket = "left-seat" });
                stand.Attachments.Add(new() { Id = "b", Asset = ModulePlans.PersonUpper, Socket = "right-seat", Inputs = { ["arms"] = 1.5f } });
                stand.Validate();
                var standDocument = s.Assets.Create(stand);
                s.Open("rider-stand");
                Check(s.NewClip("rider-wave", "Rider wave", 1, true), s);
                var wave = s.ClipDocument!;
                Check(s.Edit(wave, () =>
                {
                    ClipAuthoring.SetKey(wave.Asset, new(MotionClip.RotateKind, "head"), 0, System.Numerics.Vector3.Zero, .2f);
                    ClipAuthoring.SetKey(wave.Asset, new(MotionClip.RotateKind, "a/head"), 0, System.Numerics.Vector3.Zero, -.5f);
                    ClipAuthoring.SetKey(wave.Asset, new(MotionClip.TranslateKind, "b/head"), 0, new(0, .1f, 0), 0);
                    ClipAuthoring.SetPlay(wave.Asset, "a", 0, "walk");
                    ClipAuthoring.SetPlay(wave.Asset, "b", 0, "hit");
                }), s);
                s.Seek(.3f);
                var pose = s.Evaluate("rider-stand")!.Pose;
                var a = pose.Attachments["a"];
                var b = pose.Attachments["b"];
                Record(MathF.Abs(pose.Angles["head"] - .2f) < 1e-5f && MathF.Abs(a.Pose.Angles["head"] + .5f) < 1e-5f && MathF.Abs(b.Pose.Angles["head"]) < 1e-5f && b.Pose.Points["head"].Y - b.Rig.Rest["head"].Y > .09f && (a.Expression, b.Expression) == ("walk", "hit"),
                    "the wearer's head, a/head and b/head are three targets keyed apart; a walks while b takes the hit, and b's lifted head never reaches a");
                var reachA = System.Numerics.Vector3.Distance(a.Rig.Rest["left-hand"], a.Rig.Rest["left-shoulder"]);
                var reachB = System.Numerics.Vector3.Distance(b.Rig.Rest["left-hand"], b.Rig.Rest["left-shoulder"]);
                Record(MathF.Abs(reachB / reachA - 1.5f) < 1e-3f && b.Rig.ParameterValues["arms"] == 1.5f, $"b's arms input (1.5) lengthens its own rig's arms through the module's bindings ({reachB / reachA:0.###} of a's); a keeps the part's default");
                Check(s.Edit(standDocument, () => standDocument.Asset.Attachments.First(x => x.Id == "a").Inputs["head"] = 1.3f), s);
                Record(s.Evaluate("rider-stand")!.Pose.Attachments["a"].Rig.ParameterValues["head"] == 1.3f, "setting a's head input re-resolves that instance's rig alone");
                s.Undo();
                var wrong = CharacterModel.FromJson(stand.ToJson());
                wrong.Attachments.Add(new() { Id = "legs", Asset = ModulePlans.QuadrupedLower, Socket = "left-seat" });
                string? refusal = null;
                try { ResolvedModel.From(wrong, null, s.Assets.PartOf); } catch (InvalidDataException ex) { refusal = ex.Message; }
                Record(refusal is not null && refusal.Contains("socket 'left-seat' takes 'upper-body', part 'quadruped-lower' is a 'lower-body' (path: Model 'rider-stand' > attachments > legs > socket left-seat > usage)"),
                    "a lower body on a socket that takes upper bodies is refused with the reference path: " + refusal);
                s.SetMode(Workspace.Animate);
                s.Selection.Select(attachment: "b");
                shell.Viewport.Fit(s.Evaluate("rider-stand"));
            }),
            ("52-centaur-study", shell =>
            {
                // P10c: the centaur study is resources over the shipped modules (models/centaur.json, five clips, templates/centaur.json):
                // the lower body owns the root motion, the saddle socket sits on lower/body so the torso follows the gait, and the
                // upper body acts on its own; the outline, inspector and timeline say who mounts where and who owns the motion.
                var s = shell.Session; s.Compare.Clear();
                var centaur = s.Assets.Resolve(CentaurStudy.Id)!;
                Record(centaur.Base.Controls.Count == 1 && centaur.Attachments.Select(a => a.Id).SequenceEqual([CentaurStudy.Lower, CentaurStudy.Upper]) && centaur.MotionOwner?.Id == CentaurStudy.Lower && centaur.EvaluationOrder.Select(a => a.Id).SequenceEqual([CentaurStudy.Lower, CentaurStudy.Upper]),
                    "the centaur resolves from resources: one root control, the quadruped lower body and the Person upper body as instances, the lower body owning the root motion and evaluated first");
                s.Open(CentaurStudy.Id);
                s.SetClip("centaur-walk");
                s.Seek(.55f);
                var pose = s.Evaluate(CentaurStudy.Id)!.Pose;
                var lower = pose.Attachments[CentaurStudy.Lower];
                var upper = pose.Attachments[CentaurStudy.Upper];
                var saddle = lower.Frame("body").At(new System.Numerics.Vector3(.42f, .98f, 0));
                Record(lower.Pose.Chains.All(c => c.Reached) && lower.Pose.Contacts.Count >= 2 && lower.Pose.Contacts.All(c => c.Residual < 1e-4f) && System.Numerics.Vector2.Distance(new(saddle.X, saddle.Y), new(upper.World(Humanoid.Chest).X, upper.World(Humanoid.Chest).Y)) < 1e-4f,
                    "at 0.55 s of the walk the lower body's legs reach with the planted feet holding, and the chest sits on the saddle socket carried by lower/body");
                var walkClip = s.Assets.Clip("centaur-walk")!.Asset;
                var gait = s.Assets.Part(ModulePlans.QuadrupedLower)!.Asset.Clip("quadruped-walk")!;
                Record(MathF.Abs(PoseEvaluator.CycleTravel(centaur, walkClip).X - PoseEvaluator.CycleTravel(lower.Rig, gait).X) < 1e-5f && walkClip.Travel.Keys.Count == 0, "the centaur walk's stride is the quadruped gait's, carried by the owning module; the wearer clip itself has no travel");
                s.SetClip("centaur-slam");
                s.Seek(.55f);
                var slammed = s.Evaluate(CentaurStudy.Id)!.Pose;
                Record(slammed.Attachments[CentaurStudy.Lower].Expression == "walk" && slammed.Attachments[CentaurStudy.Upper].Expression == "hammer-slam", "the slam keeps the walk beneath and plays the hammer slam on the upper body alone");
                var travelling = ClipAuthoring.New(centaur, "centaur-drift", "Drift", 1, true);
                travelling.Travel.Keys = [new(), new() { Time = 1, Value = new(1, 0, 0) }];
                string? refusal = null;
                try { travelling.Validate(centaur); } catch (InvalidDataException ex) { refusal = ex.Message; }
                Record(refusal is not null && refusal.Contains("root motion is owned by attachment 'lower' (path: centaur > motionOwner)"), "a wearer clip that travels while the lower body owns the motion is refused with the path: " + refusal);
                s.SetClip("centaur-walk");
                s.Seek(.55f);
                s.SetMode(Workspace.Animate);
                s.Selection.Select(attachment: CentaurStudy.Upper);
                shell.Viewport.Fit(s.Evaluate(CentaurStudy.Id));
            }),
        ];
        if (acceptance)
        {
            _steps = AcceptanceSteps();
        }

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
            // The image rig: image slots on one bone node with a uniform pose scale channel (the native inherited affine channels retired in P11c).
            ModelAuthoring.AddBone(model, null, "root");
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
                ModelAuthoring.AddIkOverPoints(model.Asset, $"foot-{i}");
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
        Record(model.Asset.Attachments.Count == 0 && model.Asset.IkChains.Count() == 3, "tripod has three IK legs and no head or face");
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
            sage.Asset.Parameters["head"] = 1.25f;
            sage.Asset.Parameters["torso"] = 1.08f;
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
            entity.Asset.Colliders.Add(new() { Id = "spear-tip", Use = ColliderUses.Attack, Prop = "spear", Along = -.14f, Shape = Rectangle2D.Builder.FromSize(.42f, .26f) });
            attack.Hits.Add(new()
            {
                Id = "spear-tip",
                Collider = "spear-tip",
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
            File.WriteAllLines(Path.Combine(output, acceptance ? "acceptance-smoke.txt" : "editor-smoke.txt"), _report);
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
