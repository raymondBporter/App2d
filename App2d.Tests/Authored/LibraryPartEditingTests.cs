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
    public void ReturningToAppearanceKeepsTheLibraryPartAsTheOnlyPreviewSelection()
    {
        var session = Session();
        session.Open("quadruped");
        session.Open("person-lower");
        session.SetMode(Workspace.Model);
        session.SetMode(Workspace.Appearance);

        Assert.Equal("person-lower", session.ActiveDocument!.Id);
        Assert.Equal("person-lower", session.PartDocument!.Id);
        Assert.Empty(session.PreviewProp);
        var wearer = session.Assets.Resolve("quadruped")!;
        Assert.False(session.PreviewPartOnWearer);
        Assert.Null(session.PreviewAttachmentOn(wearer));
        Assert.Equal("quadruped", session.SubjectId);
        var standalone = Assert.Single(session.Scene());
        Assert.Equal("person-lower", Assert.Single(standalone.Model.Attachments).Asset.Id);
        Assert.Empty(wearer.Attachments);
        Assert.Equal("person-lower", session.TransportClip!.Model);
        Assert.Same(standalone.Model, Assert.Single(session.Scene()).Model);
        Assert.Equal(session.Assets.Root, standalone.Model.TextureRoot);
        session.PreviewPartOnWearer = true;
        Assert.Equal("person-lower", session.PreviewAttachmentOn(wearer)!.Asset.Id);
        Assert.Equal("quadruped", Assert.Single(session.Scene()).Id);
        session.SetMode(Workspace.Model);
        session.SetMode(Workspace.Appearance);
        Assert.True(session.PreviewPartOnWearer);
        session.SaveAll();
        Assert.False(session.MessageIsError, session.Message);
        Assert.Empty(AuthoringWorkspace.Open(_root).Resolve("quadruped")!.Attachments);
        Assert.Null(session.WeaponFor(wearer));
        Assert.Empty(session.Assets.DirtyDocuments);
    }

    [Fact]
    public void OpeningAppearanceSelectsAndPreviewsAnOrdinaryPart()
    {
        var session = Session();
        session.Open("person");
        session.SetMode(Workspace.Appearance);

        var appearance = session.PartDocument!;
        Assert.NotNull(appearance);
        Assert.Same(appearance, session.ActiveDocument);
        var wearer = session.Assets.Resolve("person")!;
        var preview = session.PreviewAttachmentOn(wearer);
        Assert.NotNull(preview);
        Assert.Equal(appearance.Id, preview!.Asset.Id);
        Assert.Equal(session.PreviewSocket, preview!.Socket.Id);
        Assert.Null(session.WeaponFor(wearer));
        Assert.Empty(session.Assets.DirtyDocuments);
    }

    [Fact]
    public void OpeningABeardReplacesTheLibraryModulePreviewWithoutChangingTheWearer()
    {
        var session = Session();
        session.Open("quadruped");
        session.Open("quadruped-lower");
        session.Open("brute-beard");

        Assert.Equal("brute-beard", session.ActiveDocument!.Id);
        var wearer = session.Assets.Resolve("quadruped")!;
        Assert.Equal("brute-beard", session.PreviewAttachmentOn(wearer)!.Asset.Id);
        Assert.Null(session.WeaponFor(wearer));
        Assert.Empty(wearer.Attachments);
        Assert.Empty(session.Assets.DirtyDocuments);
    }

    [Fact]
    public void TheShippedLibraryLoadsAndANewPartOpensInAppearanceWithAPreviewOnTheWearer()
    {
        var session = Session();
        Assert.Equal(["boot", "face-person", "person-lower", "person-upper", "quadruped-head", "quadruped-lower", "sandal"], session.Assets.Parts.Where(p => p.Asset.Behavior is null && p.Asset.Usage is not ("prop" or "hair" or "clothing" or "world")).Select(p => p.Id).Order(StringComparer.Ordinal));
        session.Open("person");
        Assert.True(session.NewPart("badge", "Badge"), session.Message);
        Assert.Equal(Workspace.Appearance, session.Mode);
        Assert.Same(session.PartDocument, session.ActiveDocument);
        Assert.Equal("person", session.SubjectId);
        Assert.True(session.PartDocument!.IsNew);
        Assert.Single(session.PartDocument.Asset.Geometry);
        session.PreviewSocket = Humanoid.HeadSocket;
        var preview = session.PreviewAttachmentOn(session.Assets.Resolve("person")!);
        Assert.NotNull(preview);
        Assert.Equal(Humanoid.HeadSocket, preview!.Socket.Id);
        Assert.Equal("badge", preview.Asset.Id);
        session.PreviewMirror = true;
        Assert.True(session.PreviewAttachmentOn(session.Assets.Resolve("person")!)!.Instance.Mirror);
        // Converting a prop keeps its pieces; the prop itself stays for the entities that equip it.
        Assert.True(session.DuplicatePart(PersonLoadout.ShortHair, "tousle", "Tousle"), session.Message);
        Assert.Equal(session.Assets.Part(PersonLoadout.ShortHair)!.Asset.Geometry.Count, session.PartDocument!.Asset.Geometry.Count);
        Assert.NotNull(session.Assets.Part(PersonLoadout.ShortHair));
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
        // The Person already wears its face instance (P7c); the boot is the second instance.
        var person = session.Assets.Model("person")!;
        Assert.Equal(2, person.Asset.Attachments.Count);
        Assert.Equal(person.Asset.Attachments[^1].Id, session.Selection.Attachment);
        Assert.True(person.Dirty);
        Assert.False(session.Assets.Variant("tall-thin")!.Dirty);
        Assert.Null(session.PreviewAttachmentOn(session.Assets.Resolve("tall-thin")!));
        Assert.Equal(2, session.Assets.Resolve("short-broad")!.Attachments.Count);
        Assert.True(session.AttachPart("boot", Humanoid.RightFootSocket, mirror: true), session.Message);
        Assert.Equal(3, person.Asset.Attachments.Count);
        Assert.True(person.Asset.Attachments[^1].Mirror);
        // Undo from the part editor reverts the attach on the base, newest first.
        Assert.True(session.CanUndo);
        session.Undo();
        Assert.Equal(2, person.Asset.Attachments.Count);
        session.Undo();
        Assert.Single(person.Asset.Attachments);
        Assert.False(session.AttachPart("boot", "nowhere"));
        Assert.Contains("no socket 'nowhere'", session.Message);
        Assert.Single(person.Asset.Attachments);
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
        var fill = session.Assets.Resolve("cinder")!.Attachment(instance)!.Parts[0].RenderMaterial.Fill;
        Assert.True(session.Edit(sandal, () => { var solid = sandal.Asset.Geometry[0]; solid.Material = solid.RenderMaterial with { Fill = "#102030" }; sandal.Asset.Validate(); }), session.Message);
        Assert.Equal("#102030", session.Assets.Resolve("cinder")!.Attachment(instance)!.Parts[0].RenderMaterial.Fill);
        Assert.Equal("#102030", session.Assets.CompileEntity("cinder-gunner")!.Model.Attachment(instance)!.Parts[0].RenderMaterial.Fill);
        Assert.Equal(["brute", "cinder", "club-caveman-build", "person", "rock-thrower-build", "shield-defender-build", "short-broad", "tall-thin"], session.Assets.Wearers("sandal").Order(StringComparer.Ordinal));
        session.Undo();
        Assert.Equal(fill, session.Assets.Resolve("cinder")!.Attachment(instance)!.Parts[0].RenderMaterial.Fill);
        session.Redo();
        session.SaveAll();
        Assert.False(session.MessageIsError, session.Message);
        var reopened = AuthoringWorkspace.Open(_root);
        Assert.Empty(reopened.LoadErrors);
        Assert.Equal("#102030", reopened.Part("sandal")!.Asset.Geometry[0].RenderMaterial.Fill);
        Assert.Equal("#102030", reopened.Resolve("brute")!.Attachment(instance)!.Parts[0].RenderMaterial.Fill);
        Assert.Contains("\"attachments\"", File.ReadAllText(Path.Combine(_root, "models", "person.json")));
        Assert.Equal(CharacterModel.CurrentVersion, reopened.Model("person")!.Asset.Version);
    }
}
