using App2d.Core.Shapes;
using App2d.Core.Characters;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Json;
using System.Numerics;
using System.Text.Json;

namespace App2d.Tests.Rendering;

public sealed class UvRectTests
{
    [Fact]
    public void CropAndFullImageOverridesUseConcreteRectangleJson()
    {
        var crop = AxisAlignedRectangle2D.Builder.CreateUvRect(.5f, 0, .5f, 1);
        var materials = new Dictionary<string, RenderMaterialDefinition2D>
        {
            ["sheet"] = new() { Texture = "sheet.png", Source = crop }
        };
        var inherited = new RenderMaterialDefinition2D { Asset = "sheet" };
        Assert.Equal(crop, inherited.Resolve(materials).Source);
        var full = inherited.WithOverride(new() { Source = AxisAlignedRectangle2D.UnitRect });
        var json = JsonSerializer.Serialize(full, AuthoredJson.Options);
        using var document = JsonDocument.Parse(json);
        var source = document.RootElement.GetProperty("source");
        Assert.False(source.TryGetProperty("kind", out _));
        Assert.Equal(0, source.GetProperty("min").GetProperty("x").GetSingle());
        Assert.Equal(1, source.GetProperty("max").GetProperty("y").GetSingle());
        var restored = JsonSerializer.Deserialize<RenderMaterialDefinition2D>(json, AuthoredJson.Options)!.Resolve(materials);
        Assert.Equal(AxisAlignedRectangle2D.UnitRect, restored.Source);
        Assert.Equal("sheet.png", restored.Texture);
        Assert.Equal(Vector2.Zero, restored.Source!.Value.Min);
        Assert.Equal(Vector2.One, restored.Source.Value.Size);
    }

    [Theory]
    [InlineData(-.1f, 0, 1, 1)]
    [InlineData(0, 0, 1.1f, 1)]
    [InlineData(0, 0, 1, 1.1f)]
    [InlineData(0, 0, 0, 1)]
    [InlineData(0, 0, 1, 0)]
    [InlineData(.8f, 0, .5f, 1)]
    [InlineData(0, 0, .000005f, 1)]
    [InlineData(float.NaN, 0, 1, 1)]
    [InlineData(0, 0, float.PositiveInfinity, 1)]
    public void StillAndAnimatedSourcesRejectInvalidUvBounds(float minX, float minY, float maxX, float maxY)
    {
        var invalid = AxisAlignedRectangle2D.UnitRect with { Min = new(minX, minY), Max = new(maxX, maxY) };
        var material = new RenderMaterialDefinition2D { Texture = "sheet.png", Source = invalid };
        Assert.Contains("material.source", Assert.Throws<InvalidDataException>(() => material.Validate("material")).Message);
        var frames = new TextureAnimation { Frames = [new() { Texture = "sheet.png", Source = invalid }] };
        Assert.Contains("texture frame source", Assert.Throws<InvalidDataException>(() => frames.Compile()).Message);
    }
}
