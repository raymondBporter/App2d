using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Drawing;
using App2d.Core.Characters.Geometry;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Mathematics;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Tests.Authored;

/// <summary>P5b of the character-system migration: every payload kind of a library part shares one placement, drawing and picking path.</summary>
public sealed class PartAttachmentDrawingTests
{
    private static readonly AuthoredCatalog Catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);

    private static CharacterModel Person() => CharacterModel.FromJson(Catalog.Models["person"].ToJson());

    /// <summary>A shape on the origin, a curve to a cuff location, an image on the cuff and one solid: all four payload kinds.</summary>
    private static PartAsset Mixed() => new()
    {
        Id = "mixed",
        Name = "Mixed",
        Materials = { ["leather"] = new() { Fill = "#c9a27a", Outline = new() } },
        Locations = [new() { Id = "cuff", Y = .2f, Angle = .3f, Layer = -.02f }],
        Parts =
        [
            new() { Id = "palm", A = PartLocation.Origin, Geometry = Ellipse2D.Builder.Create(new(.08f, .06f)), Material = new() { Asset = "leather" } },
            new() { Id = "seam", A = PartLocation.Origin, B = "cuff", Geometry = new LineSegmentCurve2D { Start = Vector2.Zero, End = Vector2.UnitY }.Validated(), Material = new() { StrokeWidth = .02f } },
            new() { Id = "badge", A = "cuff", Geometry = Rectangle2D.Builder.FromSize(.1f, .1f), Material = new() { Texture = "images/badge.png" } },
        ],
        Solids = [new() { Material = new() { Fill = "#333333" }, Outline = [new(-.03f, .1f), new(.03f, .1f), new(.03f, .14f), new(-.03f, .14f)], Thickness = .02f }],
    };

    private static Vector3 Point(Microsoft.Xna.Framework.Vector3 v) => new(v.X, v.Y, v.Z);

    private static Func<string, PartAsset?> Library(params PartAsset[] parts) => id => parts.FirstOrDefault(p => p.Id == id) ?? Catalog.Parts.GetValueOrDefault(id);

    private static (ResolvedAttachment Attachment, LayeredFrame2D Placement, EvaluatedPose Pose) Worn(PartInstance instance, PartAsset? asset = null)
    {
        var person = Person();
        person.Attachments.Add(instance);
        var resolved = ResolvedModel.From(person, null, asset is null ? Library() : Library(asset));
        var pose = PoseEvaluator.Rest(resolved);
        var attachment = resolved.Attachments.Single();
        return (attachment, attachment.Placement(new ActorPose(pose, Vector2.Zero, 1).SocketPlacement(attachment.Socket)), pose);
    }

    [Fact]
    public void TheBootPartOnTheLeftFootDrawsExactlyWhereTheBootPropDoes()
    {
        var person = Person();
        var plain = ResolvedModel.From(person);
        person.Attachments.Add(new() { Id = "left-boot", Asset = "boot", Socket = Humanoid.LeftFootSocket });
        var booted = ResolvedModel.From(person, null, Library());
        var pose = PoseEvaluator.Rest(booted);
        var viaPart = new PuppetDrawing();
        viaPart.Build(booted, pose);
        var viaProp = new PuppetDrawing();
        viaProp.Build(plain, pose);
        var bare = viaProp.Mesh.Count;
        viaProp.AddProp(Catalog.Props["boot-left"], new ActorPose(pose, Vector2.Zero, 1).Socket(booted.Base.Sockets.First(s => s.Id == Humanoid.LeftFootSocket)));
        Assert.Equal(viaProp.Mesh.Count, viaPart.Mesh.Count);
        Assert.True(viaProp.Mesh.Count > bare);
        for (var i = bare; i < viaProp.Mesh.Count; i++)
        {
            TestModels.Near(Point(viaProp.Mesh.Vertices[i].Position), Point(viaPart.Mesh.Vertices[i].Position), 1e-5f, $"vertex {i}");
            Assert.Equal(viaProp.Mesh.Vertices[i].Color, viaPart.Mesh.Vertices[i].Color);
        }
        Assert.Equal(viaProp.Batches.Count, viaPart.Batches.Count);
    }

    [Fact]
    public void OneShoeAssetOnBothFeetIsTwoInstancesAndTheRightOneIsTheMirrorImage()
    {
        var person = Person();
        person.Attachments.Add(new() { Id = "left-shoe", Asset = "sandal", Socket = Humanoid.LeftFootSocket });
        person.Attachments.Add(new() { Id = "right-shoe", Asset = "sandal", Socket = Humanoid.RightFootSocket, Mirror = true });
        var resolved = ResolvedModel.From(person, null, Library());
        var pose = PoseEvaluator.Sample(resolved, Catalog.Animations["person-walk"], .3);
        var actor = new ActorPose(pose, Vector2.Zero, 1);
        var left = resolved.Attachments[0];
        var right = resolved.Attachments[1];
        Assert.Same(left.Asset, right.Asset);
        var leftSocket = actor.SocketPlacement(left.Socket);
        var rightSocket = actor.SocketPlacement(right.Socket);
        foreach (var local in new[] { new Vector3(.1f, .05f, .01f), new Vector3(-.05f, .15f, 0) })
        {
            TestModels.Near(leftSocket.At(local), left.Placement(leftSocket).At(local), 1e-5f);
            TestModels.Near(rightSocket.At(new Vector3(-local.X, local.Y, local.Z)), right.Placement(rightSocket).At(local), 1e-5f);
        }
        var drawing = new PuppetDrawing();
        drawing.Build(resolved, pose);
        Assert.True(drawing.Mesh.Count > new Func<int>(() => { var d = new PuppetDrawing(); d.Build(ResolvedModel.From(Person()), pose); return d.Mesh.Count; })());
        // Picking finds each instance at its own foot.
        Assert.Equal(0, PartPicking2D.Distance(left, pose, leftSocket.Origin), 3);
        Assert.Equal(0, PartPicking2D.Distance(right, pose, rightSocket.Origin), 3);
        Assert.True(PartPicking2D.Distance(right, pose, leftSocket.Origin) > .5f);
    }

    [Fact]
    public void ShapeCurveImageAndMeshPayloadsShareOnePlacementAndPicking()
    {
        var (attachment, placement, pose) = Worn(new() { Id = "mixed-left", Asset = "mixed", Socket = Humanoid.LeftHandSocket, X = .1f, Y = .2f, Angle = .5f, Scale = 1.5f, FitX = 2, Mirror = true }, Mixed());
        var (world, frame) = attachment.Lookups(placement);
        // Every piece hangs from a location of the same placement.
        TestModels.Near(placement.Origin, world(PartLocation.Origin));
        TestModels.Near(placement.At(new Vector3(0, .2f, -.02f)), world("cuff"));
        var cuff = frame("cuff");
        Assert.InRange(Vector2.Distance(cuff.XAxis, Vector2.Normalize(Vector2.Transform(new(MathF.Cos(.3f), MathF.Sin(.3f)), placement.Frame.Matrix) - placement.Origin.XY) * cuff.XAxis.Length()), 0, 1e-4f);
        var palm = PartContours2D.Contour(attachment.Parts[0], world, frame, world, frame);
        TestModels.Near(placement.Origin, new(palm.Average(p => p.X), palm.Average(p => p.Y), palm.Average(p => p.Z)), 1e-3f);
        var seam = PartContours2D.Contour(attachment.Parts[1], world, frame, world, frame);
        TestModels.Near(placement.Origin, seam[0], 1e-5f);
        TestModels.Near(world("cuff"), seam[^1], 1e-5f);
        var badge = PartAttachment2D.FrameOf(attachment.Parts[2], world, frame);
        TestModels.Near(world("cuff"), badge.Origin, 1e-5f);
        var lifted = PropPlacement3D.Extend(placement);
        TestModels.Near(placement.At(new Vector3(.03f, .14f, 0)), lifted.TransformPoint(new(.03f, .14f, 0)), 1e-5f);
        // The instance's fit and mirror are in the placement: X stretched by 2 × 1.5 and reflected, Y by 1.5.
        Assert.Equal(3, placement.XAxis.Length(), 4);
        Assert.Equal(1.5f, placement.YAxis.Length(), 4);
        Assert.True(placement.Frame.Matrix.GetDeterminant() < 0);
        // Materials resolve through the asset's shared surfaces; the texture reaches the image batch.
        Assert.Equal("#c9a27a", attachment.Parts[0].RenderMaterial.Fill);
        var drawing = new PuppetDrawing();
        drawing.Build(ResolvedModel.From(Person(), null, Library(Mixed())), pose);
        var bare = drawing.Mesh.Count;
        drawing.AddAttachment(attachment, placement);
        Assert.True(drawing.Mesh.Count > bare);
        Assert.Contains(drawing.Batches, b => b.Texture == "images/badge.png");
        // One picking score for all four kinds: inside the palm, on the seam, on the badge, inside the solid.
        Assert.Equal(0, PartPicking2D.Distance(attachment, placement, placement.Origin), 3);
        Assert.InRange(PartPicking2D.Distance(attachment, placement, placement.At(new Vector3(0, .1f, 0))), 0, 1);
        Assert.Equal(0, PartPicking2D.Distance(attachment, placement, world("cuff")), 3);
        Assert.Equal(0, PartPicking2D.Distance(attachment, placement, placement.At(new Vector3(0, .12f, 0))), 3);
        Assert.True(PartPicking2D.Distance(attachment, placement, placement.At(new Vector3(2, 2, 0))) > 5);
        Assert.Equal(PartPicking2D.Distance(attachment, placement, placement.Origin), PartPicking2D.Distance(attachment, pose, placement.Origin), 5);
    }

    [Fact]
    public void SlotAttachmentsDrawAnyGeometryWithTheSlotColor()
    {
        var source = TestModels.Creature();
        var part = source.Parts[0];
        source.Slots = [new() { Id = "paint", Bone = part.A, Attachment = "art", Color = "ff000080" }];
        source.Skins = [new() { Attachments = { ["paint"] = new() { ["art"] = part.Id } } }];
        var model = ResolvedModel.From(source);
        var pose = PoseEvaluator.Rest(model);
        Assert.Single(pose.Slots);
        Assert.NotNull(pose.Slots[0].Part);
        Assert.Null(pose.Slots[0].Part!.Material?.Texture);
        var drawing = new PuppetDrawing();
        drawing.Build(model, pose);
        var without = new PuppetDrawing();
        without.Build(ResolvedModel.From(TestModels.Creature()), PoseEvaluator.Rest(ResolvedModel.From(TestModels.Creature())));
        Assert.Equal(without.Mesh.Count, drawing.Mesh.Count);
        // The slot's color tints the shape's fill: red at half alpha over the authored fill.
        var tinted = drawing.Mesh.Vertices.ToArray().Where(v => v.Color.G == 0 && v.Color.B == 0).ToArray();
        Assert.NotEmpty(tinted);
        Assert.All(tinted, v => Assert.Equal(128, v.Color.A));
    }

    [Fact]
    public void InstanceMaterialsInheritOverrideAndClearAndAHiddenInstanceIsNotDrawn()
    {
        var mixed = Mixed();
        var person = Person();
        person.Attachments.Add(new() { Id = "glove", Asset = "mixed", Socket = Humanoid.LeftHandSocket, Material = new() { Fill = "#ff0000" } });
        var over = ResolvedModel.From(person, null, Library(mixed)).Attachments[0];
        Assert.Equal("#ff0000", over.Parts[0].RenderMaterial.Fill);
        Assert.Equal("#ff0000", over.Solids[0].RenderMaterial.Fill);
        Assert.NotNull(over.Parts[0].RenderMaterial.Outline);
        Assert.Equal("leather", mixed.Parts[0].Material!.Asset);
        person.Attachments[0].Material = null;
        var inherited = ResolvedModel.From(person, null, Library(mixed)).Attachments[0];
        Assert.Equal("#c9a27a", inherited.Parts[0].RenderMaterial.Fill);
        var variant = new ModelVariant { Id = "red", Name = "Red", Base = "person", Attachments = { ["glove"] = new() { Material = new() { Asset = "leather", Fill = "#00ff00" } } } };
        var onVariant = ResolvedModel.From(person, variant, Library(mixed)).Attachments[0];
        Assert.Equal("#00ff00", onVariant.Parts[0].RenderMaterial.Fill);
        Assert.Equal("#c9a27a", ResolvedModel.From(person, null, Library(mixed)).Attachments[0].Parts[0].RenderMaterial.Fill);
        var shown = new PuppetDrawing();
        shown.Build(ResolvedModel.From(person, null, Library(mixed)), PoseEvaluator.Rest(ResolvedModel.From(person, null, Library(mixed))));
        person.Attachments[0].Hidden = true;
        var hidden = new PuppetDrawing();
        hidden.Build(ResolvedModel.From(person, null, Library(mixed)), PoseEvaluator.Rest(ResolvedModel.From(person, null, Library(mixed))));
        Assert.True(hidden.Mesh.Count < shown.Mesh.Count);
        Assert.Equal(new PuppetDrawing().Mesh.Count, 0);
    }

    [Fact]
    public void AParameterBindsAnInstancesFitAndTheWearersOverrideWins()
    {
        var mixed = Mixed();
        var person = Person();
        person.Attachments.Add(new() { Id = "glove", Asset = "mixed", Socket = Humanoid.LeftHandSocket });
        person.Parameters.Add(new()
        {
            Id = "glove-size", Name = "Glove size", Default = 1, Min = .5f, Max = 3,
            Bindings =
            [
                new() { Kind = BindingKinds.Scale, Target = PropertyRef.OfAttachment("glove", RigProperties.FitX) },
                new() { Kind = BindingKinds.Scale, Target = PropertyRef.OfAttachment("glove", RigProperties.FitY) },
                new() { Kind = BindingKinds.Set, Target = PropertyRef.OfAttachment("glove", RigProperties.Layer) },
            ],
        });
        person.Validate();
        var big = new ModelVariant { Id = "big", Name = "Big", Base = "person", Parameters = { ["glove-size"] = 2 } };
        var resolved = ResolvedModel.From(person, big, Library(mixed));
        Assert.Equal(2, resolved.Attachments[0].Instance.FitX);
        Assert.Equal(2, resolved.Attachments[0].Instance.FitY);
        Assert.Equal(2, resolved.Attachments[0].Instance.Layer);
        Assert.Equal("glove-size", resolved.Parameters.BindingOfAttachment("glove", RigProperties.FitX)!.Value.Parameter.Id);
        Assert.Equal(1, ResolvedModel.From(person, null, Library(mixed)).Attachments[0].Instance.FitX);
        big.Attachments["glove"] = new() { FitX = .5f };
        var overridden = ResolvedModel.From(person, big, Library(mixed));
        Assert.Equal(.5f, overridden.Attachments[0].Instance.FitX);
        Assert.Equal(2, overridden.Attachments[0].Instance.FitY);
        person.Parameters[^1].Bindings.Add(new() { Target = PropertyRef.OfAttachment("hat", RigProperties.FitX) });
        Assert.Contains("no attachment 'hat'", Assert.Throws<InvalidDataException>(person.Validate).Message);
        person.Parameters[^1].Bindings[^1] = new() { Target = PropertyRef.OfAttachment("glove", "girth") };
        Assert.Contains("no attachment property", Assert.Throws<InvalidDataException>(person.Validate).Message);
    }
}
