using App2d.CharacterSeeds;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Editing;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Rendering;
using App2d.Core.Shapes;
using System.Numerics;
using System.Text.Json;

namespace App2d.Tests.Authored;

public sealed class RenderMaterialDefinition2DTests
{
    [Fact]
    public void StrokeWidthInheritsFromNamedMaterialAndSurvivesVariantEditing()
    {
        var model = TestModels.Creature();
        model.Materials["stroke"] = new() { StrokeWidth = .12f, Fill = "#123456" };
        model.Parts.Single(p => p.Id == "upper-arm").Material = new() { Asset = "stroke" };
        var part = ResolvedModel.From(model).Parts.Single(p => p.Id == "upper-arm");
        Assert.Equal(.12f, part.RenderMaterial.StrokeWidth);
        var variant = new ModelVariant { Id = "thick", Name = "Thick", Base = model.Id };
        ModelAuthoring.EditPartOverride(variant, part, p => p.Material = p.RenderMaterial with { StrokeWidth = .3f });
        Assert.False(variant.Parts[part.Id].IsEmpty);
        var restored = ModelVariant.FromJson(variant.ToJson());
        var changed = ResolvedModel.From(model, restored).Parts.Single(p => p.Id == part.Id);
        Assert.Equal(.3f, changed.RenderMaterial.StrokeWidth);
        Assert.Equal(part.Geometry, changed.Geometry);
        Assert.Equal(part.ScaleX, changed.ScaleX);
        Assert.Equal(part.ScaleY, changed.ScaleY);
        restored.Parts[part.Id].Material = new() { StrokeWidth = null };
        Assert.Equal(.12f, ResolvedModel.From(model, restored).Parts.Single(p => p.Id == part.Id).RenderMaterial.StrokeWidth);
    }

    [Fact]
    public void NamedSurfacesInheritFieldsAndSharePaintWhileAllowingAnExplicitClear()
    {
        var model = CharacterModel.Load(Path.Combine(TestModels.AuthoredRoot, "models", "person.json"));
        var shared = model.Materials["spotted-hide"];
        var material = new RenderMaterialDefinition2D { Asset = "spotted-hide", Fill = "#123456" };
        var resolved = material.Resolve(model.Materials);
        Assert.Same(shared.Paint, resolved.Paint);
        Assert.Equal("#123456", resolved.Fill);
        Assert.Equal("spotted-hide", resolved.Asset);
        Assert.Null(material.Paint);
        Assert.Empty((material with { Paint = [] }).Resolve(model.Materials).Paint!);

        var different = resolved.WithOverride(new() { Asset = "plain" });
        Assert.Equal("plain", different.Asset);
        Assert.Null(different.Paint);
        Assert.Null(different.Fill);
    }

    [Fact]
    public void NamedSurfacesRejectMissingReferencesAndCycles()
    {
        var model = PersonTemplate.Model();
        model.Parts.Single(p => p.Id == "body").Material = new() { Asset = "missing" };
        Assert.Contains("Unknown material 'missing'", Assert.Throws<InvalidDataException>(model.Validate).Message);
        model.Materials["missing"] = new() { Asset = "other" };
        model.Materials["other"] = new() { Asset = "missing" };
        Assert.Contains("cycle", Assert.Throws<InvalidDataException>(model.Validate).Message);
    }

