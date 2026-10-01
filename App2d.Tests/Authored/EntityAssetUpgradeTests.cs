using App2d.Core.Characters.Authored;
using App2d.Core.Geometry;
using App2d.Core.Shapes;
using System.Numerics;
using System.Text.Json;

namespace App2d.Tests.Authored;

public sealed class EntityAssetUpgradeTests
{
    private const string Version1 = """
        {"format":"app2d-entity","version":1,"id":"old","name":"Old","model":"m","motionSet":"s",
         "movement":{"width":1,"height":2,"offsetX":0.25},
         "guard":{"prop":"shield","width":1,"height":1.5},
         "actions":[{"id":"attack","role":"attack",
           "projectile":{"width":0.4,"height":0.2},
           "hits":[{"id":"thrust","shape":"capsule","width":0.8,"height":0.2},{"id":"burst","shape":"circle","width":0.4},{"id":"swing","width":0.5,"height":0.3},{"id":"poke"}]}]}
        """;

    [Fact]
    public void VersionOneGeometryBecomesShapeDefinitions()
    {
        var upgraded = EntityAssetUpgrade.ToCurrent(Version1);
        using var document = JsonDocument.Parse(upgraded);
        Assert.Equal(EntityAsset.CurrentVersion, document.RootElement.GetProperty("version").GetInt32());
        Assert.False(document.RootElement.GetProperty("movement").TryGetProperty("width", out _));
        Assert.Equal("rectangle", document.RootElement.GetProperty("movement").GetProperty("shape").GetProperty("kind").GetString());

        var entity = AuthoredAsset.Parse<EntityAsset>(upgraded, "entity");
        Near(new Rect2D(new(-.25f, 0), new(.75f, 2)), ShapeBounds2D.Calculate(entity.Movement.Shape.Build()));
        Near(new Rect2D(new(-.5f, -.75f), new(.5f, .75f)), ShapeBounds2D.Calculate(entity.Guard!.Shape.Build()));
        var action = Assert.Single(entity.Actions);
        Near(new Rect2D(new(-.2f, -.1f), new(.2f, .1f)), ShapeBounds2D.Calculate(action.Projectile!.Shape.Build()));
        var thrust = Assert.IsType<Capsule2D>(action.Hits[0].Shape.Build());
        Assert.Equal(-.3f, thrust.Start.X, 5); Assert.Equal(.3f, thrust.End.X, 5); Assert.Equal(0f, thrust.End.Y, 5); Assert.Equal(.1f, thrust.Radius, 5);
        Assert.Equal(.2f, Assert.IsType<Circle2D>(action.Hits[1].Shape.Build()).Radius, 5);
        Near(new Rect2D(new(-.25f, -.15f), new(.25f, .15f)), ShapeBounds2D.Calculate(action.Hits[2].Shape.Build()));
        Near(new Rect2D(new(-.15f, -.15f), new(.15f, .15f)), ShapeBounds2D.Calculate(action.Hits[3].Shape.Build()));
    }

    [Fact]
    public void CurrentFilesPassThroughUntouchedAndEveryShippedEntityLoads()
    {
        var folder = Path.Combine(TestModels.AuthoredRoot, "entities");
        var hero = File.ReadAllText(Path.Combine(folder, "hero.json"));
        Assert.Same(hero, EntityAssetUpgrade.ToCurrent(hero));
        foreach (var path in Directory.GetFiles(folder, "*.json"))
        {
            var entity = EntityAsset.FromJson(File.ReadAllText(path));
            Assert.Equal(EntityAsset.CurrentVersion, entity.Version);
            Assert.IsAssignableFrom<IConvexShape2D>(entity.Movement.Shape.Build());
        }
        Assert.Throws<InvalidDataException>(() => EntityAssetUpgrade.ToCurrent("[]"));
        Assert.Throws<InvalidDataException>(() => EntityAssetUpgrade.ToCurrent("not json"));
    }

    private static void Near(Rect2D expected, Rect2D actual) =>
        Assert.True(Vector2.Distance(expected.Min, actual.Min) < 1e-5f && Vector2.Distance(expected.Max, actual.Max) < 1e-5f, $"Expected {expected}, got {actual}.");
}
