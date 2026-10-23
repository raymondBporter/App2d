using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Editing;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Tests.Authored;

/// <summary>P3c of the character-system migration: the edit commands on canonical bone nodes and the inverse drags.</summary>
public sealed class BoneEditingTests
{
    private static Vector2 XY(Vector3 v) => new(v.X, v.Y);

    /// <summary>A root bone carrying a child bone, a bound grandchild, a loose point control and a bound lamp; the root may be scaled and reflected.</summary>
    private static CharacterModel Rig(float scale = 1, bool reflect = false)
    {
        var model = ModelAuthoring.Empty("rig", "Rig");
        var root = ModelAuthoring.AddBone(model, null, "root");
        root.Local = new() { X = 3, Y = -2, Rotation = .3f, Scale = scale, Reflect = reflect, Layer = .1f };
        root.Length = 1;
        var child = ModelAuthoring.AddBone(model, "root", "child");
        child.Local = new() { Rotation = .4f, Layer = -.05f };
        ModelAuthoring.AddBone(model, "child", "tip");
        ModelAuthoring.AddControl(model, "child", ResolvedModel.From(model).Rest["child"] + new Vector3(.2f, .3f, -.02f), "marker");
        model.Controls.Add(new() { Id = "lamp", Parent = "root", Node = RigNodeKinds.Bone, Link = new() { T = .5f, OffsetY = .2f } });
        model.Validate();
        return model;
    }

    [Theory, InlineData(1, false), InlineData(2, false), InlineData(.5f, true), InlineData(2, true)]
    public void MoveRotateResizeAndTipDragRoundTripUnderReflectionAndUniformScale(float scale, bool reflect)
    {
        var model = Rig(scale, reflect);
        var before = ResolvedModel.From(model);
        Assert.True(before.Nodes["child"].Link is { IsTip: true });
        TestModels.Near(XYZ(Vector2.Transform(new(1, 0), before.RestTransforms["root"]), before.Rest["child"].Z), before.Rest["child"]);

        // Resize: the bound child and everything below it move to the new tip; frames keep their scale.
        ModelAuthoring.ResizeBone(model, "root", 1.5f);
        var longer = ResolvedModel.From(model);
        TestModels.Near(XYZ(Vector2.Transform(new(1.5f, 0), before.RestTransforms["root"]), before.Rest["child"].Z), longer.Rest["child"]);
        Assert.Equal(scale, new Frame2D(longer.RestTransforms["child"]).XAxis.Length(), 4);
        TestModels.Near(XYZ(Vector2.Transform(new(.75f, .2f), before.RestTransforms["root"]), before.Rest["lamp"].Z), longer.Rest["lamp"]);
        // The loose point control keeps its authored rest.
        TestModels.Near(before.Rest["marker"], longer.Rest["marker"]);

        // Rotate to a character direction: bound descendants turn, the point control turns about the pivot.
        ModelAuthoring.RotateRestBone(model, "root", 1.1f);
        var turned = ResolvedModel.From(model);
        Assert.Equal(1.1f, turned.Nodes["root"].RestAngle, 4);
        TestModels.Near(XYZ(XY(turned.Rest["root"]) + Rotation2D.Apply(new(1.5f * scale, 0), 1.1f), before.Rest["child"].Z), turned.Rest["child"]);
        TestModels.Near(Rotation2D.ApplyXYAround(longer.Rest["marker"], XY(longer.Rest["root"]), 1.1f - longer.Nodes["root"].RestAngle), turned.Rest["marker"]);
        Assert.Equal(turned.Nodes["child"].RestAngle, turned.Nodes["root"].RestAngle + (reflect ? -1 : 1) * .4f, 4);

        // Tip drag: rotate then resize so the tip lands exactly, whatever the inherited scale and handedness.
        var target = XY(turned.Rest["child"]) + new Vector2(-.7f, .9f);
        ModelAuthoring.MoveBoneTip(model, "child", target);
        var dragged = ResolvedModel.From(model);
        Assert.InRange(Vector2.Distance(target, Vector2.Transform(new(model.Controls.Single(c => c.Id == "child").Length, 0), dragged.RestTransforms["child"])), 0, 1e-4f);
        Assert.InRange(Vector2.Distance(target, XY(dragged.Rest["tip"])), 0, 1e-4f);
        Assert.Equal(scale, new Frame2D(dragged.RestTransforms["tip"]).XAxis.Length(), 4);

        // Move the root's origin: its local translation re-expresses the character position; descendants follow with the flag.
        var origin = dragged.Rest["root"] + new Vector3(.4f, -.6f, .03f);
        ModelAuthoring.MoveRest(model, "root", origin, children: true);
        var moved = ResolvedModel.From(model);
        TestModels.Near(origin, moved.Rest["root"]);
        TestModels.Near(dragged.Rest["tip"] + new Vector3(.4f, -.6f, .03f), moved.Rest["tip"]);
        TestModels.Near(dragged.Rest["marker"] + new Vector3(.4f, -.6f, .03f), moved.Rest["marker"]);
        // A bound origin cannot be moved; the reason names the link.
        Assert.Contains("bound to 'root'", Assert.Throws<InvalidOperationException>(() => ModelAuthoring.MoveRest(model, "child", Vector3.Zero, true)).Message);

        // JSON round trip keeps every edited setup.
        var json = model.ToJson();
        var reread = ResolvedModel.From(CharacterModel.FromJson(json));
        foreach (var control in moved.Order)
        {
            TestModels.Near(moved.Rest[control.Id], reread.Rest[control.Id], 1e-6f, control.Id);
        }
    }