    [Theory]
    [InlineData("cinder")]
    [InlineData("club-caveman-build")]
    [InlineData("shield-defender-build")]
    public void SharedSpottedMaterialDrawsExactlyLikeInlinePaint(string id)
    {
        var model = CharacterModel.Load(Path.Combine(TestModels.AuthoredRoot, "models", "person.json"));
        var variant = ModelVariant.FromJson(File.ReadAllText(Path.Combine(TestModels.AuthoredRoot, "variants", id + ".json")));
        var resolved = ResolvedModel.From(model, variant);
        var body = resolved.Parts.Single(p => p.Id == "body");
        Assert.Same(model.Materials["spotted-hide"].Paint, body.RenderMaterial.Paint);
        Assert.NotNull(body.RenderMaterial.Outline);
        Assert.Null(variant.Parts["body"].Material!.Paint);
        Assert.DoesNotContain("\"points\"", variant.ToJson());
        var inline = ModelVariant.FromJson(variant.ToJson());
        inline.Parts["body"].Material = body.RenderMaterial with { Asset = null };
        var expanded = ResolvedModel.From(model, inline);
        var clip = MotionClip.FromJson(File.ReadAllText(Path.Combine(TestModels.AuthoredRoot, "animations", "person-death.json")));
        foreach (var time in new[] { 0, .6f })
        {
            var sharedDrawing = new PuppetDrawing();
            var inlineDrawing = new PuppetDrawing();
            sharedDrawing.Build(resolved, PoseEvaluator.Sample(resolved, clip, time));
            inlineDrawing.Build(expanded, PoseEvaluator.Sample(expanded, clip, time));
            Assert.Equal(inlineDrawing.Mesh.Vertices.ToArray(), sharedDrawing.Mesh.Vertices.ToArray());
        }
    }

    [Fact]
    public void VariantMaterialSelectionAndPaintClearSurviveSerialization()
    {
        var model = CharacterModel.Load(Path.Combine(TestModels.AuthoredRoot, "models", "person.json"));
        var variant = new ModelVariant { Id = "surface-test", Name = "Surface test", Base = model.Id };
        ModelAuthoring.EditPartOverride(variant, ResolvedModel.From(model).Parts.Single(p => p.Id == "body"),
            p => p.Material = new() { Asset = "spotted-hide" });
        var body = ResolvedModel.From(model, variant).Parts.Single(p => p.Id == "body");
        ModelAuthoring.EditPartOverride(variant, body, p => p.Material = p.RenderMaterial with { Fill = "#123456", Paint = [] });
        variant = ModelVariant.FromJson(variant.ToJson());
        body = ResolvedModel.From(model, variant).Parts.Single(p => p.Id == "body");
        Assert.Equal("spotted-hide", variant.Parts["body"].Material!.Asset);
        Assert.Equal("#123456", body.RenderMaterial.Fill);
        Assert.Empty(body.RenderMaterial.Paint!);
        Assert.NotEmpty(model.Materials["spotted-hide"].Paint!);
    }

    [Fact]
    public void ModelWritesAndReadsTypedMaterials()
    {
        var model = PersonTemplate.Model();
        var json = JsonSerializer.Serialize(model, AuthoredJson.Options);
        var restored = CharacterModel.FromJson(json).ToJson();
        using var document = JsonDocument.Parse(restored);
        var body = document.RootElement
            .GetProperty("parts")
            .EnumerateArray()
            .Single(part => part.GetProperty("id").GetString() == "body");
        Assert.False(body.TryGetProperty("fill", out _));
        Assert.Equal("#d8e9db", body.GetProperty("material").GetProperty("fill").GetString());
        Assert.Equal(restored, CharacterModel.FromJson(restored).ToJson());
    }

    [Fact]
    public void PartMaterialControlsRenderedFillAndOutlineWithoutMutatingBaseOnOverride()
    {
        var part = new PuppetPart
        {
            A = "root",
            Face = "none",
            ScaleX = 1,
            ScaleY = 1,
            Geometry = Rectangle2D.Builder.FromSize(Vector2.One),
            Material = new()
            {
                Fill = "#123456",
                Outline = new()
                {
                    Color = "#abcdef",
                    Width = .05f
                }
            }
        };
        var drawing = new PuppetDrawing();
        drawing.Build("#000000", .02f, [part], _ => Vector3.Zero);
        var colors = drawing.Mesh.Vertices.ToArray().Select(vertex => vertex.Color).ToArray();
        Assert.Contains(ColorExtensions.FromHexRgb("#123456"), colors);
        Assert.Contains(ColorExtensions.FromHexRgb("#abcdef"), colors);

        var changed = ResolvedModel.Override(part, new PartOverride { Material = new() { Fill = "#654321" } });
        Assert.Equal("#123456", part.Material!.Fill);
        Assert.Equal("#654321", changed.Material!.Fill);
        Assert.Equal("#abcdef", changed.Material.Outline!.Color);

        var recolored = ResolvedModel.Override(part, new PartOverride { Material = new() { Outline = new() { Color = "#ff8800" } } });
        Assert.Equal("#abcdef", part.RenderMaterial.Outline?.Color);
        Assert.Equal("#ff8800", recolored.RenderMaterial.Outline?.Color);
    }

