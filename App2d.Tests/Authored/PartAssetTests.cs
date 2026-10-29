using App2d.CharacterSeeds;
using App2d.CharacterStudio.Editing;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Tests.Authored;

/// <summary>P5a of the character-system migration: library part assets, attachment instances and their reference graph.</summary>
public sealed class PartAssetTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "parts-" + Guid.NewGuid().ToString("N"));

    public PartAssetTests()
    {
        foreach (var file in Directory.EnumerateFiles(TestModels.AuthoredRoot, "*.json", SearchOption.AllDirectories))
        {
            var target = Path.Combine(_root, Path.GetRelativePath(TestModels.AuthoredRoot, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    public void Dispose() => Directory.Delete(_root, true);

    /// <summary>A glove: a disc on the origin and a cuff curve toward a second location, plus one extruded solid.</summary>
    internal static Part Glove(string id = "glove", string fill = "#c9a27a") => new()
    {
        Id = id,
        Name = "Glove",
        Usage = "glove",
        Materials = { ["leather"] = new() { Fill = fill, Outline = new() } },
        Locations = [new() { Id = "cuff", Y = .12f, Layer = -.01f }],
        Geometry =
        [
            new() { Id = "palm", A = PartLocation.Origin, Geometry = Ellipse2D.Builder.Create(new(.06f, .05f)), Material = new() { Asset = "leather" } },
            new() { Id = "cuff-line", A = PartLocation.Origin, B = "cuff", Geometry = new LineSegmentCurve2D { Start = Vector2.Zero, End = Vector2.UnitY }.Validated(), Material = new() { StrokeWidth = .02f } },
            new() { Id = "cuff-mesh", A = PartLocation.Origin, Frame = PartLocation.Origin, Material = new() { Fill = fill }, Mesh = new() { Outline = [new(-.03f, .1f), new(.03f, .1f), new(.03f, .14f), new(-.03f, .14f)], Thickness = .02f } },
        ],

    };

    private static Func<string, Part?> Library(params Part[] parts) => id => parts.FirstOrDefault(p => p.Id == id);

    [Fact]
    public void APartAssetRoundTripsValidatesItsPayloadAndRefusesTheAnimatedBoundary()
    {
        var glove = Glove();
        glove.Validate();
        var json = glove.ToJson();
        Assert.Contains("\"format\": \"app2d-part\"", json);
        var read = Part.FromJson(json);
        Assert.Equal(json, read.ToJson());
        Assert.Equal(glove.Geometry[^1].Mesh!.Vertices.Count, read.Geometry[^1].Mesh!.Vertices.Count);
        Assert.True(read.IsLocation("origin") && read.IsLocation("cuff") && !read.IsLocation("thumb"));
        TestModels.Near(new(0, .12f, -.01f), read.LocationFrame("cuff").Origin);

        // An animated payload needs a rig for its pieces (P7a); a static one carries no rig.
        var animated = Glove();
        animated.Payload = PartPayloads.Animated;
        Assert.Contains("at least one rig control", Assert.Throws<InvalidDataException>(animated.Validate).Message);
        var rigged = Glove();
        rigged.Layers.Add(new() { Id = "base" });
        Assert.Contains("a static payload carries no rig", Assert.Throws<InvalidDataException>(rigged.Validate).Message);
        var dangling = Glove();
        dangling.Geometry[1].B = "thumb";
        Assert.Contains("thumb", Assert.Throws<InvalidDataException>(dangling.Validate).Message);
        var empty = new Part { Id = "nothing", Name = "Nothing" };
        Assert.Contains("at least one", Assert.Throws<InvalidDataException>(empty.Validate).Message);
        var originLocation = Glove();
        originLocation.Locations.Add(new() { Id = "origin" });
        Assert.Contains("implicit", Assert.Throws<InvalidDataException>(originLocation.Validate).Message);
    }

    [Fact]
    public void OneAssetOccupiesBothHandsAsDistinctInstancesWithIndependentPlacement()
    {
        var person = PersonTemplate.Model();
        var glove = Glove();
        ModelAuthoring.AddAttachment(person, "glove", Humanoid.LeftHandSocket, Library(glove), "left-glove");
        ModelAuthoring.AddAttachment(person, "glove", Humanoid.RightHandSocket, Library(glove), "right-glove", mirror: true);
        person.Attachments[1].FitX = 1.5f;
        Assert.Equal(CharacterModel.CurrentVersion, CharacterModel.FromJson(person.ToJson()).Version);
        Assert.Equal(2, CharacterModel.FromJson(person.ToJson()).Attachments.Count);
        var resolved = ResolvedModel.From(person, null, Library(glove));
        Assert.Equal(["left-glove", "right-glove"], resolved.Attachments.Select(a => a.Id));
        Assert.All(resolved.Attachments, a => Assert.Same(glove, a.Asset));
        Assert.Equal(Humanoid.LeftHandSocket, resolved.Attachments[0].Socket.Id);
        Assert.Equal(Humanoid.RightHandSocket, resolved.Attachments[1].Socket.Id);
        // Materials resolve against the asset's shared surfaces; the asset itself is untouched.
        Assert.Equal("#c9a27a", resolved.Attachments[0].Parts[0].RenderMaterial.Fill);
        Assert.Equal("leather", glove.Geometry[0].Material!.Asset);
        Assert.Null(glove.Geometry[0].Material!.Fill);
        // The two instances place the same origin on different sockets; the mirrored one reflects X in its socket's axes.
        var pose = PoseEvaluator.Rest(resolved);
        var actor = new ActorPose(pose, Vector2.Zero, 1);
        var left = resolved.Attachments[0].Placement(actor.SocketPlacement(resolved.Attachments[0].Socket));
        var right = resolved.Attachments[1].Placement(actor.SocketPlacement(resolved.Attachments[1].Socket));
        TestModels.Near(actor.SocketPlacement(resolved.Attachments[0].Socket).Origin, left.Origin);
        TestModels.Near(actor.SocketPlacement(resolved.Attachments[1].Socket).Origin, right.Origin);
        Assert.True(left.Frame.Matrix.GetDeterminant() > 0);
        Assert.True(right.Frame.Matrix.GetDeterminant() < 0);
        Assert.Equal(1.5f * actor.SocketPlacement(resolved.Attachments[1].Socket).XAxis.Length(), right.XAxis.Length(), 4);
        Assert.Equal(1, left.XAxis.Length(), 4);
        // A missing socket or asset is refused by name; an instance never shares a drawing part's id.
        Assert.Contains("no socket 'nowhere'", Assert.Throws<InvalidDataException>(() => ModelAuthoring.AddAttachment(person, "glove", "nowhere", Library(glove))).Message);
        Assert.Contains("No part asset 'mitten'", Assert.Throws<InvalidDataException>(() => ModelAuthoring.AddAttachment(person, "mitten", Humanoid.LeftHandSocket, Library(glove))).Message);
        person.Attachments[0].Id = "body";
        Assert.Contains("shares its id", Assert.Throws<InvalidDataException>(person.Validate).Message);
        person.Attachments[0].Id = "left-glove";
        Assert.Contains("needs the part library", Assert.Throws<InvalidDataException>(() => ResolvedModel.From(person)).Message);
    }

    [Fact]
    public void SwappingAnInstanceOnOneWearerLeavesTheBaseOtherWearersAndTheSharedAssetAlone()
    {
        var person = PersonTemplate.Model();
        var glove = Glove();
        var mitten = Glove("mitten", "#333333");
        var library = Library(glove, mitten);
        ModelAuthoring.AddAttachment(person, "glove", Humanoid.LeftHandSocket, library, "left-glove");
        var tall = PersonBuild.TallThin.Apply(person, "tall", "Tall");
        var broad = PersonBuild.ShortBroad.Apply(person, "broad", "Broad");
        ModelAuthoring.EditAttachmentOverride(tall, ResolvedModel.From(person, tall, library), "left-glove", i => { i.Asset = "mitten"; i.FitY = 2; i.Material = new() { Fill = "#ff0000" }; });
        Assert.Equal(ModelVariant.CurrentVersion, ModelVariant.FromJson(tall.ToJson()).Version);
        Assert.Equal("mitten", ModelVariant.FromJson(tall.ToJson()).Override(PropertyRef.OfAttachment("left-glove", RigProperties.Asset))?.Selection);
        Assert.Equal(3, tall.Overrides.Count);
        var onTall = ResolvedModel.From(person, tall, library).Attachments.Single();
        var onBroad = ResolvedModel.From(person, broad, library).Attachments.Single();
        var onBase = ResolvedModel.From(person, null, library).Attachments.Single();
        Assert.Same(mitten, onTall.Asset);
        Assert.Equal(2, onTall.Instance.FitY);
        Assert.Equal("#ff0000", onTall.Parts[0].RenderMaterial.Fill);
        Assert.Same(glove, onBroad.Asset);
        Assert.Same(glove, onBase.Asset);
        Assert.Equal(1, onBroad.Instance.FitY);
        Assert.Equal("#c9a27a", onBroad.Parts[0].RenderMaterial.Fill);
        Assert.Equal("glove", person.Attachments[0].Asset);
        Assert.Null(mitten.Geometry[0].Material!.Fill);
        // Clearing the override restores inheritance without writing the base value into the variant.
        foreach (var property in new[] { RigProperties.Asset, RigProperties.FitY, RigProperties.Material })
        {
            ModelAuthoring.ResetOverride(tall, PropertyRef.OfAttachment("left-glove", property));
        }

        Assert.Empty(tall.Overrides);
        Assert.Same(glove, ResolvedModel.From(person, tall, library).Attachments.Single().Asset);
        var unknown = new ModelVariant { Id = "odd", Name = "Odd", Base = "person", Overrides = [PropertyOverride.Of(PropertyRef.OfAttachment("hat", RigProperties.Hidden), true)] };
        Assert.Contains("no attachment 'hat'", Assert.Throws<InvalidDataException>(() => ResolvedModel.From(person, unknown, library)).Message);
    }

    [Fact]
    public void TheWorkspaceLoadsPartsTracksTransitiveReferencesAndRefreshesWearersWhenASharedPartChanges()
    {
        var glove = Glove();
        Directory.CreateDirectory(Path.Combine(_root, "parts"));
        glove.Save(Path.Combine(_root, "parts", "glove.json"));
        var catalog = AuthoredCatalog.Load(_root);
        Assert.Empty(catalog.Errors);
        Assert.Contains("glove", catalog.Parts.Keys);

        var session = new EditorSession(AuthoringWorkspace.Open(_root));
        session.Open("person");
        var workspace = session.Assets;
        Assert.Equal(AssetKind.Part, workspace.Part("glove")!.Kind);
        var person = workspace.Model("person")!;
        Assert.True(session.Edit(person, () => ModelAuthoring.AddAttachment(person.Asset, "glove", Humanoid.LeftHandSocket, workspace.PartOf, "left-glove")), session.Message);
        Assert.True(session.Edit(person, () => ModelAuthoring.AddAttachment(person.Asset, "glove", Humanoid.RightHandSocket, workspace.PartOf, "right-glove", mirror: true)), session.Message);
        Assert.Equal(["face-person", "glove", "glove"], workspace.Uses("person").Where(u => u.Relation.StartsWith("attachment", StringComparison.Ordinal)).Select(u => u.Id));
        Assert.Contains(workspace.UsedBy("glove"), r => r.Id == "person");
        Assert.Equal(new[] { "person", "tall-thin", "short-broad", "brute", "cinder", "club-caveman-build", "rock-thrower-build", "shield-defender-build" }.Order(), workspace.Wearers("glove").Order());
        var hero = workspace.CompileEntity("hero")!;
        // The face instance (P7c) comes first on every wearer; the gloves follow it.
        Assert.Equal(3, hero.Model.Attachments.Count);

        // Editing the shared part refreshes every dependent resolution without anyone copying it.
        var before = workspace.Resolve("tall-thin")!;
        Assert.Equal("#c9a27a", before.Attachments[1].Parts[0].RenderMaterial.Fill);
        var part = workspace.Part("glove")!;
        Assert.True(session.Edit(part, () => part.Asset.Materials["leather"] = part.Asset.Materials["leather"] with { Fill = "#123456" }), session.Message);
        Assert.NotSame(before, workspace.Resolve("tall-thin"));
        Assert.Equal("#123456", workspace.Resolve("tall-thin")!.Attachments[1].Parts[0].RenderMaterial.Fill);
        Assert.Equal("#123456", workspace.Resolve("person")!.Attachments[2].Parts[0].RenderMaterial.Fill);
        Assert.Equal("#123456", workspace.CompileEntity("hero")!.Model.Attachments[1].Parts[0].RenderMaterial.Fill);
        Assert.False(person.Dirty is false);
        session.Undo();
        Assert.Equal("#c9a27a", workspace.Resolve("tall-thin")!.Attachments[1].Parts[0].RenderMaterial.Fill);

        // A variant swap is an override on that wearer; the base and the other variants keep the glove.
        var tall = workspace.Variant("tall-thin")!;
        Assert.True(session.Edit(part, () => { }), session.Message);
        var mitten = Glove("mitten", "#222222");
        var mittenDocument = workspace.Create(mitten);
        Assert.True(session.Edit(tall, () => ModelAuthoring.EditAttachmentOverride(tall.Asset, workspace.Resolve("tall-thin")!, "left-glove", i => i.Asset = "mitten")), session.Message);
        Assert.Same(mitten, workspace.Resolve("tall-thin")!.Attachments[1].Asset);
        Assert.Equal("glove", workspace.Resolve("short-broad")!.Attachments[1].Asset.Id);
        Assert.Contains(workspace.UsedBy("mitten"), r => r.Id == "tall-thin");
        Assert.Contains("tall-thin", workspace.Wearers("mitten"));
        Assert.DoesNotContain("tall-thin", workspace.Wearers("glove").Where(w => w == "tall-thin" && workspace.Resolve("tall-thin")!.Attachments.All(a => a.Asset.Id != "glove")));

        // Save everything, reopen: the part file, the base's instances and the variant's override come back.
        session.SaveAll();
        Assert.False(session.MessageIsError, session.Message);
        Assert.True(File.Exists(Path.Combine(_root, "parts", "mitten.json")));
        var reopened = AuthoringWorkspace.Open(_root);
        Assert.Empty(reopened.LoadErrors);
        Assert.Equal(3, reopened.Resolve("person")!.Attachments.Count);
        Assert.Equal("mitten", reopened.Resolve("tall-thin")!.Attachments[1].Asset.Id);
        Assert.Empty(reopened.Problems(reopened.Part("glove")!));
        Assert.Empty(reopened.Problems(reopened.Model("person")!));
        Assert.Empty(reopened.Problems(reopened.Variant("tall-thin")!));
        _ = mittenDocument;

        // Removing the base instance leaves the variant's override dangling, reported on the variant by name.
        Assert.True(session.Edit(person, () => ModelAuthoring.RemoveAttachment(person.Asset, "left-glove")), session.Message);
        Assert.Contains(workspace.Problems(tall), p => p.Contains("no attachment 'left-glove'"));
        session.Undo();
        Assert.Empty(workspace.Problems(tall));
    }

    [Fact]
    public void ATemplateInstanceKeepsItsAttachmentsAsReferencesToTheSharedLibrary()
    {
        var glove = Glove();
        var person = PersonTemplate.Model(face: true);
        ModelAuthoring.AddAttachment(person, "glove", Humanoid.LeftHandSocket, Library(glove, PersonTemplate.Face), "left-glove");
        var catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);
        var template = new ModelTemplate { Id = "gloved", Name = "Gloved", Model = "person", Animations = person.MotionSets.SelectMany(s => s.Roles.Values).Distinct().ToDictionary(c => c["person-".Length..], c => c) };
        var bundle = template.Instantiate("gloved-person", "Gloved person", id => id == "person" ? person : null, catalog.Animations.GetValueOrDefault, Library(glove, PersonTemplate.Face));
        Assert.Equal(2, bundle.Model.Attachments.Count);
        Assert.Equal("glove", bundle.Model.Attachments[^1].Asset);
        Assert.Contains("needs the part library", Assert.Throws<InvalidDataException>(() => template.Instantiate("other", "Other", id => id == "person" ? person : null, catalog.Animations.GetValueOrDefault)).Message);
        var flat = ModelAuthoring.Flatten(ResolvedModel.From(person, null, Library(glove, PersonTemplate.Face)), "flat", "Flat");
        Assert.Equal(2, flat.Attachments.Count);
    }

    [Fact]
    public void FootwearUsesTheSharedPartCodecAndRetainsItsGeometry()
    {
        var catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);
        var boot = AppearanceAuthoring.Duplicate(catalog.Parts["boot-left"], "boot", "Short leather boot");
        Assert.Equal(catalog.Parts["boot-left"].Geometry.Count, boot.Geometry.Count);
        Assert.Equal(catalog.Parts["boot-left"].Geometry[0].Mesh!.Vertices.Count, boot.Geometry[0].Mesh!.Vertices.Count);
        Assert.Equal(PartPayloads.Static, boot.Payload);
        Assert.Equal(boot.ToJson(), Part.FromJson(boot.ToJson()).ToJson());
    }
}