    [Fact]
    public void MovingWithoutChildrenKeepsBoneAndPointDescendantsInPlace()
    {
        var model = Rig(2, true);
        var free = ModelAuthoring.AddBone(model, "root", "free");
        free.Link = null;
        free.Local = new() { X = .3f, Y = .1f };
        model.Validate();
        var before = ResolvedModel.From(model);
        ModelAuthoring.MoveRest(model, "root", before.Rest["root"] + new Vector3(.5f, .5f, 0), children: false);
        var after = ResolvedModel.From(model);
        TestModels.Near(before.Rest["free"], after.Rest["free"], 1e-5f, "unbound bone child stays");
        TestModels.Near(before.Rest["marker"], after.Rest["marker"], 1e-5f, "point descendant stays");
        // A bound child necessarily follows its parent.
        TestModels.Near(before.Rest["child"] + new Vector3(.5f, .5f, 0), after.Rest["child"], 1e-5f, "bound child follows");

        // A point parent with a bone child: the bone child re-expresses its local setup to stay.
        var person = CharacterModel.FromJson(AuthoredCatalog.Load(TestModels.AuthoredRoot).Models["person"].ToJson());
        var rest = ResolvedModel.From(person);
        ModelAuthoring.MoveRest(person, "chest", rest.Rest["chest"] + new Vector3(0, .2f, 0), children: false);
        var lifted = ResolvedModel.From(person);
        TestModels.Near(rest.Rest["left-shoulder"], lifted.Rest["left-shoulder"], 1e-5f);
        TestModels.Near(rest.Rest["left-hand"], lifted.Rest["left-hand"], 1e-5f);
        TestModels.Near(rest.Rest["right-shoulder"], lifted.Rest["right-shoulder"], 1e-5f);
        ModelAuthoring.MoveRest(person, "chest", rest.Rest["chest"], children: true);
        var back = ResolvedModel.From(person);
        TestModels.Near(rest.Rest["left-hand"] - new Vector3(0, .2f, 0), back.Rest["left-hand"], 1e-5f, "with children the arm moves with the chest");
    }

