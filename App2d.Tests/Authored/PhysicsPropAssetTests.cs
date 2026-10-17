using App2d.Core.Characters.Authored;
using App2d.Core.Geometry.Operations.Bounds;
using App2d.Core.Physics;
using App2d.Core.Shapes;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace App2d.Tests.Authored;

public sealed class PhysicsPropAssetTests
{
    private static string DumbbellJson => File.ReadAllText(Path.Combine(TestAssetPath.AuthoredRoot, "physics-props", "dumbbell.json"));

    [Fact]
    public void DumbbellLoadsItsCompositePartsAndPhysicsSettingsFromJsonAndRoundTrips()
    {
        var asset = PhysicsPropAsset.FromJson(DumbbellJson);
        var shape = Assert.IsType<CompositeShape2D>(asset.Shape);
        Assert.Collection(shape.Parts.ToArray(),
            part => Assert.Equal(Rectangle2D.Builder.Create(new(-36, -9), new(36, 9)), Assert.IsType<Rectangle2D>(part)),
            part => Assert.Equal(Circle2D.Builder.Create(16, new(-42, 0)), Assert.IsType<Circle2D>(part)),
            part => Assert.Equal(Circle2D.Builder.Create(16, new(42, 0)), Assert.IsType<Circle2D>(part)));
        var bounds = ShapeBounds2D.Calculate(shape);
        Assert.Equal(new Vector2(-58, -16), bounds.Min);
        Assert.Equal(new Vector2(58, 16), bounds.Max);
        Assert.Equal(BodyMotionType2D.Dynamic, asset.Body.MotionType);
        Assert.Equal(2f, asset.Body.Mass);
        Assert.Equal(1800f, asset.Body.MomentOfInertia);
        Assert.False(asset.Body.FreezeRotation);
        Assert.Equal(.4f, asset.Body.Friction);
        Assert.Equal(.15f, asset.Body.Restitution);
        Assert.Equal(.8f, asset.HitVelocityScale);
        Assert.Equal(4.5f, asset.HitAngularKick);
        Assert.Equal(asset.ToJson(), PhysicsPropAsset.FromJson(asset.ToJson()).ToJson());
    }

    [Fact]
    public void InvalidCompositeChildrenAreRejectedAtTheAssetBoundary()
    {
        var json = JsonNode.Parse(DumbbellJson)!;
        json["shape"]!["parts"]![1]!["radius"] = 0;

        Assert.Throws<ArgumentOutOfRangeException>(() => PhysicsPropAsset.FromJson(json.ToJsonString()));
    }

    [Fact]
    public void MisspelledPhysicsSettingsAreRejected()
    {
        var json = JsonNode.Parse(DumbbellJson)!;
        json["body"]!["fricton"] = .4f;

        Assert.Throws<JsonException>(() => PhysicsPropAsset.FromJson(json.ToJsonString()));
    }

    [Fact]
    public void CatalogReportsBrokenPhysicsPropsWithoutHidingValidOnes()
    {
        var root = Path.Combine(Path.GetTempPath(), $"physics-props-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "physics-props"));
            File.WriteAllText(Path.Combine(root, "physics-props", "dumbbell.json"), DumbbellJson);
            var invalid = JsonNode.Parse(DumbbellJson)!;
            invalid["id"] = "broken-prop";
            invalid["body"]!["mass"] = 0;
            File.WriteAllText(Path.Combine(root, "physics-props", "broken.json"), invalid.ToJsonString());

            var catalog = AuthoredCatalog.Load(root);

            Assert.Equal("dumbbell", Assert.Single(catalog.PhysicsProps).Key);
            Assert.Contains("broken.json", Assert.Single(catalog.Errors));
            Assert.EndsWith("dumbbell.json", catalog.PathOf("dumbbell"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
