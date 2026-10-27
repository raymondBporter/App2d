using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Geometry;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Shapes;
using System.Text.Json.Nodes;

namespace App2d.Tests.Authored;

public sealed class WorldPartConversionTests
{
    private const string Legacy = """
        { "format":"app2d-physics-prop", "version":1, "id":"test-body", "name":"Test body",
          "shape":{"kind":"circle", "center":{"x":12,"y":-7}, "radius":16}, "fill":"#123456",
          "body":{"motionType":"Dynamic", "mass":2, "momentOfInertia":1800, "freezeRotation":false, "friction":0.4},
          "hitVelocityScale":0.8, "hitAngularKick":4.5 }
        """;

    [Fact]
    public void OfflineConversionPreservesUnitsAndSettingsAndExplicitlyRetiresHitGains()
    {
        var part = WorldPartConversion.Convert(Legacy);
        Assert.Equal(1, part.Scale);
        var shape = Assert.IsType<Circle2D>(PartShapes2D.Collision(part));
        Assert.Equal(16, shape.Radius);
        Assert.Equal(new(12, -7), shape.Center);
        Assert.Equal(1800, part.Physics!.MomentOfInertia);
        Assert.Equal(.4f, part.Physics.Friction);
        Assert.DoesNotContain("hitVelocity", part.ToJson());
        Assert.DoesNotContain("hitAngular", part.ToJson());
        Assert.ThrowsAny<Exception>(() => Part.FromJson(Legacy));
        var invalid = JsonNode.Parse(Legacy)!.AsObject();
        invalid["unknown"] = 1;
        Assert.Throws<InvalidDataException>(() => WorldPartConversion.Convert(invalid.ToJsonString()));
    }

    [Fact]
    public void ScratchConversionReportsRemovedFieldsAndIsRepeatableWithoutChangingSource()
    {
        var root = Path.Combine(Path.GetTempPath(), "world-parts-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var output = Path.Combine(root, "output");
        Directory.CreateDirectory(Path.Combine(source, "physics-props"));
        var path = Path.Combine(source, "physics-props", "test-body.json");
        try
        {
            File.WriteAllText(path, Legacy);
            var result = FormatConversion.Convert(source, output);
            Assert.True(result.Converted, string.Join(";", result.Files));
            Assert.Equal(["hitVelocityScale", "hitAngularKick"], Assert.Single(result.Files).Dropped);
            Assert.Equal(Legacy, File.ReadAllText(path));
            Assert.Equal("test-body", Part.Load(Path.Combine(output, "parts", "test-body.json")).Id);
            Assert.StartsWith("unchanged", Assert.Single(FormatConversion.Convert(source, output).Files).Status);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void OldLevelObjectsKeepTheirPathSurfaceAndCollisionOnlyPayload()
    {
        var current = Part.Load(Path.Combine(TestAssetPath.StaticRoot, "levels", "cavern", "objects", "lift.json"));
        var json = JsonNode.Parse(current.ToJson())!.AsObject();
        var old = new JsonObject { ["format"] = "app2d-object", ["version"] = 1, ["id"] = "lift", ["name"] = "Lift" };
        foreach (var key in new[] { "physics", "surface", "motion" }) old[key] = json[key]!.DeepClone();
        old["collision"] = json["geometry"]![0]!["geometry"]!.DeepClone();
        var converted = WorldPartConversion.Convert(old.ToJsonString());
        Assert.Empty(converted.Geometry);
        Assert.Equal(current.Motion!.CreateMotion().Position(1.5), converted.Motion!.CreateMotion().Position(1.5));
        Assert.Equal(current.Surface, converted.Surface);
        Assert.Equal(current.Geometry[0].Geometry!.ToJson(), converted.Collision!.ToJson());
    }
}