    [Fact]
    public void ReparentingKeepsTheCharacterFrameAndDropsTheLink()
    {
        var model = Rig(2, true);
        var before = ResolvedModel.From(model);
        ModelAuthoring.Reparent(model, "tip", "root");
        var tip = model.Controls.Single(c => c.Id == "tip");
        Assert.Null(tip.Link);
        Assert.Equal("root", tip.Parent);
        var after = ResolvedModel.From(model);
        TestModels.Near(before.Rest["tip"], after.Rest["tip"], 1e-5f);
        Assert.Equal(before.Nodes["tip"].RestAngle, after.Nodes["tip"].RestAngle, 4);
        Assert.Equal(before.Nodes["tip"].Handedness, after.Nodes["tip"].Handedness);
        Assert.Equal(new Frame2D(before.RestTransforms["tip"]).XAxis.Length(), new Frame2D(after.RestTransforms["tip"]).XAxis.Length(), 4);
        ModelAuthoring.Reparent(model, "tip", null);
        var rooted = ResolvedModel.From(model);
        TestModels.Near(before.Rest["tip"], rooted.Rest["tip"], 1e-5f);
        Assert.Equal(2, rooted.Nodes["tip"].Setup.Scale, 4);
        Assert.True(rooted.Nodes["tip"].Setup.Reflect);
        var (wheel, _) = TestModels.Wheel();
        wheel.Controls.Add(new() { Id = "pin", Node = RigNodeKinds.Bone, Local = new() { X = 1 } });
        wheel.Validate();
        Assert.Contains("affine", Assert.Throws<InvalidOperationException>(() => ModelAuthoring.Reparent(wheel, "pin", "wheel-root")).Message);
    }

    [Theory, InlineData(1, false), InlineData(2, false), InlineData(.5f, true), InlineData(2, true)]
    public void AnimatedDragIsTheInverseOfEvaluationUnderReflectionAndScale(float scale, bool reflect)
    {
        var model = ResolvedModel.From(Rig(scale, reflect));
        var clip = ClipAuthoring.New(model, "rig-move", "Move");
        ClipAuthoring.SetKey(clip, new(MotionClip.RotateKind, "root"), 0, default, .7f);
        ClipAuthoring.SetKey(clip, new(MotionClip.TranslateKind, "root"), 0, new(.2f, -.1f, .04f));
        clip.Validate(model);
        foreach (var control in new[] { "lamp", "marker", "child", "root" })
        {
            var pose = PoseEvaluator.Sample(model, clip, 0);
            var desired = pose.World(control) + new Vector3(.35f, -.25f, .02f);
            ClipAuthoring.Pose(model, clip, pose, 0, control, desired);
            clip.Validate(model);
            TestModels.Near(desired, PoseEvaluator.Sample(model, clip, 0).World(control), 1e-4f, control);
        }
        // The keyed delta is in the carrier's own axes, so a mirrored parent keys a mirrored value.
        var key = ClipAuthoring.Value(clip, new(MotionClip.TranslateKind, "child"), 0).Value;
        Assert.True(MathF.Abs(key.X) > 1e-3f || MathF.Abs(key.Y) > 1e-3f);
    }

