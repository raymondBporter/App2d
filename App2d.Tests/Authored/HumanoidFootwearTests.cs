using App2d.CharacterSeeds;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Geometry;
using System.Numerics;

namespace App2d.Tests.Authored;

public sealed class HumanoidFootwearTests
{
    [Fact]
    public void SharedSkinMaterialChangesTheHeadHandsAndFeetOnPackagedAndNewModels()
    {
        foreach (var model in new[] { CharacterModel.Load(Path.Combine(TestModels.AuthoredRoot, "models", "person.json")), PersonTemplate.Model() })
        {
            model.Materials["skin"] = model.Materials["skin"] with { Fill = "#123456" };
            var resolved = ResolvedModel.From(model, null, PersonTemplate.PartOf);
            foreach (var id in new[] { Humanoid.Head, Humanoid.LeftHand, Humanoid.RightHand, Humanoid.LeftFoot, Humanoid.RightFoot })
                Assert.Equal("#123456", resolved.Parts.Single(p => p.Id == id).RenderMaterial.Fill);
        }
    }

    [Fact]
    public void RegeneratingAnOlderModelKeepsArtistMaterialsAndAddsTheNewSkinMaterial()
    {
        var root = Path.Combine(Path.GetTempPath(), "footwear-seed-" + Guid.NewGuid().ToString("N"));
        try
        {
            var old = PersonTemplate.Model();
            foreach (var part in old.Parts.Where(p => p.Material?.Asset == "skin").ToArray())
                old.Parts[old.Parts.IndexOf(part)] = part with { Material = new() { Fill = "#fff8e7" } };
            old.Materials.Remove("skin");
            old.Materials["artist-surface"] = new() { Fill = "#123456" };
            old.Save(Path.Combine(root, "models", "person.json"));
            // A forced regeneration (P11b): the artist's materials are merged into the generated model, which then replaces the file.
            PersonTemplate.WriteStudies(root, new SeedReport(force: true));
            var regenerated = CharacterModel.Load(Path.Combine(root, "models", "person.json"));
            Assert.Equal("#123456", regenerated.Materials["artist-surface"].Fill);
            Assert.Contains("skin", regenerated.Materials.Keys);
            ResolvedModel.From(regenerated, null, PersonTemplate.PartOf);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void SharedHumanoidAndItsSeedHaveVisibleHandsAndFeetWithMatchingSkin()
    {
        var catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);
        Assert.Empty(catalog.Errors);
        var seed = ResolvedModel.From(PersonTemplate.Model());
        foreach (var model in catalog.Entities.Values.Select(e => e.Model).Where(m => m.Base.Id == "person").Append(seed))
        {
            foreach (var id in new[] { Humanoid.LeftHand, Humanoid.RightHand, Humanoid.LeftFoot, Humanoid.RightFoot })
            {
                var part = Assert.Single(model.Parts, p => p.Id == id);
                Assert.False(part.Hidden);
                Assert.Equal(model.Parts.Single(p => p.Id == Humanoid.Head).RenderMaterial.Fill, part.RenderMaterial.Fill);
                Assert.NotEmpty(PartContours2D.Contour(part, PoseEvaluator.Rest(model).World));
            }
        }
    }

    [Theory]
    [InlineData("sandal", "cinder-gunner")]
    [InlineData("boot", "maul-brute")]
    public void BothShoesFollowTheirOwnFootAndShinAcrossExistingActionsAndFacings(string style, string entityId)
    {
        var catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);
        var entity = catalog.Entities[entityId];
        var drawing = new PuppetDrawing();
        foreach (var clip in entity.Roles.Values.Select(r => r.Clip).Concat(entity.Actions.Values.Select(a => a.Clip)))
        {
            foreach (var at in new[] { 0, clip.Duration / 2, clip.Duration })
            {
                var local = PoseEvaluator.Sample(entity.Model, clip, at);
                drawing.Build(entity, local);
                Assert.True(drawing.Mesh.Count > 0);
                foreach (var facing in new[] { -1, 1 })
                {
                    var pose = new ActorPose(local, new(3, 2), facing);
                    foreach (var (side, foot, knee, socketId) in new[]
                    {
                        ("left", Humanoid.LeftFoot, Humanoid.LeftKnee, Humanoid.LeftFootSocket),
                        ("right", Humanoid.RightFoot, Humanoid.RightKnee, Humanoid.RightFootSocket)
                    })
                    {
                        var item = Assert.Single(entity.Equipment, e => e.Socket.Id == socketId);
                        Assert.Equal(style + "-" + side, item.Prop.Id);
                        var frame = pose.Socket(item.Socket);
                        TestModels.Near(pose.World(foot), PartPlacement3D.Point(frame, item.Prop, item.Prop.Point(EquipmentPoints.Grip)!.Value));
                        var up = pose.World(knee) - pose.World(foot);
                        up.Z = 0;
                        TestModels.Near(Vector3.Normalize(up), frame.YAxis);
                        var bare = PartAttachment2D.FrameOf(entity.Model.Parts.Single(p => p.Id == foot), local.World);
                        var point = new Vector2(.04f, 0);
                        // Both the bare foot and its shoe use the same local XY axes and mirroring.
                        var onFoot = pose.Place(bare.At(point));
                        var onShoe = PartPlacement3D.Point(frame, item.Prop, new(point.X, point.Y));
                        TestModels.Near(onFoot with { Z = onShoe.Z }, onShoe);
                    }
                }
            }
        }
    }

