using App2d.CharacterStudio.Authoring;
using App2d.CharacterStudio.Editing;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Tests.Authored;

/// <summary>Scratch-only manual authoring paths: no finished content is written to the shipped library.</summary>
public sealed class PrimitiveLibraryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "primitive-library-" + Guid.NewGuid().ToString("N"));
    public PrimitiveLibraryTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    private EditorSession Session() => new(AuthoringWorkspace.Open(_root));

    private static void BuildPiece(EditorSession session)
    {
        Assert.True(session.NewGeometry("outline", "Test outline", ShapeKinds2D.SimplePolygon), session.Message);
        Assert.True(session.NewRenderMaterial("surface", "Test surface"), session.Message);
        Assert.True(session.NewPiece("badge", "Test piece"), session.Message);
        var piece = session.Assets.Piece("badge")!;
        Assert.True(session.Edit(piece, () =>
        {
            piece.Asset.Geometry = "outline";
            piece.Asset.Material = new() { Asset = "surface" };
            piece.Asset.Groups = ["Clothes/Armor", "Test"];
            piece.Asset.Attachment = "mount";
        }), session.Message);
    }

    private static void AddWearer(EditorSession session)
    {
        session.Assets.Create(new CharacterModel
        {
            Id = "wearer", Name = "Test wearer",
            Controls = [new() { Id = "root" }, new() { Id = "tip", Rest = new(0, 1, 0) }],
            Sockets = [new() { Id = "mount", Control = "root", Toward = "tip" }],
        });
        session.Open("wearer");
        session.Open("badge");
    }

    [Fact]
    public void GeometryMaterialAndPieceSaveIndependentlyAndAttachWithoutAPartFile()
    {
        var session = Session();
        BuildPiece(session);
        AddWearer(session);
        Assert.Equal(Workspace.Library, session.Mode);
        Assert.False(session.PreviewPartOnWearer);
        Assert.Equal("badge", session.ActiveDocument!.Id);
        Assert.True(session.AttachPart("badge", "mount"), session.Message);
        var resolved = session.Assets.Resolve("wearer")!;
        Assert.Equal("badge", Assert.Single(resolved.Attachments).Asset.Id);
        Assert.Equal("#b8bdc5", resolved.Attachments[0].Parts[0].RenderMaterial.Fill);
        session.SaveAll();
        Assert.False(session.MessageIsError, session.Message);
        Assert.False(Directory.Exists(Path.Combine(_root, "parts")));
        var pieceJson = File.ReadAllText(Path.Combine(_root, "pieces", "badge.json"));
        Assert.Contains("\"geometry\": \"outline\"", pieceJson);
        Assert.Contains("\"asset\": \"surface\"", pieceJson);
        Assert.DoesNotContain("vertices", pieceJson);
        var workspace = AuthoringWorkspace.Open(_root);
        Assert.Empty(workspace.LoadErrors);
        Assert.Empty(workspace.Problems(workspace.Piece("badge")!));
        Assert.Equal(["Clothes/Armor", "Test"], workspace.Piece("badge")!.Asset.Groups);
        Assert.Single(workspace.Resolve("wearer")!.Attachments);
        var runtime = AuthoredCatalog.Load(_root);
        Assert.Empty(runtime.Errors);
        Assert.Single(runtime.Resolve("wearer").Attachments);
        Assert.True(runtime.Parts.ContainsKey("badge"));
        Assert.Equal("mount", runtime.Pieces["badge"].Attachment);
    }

    [Fact]
    public void SharedEditsInvalidateWearerAndUndoRestoresGeometryAndMaterial()
    {
        var session = Session(); BuildPiece(session); AddWearer(session);
        Assert.True(session.AttachPart("badge", "mount"), session.Message);
        var before = session.Assets.Resolve("wearer")!;
        session.Open("outline");
        var doc = session.Assets.Geometry("outline")!;
        session.Edit(doc, () => doc.Replace(GeometryAssetEditing.TransformPoints(doc.Asset, Matrix3x2.CreateScale(2))));
        var after = session.Assets.Resolve("wearer")!;
        Assert.NotSame(before, after);
        var originalPoints = GeometryAssetEditing.Points(new() { Geometry = before.Attachments[0].Parts[0].Geometry });
        Assert.NotEqual(originalPoints[0], GeometryAssetEditing.Points(doc.Asset)[0]);
        session.Undo();
        Assert.Equal(originalPoints, GeometryAssetEditing.Points(doc.Asset));
        session.Open("surface");
        var material = session.Assets.RenderMaterial("surface")!;
        session.Edit(material, () => material.Asset.Material = material.Asset.Material with { Fill = "#102030" });
        Assert.Equal("#102030", session.Assets.Resolve("wearer")!.Attachments[0].Parts[0].RenderMaterial.Fill);
        session.Undo();
        Assert.Equal("#b8bdc5", session.Assets.Resolve("wearer")!.Attachments[0].Parts[0].RenderMaterial.Fill);
        Assert.Contains(session.Assets.UsedBy("outline"), r => r.Id == "badge" && r.Relation == "geometry");
        Assert.Contains(session.Assets.UsedBy("surface"), r => r.Id == "badge" && r.Relation == "render material");
    }

    [Fact]
    public void PolygonPointsCanBeInsertedDraggedRemovedAndTransformedWithoutChangingOriginal()
    {
        var geometry = new GeometryAsset2D { Id = "polygon", Name = "Polygon", Geometry = GeometryJson2D.CreateDefault(ShapeKinds2D.SimplePolygon) };
        var edited = GeometryAssetEditing.InsertAfter(geometry, 0);
        edited = GeometryAssetEditing.InsertAfter(edited, 2);
        Assert.Equal(6, GeometryAssetEditing.Points(edited).Count);
        Assert.Equal(4, GeometryAssetEditing.Points(geometry).Count);
        var points = GeometryAssetEditing.Points(edited).ToArray();
        points[1] += new Vector2(0, -.1f);
        edited = GeometryAssetEditing.WithPoints(edited, points);
        edited.Validate();
        var rotated = GeometryAssetEditing.TransformPoints(edited, Matrix3x2.CreateRotation(MathF.PI / 2));
        rotated.Validate();
        Assert.Equal(-points[0].Y, GeometryAssetEditing.Points(rotated)[0].X, 5);
        Assert.Equal(5, GeometryAssetEditing.Points(GeometryAssetEditing.RemovePoint(edited, 1)).Count);
        var invalid = GeometryAssetEditing.WithPoints(geometry, [new(0, 0), new(1, 1), new(0, 1), new(1, 0)]);
        Assert.ThrowsAny<Exception>(invalid.Validate);
        geometry.Validate();
    }

    [Theory]
    [MemberData(nameof(DrawableKinds))]
    public void EveryOfferedGeometryKindValidatesAndRoundTrips(string kind)
    {
        var asset = new GeometryAsset2D { Id = "geometry", Name = "Geometry", Geometry = GeometryJson2D.CreateDefault(kind) };
        asset.Validate();
        var copy = GeometryAsset2D.FromJson(asset.ToJson());
        Assert.Equal(kind, copy.Kind);
        var points = GeometryAssetEditing.Points(copy);
        if (points.Count > 0) GeometryAssetEditing.WithPoints(copy, points).Validate();
    }
    public static IEnumerable<object[]> DrawableKinds() => GeometryJson2D.DrawableKinds.Select(k => new object[] { k });

    [Fact]
    public void MeshOutlineEditsRoundTripAndRebuildWithoutMutatingTheLibraryMesh()
    {
        var mesh = PartMesh.Extrude([new(0, 0), new(1, 0), new(1, 1), new(0, 1)], .1f);
        var asset = new GeometryAsset2D { Id = "mesh", Name = "Mesh", Mesh = mesh };
        var edited = GeometryAssetEditing.InsertAfter(asset, 0);
        edited.Validate();
        var entry = GeometryAsset2D.FromJson(edited.ToJson()).Entry(RenderMaterialDefinition2D.Ink);
        entry.Validate(id => true);
        Assert.Equal(5, entry.Mesh!.Outline!.Count);
        Assert.Equal(4, mesh.Outline!.Count);
        Assert.Equal(10, entry.Mesh.Vertices.Count);
    }

    [Fact]
    public void MissingReferencesAreReportedAndCanBeRepairedWithoutLosingThePiece()
    {
        var session = Session(); BuildPiece(session);
        var piece = session.Assets.Piece("badge")!;
        session.Edit(piece, () => piece.Asset.Geometry = "missing");
        Assert.Contains(session.Assets.Problems(piece), p => p.Contains("no geometry 'missing'"));
        Assert.True(session.Save(piece), session.Message);
        var reopened = Session();
        Assert.Empty(reopened.Assets.LoadErrors);
        Assert.NotNull(reopened.Assets.Piece("badge"));
        Assert.NotEmpty(reopened.Assets.Problems(reopened.Assets.Piece("badge")!));
    }

    [Fact]
    public void SocketCreationAndDirectAttachmentUndoFromThePieceEditor()
    {
        var session = Session(); BuildPiece(session); AddWearer(session);
        Assert.True(session.AddPieceSocket("second-mount", "root", "tip"), session.Message);
        Assert.True(session.AttachPart("badge", "second-mount", true), session.Message);
        Assert.True(Assert.Single(session.Assets.Resolve("wearer")!.Attachments).Instance.Mirror);
        session.Undo();
        Assert.Empty(session.Assets.Resolve("wearer")!.Attachments);
        session.Undo();
        Assert.DoesNotContain(session.Assets.Model("wearer")!.Asset.Sockets, s => s.Id == "second-mount");
        Assert.False(session.AddPieceSocket("bad", "unknown", null));
        Assert.DoesNotContain(session.Assets.Model("wearer")!.Asset.Sockets, s => s.Id == "bad");
    }

    [Fact]
    public void EquipmentAndTestSnapshotsResolvePiecesAndKeepIndependentMaterials()
    {
        var session = Session(); BuildPiece(session); AddWearer(session);
        var model = session.Assets.Model("wearer")!;
        var idle = ClipAuthoring.New(session.Assets.Resolve(model.Id)!, "idle", "Idle");
        session.Assets.Create(idle);
        session.Edit(model, () => model.Asset.MotionSets.Add(new() { Id = "standard", Name = "Standard", Roles = new() { ["idle"] = idle.Id } }));
        var entity = EntityAuthoring.New(EntityAuthoring.Stationary, "actor", "Test actor", session.Assets.Resolve(model.Id)!);
        session.Assets.Create(entity);
        Assert.True(session.EquipAppearance(entity.Id, "badge", "mount"), session.Message);
        var (snapshots, problems) = session.Assets.SnapshotEntities();
        Assert.Empty(problems);
        var snapshot = Assert.Single(snapshots);
        var material = session.Assets.RenderMaterial("surface")!;
        session.Edit(material, () => material.Asset.Material = material.Asset.Material with { Fill = "#102030" });
        Assert.Equal("#b8bdc5", Assert.Single(snapshot.Equipment).Prop.Geometry[0].RenderMaterial.Fill);
        Assert.Equal("#102030", Assert.Single(session.Assets.CompileEntity(entity.Id)!.Equipment).Prop.Geometry[0].RenderMaterial.Fill);
    }
}
