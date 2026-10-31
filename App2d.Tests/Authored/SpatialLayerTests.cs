using App2d.CharacterStudio.Editing;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Meshes;
using App2d.Core.Mathematics;
using App2d.Core.Mathematics.Transforms;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;

namespace App2d.Tests.Authored;

public sealed class SpatialLayerTests
{
    private static Part Asset() => new()
    {
        Id = "curve-study", Name = "Spatial curve study",
        Geometry = [new()
        {
            SpatialCurve = new CubicBezier<Vector3f, float>(new(-1, 0, -2), new(-.5f, 1, 3), new(.5f, 1, -3), new(1, 0, 2)),
            Layer = -.3f, CurveSections = 32,
            Material = new() { Fill = "#fa8060", StrokeWidth = .03f }
        }]
    };

    [Fact]
    public void SpatialCurveAuthoringRoundTripsAndDrawsRealDepth()
    {
        var asset = Asset();
        var json = asset.ToJson();
        var restored = Part.FromJson(json);
        var original = asset.Geometry[0];
        var part = restored.Geometry[0];
        Assert.Contains("\"spatialCurve\"", json);
        Assert.Contains("\"layer\"", json);
        Assert.DoesNotContain("\"depth\"", json);
        Assert.Equal(original.SpatialCurve!.Evaluate(.25f), part.SpatialCurve!.Evaluate(.25f));

        var contour = PartContours2D.Contour(part, _ => Vector3.Zero);
        Assert.Equal(-2, contour[0].Z);
        Assert.Equal(2, contour[^1].Z);
        Assert.Equal(part.Layer, contour.LayerAt(0));
        Assert.Equal(0, PartPicking2D.Distance(part, _ => Vector3.Zero, contour[8]));

        var drawing = new PuppetDrawing();
        drawing.Build(restored);
        var before = drawing.Mesh.Vertices.ToArray();
        Assert.Contains(before, v => v.Position.Z < -1);
        Assert.Contains(before, v => v.Position.Z > 1);
        part.Layer += 8;
        drawing.Build(restored);
        var after = drawing.Mesh.Vertices.ToArray();
        Assert.Equal(before.Select(v => v.Position), after.Select(v => v.Position));
        Assert.All(after.Zip(before), pair => Assert.Equal(8, pair.First.Layer - pair.Second.Layer, 5));

        var spatial = new TriangleMeshBuilder3D<VertexPositionColorTexture, Color4b>((p, c, uv) => new(new(p.X, p.Y, p.Z), c, new(uv.X, uv.Y)));
        drawing.Mesh.CopySpatialTo(spatial);
        Assert.Equal(after.Select(v => v.Position), spatial.Vertices.ToArray().Select(v => v.Position));
    }

    [Fact]
    public void PhysicalDepthRotatesWhileLayerIsAnIndependentAttribute()
    {
        var contour = new PartContour([new(1, 2, 3), new(4, 5, -2)], layers: [.25f, -.5f]);
        var frame = Frame3D.FromRotation(new(0, MathF.PI / 2, 0), new(7, 8, 9));
        var transformed = contour.TransformedBy(frame);
        Assert.Equal(frame.TransformPoint(contour[0]), transformed[0]);
        Assert.Equal(frame.TransformPoint(contour[1]), transformed[1]);
        Assert.Equal(.25f, transformed.LayerAt(0));
        Assert.Equal(-.5f, transformed.LayerAt(1));
        var placement = LayeredFrame2D.Local(Vector2.One, 0, new(1, 2), 8);
        Assert.Equal(0, PartPlacement3D.Extend(placement).Origin.Z);
        Assert.Equal(PartPlacement3D.Lift(placement, new(.3f, .4f, .5f)), PartPlacement3D.Lift(placement with { Layer = -8 }, new(.3f, .4f, .5f)));
    }

    [Fact]
    public void SpatialCurveRejectsAmbiguousOrNonfiniteDefinitions()
    {
        var part = Asset().Geometry[0];
        Assert.Throws<InvalidDataException>(() => (part with { B = "other" }).Validate(_ => true));
        Assert.Throws<InvalidDataException>(() => (part with { Mesh = new() }).Validate(_ => true));
        Assert.Throws<InvalidDataException>(() => (part with { SpatialCurve = new QuadraticBezier<Vector3f, float>(new(float.NaN, 0, 0), default, default) }).Validate(_ => true));
    }
}
