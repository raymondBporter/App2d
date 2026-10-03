using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Editing;
using App2d.Core.Rendering;
using App2d.Core.Rendering.Characters;
using System.Numerics;
using System.Text.Json;

namespace App2d.Tests.Authored;

public sealed class RenderMaterialDefinition2DTests
{
    [Fact]
    public void ModelWritesMaterialsAndLegacyAppearanceStillLoads()
    {
        var model = PersonTemplate.Model();
        var oldJson = JsonSerializer.Serialize(model, AuthoredJson.Options);
        var migrated = CharacterModel.FromJson(oldJson).ToJson();
        using var document = JsonDocument.Parse(migrated);
        var body = document.RootElement.GetProperty("parts").EnumerateArray()
            .Single(part => part.GetProperty("id").GetString() == "body");
        Assert.False(body.TryGetProperty("fill", out _));
        Assert.Equal("#d8e9db", body.GetProperty("material").GetProperty("fill").GetString());
        Assert.Equal(migrated, CharacterModel.FromJson(migrated).ToJson());
    }

    [Fact]
    public void PartMaterialControlsRenderedFillAndOutlineWithoutMutatingBaseOnOverride()
    {
        var part = new PuppetPart
        {
            A = "root", Face = "none", Width = 1, Height = 1,
            Material = new()
            {
                Fill = "#123456",
                Outline = new() { Color = "#abcdef", Width = .05f }
            }
        };
        var drawing = new PuppetDrawing();
        drawing.Build("#000000", .02f, [part], _ => Vector3.Zero);
        var colors = drawing.Mesh.Vertices.ToArray().Select(vertex => vertex.Color).ToArray();
        Assert.Contains(ColorExtensions.FromHexRgb("#123456"), colors);
        Assert.Contains(ColorExtensions.FromHexRgb("#abcdef"), colors);

        var changed = ResolvedModel.Override(part, new PartOverride { Fill = "#654321" });
        Assert.Equal("#123456", part.Material!.Fill);
        Assert.Equal("#654321", changed.Material!.Fill);
        Assert.Equal("#abcdef", changed.Material.Outline!.Color);

        var recolored = ResolvedModel.Override(part, new PartOverride { OutlineColor = "#ff8800" });
        Assert.Equal("#abcdef", part.OutlineColor);
        Assert.Equal("#ff8800", recolored.OutlineColor);
    }

    [Fact]
    public void PropWritesMaterialsAndUsesThemForDrawing()
    {
        var prop = new PropAsset
        {
            Id = "material-prop", Name = "Material prop",
            Shapes =
            [
                new PropShape
                {
                    Kind = "polygon", Points = [new(0, 0), new(1, 0), new(0, 1)],
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
        drawing.AddProp(prop, new SocketFrame(Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ));
        var colors = drawing.Mesh.Vertices.ToArray().Select(vertex => vertex.Color).ToArray();
        Assert.Contains(ColorExtensions.FromHexRgb("#224466"), colors);
        Assert.Contains(ColorExtensions.FromHexRgb("#ffeeaa"), colors);

        var solid = PropGeometry.Extrude([new(0, 0), new(1, 0), new(0, 1)], .1f, "#224466");
        solid.OutlineColor = "#ff8800";
        Assert.Equal("#ff8800", solid.Material!.Outline!.Color);
        solid.Outlined = false;
        Assert.Null(solid.Material.Outline);
    }

    [Fact]
    public void UndoRestoresTypedGeometryAndMaterialWithoutChangingTheSnapshot()
    {
        var model = CharacterModel.FromJson(PersonTemplate.Model().ToJson());
        var document = AssetDocuments.Of(model, null);
        var before = document.Serialize();
        document.Edit(() => document.Asset.Parts.Single(part => part.Id == "body").Fill = "#123456");
        document.Undo();
        Assert.Equal(before, document.Serialize());
    }
}