    [Fact]
    public void VariantsOverrideBoneSetupsThroughTypedOverrides()
    {
        var catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);
        var person = catalog.Resolve("person");
        var variant = new ModelVariant { Id = "v", Name = "V", Base = "person" };
        // An unbound bone node moves through its local setup; a tip-bound one is refused with the link named.
        ModelAuthoring.MoveRest(person, variant, "left-shoulder", person.Rest["left-shoulder"] + new Vector3(.05f, 0, 0), true);
        Assert.True(variant.HasOverride(PropertyRef.OfControl("left-shoulder", RigProperties.LocalX)));
        Assert.False(variant.HasOverride(PropertyRef.OfControl("left-shoulder", RigProperties.LocalY)));
        TestModels.Near(person.Rest["left-hand"] + new Vector3(.05f, 0, 0), ResolvedModel.From(person.Base, variant).Rest["left-hand"], 1e-5f);
        Assert.Contains("bound to", Assert.Throws<InvalidOperationException>(() => ModelAuthoring.MoveRest(person, variant, "left-elbow", Vector3.Zero, true)).Message);
        variant.Overrides.Clear();
        // Moving the chest alone re-expresses its unbound children (the bone shoulder and the point controls) so they stay put.
        ModelAuthoring.MoveRest(person, variant, "chest", person.Rest["chest"] + Vector3.UnitY * .1f, false);
        var kept = ResolvedModel.From(person.Base, variant);
        foreach (var child in person.Base.Controls.Where(c => c.Parent == "chest"))
        {
            TestModels.Near(person.Rest[child.Id], kept.Rest[child.Id], 1e-5f, child.Id);
        }
        TestModels.Near(person.Rest["chest"] + Vector3.UnitY * .1f, kept.Rest["chest"], 1e-5f);
        variant.Overrides.Clear();
        // Moving the chest with its children needs no child override: the arm follows through the hierarchy.
        ModelAuthoring.MoveRest(person, variant, "chest", person.Rest["chest"] + new Vector3(0, .1f, 0), true);
        Assert.Single(variant.Overrides);
        var lifted = ResolvedModel.From(person.Base, variant);
        TestModels.Near(person.Rest["left-hand"] + new Vector3(0, .1f, 0), lifted.Rest["left-hand"], 1e-5f);
        // Flattening a variant writes the fitted bone setup, not a frozen rest.
        var tall = ResolvedModel.From(person.Base, catalog.Variants["tall-thin"]);
        var flat = ModelAuthoring.Flatten(tall, "flat-tall", "Flat tall");
        var shoulder = flat.Controls.Single(c => c.Id == "left-shoulder");
        Assert.Equal(RigNodeKinds.Bone, shoulder.Node);
        Assert.Equal(tall.Nodes["left-shoulder"].Length, shoulder.Length, 5);
        Assert.Equal(default, shoulder.Rest);
        foreach (var control in tall.Order)
        {
            TestModels.Near(tall.Rest[control.Id], ResolvedModel.From(flat).Rest[control.Id], 1e-5f, control.Id);
        }
    }

    [Fact]
    public void TheSessionEditsBonesAsOneUndoStepAndReportsRefusals()
    {
        var root = Path.Combine(Path.GetTempPath(), "bone-editing-" + Guid.NewGuid().ToString("N"));
        try
        {
            var session = new EditorSession(AuthoringWorkspace.Open(root));
            Assert.True(session.NewModel("arm", "Arm", "empty"), session.Message);
            var document = session.SubjectModel!;
            Assert.True(session.Edit(document, () =>
            {
                ModelAuthoring.AddBone(document.Asset, null, "upper");
                ModelAuthoring.AddBone(document.Asset, "upper", "lower");
                ModelAuthoring.AddBone(document.Asset, "lower", "hand");
                ModelAuthoring.AddPartToBone(document.Asset, RoundedRectangle2D.Builder.FromSize(.4f, .2f, .05f), "upper");
            }), session.Message);
            Assert.All(document.Asset.Controls, c => Assert.Equal(RigNodeKinds.Bone, c.Node));
            session.SetMode(Workspace.Model);
            var before = document.Serialize();
            session.BeginDrag();
            session.DragControl("upper", new(.3f, .2f, 0));
            session.DragControl("upper", new(.5f, .4f, 0));
            session.EndDrag();
            TestModels.Near(new(.5f, .4f, 0), session.Assets.Resolve("arm")!.Rest["upper"]);
            session.Undo();
            Assert.Equal(before, document.Serialize());
            session.DragControl("lower", new(2, 2, 0));
            Assert.True(session.MessageIsError, "a bound origin refuses the drag");
            Assert.Contains("bound to 'upper'", session.Message);
            Assert.True(session.Save(document), session.Message);
            var reopened = AuthoredCatalog.Load(root).Resolve("arm");
            TestModels.Near(new(1, 0, 0), reopened.Rest["hand"]);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private static Vector3 XYZ(Vector2 xy, float z) => new(xy, z);
}
