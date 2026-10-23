using App2d.CharacterSeeds;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Editing;

namespace App2d.Tests.Authored;

/// <summary>P5c of the character-system migration: the session's library-part commands and their undo, preview and save behavior.</summary>
public sealed class LibraryPartEditingTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "library-parts-" + Guid.NewGuid().ToString("N"));

    public LibraryPartEditingTests()
    {
        foreach (var file in Directory.EnumerateFiles(TestModels.AuthoredRoot, "*.json", SearchOption.AllDirectories))
        {
            var target = Path.Combine(_root, Path.GetRelativePath(TestModels.AuthoredRoot, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    public void Dispose() => Directory.Delete(_root, true);

    private EditorSession Session() => new(AuthoringWorkspace.Open(_root)) { SeedAppearance = id => PersonWardrobe.Props().FirstOrDefault(p => p.Id == id) };

    [Fact]
    public void TheShippedLibraryLoadsAndANewPartOpensInAppearanceWithAPreviewOnTheWearer()
    {
        var session = Session();
        Assert.Equal(["boot", "sandal"], session.Assets.Parts.Select(p => p.Id).Order(StringComparer.Ordinal));
        session.Open("person");
        Assert.True(session.NewPart("badge", "Badge"), session.Message);
        Assert.Equal(Workspace.Appearance, session.Mode);
        Assert.Same(session.PartDocument, session.ActiveDocument);
        Assert.Equal("person", session.SubjectId);
        Assert.True(session.PartDocument!.IsNew);
        Assert.Single(session.PartDocument.Asset.Parts);
        session.PreviewSocket = Humanoid.HeadSocket;
        var preview = session.PreviewAttachmentOn(session.Assets.Resolve("person")!);
        Assert.NotNull(preview);
        Assert.Equal(Humanoid.HeadSocket, preview!.Socket.Id);
        Assert.Equal("badge", preview.Asset.Id);
        session.PreviewMirror = true;
        Assert.True(session.PreviewAttachmentOn(session.Assets.Resolve("person")!)!.Instance.Mirror);
        // Converting a prop keeps its pieces; the prop itself stays for the entities that equip it.
        Assert.True(session.NewPart("tousle", "Tousle", PersonLoadout.ShortHair), session.Message);
        Assert.Equal(session.Assets.Prop(PersonLoadout.ShortHair)!.Asset.Solids.Count, session.PartDocument!.Asset.Solids.Count);
        Assert.NotNull(session.Assets.Prop(PersonLoadout.ShortHair));
        Assert.True(session.DuplicatePart("tousle", "tousle-2", "Tousle copy"), session.Message);
        Assert.Equal("tousle-2", session.PartDocument!.Id);
        Assert.False(session.NewPart("boot", "Boot again"));
        Assert.Contains("already used", session.Message);
    }

    [Fact]
    public void AttachingFromTheAppearanceEditorEditsTheWearersBaseAndUndoesThereInOrder()
    {
        var session = Session();
        session.Open("tall-thin");
        session.Open("boot");
        Assert.Equal("tall-thin", session.SubjectId);
        Assert.True(session.AttachPart("boot", Humanoid.LeftFootSocket), session.Message);
        Assert.Contains("base 'person'", session.Message);
        var person = session.Assets.Model("person")!;
        Assert.Single(person.Asset.Attachments);
        Assert.Equal(person.Asset.Attachments[0].Id, session.Selection.Attachment);
        Assert.True(person.Dirty);
        Assert.False(session.Assets.Variant("tall-thin")!.Dirty);
        Assert.Null(session.PreviewAttachmentOn(session.Assets.Resolve("tall-thin")!));
        Assert.Single(session.Assets.Resolve("short-broad")!.Attachments);
        Assert.True(session.AttachPart("boot", Humanoid.RightFootSocket, mirror: true), session.Message);
        Assert.Equal(2, person.Asset.Attachments.Count);
        Assert.True(person.Asset.Attachments[1].Mirror);
        // Undo from the part editor reverts the attach on the base, newest first.
        Assert.True(session.CanUndo);
        session.Undo();
        Assert.Single(person.Asset.Attachments);
        session.Undo();
        Assert.Empty(person.Asset.Attachments);
        Assert.False(session.AttachPart("boot", "nowhere"));
        Assert.Contains("no socket 'nowhere'", session.Message);
        Assert.Empty(person.Asset.Attachments);
    }

    [Fact]
    public void ASharedEditReachesEveryWearerAndSaveAllWritesThePartWithThem()
    {
        var session = Session();
        session.Open("person");
        Assert.True(session.AttachPart("sandal", Humanoid.LeftFootSocket), session.Message);
        var instance = session.Selection.Attachment!;
        session.Open("sandal");
        var sandal = session.PartDocument!;
        var fill = session.Assets.Resolve("cinder")!.Attachment(instance)!.Solids[0].RenderMaterial.Fill;
        Assert.True(session.Edit(sandal, () => { var solid = sandal.Asset.Solids[0]; solid.Material = solid.RenderMaterial with { Fill = "#102030" }; sandal.Asset.Validate(); }), session.Message);
        Assert.Equal("#102030", session.Assets.Resolve("cinder")!.Attachment(instance)!.Solids[0].RenderMaterial.Fill);
        Assert.Equal("#102030", session.Assets.CompileEntity("cinder-gunner")!.Model.Attachment(instance)!.Solids[0].RenderMaterial.Fill);
        Assert.Equal(["brute", "cinder", "club-caveman-build", "person", "rock-thrower-build", "shield-defender-build", "short-broad", "tall-thin"], session.Assets.Wearers("sandal").Order(StringComparer.Ordinal));
        session.Undo();
        Assert.Equal(fill, session.Assets.Resolve("cinder")!.Attachment(instance)!.Solids[0].RenderMaterial.Fill);
        session.Redo();
        session.SaveAll();
        Assert.False(session.MessageIsError, session.Message);
        var reopened = AuthoringWorkspace.Open(_root);
        Assert.Empty(reopened.LoadErrors);
        Assert.Equal("#102030", reopened.Part("sandal")!.Asset.Solids[0].RenderMaterial.Fill);
        Assert.Equal("#102030", reopened.Resolve("brute")!.Attachment(instance)!.Solids[0].RenderMaterial.Fill);
        Assert.Contains("\"attachments\"", File.ReadAllText(Path.Combine(_root, "models", "person.json")));
        Assert.Equal(CharacterModel.CurrentVersion, reopened.Model("person")!.Asset.Version);
    }
}
