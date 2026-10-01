using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Editing;

namespace App2d.Tests.Authored;

public sealed class AppearanceAuthoringTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "appearance-" + Guid.NewGuid().ToString("N"));
    public AppearanceAuthoringTests()
    {
        foreach (var file in Directory.EnumerateFiles(TestModels.AuthoredRoot, "*.json", SearchOption.AllDirectories))
        {
            var target = Path.Combine(_root, Path.GetRelativePath(TestModels.AuthoredRoot, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file, target);
        }
    }
    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public void NewHairEditsUndoEquipAndReopenInThePlayerLoadout()
    {
        var s = new EditorSession(AuthoringWorkspace.Open(_root)); s.Open("hero");
        Assert.True(s.NewAppearance("custom-hair", "Custom hair", "hair"), s.Message);
        Assert.Equal(Workspace.Appearance, s.Mode);
        var doc = s.AppearanceDocument!; var before = doc.Asset.ToJson();
        var points = doc.Asset.Solids[1].Outline!.Select(p => p with { Y = p.Y + .05f }).ToArray();
        s.BeginDrag(); s.Change(doc, () => AppearanceAuthoring.Cutout(doc.Asset, 1, points, .02f)); s.EndDrag();
        s.Undo(); Assert.Equal(before, doc.Asset.ToJson()); s.Redo();
        Assert.Equal(.02f, doc.Asset.Solids[1].Thickness);
        Assert.True(s.EquipAppearance("hero", doc.Id, PersonWardrobe.HeadSocket), s.Message);
        s.Undo(); Assert.DoesNotContain(s.EntityDocument!.Asset.Equipment, e => e.Prop == doc.Id);
        s.Redo(); Assert.Contains(s.EntityDocument!.Asset.Equipment, e => e.Prop == doc.Id);
        s.Edit(s.EntityDocument, () => s.EntityDocument.Asset.Equipment.RemoveAll(e => e.Prop == PersonWardrobe.ShortHair));
        s.SaveAll(); Assert.False(s.MessageIsError, s.Message);
        Assert.DoesNotContain("\"vertices\"", File.ReadAllText(Path.Combine(_root, "props", "custom-hair.json")));
        var catalog = AuthoredCatalog.Load(_root); Assert.Empty(catalog.Errors);
        var clip = catalog.Animations["player-idle"];
        var equipped = PersonLoadout.Dressed(clip, 0, PersonGear.None, catalog.Entities["hero"]).ToArray();
        Assert.Equal(("custom-hair", PersonWardrobe.HeadSocket), Assert.Single(equipped));
        Assert.NotNull(catalog.Props[doc.Id].Solids[1].Outline);
        Assert.Equal(.02f, catalog.Props[doc.Id].Solids[1].Thickness);
    }

    [Fact]
    public void DuplicateKeepsIndependentGeometryAndBadOutlineLeavesItIntact()
    {
        var original = AppearanceAuthoring.New("original", "Original", "skirt", "#222b32", .045f);
        var copy = AppearanceAuthoring.Duplicate(original, "copy", "Copy");
        copy.Solids[0].Outline![0] = new(4, 4);
        Assert.NotEqual(original.Solids[0].Outline![0], copy.Solids[0].Outline![0]);
        var before = original.ToJson();
        Assert.Throws<InvalidDataException>(() => AppearanceAuthoring.Cutout(original, 0, [new(0, 0), new(1, 0), new(2, 0)], .02f));
        Assert.Equal(before, original.ToJson());
    }

    [Fact]
    public void HairOutlineJsonRebuildsItsMeshAndKeepsMeshOnlyProps()
    {
        var hair = PersonWardrobe.Props().Single(p => p.Id == PersonWardrobe.ShortHair);
        var json = hair.ToJson();
        Assert.Contains("\"outline\"", json);
        Assert.DoesNotContain("\"vertices\"", json);
        Assert.DoesNotContain("\"triangles\"", json);

        var loaded = PropAsset.FromJson(json);
        Assert.Equal(json, loaded.ToJson());
        for (var i = 0; i < hair.Solids.Count; i++)
        {
            Assert.Equal(hair.Solids[i].Vertices.ToArray(), loaded.Solids[i].Vertices.ToArray());
            Assert.Equal(hair.Solids[i].Triangles.ToArray(), loaded.Solids[i].Triangles.ToArray());
        }

        var meshOnly = new PropAsset { Id = "mesh-only", Name = "Mesh only", Solids = [PropGeometry.Blade(0, .5f, 1, .1f, .02f, "#aaaaaa")] };
        Assert.Contains("\"vertices\"", meshOnly.ToJson());
        Assert.Contains("\"triangles\"", meshOnly.ToJson());
        PropAsset.FromJson(meshOnly.ToJson());
    }

    [Fact]
    public void EditedHairJsonSurvivesWardrobeSeedingAndSuppliesNewHairTemplate()
    {
        var path = Path.Combine(_root, "props", PersonWardrobe.ShortHair + ".json");
        var original = File.ReadAllText(path);
        Assert.DoesNotContain("\"vertices\"", original);
        var edited = original.Replace("#654334", "#123456", StringComparison.Ordinal);
        Assert.NotEqual(original, edited);
        File.WriteAllText(path, edited);

        PersonWardrobe.Write(_root);
        Assert.Equal(edited, File.ReadAllText(path));

        var session = new EditorSession(AuthoringWorkspace.Open(_root));
        session.Open("hero");
        Assert.True(session.NewAppearance("artist-hair", "Artist hair", "hair"), session.Message);
        Assert.Equal("#123456", session.AppearanceDocument!.Asset.Solids[0].Fill);

        PersonWardrobe.Write(_root, replaceExistingProps: true);
        Assert.Equal("#654334", PropAsset.FromJson(File.ReadAllText(path)).Solids[0].Fill);
    }
}