    [Fact]
    public void PropWritesMaterialsAndUsesThemForDrawing()
    {
        var prop = new PropAsset
        {
            Id = "material-prop",
            Name = "Material prop",
            Shapes =
            [
                new PropShape
                {
                    Geometry = new SimplePolygon2D { Vertices = [new(0, 0), new(1, 0), new(0, 1)] }.Validated(),
                    Material = new() { Fill = "#224466", Outline = new() { Color = "#ffeeaa", Width = .03f } }
                }
            ]
        };
        var json = prop.ToJson();
        using var document = JsonDocument.Parse(json);
        var shape = document.RootElement.GetProperty("shapes")[0];
        Assert.False(shape.TryGetProperty("fill", out _));
        Assert.Equal("#224466", shape.GetProperty("material").GetProperty("fill").GetString());
        Assert.Equal(json, PropAsset.FromJson(json).ToJson());

        var drawing = new PuppetDrawing();
        drawing.AddProp(prop, new Frame3D(Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ));
        var colors = drawing.Mesh.Vertices.ToArray().Select(vertex => vertex.Color).ToArray();
        Assert.Contains(ColorExtensions.FromHexRgb("#224466"), colors);
        Assert.Contains(ColorExtensions.FromHexRgb("#ffeeaa"), colors);

        var solid = PropGeometry.Extrude([new(0, 0), new(1, 0), new(0, 1)], .1f, "#224466");
        solid.Material = solid.RenderMaterial with
        {
            Outline = solid.RenderMaterial.Outline! with
            {
                Color = "#ff8800"
            }
        };
        Assert.Equal("#ff8800", solid.Material!.Outline!.Color);
        solid.Material = solid.RenderMaterial with
        {
            Outline = null
        };
        Assert.Null(solid.Material.Outline);
    }

    [Fact]
    public void UndoRestoresTypedGeometryAndMaterialWithoutChangingTheSnapshot()
    {
        var model = CharacterModel.FromJson(PersonTemplate.Model().ToJson());
        var document = AssetDocuments.Of(model, null);
        var before = document.Serialize();
        document.Edit(() =>
        {
            var part = document.Asset.Parts.Single(part => part.Id == "body");
            part.Material = part.RenderMaterial with
            {
                Fill = "#123456"
            };
        });
        document.Undo();
        Assert.Equal(before, document.Serialize());
    }

    [Fact]
    public void PropUndoRestoresPointsDerivedFromTypedGeometry()
    {
        var prop = new PropAsset
        {
            Id = "undo-prop",
            Name = "Undo prop",
            Shapes = [new()
            {
                Geometry = new LineSegmentCurve2D { Start = new(0, 0), End = new(1, 0) }.Validated(),
                Depths = [.1f, .2f], Material = new() { Fill = "#224466", Outline = new() }
            }]
        };
        prop.Validate();
        var document = AssetDocuments.Of(prop, null);
        document.Edit(() => document.Asset.Shapes[0].Material = document.Asset.Shapes[0].RenderMaterial with { StrokeWidth = .2f });
        document.Undo();
        Assert.Equal([.1f, .2f], document.Asset.Shapes[0].Points.Select(p => p.Z));
    }
}
