using App2d.Core.Characters.Authored;
using App2d.Core.Geometry.Operations.Bounds;
using App2d.Core.Physics;
using App2d.Core.Shapes;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace App2d.Tests.Authored;

public sealed class PhysicalPartAssetTests
{
    private static string DumbbellJson => File.ReadAllText(Path.Combine(TestAssetPath.AuthoredRoot, "parts", "dumbbell.json"));

    [Fact]
    public void DumbbellLoadsItsCompositePartsAndPhysicsSettingsFromJsonAndRoundTrips()
    {
        var asset = Part.FromJson(DumbbellJson);
        var shape = Assert.IsType<CompositeShape2D>(App2d.Core.Characters.Geometry.PartShapes2D.Collision(asset));
        Assert.Collection(shape.Parts.ToArray(),
            part => Assert.Equal(Rectangle2D.Builder.Create(new(-36, -9), new(36, 9)), Assert.IsType<Rectangle2D>(part)),
            part => Assert.Equal(Circle2D.Builder.Create(16, new(-42, 0)), Assert.IsType<Circle2D>(part)),
            part => Assert.Equal(Circle2D.Builder.Create(16, new(42, 0)), Assert.IsType<Circle2D>(part)));
        var bounds = ShapeBounds2D.Calculate(shape);
        Assert.Equal(new Vector2(-58, -16), bounds.Min);
        Assert.Equal(new Vector2(58, 16), bounds.Max);
        Assert.Equal(BodyMotionType2D.Dynamic, asset.Physics!.MotionType);
        Assert.Equal(2f, asset.Physics!.Mass);
        Assert.Equal(1800f, asset.Physics!.MomentOfInertia);
        Assert.False(asset.Physics!.FreezeRotation);
        Assert.Equal(.4f, asset.Physics!.Friction);
        Assert.Equal(.15f, asset.Physics!.Restitution);
        Assert.Equal(asset.ToJson(), Part.FromJson(asset.ToJson()).ToJson());
    }

    [Fact]
    public void InvalidCompositeChildrenAreRejectedAtTheAssetBoundary()
    {
        var json = JsonNode.Parse(DumbbellJson)!;
        json["geometry"]![1]!["geometry"]!["radius"] = 0;

        Assert.Throws<ArgumentOutOfRangeException>(() => Part.FromJson(json.ToJsonString()));
    }

    [Fact]
    public void MisspelledPhysicsSettingsAreRejected()
    {
        var json = JsonNode.Parse(DumbbellJson)!;
        json["physics"]!["fricton"] = .4f;

        Assert.Throws<JsonException>(() => Part.FromJson(json.ToJsonString()));
    }

    [Fact]
    public void CatalogReportsBrokenPhysicsPropsWithoutHidingValidOnes()
    {
        var root = Path.Combine(Path.GetTempPath(), $"physics-props-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "parts"));
            File.WriteAllText(Path.Combine(root, "parts", "dumbbell.json"), DumbbellJson);
            var invalid = JsonNode.Parse(DumbbellJson)!;
            invalid["id"] = "broken-prop";
            invalid["physics"]!["mass"] = 0;
            File.WriteAllText(Path.Combine(root, "parts", "broken.json"), invalid.ToJsonString());

            var catalog = AuthoredCatalog.Load(root);

            Assert.Equal("dumbbell", Assert.Single(catalog.Parts).Key);
            Assert.Contains("broken.json", Assert.Single(catalog.Errors));
            Assert.EndsWith("dumbbell.json", catalog.PathOf("dumbbell"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
