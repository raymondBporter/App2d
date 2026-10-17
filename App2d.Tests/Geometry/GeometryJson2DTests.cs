using App2d.CharacterSeeds;
using App2d.Core.Characters.Authored;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Operations.Bounds;
using App2d.Core.Geometry.Operations.Distance;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Shapes;
using App2d.Core.Spatial;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace App2d.Tests.Geometry;

public sealed class GeometryJson2DTests
{
    private sealed record GeometryDocument(IGeometryValue2D Geometry);

    [Fact]
    public void DrawingEditorsUseLibraryGeometryAndOneJsonKindWithoutCharacterPresets()
    {
        foreach (var kind in GeometryJson2D.DrawableKinds)
        {
            var geometry = GeometryJson2D.CreateDefault(kind);
            Assert.Equal(kind, geometry.Kind);
            using var json = JsonDocument.Parse(geometry.ToJson());
            Assert.Single(json.RootElement.EnumerateObject(), p => p.Name == "kind");
            var model = ModelAuthoring.Empty("geometry-test", "Geometry test");
            ModelAuthoring.AddControl(model, null, Vector3.Zero, "root");
            ModelAuthoring.AddControl(model, "root", Vector3.UnitY, "tip");
            var part = ModelAuthoring.AddPart(model, geometry, "root", geometry is ICurve2D ? "tip" : null);
            var saved = CharacterModel.FromJson(model.ToJson());
            Assert.Equal(geometry.ToJson(), saved.Parts.Single().Geometry!.ToJson());
            Assert.Equal(part.Id, saved.Parts.Single().Id);
            using var modelJson = JsonDocument.Parse(saved.ToJson());
            Assert.False(modelJson.RootElement.GetProperty("parts")[0].TryGetProperty("kind", out _));
            var resolved = ResolvedModel.From(saved);
            var drawing = new PuppetDrawing();
            drawing.Build(resolved, PoseEvaluator.Rest(resolved));
            Assert.True(drawing.Mesh.Count > 0, kind);
        }
    }

    [Fact]
    public void OneTaggedFieldRoundTripsShapesAndCurves()
    {
        IGeometryValue2D[] definitions =
        [
            new Circle2D { Center = new(1, 2), Radius = 3 }.Validated(),
            Rectangle2D.Builder.FromSize(2, 3),
            AxisAlignedRectangle2D.Builder.FromSize(2, 3),
            new SimplePolygon2D { Vertices = [new(0, 0), new(3, 0), new(3, 1), new(1, 1), new(1, 3), new(0, 3)] }.Validated(),
            new LineSegmentCurve2D { Start = new(1, 2), End = new(3, 4) }.Validated(),
            new PolylineCurve2D { Points = [new(0, 0), new(1, 2), new(2, 0)] }.Validated()
        ];

        foreach (var definition in definitions)
        {
            var json = JsonSerializer.Serialize(new GeometryDocument(definition), GeometryJson2D.JsonOptions);
            var restored = JsonSerializer.Deserialize<GeometryDocument>(json, GeometryJson2D.JsonOptions);
            Assert.Equal(definition.ToJson(), restored?.Geometry.ToJson());
            Assert.Equal(definition.ToJson(), GeometryJson2D.FromJson(definition.ToJson()).ToJson());
        }
    }

    [Fact]
    public void PolylinePreservesEachAuthoredPoint()
    {
        var curve = PolylineCurve2D.Builder.Create([new(0, 0), new(1, 2), new(2, 0)]);
        Assert.Equal(new System.Numerics.Vector2(1, 2), curve.Evaluate(.5f));
        Assert.Equal(new System.Numerics.Vector2(2, 0), curve.Evaluate(1));
        Assert.IsType<PolylineCurve2D>(curve);
    }

