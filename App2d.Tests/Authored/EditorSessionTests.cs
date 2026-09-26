using System.Numerics;
using App2d.Core.Characters;
using App2d.Core.Characters.Editing;

namespace App2d.Tests.Authored;

/// <summary>The phase-two acceptance paths, driven through the same session the editor's views use.</summary>
public sealed class EditorSessionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"editor-{Guid.NewGuid():N}");

    public EditorSessionTests() => PersonTemplate.WriteStudies(_root);
    public void Dispose() => Directory.Delete(_root, true);

    private EditorSession Session()
    {
        var workspace = AuthoringWorkspace.Open(_root);
        Assert.Empty(workspace.LoadErrors);
        return new(workspace);
    }

    private static void Ok(EditorSession session, bool result) => Assert.True(result, session.Message);

    [Fact]
    public void ATestSnapshotPlaysUnsavedDraftsAndIgnoresLaterEdits()
    {
        var session = Session();
        var thrust = session.Assets.Clip("person-thrust")!;
        Ok(session, session.Edit(thrust, () => thrust.Asset.Markers.Single(m => m.Id == "strike").Time = .3f));
        var (entities, problems) = session.Assets.SnapshotEntities();
        Assert.Empty(problems);
        var guard = entities.Single(e => e.Id == "spear-guard");
        Assert.Equal(.3f, guard.Actions["attack"].Hits[0].Start); // the unsaved marker move reaches the playtest
        Ok(session, session.Edit(thrust, () => thrust.Asset.Markers.Single(m => m.Id == "strike").Time = .35f));
        Assert.Equal(.3f, guard.Actions["attack"].Clip.Markers.Single(m => m.Id == "strike").Time); // a running test never sees later edits
        Assert.Equal(.4f, AuthoredCatalog.Load(_root).Animations["person-thrust"].Markers.Single(m => m.Id == "strike").Time);

        Ok(session, session.Edit(thrust, () => thrust.Asset.Markers.RemoveAll(m => m.Id == "recover")));
        (entities, problems) = session.Assets.SnapshotEntities();
        Assert.DoesNotContain(entities, e => e.Id == "spear-guard");
        Assert.Contains(problems, p => p.Contains("no marker 'recover'"));
    }

    [Fact]
    public void ANamedTallVariantSavesAndReopens()
    {
        var session = Session();
        Ok(session, session.NewVariant("tall-guard", "Tall guard", "person", "tall-thin"));
        Assert.True(session.SubjectVariant!.IsNew);
        Ok(session, session.Save(session.SubjectVariant));
        Assert.False(session.SubjectVariant.Dirty);

        var reopened = Session();
        var variant = reopened.Assets.Variant("tall-guard")!;
        Assert.Equal("Tall guard", variant.Asset.Name);
        Assert.Equal(PersonBuild.TallThin.Legs, variant.Asset.Build["legs"]);
        Assert.Empty(reopened.Assets.Problems(variant));
    }

    [Fact]
    public void ChangingTheBuildWhileWalkingKeepsPlayingAndReshapesTheBody()
    {
        var session = Session();
        Ok(session, session.NewVariant("tall-guard", "Tall guard", "person", "tall-thin"));
        session.SetClip("person-walk"); session.TogglePlay(); session.Tick(.3f);
        var before = session.Evaluate("tall-guard")!;
        Assert.NotNull(before.Clip);
        var variant = session.SubjectVariant!;
        for (var frame = 0; frame < 5; frame++) { session.Change(variant, () => variant.Asset.Build["legs"] = 1.2f + .04f * (frame + 1)); session.Tick(1 / 60f); }
        session.CommitAll();
        Assert.True(session.Transport.Playing);
        var after = session.Evaluate("tall-guard")!;
        Assert.True(after.Model.Rest["hips"].Y > before.Model.Rest["hips"].Y + .05f);
        Assert.All(after.Pose.Chains, c => Assert.True(c.Reached, c.Chain));
        variant.Undo();
        Assert.Equal(1.2f, variant.Asset.Build["legs"], 4);
    }

    [Fact]
    public void ADragOnASharedClipIsOneUndoStepAndReachesEveryDependentPreview()
    {
        var session = Session();
        session.Open("person-walk"); Assert.Equal(Workspace.Animate, session.Mode);
        session.Pin("tall-thin"); session.Pin("short-broad");
        session.Seek(.2f);
        var before = session.Scene().Select(s => s.Pose.World("chest")).ToArray();
        Assert.Equal(3, before.Length);
        session.BeginDrag();
        for (var frame = 1; frame <= 6; frame++) session.DragControl("chest", before[0] + new Vector3(0, .02f * frame, 0));
        session.EndDrag();
        var after = session.Scene().Select(s => s.Pose.World("chest")).ToArray();
        Assert.Equal(before[0].Y + .12f, after[0].Y, 3);
        Assert.All(Enumerable.Range(1, 2), i => Assert.True(after[i].Y > before[i].Y + .05f, $"pin {i} should follow the shared clip"));
        var clip = session.ClipDocument!;
        Assert.True(clip.Dirty);
        session.Undo();
        Assert.False(clip.CanUndo);
        var undone = session.Scene().Select(s => s.Pose.World("chest")).ToArray();
        for (var i = 0; i < 3; i++) TestModels.Near(before[i], undone[i], 1e-5f, $"subject {i}");
    }

    [Fact]
    public void WithAutokeyOffADragIsHeldUntilKeyPose()
    {
        var session = Session();
        session.Open("person-walk"); session.AutoKey = false; session.Seek(.3f);
        var chest = session.Evaluate("person")!.Pose.World("chest");
        session.DragControl("chest", chest + new Vector3(0, .1f, 0)); session.EndDrag();
        Assert.True(session.HasPendingPose);
        Assert.False(session.ClipDocument!.Dirty);
        Assert.Equal(chest.Y + .1f, session.Evaluate("person")!.Pose.World("chest").Y, 3);
        session.KeyPose();
        Assert.False(session.HasPendingPose);
        Assert.True(session.ClipDocument.Dirty);
        Assert.Equal(chest.Y + .1f, session.Evaluate("person")!.Pose.World("chest").Y, 3);

        session.DragControl("chest", chest); session.EndDrag();
        session.Seek(.4f);
        Assert.False(session.HasPendingPose);
    }

    [Fact]
    public void AVariantNeverEditsItsBase()
    {
        var session = Session();
        session.Open("tall-thin"); session.EditRig = true;
        var basis = session.Assets.Model("person")!; var head = session.Evaluate("tall-thin")!.Model.Rest["head"];
        session.DragControl("head", head + new Vector3(0, .1f, 0)); session.EndDrag();
        Assert.False(basis.Dirty);
        Assert.Equal(head.Y + .1f, session.SubjectVariant!.Asset.Rest["head"].Y, 4);
        Assert.False(session.Edit(session.SubjectModel, () => { }));
    }

    [Fact]
    public void AHeadlessThreeLeggedModelIsBuiltAndAnimatedFromEmpty()
    {
        var session = Session();
        Ok(session, session.NewModel("tripod", "Tripod", "empty"));
        var model = session.SubjectModel!;
        Ok(session, session.Edit(model, () =>
        {
            ModelAuthoring.AddControl(model.Asset, null, new(0, .9f, 0), "body");
            for (var i = 0; i < 3; i++)
            {
                var x = (i - 1) * .35f;
                ModelAuthoring.AddControl(model.Asset, "body", new(x, .8f, 0), $"hip-{i}");
                ModelAuthoring.AddControl(model.Asset, $"hip-{i}", new(x + .15f, .4f, 0), $"knee-{i}");
                ModelAuthoring.AddControl(model.Asset, $"knee-{i}", new(x, 0, 0), $"foot-{i}");
                ModelAuthoring.AddChain(model.Asset, $"foot-{i}");
                ModelAuthoring.AddPart(model.Asset, "stroke", $"hip-{i}", $"knee-{i}"); ModelAuthoring.AddPart(model.Asset, "stroke", $"knee-{i}", $"foot-{i}");
            }
            var shell = ModelAuthoring.AddPart(model.Asset, "ellipse", "body"); shell.Width = .9f; shell.Height = .35f;
        }));
        Assert.Equal(3, model.Asset.Chains.Count);
        Assert.DoesNotContain(model.Asset.Parts, p => p.Face != "none");
        Ok(session, session.Save(model));

        Ok(session, session.NewClip("tripod-walk", "Tripod walk", 1));
        var clip = session.ClipDocument!;
        Ok(session, session.Edit(clip, () => clip.Asset.Travel.Keys = [new() { Time = 0 }, new() { Time = 1, X = .6f }]));
        foreach (var (time, lift) in new[] { (0f, 0f), (.25f, .12f), (.5f, 0f), (.75f, .12f), (1f, 0f) })
        {
            session.Seek(time);
            var pose = session.Evaluate("tripod")!.Pose;
            session.DragControl("body", pose.World("body") + new Vector3(0, lift * .3f, 0));
            session.DragControl("foot-1", pose.World("foot-1") + new Vector3(0, lift, 0));
            session.EndDrag();
        }
        session.Seek(0);
        Ok(session, session.Edit(clip, () => ClipAuthoring.Plant(session.Evaluate("tripod")!.Model, clip.Asset, session.Evaluate("tripod")!.Pose, "foot-0-chain", 0, .5f)));
        Assert.Empty(session.Assets.Problems(clip));
        Ok(session, session.Save(clip));

        var reopened = Session();
        var tripod = reopened.Assets.Resolve("tripod")!;
        var walk = reopened.Assets.Clip("tripod-walk")!.Asset; walk.Validate(tripod);
        var mid = PoseEvaluator.Sample(tripod, walk, .25);
        Assert.True(mid.World("foot-1").Y > .05f, "the middle foot lifts");
        Assert.All(mid.Chains, c => Assert.True(c.Reached, c.Chain));
        Assert.Single(mid.Contacts);
    }

    [Fact]
    public void AStructuralBaseEditMovesCompatibleClipsToTheNewRevisionOnSave()
    {
        var session = Session();
        session.Open("person");
        var person = session.SubjectModel!;
        Ok(session, session.Edit(person, () => ModelAuthoring.AddControl(person.Asset, "head", new(0, 2.3f, 0), "hat")));
        Assert.True(session.Assets.StructureChanged(person));
        Assert.All(session.Assets.Dependents("person").Clips, c => Assert.Empty(session.Assets.Problems(c)));
        Ok(session, session.Save(person));
        Assert.Equal(2, person.Asset.StructureRevision);
        Assert.All(session.Assets.Dependents("person").Clips, c => { Assert.Equal(2, c.Asset.StructureRevision); Assert.True(c.Dirty); });
        session.SaveAll();
        var reopened = Session();
        Assert.All(reopened.Assets.Documents, d => Assert.Empty(reopened.Assets.Problems(d)));
    }

    [Fact]
    public void RemovingStructureThatAClipKeysLeavesTheClipReportingIt()
    {
        var session = Session();
        session.Open("person");
        var person = session.SubjectModel!;
        Assert.False(session.Edit(person, () => ModelAuthoring.RemoveControl(person.Asset, "chest")), "a control with children is refused");
        Ok(session, session.Edit(person, () => ModelAuthoring.RemoveChain(person.Asset, "left-arm")));
        var walk = session.Assets.Clip("person-walk")!;
        Assert.Contains(session.Assets.Problems(walk), p => p.Contains("left-arm"));
        session.Save(person);
        Assert.Equal(1, walk.Asset.StructureRevision);
    }

    [Fact]
    public void ADiscardedAssetsCachedResultsNeverReachANewAssetWithTheSameId()
    {
        var session = Session();
        Ok(session, session.NewModel("trial", "Trial", "person"));
        Assert.NotNull(session.Assets.Resolve("trial"));
        var revision = session.Assets.Revision;
        Ok(session, session.Discard("trial"));
        Assert.True(session.Assets.Revision > revision, "discarding must never repeat an earlier revision");
        Ok(session, session.NewModel("trial", "Trial", "empty"));
        var resolved = session.Assets.Resolve("trial");
        Assert.True(resolved is null || ReferenceEquals(resolved.Base, session.Assets.Model("trial")!.Asset), "resolved from the discarded model's cache");
    }

    [Fact]
    public void ANewAssetNeverOverwritesAFileThatDidNotLoad()
    {
        var path = Path.Combine(_root, "models", "broken.json");
        File.WriteAllText(path, "{ not json");
        var session = new EditorSession(AuthoringWorkspace.Open(_root));
        Assert.NotEmpty(session.Assets.LoadErrors);
        Assert.True(session.Assets.Exists("broken"));
        Assert.NotEqual("broken", session.Assets.SuggestId("broken"));
        Assert.False(session.NewModel("broken", "Broken", "empty"));
        Assert.Equal("{ not json", File.ReadAllText(path));
    }

    [Fact]
    public void ShorteningTheOpenClipBringsTheTimeBackInsideIt()
    {
        var session = Session();
        session.Open("person-walk");
        var clip = session.ClipDocument!; var duration = clip.Asset.Duration;
        session.Seek(duration * .9f);
        session.Change(clip, () => ClipAuthoring.Retime(clip.Asset, duration / 2));
        session.CommitAll();
        Assert.True(session.Transport.Time <= clip.Asset.Duration);
        session.DragControl("chest", session.Evaluate("person")!.Pose.World("chest") + new Vector3(0, .05f, 0)); session.EndDrag();
        Assert.Empty(session.Assets.Problems(clip));
    }

    [Fact]
    public void UndoAfterSavingALookRevertsTheBaseThenTheVariant()
    {
        var session = Session();
        session.Open("tall-thin");
        var variant = session.SubjectVariant!; var basis = session.Assets.Model(variant.Asset.Base)!;
        Ok(session, session.ApplyLook("sage"));
        Ok(session, session.SaveLook("mine", "Mine"));
        Assert.Contains(basis.Asset.Looks, l => l.Id == "mine");
        session.Undo();
        Assert.DoesNotContain(basis.Asset.Looks, l => l.Id == "mine");
        Assert.True(variant.CanUndo, "the variant's look is still applied");
        session.Undo();
        Assert.False(variant.Dirty);
        session.Redo();
        Assert.True(variant.Dirty);
        Assert.DoesNotContain(basis.Asset.Looks, l => l.Id == "mine");
    }
}
