using App2d.Core.Curves;
using App2d.Core.Geometry;
using App2d.Core.Shapes;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Collision.Queries;
using App2d.Core;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace App2d.Tests.Geometry;

public sealed class GeometryDefinition2DTests
{
    private sealed record GeometryDocument(GeometryDefinition2D Geometry);

    [Fact]
    public void OneTaggedFieldRoundTripsShapesAndCurves()
    {
        GeometryDefinition2D[] definitions =
        [
            new CircleShapeDefinition2D { Center = new(1, 2), Radius = 3 },
            new SimplePolygonShapeDefinition2D { Vertices = [new(0, 0), new(3, 0), new(3, 1), new(1, 1), new(1, 3), new(0, 3)] },
            new LineCurveDefinition2D { Start = new(1, 2), End = new(3, 4) },
            new PolylineCurveDefinition2D { Points = [new(0, 0), new(1, 2), new(2, 0)] }
        ];

        foreach (var definition in definitions)
        {
            var json = JsonSerializer.Serialize(new GeometryDocument(definition), GeometryDefinition2D.JsonOptions);
            var restored = JsonSerializer.Deserialize<GeometryDocument>(json, GeometryDefinition2D.JsonOptions);
            Assert.Equal(definition.ToGeometryJson(), restored?.Geometry.ToGeometryJson());
            Assert.Equal(definition.ToGeometryJson(), GeometryDefinition2D.FromJson(definition.ToGeometryJson()).ToGeometryJson());
        }
    }

    [Fact]
    public void PolylinePreservesEachAuthoredPoint()
    {
        var curve = new PolylineCurve2D([new(0, 0), new(1, 2), new(2, 0)]);
        Assert.Equal(new System.Numerics.Vector2(1, 2), curve.Evaluate(.5f));
        Assert.Equal(new System.Numerics.Vector2(2, 0), curve.Evaluate(1));
        Assert.IsType<PolylineCurveDefinition2D>(CurveDefinition2D.FromCurve(curve));
    }

    [Fact]
    public void ConcavePolygonKeepsItsNotchForContainmentDistanceAndBounds()
    {
        var polygon = new SimplePolygon2D([
            new(0, 0), new(3, 0), new(3, 1), new(1, 1), new(1, 3), new(0, 3)
        ]);

        Assert.True(polygon.ContainsPoint(new(0.5f, 2.5f)));
        Assert.False(polygon.ContainsPoint(new(2.5f, 2.5f)));
        Assert.Equal(5f, polygon.Area, 5);
        Assert.Equal(new System.Numerics.Vector2(3, 3), ShapeBounds2D.Calculate(polygon).Max);
        Assert.Equal(1.5f, ShapeDistance2D.Distance(new System.Numerics.Vector2(2.5f, 2.5f), polygon), 5);
        Assert.Equal(0f, ShapeDistance2D.Distance(new System.Numerics.Vector2(.5f, 2.5f), polygon), 5);
        Assert.True(ShapeDistance2D.Distance(polygon, new Circle2D(.25f, new(2.5f, 2.5f))) > 1f);
        Assert.True(RayIntersection2D.TryIntersect(new Ray2D(new(.5f, .5f), System.Numerics.Vector2.UnitX),
            new SpatialObject2D(polygon), 10, out var exit));
        Assert.Equal(2.5f, exit.Distance, 5);
        Assert.True(RayIntersection2D.TryIntersect(new Ray2D(new(2, 2), -System.Numerics.Vector2.UnitX),
            new SpatialObject2D(polygon), 10, out var notch));
        Assert.Equal(1f, notch.Distance, 5);
    }

    [Fact]
    public void ExistingShapeAndCurveJsonCanBeReadThroughTheSharedRoot()
    {
        var shape = new CapsuleShapeDefinition2D { Start = new(0, 0), End = new(1, 0), Radius = .5f };
        var curve = new ArcCurveDefinition2D { Center = new(0, 0), Radius = 2, StartAngleRadians = 0, SweepAngleRadians = 1 };

        Assert.IsType<CapsuleShapeDefinition2D>(GeometryDefinition2D.FromJson(shape.ToJson()));
        Assert.IsType<ArcCurveDefinition2D>(GeometryDefinition2D.FromJson(curve.ToJson()));
    }

    [Fact]
    public void ModelRequiresTypedParts()
    {
        var model = PersonTemplate.Model();
        var typed = model.ToJson();
        Assert.Contains("\"geometry\"", typed);
        Assert.DoesNotContain("\"editorKind\"", typed);
        Assert.Equal(typed, CharacterModel.FromJson(typed).ToJson());
        Assert.IsType<SimplePolygonShapeDefinition2D>(CharacterModel.FromJson(typed).Parts.Single(part => part.Id == "body").Geometry);
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
                new PropShape { Geometry = new PolylineCurveDefinition2D { Points = [new(0, 0), new(.5f, 1), new(1, 0)] }, Depths = [.1f, .2f, .3f], Material = new() { Fill = "#c8b18a", Outline = new() } },
                new PropShape { Geometry = new SimplePolygonShapeDefinition2D { Vertices = [new(0, 0), new(2, 0), new(2, 1), new(1, 1), new(1, 2), new(0, 2)] }, Material = new() { Fill = "#c8b18a", Outline = new() } }
            ]
        };
        var typed = prop.ToJson();
        Assert.Contains("\"kind\": \"polyline\"", typed);
        Assert.Contains("\"kind\": \"simple-polygon\"", typed);
        var restored = PropAsset.FromJson(typed);
        Assert.Equal(typed, restored.ToJson());
        Assert.Equal([.1f, .2f, .3f], restored.Shapes[0].Points.Select(point => point.Z));
        Assert.IsType<PolylineCurveDefinition2D>(restored.Shapes[0].Geometry);
        Assert.IsType<SimplePolygonShapeDefinition2D>(restored.Shapes[1].Geometry);
        var oldField = JsonNode.Parse(typed)!.AsObject();
        oldField["shapes"]![0]!["points"] = new JsonArray();
        Assert.Throws<JsonException>(() => PropAsset.FromJson(oldField.ToJsonString()));
    }
}