    [Fact]
    public void ConcavePolygonKeepsItsNotchForContainmentDistanceAndBounds()
    {
        var polygon = SimplePolygon2D.Builder.Create([
            new(0, 0), new(3, 0), new(3, 1), new(1, 1), new(1, 3), new(0, 3)
        ]);

        Assert.True(polygon.ContainsPoint(new(0.5f, 2.5f)));
        Assert.False(polygon.ContainsPoint(new(2.5f, 2.5f)));
        Assert.Equal(5f, polygon.Area, 5);
        Assert.Equal(new System.Numerics.Vector2(3, 3), ShapeBounds2D.Calculate(polygon).Max);
        Assert.Equal(1.5f, ShapeDistance2D.Distance(new System.Numerics.Vector2(2.5f, 2.5f), polygon), 5);
        Assert.Equal(0f, ShapeDistance2D.Distance(new System.Numerics.Vector2(.5f, 2.5f), polygon), 5);
        Assert.True(ShapeDistance2D.Distance(polygon, Circle2D.Builder.Create(.25f, new(2.5f, 2.5f))) > 1f);
        Assert.True(SpatialRaycast2D.TryIntersect(new Ray2D(new(.5f, .5f), Vector2.UnitX),
            new SpatialObject2D(polygon), 10, out var exit));
        Assert.Equal(2.5f, exit.Distance, 5);
        Assert.True(SpatialRaycast2D.TryIntersect(new Ray2D(new(2, 2), -Vector2.UnitX),
            new SpatialObject2D(polygon), 10, out var notch));
        Assert.Equal(1f, notch.Distance, 5);
    }

    [Fact]
    public void ExistingShapeAndCurveJsonCanBeReadThroughTheSharedRoot()
    {
        var shape = new Capsule2D { Start = new(0, 0), End = new(1, 0), Radius = .5f }.Validated();
        var curve = new Arc2D { Center = new(0, 0), Radius = 2, StartAngleRadians = 0, SweepAngleRadians = 1 }.Validated();

        Assert.IsType<Capsule2D>(GeometryJson2D.FromJson(shape.ToJson()));
        Assert.IsType<Arc2D>(GeometryJson2D.FromJson(curve.ToJson()));
    }

    [Fact]
    public void ModelRequiresTypedParts()
    {
        var model = PersonTemplate.Model();
        var typed = model.ToJson();
        Assert.Contains("\"geometry\"", typed);
        Assert.DoesNotContain("\"editorKind\"", typed);
        Assert.Equal(typed, CharacterModel.FromJson(typed).ToJson());
        Assert.IsType<SimplePolygon2D>(CharacterModel.FromJson(typed).Parts.Single(part => part.Id == "body").Geometry);
        var missing = JsonNode.Parse(typed)!.AsObject();
        missing["parts"]![0]!.AsObject().Remove("geometry");
        Assert.Throws<InvalidDataException>(() => CharacterModel.FromJson(missing.ToJsonString()));
        var oldField = JsonNode.Parse(typed)!.AsObject();
        oldField["parts"]![0]!["kind"] = "box";
        Assert.Throws<JsonException>(() => CharacterModel.FromJson(oldField.ToJsonString()));
    }

    [Fact]
    public void PropKeepsTypedGeometryAndStrokeDepth()
    {
        var prop = new PropAsset
        {
            Id = "test-prop",
            Name = "Test prop",
            Shapes =
            [
                new PropShape { Geometry = new PolylineCurve2D { Points = [new(0, 0), new(.5f, 1), new(1, 0)] }.Validated(), Depths = [.1f, .2f, .3f], Material = new() { Fill = "#c8b18a", Outline = new() } },
                new PropShape { Geometry = new SimplePolygon2D { Vertices = [new(0, 0), new(2, 0), new(2, 1), new(1, 1), new(1, 2), new(0, 2)] }.Validated(), Material = new() { Fill = "#c8b18a", Outline = new() } }
            ]
        };
        var typed = prop.ToJson();
        Assert.Contains("\"kind\": \"polyline\"", typed);
        Assert.Contains("\"kind\": \"simple-polygon\"", typed);
        var restored = PropAsset.FromJson(typed);
        Assert.Equal(typed, restored.ToJson());
        Assert.Equal([.1f, .2f, .3f], restored.Shapes[0].Points.Select(point => point.Z));
        Assert.IsType<PolylineCurve2D>(restored.Shapes[0].Geometry);
        Assert.IsType<SimplePolygon2D>(restored.Shapes[1].Geometry);
        var oldField = JsonNode.Parse(typed)!.AsObject();
        oldField["shapes"]![0]!["points"] = new JsonArray();
        Assert.Throws<JsonException>(() => PropAsset.FromJson(oldField.ToJsonString()));
    }
}
