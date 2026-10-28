using App2d.Core.Characters;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Json;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

namespace App2d.Tests.IO;

public sealed class JsonProfilesTests
{
    [Fact]
    public void SharedContractsAreReadOnlyIndentedAndUsedByTheExistingEntryPoints()
    {
        foreach (var property in typeof(JsonProfiles).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
        {
            var options = Assert.IsType<JsonSerializerOptions>(property.GetValue(null));
            Assert.Same(options, property.GetValue(null));
            Assert.True(options.IsReadOnly);
            Assert.True(options.WriteIndented);
            Assert.Throws<InvalidOperationException>(() => options.WriteIndented = false);
        }
        Assert.Same(JsonProfiles.Authored, AuthoredJson.Options);
        Assert.Same(JsonProfiles.Geometry, GeometryJson2D.JsonOptions);
        Assert.Same(JsonProfiles.Puppet, PuppetDefinition.JsonOptions);
    }

    [Fact]
    public void ContentAndOwnedSchemasRetainTheirDifferentInputContracts()
    {
        Assert.Equal(3, JsonSerializer.Deserialize<Counter>("""{"COUNT":"3","future":true}""", JsonProfiles.Web)!.Count);
        Assert.Equal(3, JsonSerializer.Deserialize<Counter>("""{"COUNT":3,"future":true}""", JsonProfiles.CaseInsensitive)!.Count);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Counter>("""{"count":"3"}""", JsonProfiles.CaseInsensitive));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Counter>("""{"COUNT":3}""", JsonProfiles.StrictCamelCase));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Counter>("""{"count":"3"}""", JsonProfiles.StrictCamelCase));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Counter>("""{"count":3,"future":true}""", JsonProfiles.StrictCamelCase));
        Assert.Equal(3, JsonSerializer.Deserialize<Counter>("""{"count":3}""", JsonProfiles.StrictCamelCase)!.Count);
    }

    [Fact]
    public void WorldDocumentsWriteReadableCoordinatesAndOmitOnlyAbsentValues()
    {
        var value = new WorldValue(new(12.5f, -3), null);
        var json = JsonSerializer.Serialize(value, JsonProfiles.World);
        Assert.Contains('\n', json);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(12.5f, document.RootElement.GetProperty("position").GetProperty("x").GetSingle());
        Assert.False(document.RootElement.TryGetProperty("optional", out _));
        Assert.Equal(value, JsonSerializer.Deserialize<WorldValue>(json, JsonProfiles.World));
    }

    private sealed record Counter { public int Count { get; init; } }
    private sealed record WorldValue(Vector2 Position, string? Optional);
}