    [Theory]
    [InlineData("sandal")]
    [InlineData("boot")]
    public void FootwearJsonKeepsEditableOutlinesAndMaterialChanges(string style)
    {
        var catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);
        foreach (var side in new[] { "left", "right" })
        {
            var original = catalog.Parts[style + "-" + side];
            var json = original.ToJson();
            Assert.DoesNotContain("\"vertices\"", json);
            var copy = Part.FromJson(json);
            copy.Geometry[0].Material = copy.Geometry[0].RenderMaterial with { Fill = "#123456" };
            var edited = Part.FromJson(copy.ToJson());
            Assert.Equal("#123456", edited.Geometry[0].RenderMaterial.Fill);
            Assert.NotEqual(original.Geometry[0].RenderMaterial.Fill, edited.Geometry[0].RenderMaterial.Fill);
            Assert.All(edited.Geometry, solid => Assert.NotEmpty(solid.Mesh!.Outline!));
            Assert.Equal(original.Attachment, edited.Attachment);
        }
    }

    [Fact]
    public void ReplacingFootwearKeepsTheSameWeaponsActionsAndGameplayRegions()
    {
        var catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);
        var original = catalog.Entities["cinder-gunner"];
        var copy = EntityAuthoring.Duplicate(original.Asset, "booted-cinder", "Booted Cinder");
        copy.Equipment.RemoveAll(e => e.Socket is Humanoid.LeftFootSocket or Humanoid.RightFootSocket);
        copy.Equipment.AddRange(PersonWardrobe.Footwear("boot"));
        var dressed = ResolvedEntity.Compile(copy, catalog.Resolve, id => catalog.Animations.GetValueOrDefault(id), id => catalog.Parts.GetValueOrDefault(id));
        Assert.Same(original.Model, dressed.Model);
        Assert.Equal(original.Asset.Behavior!.Damage.Health, dressed.Asset.Behavior!.Damage.Health);
        Assert.Same(original.Actions["attack"].Clip, dressed.Actions["attack"].Clip);
        var pose = new ActorPose(PoseEvaluator.Sample(original.Model, original.Actions["attack"].Clip, .6f), Vector2.Zero, 1);
        Assert.Equal(EntityCollision.Hurt(original, pose), EntityCollision.Hurt(dressed, pose));
        Assert.Equal(EntityCollision.Muzzle(original, pose), EntityCollision.Muzzle(dressed, pose));
        Assert.Equal(EntityCollision.Movement(original, Vector2.Zero, 1), EntityCollision.Movement(dressed, Vector2.Zero, 1));
    }
}
