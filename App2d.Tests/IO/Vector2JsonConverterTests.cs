using App2d.Core.Characters;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Geometry.Shapes;
using App2d.Core.IO;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.Tests.IO;

public sealed class Vector2JsonConverterTests
{
    public static TheoryData<string> AuthoringOptions =>
    [
        nameof(IGeometryValue2D),
        nameof(IShape2D),
        nameof(ICurve2D),
        nameof(AuthoredJson.Options),
        nameof(PuppetDefinition)
    ];

    private static JsonSerializerOptions GetOptions(string optionsName) => optionsName switch
    {
        nameof(IGeometryValue2D) => GeometryJson2D.JsonOptions,
        nameof(IShape2D) => GeometryJson2D.JsonOptions,
        nameof(ICurve2D) => GeometryJson2D.JsonOptions,
        nameof(AuthoredJson.Options) => AuthoredJson.Options,
        nameof(PuppetDefinition) => PuppetDefinition.JsonOptions,
        _ => throw new ArgumentOutOfRangeException(nameof(optionsName), optionsName, null)
    };

    [Theory]
    [MemberData(nameof(AuthoringOptions))]
    public void VectorsAndVectorListsKeepTheExistingCoordinateFormat(string optionsName)
    {
        var options = GetOptions(optionsName);
        var vectors = new List<Vector2> { new(1.25f, -2.5f), Vector2.Zero };
        var json = JsonSerializer.Serialize(vectors, options);
        using var document = JsonDocument.Parse(json);
        var first = document.RootElement[0];
        Assert.Equal(["x", "y"], first.EnumerateObject().Select(property => property.Name));
        Assert.Equal(1.25f, first.GetProperty("x").GetSingle());
        Assert.Equal(-2.5f, first.GetProperty("y").GetSingle());
        Assert.Equal(vectors, JsonSerializer.Deserialize<List<Vector2>>(json, options));
        Assert.Equal(new Vector2(3, 4), JsonSerializer.Deserialize<Vector2>("{\"Y\":4,\"X\":3}", options));
    }

    [Fact]
    public void TheConverterCanBeRegisteredElsewhereAndRespectsNamingAndNumberPolicies()
    {
        var defaults = new JsonSerializerOptions
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            Converters = { new Vector2JsonConverter() }
        };
        Assert.Equal("{\"X\":1,\"Y\":2}", JsonSerializer.Serialize(new Vector2(1, 2), defaults));
        Assert.Equal(new Vector2(1, 0), JsonSerializer.Deserialize<Vector2>("{\"X\":1}", defaults));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Vector2>("{\"x\":1}", defaults));

        var quoted = new JsonSerializerOptions(defaults)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString
        };
        var json = JsonSerializer.Serialize(new Vector2(1.25f, -2.5f), quoted);
        Assert.Equal("{\"x\":\"1.25\",\"y\":\"-2.5\"}", json);
        Assert.Equal(new Vector2(1.25f, -2.5f), JsonSerializer.Deserialize<Vector2>(json, quoted));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[1,2]")]
    [InlineData("{\"x\":true,\"y\":2}")]
    [InlineData("{\"x\":\"invalid\",\"y\":2}")]
    [InlineData("{\"x\":1,\"y\":2,\"yy\":3}")]
    [InlineData("{\"x\":1")]
    public void StrictOptionsRejectMalformedCoordinatesAndUnknownMembers(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Vector2>(json, GeometryJson2D.JsonOptions));
    }

    [Fact]
    public void TolerantOptionsSkipNestedUnknownValuesAndDefaultMissingCoordinatesToZero()
    {
        Assert.Equal(new Vector2(0, 2), JsonSerializer.Deserialize<Vector2>(
            "{\"extra\":{\"nested\":[1,2,3]},\"y\":2}", new JsonSerializerOptions(AuthoredJson.Options) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip }));
    }
}
