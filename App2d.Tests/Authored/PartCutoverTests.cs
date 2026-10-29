using App2d.CharacterSeeds;
using App2d.CharacterStudio.Editing;
using App2d.Core;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Operations.Bounds;
using App2d.Core.Physics;
using App2d.Core.Shapes;
using App2d.Gameplay.World;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace App2d.Tests.Authored;

public sealed class PartCutoverTests
{
    [Theory]
    [InlineData("app2d-prop", 3)]
    [InlineData("app2d-entity", 5)]
    [InlineData("app2d-physics-prop", 1)]
    [InlineData("app2d-object", 1)]
    [InlineData("app2d-part", 3)]
    public void RetiredFormatsCannotSilentlyOpenAsCurrentParts(string format, int version)
    {
        var json = JsonNode.Parse(Body().ToJson())!;
        json["format"] = format;
        json["version"] = version;
        Assert.Throws<InvalidDataException>(() => Part.FromJson(json.ToJsonString()));
    }

    [Fact]
    public void RigidBodiesUseOnlyEnabledCollisionEntriesWithTheSharedPlacementAndMask()
    {
        var part = Body();
        part.Scale = 2;
        part.Locations.Add(new() { Id = "mount", X = 3, Y = 2 });
        part.Geometry[0].A = "mount";
        part.Geometry[0].OffsetX = 1;
        part.Geometry[0].Collision!.Mask = 3;
        part.Geometry.Add(new() { Id = "disabled", Geometry = Circle2D.Builder.Create(100), Collision = new() { Enabled = false, Mask = 16 } });
        part.Geometry.Add(new() { Id = "decoration", Frame = "origin", Geometry = LineSegmentCurve2D.Builder.Create(Vector2.Zero, new(100, 100)), Material = new() { StrokeWidth = .1f } });
        var physics = new PhysicsWorld2D();
        using var body = new AuthoredObject2D(EntityId2D.Create(), new("mixed", part, Vector2.Zero), physics, 1, 6);
        var bounds = ShapeBounds2D.Calculate(body.WorldObject.Shape);
        Assert.Equal(new Vector2(6, 2), bounds.Min);
        Assert.Equal(new Vector2(10, 6), bounds.Max);
        Assert.Equal(2u, body.Body!.CollisionMask);
        Assert.Single(physics.Bodies);
        part.Geometry[1].Collision!.Enabled = true;
        Assert.Throws<InvalidDataException>(part.Validate);
    }

    [Fact]
    public void AWholeCompositeCanBeACollisionOnlyEntry()
    {
        var part = Body();
        part.Geometry[0].Geometry = CompositeShape2D.Builder.Create([Circle2D.Builder.Create(1), Circle2D.Builder.Create(1, new(3, 0))]);
        part.Validate();
        Assert.IsType<CompositeShape2D>(PartShapes2D.Collision(Part.FromJson(part.ToJson())));
        Assert.Null(part.Geometry[0].ResolveMaterial(new Dictionary<string, RenderMaterialDefinition2D>(), new() { Fill = "#ff0000" }).Material);
    }

    [Fact]
    public void AnAnimatedPartsRigidBodyUsesItsBindPoseWhileItsArtworkAnimates()
    {
        var part = PartSeeds.Tail();
        part.Geometry.Add(new() { Id = "body", A = part.Controls[0].Id, Geometry = Circle2D.Builder.Create(.2f), Collision = new() });
        part.Physics = new();
        part.Validate();
        var before = PartShapes2D.Collision(part);
        var drawing = new PuppetDrawing();
        drawing.Build(part, .4);
        Assert.True(drawing.Mesh.Count > 0);
        Assert.Equal(ShapeBounds2D.Calculate(before), ShapeBounds2D.Calculate(PartShapes2D.Collision(part)));
        Assert.Equal(part.ToJson(), Part.FromJson(part.ToJson()).ToJson());
    }

    [Fact]
    public void BodyAndGeometryEditsUseTheSamePartDocumentUndoAndSavePath()
    {
        var root = Path.Combine(Path.GetTempPath(), "part-cutover-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var workspace = AuthoringWorkspace.Open(root);
            var doc = workspace.Create(Body());
            var before = doc.Asset.ToJson();
            doc.Edit(() => { doc.Asset.Physics!.Mass = 7; doc.Asset.Geometry[0].Material = new() { Fill = "#123456" }; });
            doc.Undo();
            Assert.Equal(before, doc.Asset.ToJson());
            doc.Redo();
            Assert.Empty(workspace.Save(doc).Problems);
            var reopened = AuthoringWorkspace.Open(root);
            Assert.Empty(reopened.LoadErrors);
            Assert.Equal(doc.Asset.ToJson(), reopened.Part(doc.Id)!.Asset.ToJson());
            Assert.Empty(reopened.Entities);
            Assert.EndsWith(Path.Combine("parts", "body.json"), doc.Path);
            var json = JsonNode.Parse(doc.Asset.ToJson())!;
            json["collision"] = json["geometry"]![0]!["geometry"]!.DeepClone();
            Assert.Throws<JsonException>(() => Part.FromJson(json.ToJsonString()));
        }
        finally { Directory.Delete(root, true); }
    }

    private static Part Body() => new()
    {
        Id = "body",
        Name = "Body",
        Physics = new() { Mass = 2, FreezeRotation = false },
        Geometry = [new() { Id = "shape", Geometry = Circle2D.Builder.Create(1), Collision = new() }]
    };
}
