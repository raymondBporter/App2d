using App2d.CharacterStudio.Authoring;
using App2d.CharacterStudio.CharacterSeeds;
using App2d.CharacterStudio.Editing;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Constraints;
using App2d.Core.Geometry;
using App2d.Core.Mathematics;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.CharacterStudio.Editor;

/// <summary>
/// The character-system migration plan's section 6 reference workflow (final acceptance, P11c), run end to end on a scratch
/// copy of the authored library through the editor session with a frame after every step: a person from the template with
/// its shared walk, a variant with four proportion edits, an attachment on the shin, a shin guard under knee and ankle turns,
/// a library outfit with one asset on two sockets, instance override / reset / shared edit, game face expressions while
/// walking, a joint limit with an explicit angle and a normalized bend, a sensor on the weapon
/// with its activation and behaviors, hits / block / miss / kill / several targets in the Test arena, outcomes driving cues
/// and timeline seeking driving none, save / reopen / undo, the quadruped and flier repeats, and the centaur composition.
/// </summary>
internal sealed partial class EditorSmoke
{
    private const string Walker = "walker", TallWalker = "walker-tall", WalkerGuard = "walker-guard";

    private (string Name, Action<EditorShell> Run)[] AcceptanceSteps() =>
    [
        ("01-person-from-template", shell =>
        {
            var s = shell.Session; s.Compare.Clear();
            Check(s.NewModel(Walker, "Walker", "person", "standard"), s);
            var walker = s.Assets.Resolve(Walker)!;
            var walk = walker.Base.MotionSets[0].Roles["walk"];
            Record(walker.Base.Version == CharacterModel.CurrentVersion && walker.Base.IkChains.Count() == 4 && walker.Attachment(PersonTemplate.FaceInstance) is { IsAnimated: true }
                && s.Assets.Clips.Count(c => c.Asset.Model == Walker) == s.Assets.ModelTemplates["person"].Animations.Count && s.Assets.ModelTemplates["person"].ContractProblems(walker.Base, s.Assets.PartOf).Count == 0,
                $"a person created from the published template is a bundle of its own at model version {CharacterModel.CurrentVersion} (four bone constraints, the face instance, the template's {s.Assets.Clips.Count(c => c.Asset.Model == Walker)} library clips copied) and the contract holds on it: "
                + string.Join(" | ", s.Assets.ModelTemplates["person"].ContractProblems(walker.Base, s.Assets.PartOf)));
            s.SetClip(walk);
            s.TogglePlay();
            s.Tick(.3f);
            var pose = s.Evaluate(Walker)!.Pose;
            Record(s.Transport.Playing && pose.Chains.All(c => c.Reached) && pose.Contacts.All(c => c.Residual < 1e-4f), "its walk plays with every limb reaching and the planted feet holding");
            s.TogglePlay();
            s.Seek(.3f);
            shell.Viewport.Fit(s.Evaluate(Walker));
        }),
        ("02-variant-proportions", shell =>
        {
            var s = shell.Session;
            Check(s.NewVariant(TallWalker, "Tall walker", Walker), s);
            var tall = s.SubjectVariant!;
            var basis = s.Assets.Model(Walker)!;
            var before = s.Assets.Resolve(Walker)!;
            var forearm = before.Nodes["left-elbow"].Length;
            var shin = before.Nodes["left-knee"].Length;
            Check(s.Edit(tall, () =>
            {
                ModelAuthoring.SetOverride(tall.Asset, basis.Asset, PropertyOverride.Of(PropertyRef.OfControl("left-elbow", RigProperties.Length), forearm + .12f));
                ModelAuthoring.SetOverride(tall.Asset, basis.Asset, PropertyOverride.Of(PropertyRef.OfControl("left-knee", RigProperties.Length), shin + .15f));
                tall.Asset.Parameters["width"] = 1.25f;
                tall.Asset.Parameters["head"] = 1.2f;
            }), s);
            var after = s.Assets.Resolve(TallWalker)!;
            Record(MathF.Abs(after.Nodes["left-elbow"].Length - forearm - .12f) < 1e-5f && MathF.Abs(after.Nodes["left-knee"].Length - shin - .15f) < 1e-5f
                && after.ParameterValues["width"] == 1.25f && after.ParameterValues["head"] == 1.2f && after.Provenance(PropertyRef.OfControl("left-elbow", RigProperties.Length)) == PropertySource.Override
                && s.Assets.Resolve(Walker)!.Nodes["left-elbow"].Length == forearm && !basis.Dirty,
                "the variant lengthens one forearm and one shin as overrides and widens the torso and head through the parameters; the base is untouched");
            s.SetClip(before.Base.MotionSets[0].Roles["walk"]);
            s.Seek(.3f);
            var pose = s.Evaluate(TallWalker)!.Pose;
            Record(pose.Chains.All(c => c.Reached) && pose.Contacts.All(c => c.Residual < 1e-4f), "the shared walk plays on the variant with every limb reaching");
            s.SetMode(Workspace.Model);
            s.EditRig = true;
            s.Selection.Select(control: "left-knee");
            shell.Viewport.Fit(s.Evaluate(TallWalker));
        }),
        ("03-attachment-at-shin-midpoint", shell =>
        {
            var s = shell.Session;
            var basis = s.Assets.Model(Walker)!;
            var shin = s.Assets.Resolve(Walker)!.Nodes["left-knee"].Length;
            Check(s.Edit(basis, () =>
            {
                basis.Asset.Sockets.Add(new() { Id = "left-shin", Control = "left-knee", Frame = "left-knee", Transform = new(new(shin / 2, 0)) });
                ModelAuthoring.AddPart(basis.Asset, new Core.Geometry.Curves.LineSegmentCurve2D { Start = Vector2.Zero, End = Vector2.UnitY }.Validated(), "left-knee", "left-foot");
                basis.Asset.Validate();
            }), s);
            Check(s.NewPart("shin-pad", "Shin pad"), s);
            s.Open(Walker);
            Check(s.AttachPart("shin-pad", "left-shin"), s);
            var pad = basis.Asset.Attachments[^1].Id;
            Vector3 Mid(ResolvedModel m) => (m.Rest["left-knee"] + m.Rest["left-foot"]) / 2;
            var standard = s.Assets.Resolve(Walker)!;
            var padAt = Placement(standard, PoseEvaluator.Rest(standard), pad).Origin;
            Record(Vector2.Distance(new(padAt.X, padAt.Y), new(Mid(standard).X, Mid(standard).Y)) < 1e-4f, "the pad sits on the shin's midpoint socket at rest");
            // The variant's longer shin: the socket rides the knee bone's frame, the midpoint part spans the new length.
            var tall = s.Assets.Resolve(TallWalker)!;
            var tallPad = Placement(tall, PoseEvaluator.Rest(tall), pad).Origin;
            var alongKnee = Vector2.Normalize(new Vector2(tall.Rest["left-foot"].X, tall.Rest["left-foot"].Y) - new Vector2(tall.Rest["left-knee"].X, tall.Rest["left-knee"].Y));
            var padOffset = new Vector2(tallPad.X, tallPad.Y) - new Vector2(tall.Rest["left-knee"].X, tall.Rest["left-knee"].Y);
            Record(MathF.Abs(Vector2.Dot(padOffset, alongKnee) - shin / 2) < 1e-4f && MathF.Abs(padOffset.Length() - shin / 2) < 1e-4f && MathF.Abs(tall.Nodes["left-knee"].Length - shin - .15f) < 1e-5f,
                "on the longer shin the pad follows the knee bone's frame along the segment (the socket's offset in the bone's axes), with the shin part spanning the new length");
            s.Open(basis.Asset.MotionSets[0].Roles["walk"]);
            s.SetSubject(TallWalker);
            s.Seek(.3f);
            s.Selection.Select(attachment: pad);
            shell.Viewport.Fit(s.Scene().FirstOrDefault());
        }),
        ("04-shin-guard-no-shear", shell =>
        {
            var s = shell.Session;
            Check(s.DuplicatePart("boot-left", "shin-guard", "Shin guard"), s);
            s.Open(Walker);
            Check(s.AttachPart("shin-guard", "left-shin"), s);
            var basis = s.Assets.Model(Walker)!;
            var guard = basis.Asset.Attachments[^1].Id;
            var walker = s.Assets.Resolve(Walker)!;
            var walk = s.Assets.Clip(basis.Asset.MotionSets[0].Roles["walk"])!.Asset;
            var run = s.Assets.Clip(basis.Asset.MotionSets[0].Roles["run"])!.Asset;
            var worst = 0f;
            var turned = 0f;
            foreach (var clip in new[] { walk, run })
            {
                for (var i = 0; i <= 24; i++)
                {
                    var pose = PoseEvaluator.Sample(walker, clip, i * clip.Duration / 24.0, repeat: true);
                    var frame = Placement(walker, pose, guard);
                    worst = MathF.Max(worst, MathF.Abs(Vector2.Dot(Vector2.Normalize(frame.XAxis), Vector2.Normalize(frame.YAxis))));
                    worst = MathF.Max(worst, MathF.Abs(frame.XAxis.Length() / frame.YAxis.Length() - 1));
                    turned = MathF.Max(turned, MathF.Abs(pose.Angles["left-knee"] - walker.Nodes["left-knee"].RestAngle));
                }
            }
            Record(turned > .3f && worst < 1e-4f, $"through the walk and run the knee turns by up to {float.RadiansToDegrees(turned):0} degrees and the guard's frame stays a similarity (axes orthogonal and equal within {worst:E1}): no shear");
            s.Open(basis.Asset.MotionSets[0].Roles["run"]);
            s.Seek(.35f);
            s.Selection.Select(attachment: guard);
            shell.Viewport.Fit(s.Scene().FirstOrDefault());
        }),
        ("05-library-outfit", shell =>
        {
            var s = shell.Session;
            Check(s.NewPart("glove", "Glove"), s);
            Check(s.DuplicatePart("hair-short", "hair", "Hair"), s);
            Check(s.DuplicatePart("brute-beard", "beard", "Beard"), s);
            Check(s.DuplicatePart("cinder-hide-wrap", "shirt", "Shirt"), s);
            s.Open(Walker);
            Check(s.AttachPart("glove", Humanoid.LeftHandSocket), s);
            Check(s.AttachPart("glove", Humanoid.RightHandSocket, mirror: true), s);
            Check(s.AttachPart("hair", Humanoid.HeadSocket), s);
            Check(s.AttachPart("beard", Humanoid.HeadSocket), s);
            Check(s.AttachPart("shirt", Humanoid.BodySocket), s);
            Check(s.AttachPart("boot", Humanoid.LeftFootSocket), s);
            Check(s.AttachPart("boot", Humanoid.RightFootSocket, mirror: true), s);
            var basis = s.Assets.Model(Walker)!;
            var worn = basis.Asset.Attachments;
            Record(worn.Count(a => a.Asset == "glove") == 2 && worn.Count(a => a.Asset == "boot") == 2 && worn.Where(a => a.Asset == "boot").Select(a => a.Socket).SequenceEqual([Humanoid.LeftFootSocket, Humanoid.RightFootSocket])
                && worn.Any(a => a.Asset == "hair") && worn.Any(a => a.Asset == "beard") && worn.Any(a => a.Asset == "shirt") && s.Assets.Problems(basis).Count == 0,
                "the walker wears gloves, hair, beard, shirt and shoes from library assets, the glove and the shipped boot each on two sockets as two instances (the weapon is equipment on the entity, step 9)");
            s.Open(basis.Asset.MotionSets[0].Roles["walk"]);
            s.Pin(TallWalker);
            s.Seek(.3f);
            shell.Viewport.Fit(s.Scene().FirstOrDefault());
        }),
        ("06-instance-override-reset-shared-edit", shell =>
        {
            var s = shell.Session;
            var basis = s.Assets.Model(Walker)!;
            var rightBoot = basis.Asset.Attachments.Last(a => a.Asset == "boot").Id;
            s.Open(TallWalker);
            var tall = s.SubjectVariant!;
            Check(s.Edit(tall, () => ModelAuthoring.EditAttachmentOverride(tall.Asset, s.Assets.Resolve(TallWalker)!, rightBoot, i => i.Asset = "sandal")), s);
            Record(s.Assets.Resolve(TallWalker)!.Attachment(rightBoot)!.Asset.Id == "sandal" && s.Assets.Resolve(Walker)!.Attachment(rightBoot)!.Asset.Id == "boot",
                "an instance override swaps the tall walker's right shoe to the sandal while the base keeps its boot");
            Check(s.Edit(tall, () => ModelAuthoring.ResetOverride(tall.Asset, PropertyRef.OfAttachment(rightBoot, RigProperties.Asset))), s);
            Record(s.Assets.Resolve(TallWalker)!.Attachment(rightBoot)!.Asset.Id == "boot" && !tall.Asset.HasOverride(PropertyRef.OfAttachment(rightBoot, RigProperties.Asset)), "resetting the override restores inheritance");
            var fillBefore = s.Assets.Resolve(TallWalker)!.Attachment(rightBoot)!.Parts[0].RenderMaterial.Fill;
            s.Open("boot");
            var boot = s.PartDocument!;
            Check(s.Edit(boot, () => { var solid = boot.Asset.Geometry[0]; solid.Material = solid.RenderMaterial with { Fill = "#7a1f1f" }; boot.Asset.Validate(); }), s);
            Record(s.Assets.Resolve(Walker)!.Attachment(rightBoot)!.Parts[0].RenderMaterial.Fill == "#7a1f1f" && s.Assets.Resolve(TallWalker)!.Attachment(rightBoot)!.Parts[0].RenderMaterial.Fill == "#7a1f1f"
                && s.Assets.Resolve("person")!.Attachments.All(a => a.Asset.Id != "boot" || a.Parts[0].RenderMaterial.Fill == "#7a1f1f"),
                "editing the shared boot asset once refreshes every wearer: the walker, the tall walker and whatever else wears it");
            s.Undo();
            Record(s.Assets.Resolve(TallWalker)!.Attachment(rightBoot)!.Parts[0].RenderMaterial.Fill == fillBefore, "the shared edit undoes from the part editor");
            s.Redo();
            s.Open(basis.Asset.MotionSets[0].Roles["walk"]);
            s.SetSubject(TallWalker);
            s.Pin(Walker);
            s.Seek(.3f);
            s.Selection.Select(attachment: rightBoot);
            shell.Viewport.Fit(s.Scene().FirstOrDefault());
        }),
        ("07-game-face-expression", shell =>
        {
            var s = shell.Session;
            s.Open(Walker);
            s.ShowRest = s.EditRig = false;
            s.Expression = "happy";
            s.Seek(.9f);
            var subject = s.Evaluate(Walker)!;
            var face = subject.Pose.Attachments[PersonTemplate.FaceInstance];
            Record(face.Attachment.Asset.Id == ProceduralFace.AssetId && face.Expression == "happy" && subject.Pose.Chains.All(c => c.Reached),
                "the walker keeps its game face and smiles while its legs keep reaching");
            s.Expression = "hurt";
            Record(s.Evaluate(Walker)!.Pose.Attachments[PersonTemplate.FaceInstance].Expression == "hurt", "the game face responds to a gameplay expression request");
            s.Expression = null;
            s.Seek(.95f);
            s.Selection.Select(attachment: PersonTemplate.FaceInstance);
            shell.Viewport.Fit(s.Scene().FirstOrDefault());
        }),
        ("08-joint-limit-angle-and-bend", shell =>
        {
            var s = shell.Session;
            var basis = s.Assets.Model(Walker)!;
            Record(s.Assets.Problems(basis).Count == 0 && s.Assets.Clips.Where(c => c.Asset.Model == Walker).All(c => s.Assets.Problems(c).Count == 0), "the walker and its clips validate with the game face");
            s.Open(Walker);
            var elbowRest = s.Assets.Resolve(Walker)!.Nodes["left-elbow"].Rotation;
            Check(s.Edit(basis, () =>
            {
                var crest = ModelAuthoring.AddBone(basis.Asset, Humanoid.Head, "crest");
                crest.Local = new() { Y = .2f, Rotation = MathF.PI / 2 };
                crest.Length = .15f;
                basis.Asset.Validate();
                ModelAuthoring.AddPartToBone(basis.Asset, RoundedRectangle2D.Builder.FromSize(.15f, .04f, .01f), "crest");
                ModelAuthoring.SetLimit(basis.Asset, "crest", new() { Min = MathF.PI / 2 - .6f, Max = MathF.PI / 2 + .6f });
                ModelAuthoring.SetLimit(basis.Asset, "left-elbow", new() { Min = elbowRest - .9f, Max = elbowRest + .9f });
            }), s);
            Check(s.NewClip("walker-pose", "Walker pose", 1, false), s);
            var clip = s.ClipDocument!;
            var model = s.Assets.Resolve(Walker)!;
            Check(s.Edit(clip, () =>
            {
                ClipAuthoring.SetKey(clip.Asset, new(MotionClip.RotateKind, Humanoid.LeftShoulder), 0, default, .5f);
                ClipAuthoring.SetKey(clip.Asset, new(MotionClip.BendKind, "crest"), 0, new(1, 0, 0));
            }), s);
            s.Seek(0);
            // Targets are authored from world positions through the drag inverse (RigConstraintEvaluator.TargetDelta), as Studio's drag does:
            // one beside the shoulder pivot needs the elbow folded far past its +-0.9 rad range, one 96% of the way to the rest hand stays inside it.
            var chain = model.Chains[Humanoid.LeftArm];
            var keyed = s.Evaluate(Walker)!.Pose;
            var pivot = keyed.World(Humanoid.LeftUpperArmBone);
            var hand = keyed.World(Humanoid.LeftHand);
            var folded = RigConstraintEvaluator.TargetDelta(model, keyed, chain, pivot + new Vector3(.04f, -.04f, 0));
            var inside = RigConstraintEvaluator.TargetDelta(model, keyed, chain, pivot + (hand - pivot) * .96f);
            Check(s.Edit(clip, () => ClipAuthoring.SetKey(clip.Asset, new(MotionClip.TargetKind, Humanoid.LeftArm), 0, folded)), s);
            var pose = s.Evaluate(Walker)!.Pose;
            var arm = pose.Chains.Single(c => c.Chain == Humanoid.LeftArm);
            Record(MathF.Abs(pose.Angles[Humanoid.LeftShoulder] - model.Nodes[Humanoid.LeftShoulder].RestAngle - .5f) < 1e-4f, "an explicit angle key turns the shoulder pivot by exactly its value");
            Record(pose.Limits["crest"].State == ConstraintLimitState1D.Upper && MathF.Abs(Angles.WrapSigned(pose.Angles["crest"] - pose.Angles[Humanoid.Head] - (MathF.PI / 2 + .6f))) < 1e-3f,
                "a normalized bend of 1 lands the crest exactly on its upper limit, parent-local");
            Record(arm.Limited && !arm.Reached && pose.Limits["left-elbow"].State != ConstraintLimitState1D.Inactive,
                $"a target that needs the elbow past its adjusted limit is held at the bound and reported unreached by {arm.Residual:F3}");
            Check(s.Edit(clip, () => ClipAuthoring.SetKey(clip.Asset, new(MotionClip.TargetKind, Humanoid.LeftArm), 0, inside)), s);
            var reach = s.Evaluate(Walker)!.Pose.Chains.Single(c => c.Chain == Humanoid.LeftArm);
            Record(reach is { Reached: true, Limited: false }, $"a target inside the elbow's range is reached exactly (residual {reach.Residual:E1}, limited {reach.Limited})");
            s.SetMode(Workspace.Animate);
            s.Selection.Select(control: "left-elbow");
            shell.Viewport.Fit(s.Evaluate(Walker));
        }),
        ("09-weapon-sensor-and-behaviors", shell =>
        {
            var s = shell.Session;
            var basis = s.Assets.Model(Walker)!;
            // The template copies the locomotion library; the thrust is authored for the walker from the shipped Person thrust (same control ids and measures).
            var thrust = MotionClip.FromJson(s.Assets.Clip("person-thrust")!.Asset.ToJson());
            thrust.Id = "walker-thrust";
            thrust.Name = "Walker thrust";
            thrust.Model = Walker;
            thrust.StructureRevision = basis.Asset.StructureRevision;
            thrust.Validate(s.Assets.Resolve(Walker)!);
            s.Assets.Create(thrust);
            Check(s.NewEntity(WalkerGuard, "Walker guard", TallWalker, EntityAuthoring.Guard), s);
            Check(s.SetEntityRole("attack", thrust.Id), s);
            var entity = s.EntityDocument!;
            Check(s.Edit(entity, () => entity.Asset.Equipment.Add(new() { Prop = "spear", Socket = Humanoid.RightGripSocket })), s);
            Check(s.AddCollider(new() { Id = "spear-tip", Prop = "spear", Point = EquipmentPoints.Tip, Along = -.14f, Geometry = Rectangle2D.Builder.FromSize(.42f, .26f) }, ColliderUses.Attack), s);
            Check(s.Edit(entity, () =>
            {
                var attack = entity.Asset.Behavior!.Actions.Single(a => a.Id == "attack");
                attack.Hits.Add(new() { Id = "spear-tip", Collider = "spear-tip", Start = new() { Marker = "strike" }, Finish = new() { Marker = "recover" } });
                attack.Events.Add(new() { Id = "swing", At = new() { Marker = "strike" }, Sound = "swing" });
                entity.Asset.Behavior!.Strikes["spear-tip"] = new() { Damage = 2, Sound = "hit" };
                entity.Asset.Behavior!.Damage.Health = 6;
            }), s);
            var resolved = s.Entity!;
            var window = resolved.Actions["attack"].Hits[0];
            Record(resolved.Collider("spear-tip") is { Use: ColliderUses.Attack, Binding: ColliderBindings.Prop } && window.Collider.Id == "spear-tip" && window.Strike.Damage == 2 && window.Strike.Sound == "hit" && resolved.Damage.Health == 6,
                "the entity equips the spear and declares a sensor on its tip: the attack window enables it, its strike and the entity's health are typed behaviors");
            s.PreviewActionOf("attack");
            s.Seek(window.Start - .02f);
            var before = s.EvaluateEntity()!;
            s.Seek(window.Start + .02f);
            var inside = s.EvaluateEntity()!;
            s.Seek(window.Finish + .02f);
            var after = s.EvaluateEntity()!;
            Record(!before.Colliders.First(c => c.Id == "spear-tip").Enabled && inside.Colliders.First(c => c.Id == "spear-tip").Enabled && inside.Attacks.Count == 1 && !after.Colliders.First(c => c.Id == "spear-tip").Enabled,
                "the sensor's activation follows the window: disabled before, enabled with the attack region inside, disabled after");
            s.Seek(window.Start + .02f);
            s.Selection.Collider = "spear-tip";
            shell.Viewport.Fit(s.Scene().FirstOrDefault());
        }),
        ("10-hits-block-miss-kill-several", shell =>
        {
            var s = shell.Session;
            shell.LiveTest = false;
            AuthoredArena Fight(string[] roster, Vector2[] positions, int ticks, bool press = false)
            {
                shell.Test.Stop();
                shell.Test.Roster.Clear();
                shell.Test.Roster.AddRange(roster);
                shell.Test.Start();
                var arena = shell.Test.Arena ?? throw new InvalidOperationException("The Test arena did not start: " + string.Join(" | ", shell.Test.Problems));
                for (var i = 0; i < positions.Length; i++)
                {
                    arena.Teleport(i, positions[i]);
                }

                for (var i = 0; i < ticks; i++)
                {
                    // The first roster entry is the controlled actor: it attacks only on a press (one, on the first tick).
                    shell.Test.Advance(1 / 120f, new ArenaInput(Attack: press && i == 1));
                }

                shell.Test.Paused = true;
                return arena;
            }
            // Hits: the walker guard's thrust against the player's three hurt colliders lands once per thrust.
            var hits = Fight(["player", WalkerGuard], [new(0, 0), new(2.1f, 0)], 240);
            var thrusts = hits.Events.Count(e => e.Actor == 1 && e.Event.Id == "strike");
            Record(hits.Hits.Count > 0 && hits.Hits.All(h => h is { Attacker: 1, Target: 0, Window: "spear-tip", Damage: 2 }) && hits.Hits.Count <= thrusts && hits.Player.Health == Catalog(s).Entities["player"].Damage.Health - 2 * hits.Hits.Count,
                $"the guard's thrust hits the player ({hits.Hits.Count} hit(s) over {thrusts} thrust(s), one per thrust whatever the hurt collider count), each for its typed damage");
            // Block: the same thrust into the shield defender's raised guard is taken by the guard, not the hurt geometry.
            var block = Fight(["shield-defender", WalkerGuard], [new(0, 0), new(2.1f, 0)], 240);
            Record(block.Blocks.Count > 0 && block.Blocks.All(b => b is { Attacker: 1, Target: 0, Window: "spear-tip" }) && !block.Hits.Any(h => h.Target == 0) && block.Actors[0].Health == Catalog(s).Entities["shield-defender"].Damage.Health,
                $"the thrust into the shield defender's front guard is blocked ({block.Blocks.Count} block(s), no damage)");
            // Miss: out of reach, nothing lands.
            var miss = Fight(["player", WalkerGuard], [new(0, 0), new(6, 0)], 60);
            Record(miss.Hits.Count == 0 && miss.Blocks.Count == 0, "out of reach the thrust misses: no hit or block");
            // Kill: the stalker pest (health 2) dies to one typed strike of 2.
            var kill = Fight(["stalker-pest", WalkerGuard], [new(0, 0), new(2.0f, 0)], 360);
            Record(kill.Hits.Any(h => h.Target == 0) && !kill.Actors[0].Alive, "the stalker pest takes the strike and dies");
            // Several targets: the walker guard, controlled, thrusts once into the player and the spear guard standing together; one attack
            // instance strikes both (the brute's slam is the reference set's recorded miss, so it is not the probe here).
            var several = Fight([WalkerGuard, "player", "spear-guard"], [new(0, 0), new(2.0f, 0), new(2.15f, 0)], 120, press: true);
            var struck = several.Hits.Where(h => h.Attacker == 0).Select(h => h.Target).Distinct().ToList();
            Record(struck.Count == 2, $"one thrust of the walker guard strikes both targets ({string.Join("; ", several.Hits.Where(h => h.Attacker == 0).Select(h => $"tick {h.Tick} target {h.Target}"))})");
            shell.Test.Stop();
            _ = Fight(["player", WalkerGuard], [new(0, 0), new(2.1f, 0)], 12);
            shell.Viewport.Fit(s.Scene().FirstOrDefault());
        }),
        ("11-cues-from-outcomes-not-seeking", shell =>
        {
            var s = shell.Session;
            var arena = shell.Test.Arena!;
            Record(arena.Hits.Count == 0 && arena.Events.All(e => e.Event.Sound is null), "twelve ticks in, nothing has landed and no sound has been raised");
            shell.Test.Paused = false;
            for (var i = 0; i < 480 && arena.Hits.Count == 0; i++)
            {
                shell.Test.Advance(1 / 120f, default);
            }
            shell.Test.Paused = true;
            var strike = s.Assets.CompileEntity(WalkerGuard)!.Actions["attack"].Hits[0].Strike;
            var sounds = arena.Events.Where(e => e.Event.Sound is not null).ToList();
            Record(arena.Hits.Count > 0 && sounds.Any(e => e.Event.Id == "swing") && strike.Sound == "hit",
                $"the swing's event sound and the accepted contact with its strike sound are the facts the game's audio and effects read ({arena.Hits.Count} hit(s), first at tick {arena.Hits[0].Tick}, strike sound '{strike.Sound}')");
            shell.Test.Stop();
            s.Open(WalkerGuard);
            s.PreviewActionOf("attack");
            var window = s.Entity!.Actions["attack"].Hits[0];
            var attacks = 0;
            for (var i = 0; i <= 20; i++)
            {
                s.Seek(window.Start - .05f + i * (window.Finish - window.Start + .1f) / 20);
                attacks += s.EvaluateEntity()!.Attacks.Count;
            }
            Record(attacks > 0 && shell.Test.Arena is null, "seeking the timeline through the window previews the enabled sensor but raises no contact, hit or cue: those come from the arena's accepted outcomes only");
            s.Seek(window.Start + .02f);
            shell.Viewport.Fit(s.Scene().FirstOrDefault());
        }),
        ("12-save-reopen-undo", shell =>
        {
            var s = shell.Session;
            var basis = s.Assets.Model(Walker)!;
            var walkId = basis.Asset.MotionSets[0].Roles["walk"];
            var liveWalk = PoseEvaluator.Sample(s.Assets.Resolve(TallWalker)!, s.Assets.Clip(walkId)!.Asset, .3, repeat: true);
            var liveGuard = s.Assets.CompileEntity(WalkerGuard)!;
            s.SaveAll();
            Record(!s.MessageIsError && !s.Assets.DirtyDocuments.Any(), "save all writes every dependent asset: " + s.Message);
            var reopened = AuthoringWorkspace.Open(s.Assets.Root);
            var reopenedWalk = PoseEvaluator.Sample(reopened.Resolve(TallWalker)!, reopened.Clip(walkId)!.Asset, .3, repeat: true);
            var reopenedGuard = reopened.CompileEntity(WalkerGuard);
            var same = liveWalk.Points.All(p => Vector3.Distance(p.Value, reopenedWalk.Points[p.Key]) < 1e-5f) && liveWalk.Attachments.Keys.SequenceEqual(reopenedWalk.Attachments.Keys);
            Record(reopened.LoadErrors.Count == 0 && reopened.Documents.All(d => reopened.Problems(d).Count == 0) && same && reopenedGuard is not null
                && reopenedGuard.Colliders.Select(c => c.Id).SequenceEqual(liveGuard.Colliders.Select(c => c.Id)) && reopenedGuard.Actions["attack"].Hits[0].Strike == liveGuard.Actions["attack"].Hits[0].Strike,
                "reopened from disk, the tall walker's walk pose, its outfit and the guard's colliders and strike are the same");
            // Undo across a shared asset edit and an instance edit, in order.
            var rightBoot = basis.Asset.Attachments.Last(a => a.Asset == "boot").Id;
            s.Open("boot");
            var boot = s.PartDocument!;
            var fill = boot.Asset.Geometry[0].RenderMaterial.Fill;
            Check(s.Edit(boot, () => { var solid = boot.Asset.Geometry[0]; solid.Material = solid.RenderMaterial with { Fill = "#2f6f2f" }; boot.Asset.Validate(); }), s);
            s.Open(TallWalker);
            var tall = s.SubjectVariant!;
            Check(s.Edit(tall, () => ModelAuthoring.EditAttachmentOverride(tall.Asset, s.Assets.Resolve(TallWalker)!, rightBoot, i => i.Asset = "sandal")), s);
            Record(s.Assets.Resolve(TallWalker)!.Attachment(rightBoot)!.Asset.Id == "sandal" && s.Assets.Resolve(Walker)!.Attachment(rightBoot)!.Parts[0].RenderMaterial.Fill == "#2f6f2f", "a shared edit and an instance edit stand");
            s.Undo();
            Record(s.Assets.Resolve(TallWalker)!.Attachment(rightBoot)!.Asset.Id == "boot" && s.Assets.Resolve(Walker)!.Attachment(rightBoot)!.Parts[0].RenderMaterial.Fill == "#2f6f2f", "the first undo takes back the instance edit");
            s.Open("boot");
            s.Undo();
            Record(s.Assets.Resolve(Walker)!.Attachment(rightBoot)!.Parts[0].RenderMaterial.Fill == fill, "the second undo takes back the shared edit on its own document");
            s.Open(walkId);
            s.SetSubject(TallWalker);
            s.Seek(.3f);
            shell.Viewport.Fit(s.Scene().FirstOrDefault());
        }),
        ("13a-quadruped", shell =>
        {
            var s = shell.Session; s.Compare.Clear();
            Check(s.NewModel("hound", "Hound", "quadruped", "long-legged"), s);
            var hound = s.Assets.Resolve("hound")!;
            Check(s.NewVariant("hound-tall", "Tall hound", "hound"), s);
            var tall = s.SubjectVariant!;
            Check(s.Edit(tall, () => tall.Asset.Parameters["legs"] = 1.45f), s);
            var walkId = hound.Base.MotionSets[0].Roles["walk"];
            var pose = PoseEvaluator.Sample(s.Assets.Resolve("hound-tall")!, s.Assets.Clip(walkId)!.Asset, .3, repeat: true);
            Record(s.Assets.Resolve("hound-tall")!.Rest["body"].Y > hound.Rest["body"].Y && pose.Chains.All(c => c.Reached) && pose.Contacts.All(c => c.Residual < 1e-4f),
                "a quadruped from the template with a longer-legged variant plays the shared walk with four legs reaching and planted feet holding");
            Check(s.NewPart("saddle", "Saddle"), s);
            s.Open("hound");
            Check(s.AttachPart("saddle", "body-art"), s);
            var saddle = s.Assets.Model("hound")!.Asset.Attachments[^1].Id;
            var runId = hound.Base.MotionSets[0].Roles["run"];
            var worst = 0f;
            for (var i = 0; i <= 16; i++)
            {
                var tallHound = s.Assets.Resolve("hound-tall")!;
                var frame = Placement(tallHound, PoseEvaluator.Sample(tallHound, s.Assets.Clip(runId)!.Asset, i / 16.0, repeat: true), saddle);
                worst = MathF.Max(worst, MathF.Abs(Vector2.Dot(Vector2.Normalize(frame.XAxis), Vector2.Normalize(frame.YAxis))));
            }
            Record(worst < 1e-4f, "the saddle rides the body socket through the run as a similarity");
            var rush = s.Assets.Clips.Single(c => c.Asset.Model == "hound" && c.Id.EndsWith("-rush", StringComparison.Ordinal)).Id;
            Check(s.NewEntity("hound-guard", "Hound guard", "hound-tall", EntityAuthoring.Guard), s);
            Check(s.SetEntityRole("attack", rush), s);
            Check(s.AddCollider(new() { Id = "bite", A = "head", Frame = "head", OffsetX = .25f, Geometry = Circle2D.Builder.Create(.22f) }, ColliderUses.Attack), s);
            var entity = s.EntityDocument!;
            // The guard template's range (1.6) suits a spear; the hound attacks from where its bite actually sits ahead of its feet.
            var biteAhead = s.Entity!.Collider("bite")!.Region(new ActorPose(PoseEvaluator.Rest(s.Entity.Model), Vector2.Zero, 1)).Bounds.Max.X;
            Check(s.Edit(entity, () =>
            {
                entity.Asset.Behavior!.Controller.Range = MathF.Round(biteAhead, 2);
                entity.Asset.Behavior!.Actions.Single(a => a.Id == "attack").Hits.Add(new() { Id = "bite", Collider = "bite", Start = new() { At = .1f }, Finish = new() { At = .7f } });
                entity.Asset.Behavior!.Strikes["bite"] = new() { Damage = 1, Sound = "hit" };
            }), s);
            Record(s.Entity?.Actions["attack"].Hits[0].Collider.Id == "bite" && s.Assets.Problems(entity).Count == 0, "a hound entity on the variant declares a bite sensor on its head and compiles: " + (s.EntityError ?? "ok"));
            shell.Test.Stop();
            shell.Test.Roster.Clear();
            shell.Test.Roster.AddRange(["player", "hound-guard"]);
            shell.Test.Start();
            var arena = shell.Test.Arena!;
            arena.Teleport(0, new(0, 0));
            arena.Teleport(1, new(2.4f, 0));
            for (var i = 0; i < 720 && !arena.Hits.Any(h => h.Attacker == 1); i++)
            {
                shell.Test.Advance(1 / 120f, default);
            }
            shell.Test.Paused = true;
            var houndActor = arena.Actors[1];
            Record(arena.Hits.Any(h => h is { Attacker: 1, Target: 0, Window: "bite" }),
                $"the hound's bite lands on the player in the Test arena ({arena.Hits.Count} hit(s); hound at {houndActor.Position.X:0.00} facing {houndActor.Facing}, role {houndActor.Animator.Role}, {arena.Events.Count(e => e.Actor == 1)} hound event(s), tick {arena.Tick})");
            shell.Test.Stop();
            s.Open(walkId);
            s.SetSubject("hound-tall");
            s.Pin("hound");
            s.Seek(.3f);
            s.Selection.Select(attachment: saddle);
            shell.Viewport.Fit(s.Scene().FirstOrDefault());
        }),
        ("13b-flier", shell =>
        {
            var s = shell.Session; s.Compare.Clear();
            Check(s.NewModel("flier", "Flier", "pterosaur", "broad-winged"), s);
            var flier = s.Assets.Resolve("flier")!;
            Check(s.NewVariant("flier-wide", "Wide flier", "flier"), s);
            var wide = s.SubjectVariant!;
            Check(s.Edit(wide, () => wide.Asset.Parameters["wings"] = 1.3f), s);
            var hoverId = flier.Base.MotionSets[0].Roles["hover"];
            var hover = PoseEvaluator.Sample(s.Assets.Resolve("flier-wide")!, s.Assets.Clip(hoverId)!.Asset, .5, repeat: true);
            Record(s.Assets.Resolve("flier-wide")!.ParameterValues["wings"] == 1.3f && hover.Chains.All(c => c.Reached), "a flier from the template with a wider-winged variant plays the shared hover with its chains reaching");
            Check(s.NewPart("crest-plume", "Crest plume"), s);
            s.Open("flier");
            Check(s.AttachPart("crest-plume", "head-art"), s);
            var plume = s.Assets.Model("flier")!.Asset.Attachments[^1].Id;
            // The template copies the flight library including the swoop with its flight-phase markers; the attack action carries the
            // flight phases the flying controller needs, as the shipped flier's does.
            var swoop = s.Assets.Clip("flier-swoop")!.Asset;
            var shippedAttack = s.Assets.Entity("flying-pterosaur")!.Asset.Behavior!.Actions.Single(a => a.Id == "attack");
            Check(s.NewEntity("flier-pest", "Flier pest", "flier-wide", EntityAuthoring.Stationary), s);
            var entity = s.EntityDocument!;
            Check(s.AddCollider(new() { Id = "beak", A = "jaw", Frame = "jaw", OffsetX = .2f, Geometry = Circle2D.Builder.Create(.18f) }, ColliderUses.Attack), s);
            Check(s.Edit(entity, () =>
            {
                entity.Asset.Behavior!.Controller.Kind = EntityControllers.Flying;
                entity.Asset.Behavior!.Controller.Flight = new();
                entity.Asset.Behavior!.Controller.Range = 3.2f;
                entity.Asset.Behavior!.Actions.Add(new()
                {
                    Id = "attack",
                    Clip = swoop.Id,
                    Hits = [new() { Id = "beak", Collider = "beak", Start = new() { Marker = "rush" }, Finish = new() { Marker = "brake" } }],
                    Events = [.. shippedAttack.Events.Select(e => new ActionEvent { Id = e.Id, At = new() { Marker = e.At.Marker, At = e.At.At }, Sound = e.Sound })],
                });
                entity.Asset.Behavior!.Strikes["beak"] = new() { Damage = 2, Sound = "hit" };
            }), s);
            Record(s.Entity is { } resolved && resolved.Controller.Id == EntityControllers.Flying && resolved.Actions["attack"].Hits[0].Collider.Id == "beak" && s.Assets.Problems(entity).Count == 0,
                "a flying entity on the variant declares a beak sensor over its swoop with the flight phases and compiles: " + (s.EntityError ?? "ok"));
            s.PreviewActionOf("attack");
            var window = s.Entity?.Actions["attack"].Hits[0];
            s.Seek((window?.Start ?? 0) + .02f);
            var preview = s.EvaluateEntity();
            Record(preview is not null && preview.Colliders.First(c => c.Id == "beak").Enabled && preview.Attacks.Count == 1, "inside the swoop's rush window the beak sensor is enabled at the jaw");
            s.Selection.Collider = "beak";
            shell.Viewport.Fit(s.Scene().FirstOrDefault());
        }),
        ("14-centaur-composition", shell =>
        {
            var s = shell.Session; s.Compare.Clear();
            const string lower = "lower", upper = "upper";
            var model = new CharacterModel { Id = "my-centaur", Name = "My centaur", Ink = "#242a29", LineWidth = .045f };
            model.Controls.Add(new() { Id = "root", Rest = new(0, .92f, 0) });
            model.Parts.Add(new() { Id = "root-mark", A = "root", Geometry = Ellipse2D.Builder.Create(new(.02f, .02f)), Material = new() { Fill = "#000000" }, Hidden = true });
            model.Sockets.Add(new() { Id = "mount", Control = "root", Frame = CharacterModel.Locomotion, Usage = ModulePlans.LowerBody });
            model.Sockets.Add(new() { Id = "saddle", Control = lower + Part.Namespace + "body", Transform = new(new(.42f, .98f)), Usage = ModulePlans.UpperBody });
            model.Attachments.Add(new() { Id = lower, Asset = ModulePlans.QuadrupedLower, Socket = "mount" });
            model.Attachments.Add(new() { Id = upper, Asset = ModulePlans.PersonUpper, Socket = "saddle", Scale = .9f, Layer = -.05f });
            model.MotionOwner = lower;
            model.Validate();
            s.Assets.Create(model);
            s.Open("my-centaur");
            Check(s.NewClip("my-centaur-walk", "Centaur walk", 1, true), s);
            var walk = s.ClipDocument!;
            Check(s.Edit(walk, () =>
            {
                ClipAuthoring.SetPlay(walk.Asset, lower, 0, "walk");
                ClipAuthoring.SetPlay(walk.Asset, upper, 0, "hammer-slam");
            }), s);
            s.Seek(.55f);
            var centaur = s.Assets.Resolve("my-centaur")!;
            var pose = s.Evaluate("my-centaur")!.Pose;
            var gait = pose.Attachments[lower];
            var torso = pose.Attachments[upper];
            var saddle = gait.Frame("body").At(new Vector3(.42f, .98f, 0));
            // The upper body's connection (its chest) is placed on the saddle; the slam then moves the module's own chest within that placement.
            var seat = Vector2.Distance(new(saddle.X, saddle.Y), new(torso.Placement.Origin.X, torso.Placement.Origin.Y));
            Record(centaur.Base.Controls.Count == 1 && centaur.MotionOwner?.Id == lower && (gait.Expression, torso.Expression) == ("walk", "hammer-slam"),
                $"the centaur is one root control wearing the quadruped lower body and the Person upper body, the lower body owning the motion and walking while the upper body slams ({gait.Expression}, {torso.Expression})");
            Record(gait.Pose.Chains.All(c => c.Reached) && gait.Pose.Contacts.Count >= 2,
                $"the lower body's legs reach with its planted feet holding ({gait.Pose.Chains.Count(c => c.Reached)}/{gait.Pose.Chains.Count} reached, {gait.Pose.Contacts.Count} contacts)");
            Record(seat < 1e-4f, $"the upper body's connection rides the saddle socket on lower/body as the gait moves it ({seat:E1} apart)");
            var quadrupedWalk = s.Assets.Part(ModulePlans.QuadrupedLower)!.Asset.Clip("quadruped-walk")!;
            Record(MathF.Abs(PoseEvaluator.CycleTravel(centaur, walk.Asset).X - PoseEvaluator.CycleTravel(gait.Rig, quadrupedWalk).X) < 1e-5f, "the stride is the quadruped gait's, carried by the owning module, with no rig copied and no dedicated runtime type");
            s.SaveAll();
            var reopened = AuthoringWorkspace.Open(s.Assets.Root);
            Record(reopened.LoadErrors.Count == 0 && reopened.Resolve("my-centaur")!.Attachments.Count == 2 && reopened.Problems(reopened.Model("my-centaur")!).Count == 0, "the composition saves and reopens as resources");
            s.SetMode(Workspace.Animate);
            s.Selection.Select(attachment: upper);
            shell.Viewport.Fit(s.Evaluate("my-centaur"));
        }),
    ];

    private static AuthoredCatalog Catalog(EditorSession s) => AuthoredCatalog.Load(s.Assets.Root);

    /// <summary>A part instance's placement at a pose: the socket's placement at that pose composed with the instance's own offset (the drawing's rule).</summary>
    private static Core.Characters.Geometry.LayeredFrame2D Placement(ResolvedModel model, EvaluatedPose pose, string instance)
    {
        var attachment = model.Attachment(instance) ?? throw new InvalidOperationException($"'{model.Id}' has no attachment '{instance}'.");
        return attachment.Placement(new ActorPose(pose, Vector2.Zero, 1).SocketPlacement(attachment.Socket));
    }
}
