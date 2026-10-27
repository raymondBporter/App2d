using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Geometry;
using App2d.Core.Mathematics;
using App2d.Core.Mathematics.Transforms;
using System.Numerics;
using System.Text.Json;

namespace App2d.Tests.Authored;

public sealed class PartConsolidationTests
{
    private static Vector3 Point(Microsoft.Xna.Framework.Vector3 p) => new(p.X, p.Y, p.Z);
    private const string Legacy = """
        { "format":"app2d-prop", "version":3, "id":"tool", "name":"Tool", "scale":1.7,
          "grip":{"x":0.2,"y":-0.1,"z":0.03}, "tip":{"x":1,"y":0.2,"z":0.4},
          "secondGrip":{"x":0.1,"y":0.2,"z":0.3}, "muzzle":{"x":0.8,"y":0.1,"z":0.2},
          "solids":[{"vertices":[{"x":0,"y":0,"z":0.1},{"x":0.4,"y":0,"z":0.1},{"x":0,"y":0.3,"z":0.1}],
                      "triangles":[0,1,2],"material":{"fill":"#cccccc"}}],
          "shapes":[{"geometry":{"kind":"line","start":{"x":0,"y":0},"end":{"x":1,"y":0}},
                      "depths":[0.1,0.3],"material":{"fill":"#123456","strokeWidth":0.02}}] }
        """;

    [Fact]
    public void ConversionPreservesGeometryUnitsAndNamedPointsAndIsNeverImplicit()
    {
        Assert.Throws<JsonException>(() => Part.FromJson(Legacy));
        var part = PropPartConversion.Convert(Legacy);
        Assert.Equal(EquipmentPoints.Grip, part.Pivot);
        Assert.Equal(new PuppetPoint(.2f, -.1f, .03f), part.Point(EquipmentPoints.Grip));
        Assert.Equal(new PuppetPoint(1, .2f, .4f), part.Point(EquipmentPoints.Tip));
        Assert.Equal(new PuppetPoint(.1f, .2f, .3f), part.Point(EquipmentPoints.SecondGrip));
        Assert.Equal(new PuppetPoint(.8f, .1f, .2f), part.Point(EquipmentPoints.Muzzle));
        Assert.Equal(["mesh-1", "shape-1"], part.Geometry.Select(g => g.Id));
        Assert.Equal([.1f, .3f], part.Geometry[1].Depths);
        Assert.Equal(part.ToJson(), Part.FromJson(part.ToJson()).ToJson());
        Assert.Throws<InvalidDataException>(() => PropPartConversion.Convert(Legacy.Replace("\"scale\":1.7", "\"unrecognized\":42")));
    }

    [Fact]
    public void ConversionRefusesCollidingLibraryIdsWithoutOverwritingEitherSource()
    {
        var root = Path.Combine(Path.GetTempPath(), "part-conversion-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "source", "parts"));
            Directory.CreateDirectory(Path.Combine(root, "source", "props"));
            var part = PropPartConversion.Convert(Legacy);
            part.Name = "Existing library part";
            var existing = part.ToJson();
            File.WriteAllText(Path.Combine(root, "source", "parts", "tool.json"), existing);
            File.WriteAllText(Path.Combine(root, "source", "props", "tool.json"), Legacy);
            var result = FormatConversion.Convert(Path.Combine(root, "source"), Path.Combine(root, "output"));
            Assert.False(result.Converted);
            Assert.Contains(result.Files, f => f.Kind == "prop" && f.Status.Contains("Multiple source assets"));
            Assert.Equal(existing, File.ReadAllText(Path.Combine(root, "output", "parts", "tool.json")));
            Assert.Equal(Legacy, File.ReadAllText(Path.Combine(root, "source", "props", "tool.json")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void TiltedInstancesShareGeometryButKeepPhysicalDepthSeparateFromLayers(int facing)
    {
        var part = PropPartConversion.Convert(Legacy);
        part.Geometry.RemoveAt(1);
        part.Locations.Add(new() { Id = "mount", X = .4f, Y = .1f, Z = .2f, Layer = .4f });
        var geometry = part.Geometry[0];
        geometry.A = geometry.Frame = "mount";
        geometry.Depth = .6f;
        part.Validate();
        var sockets = new Dictionary<string, ModelSocket> { ["one"] = new() { Id = "one" }, ["two"] = new() { Id = "two" } };
        ResolvedAttachment Resolve(string id, string socket) => ResolvedAttachment.Resolve(
            new() { Id = id, Asset = part.Id, Socket = socket, Layer = .7f }, _ => part, sockets, "test");
        var first = Resolve("first", "one");
        var second = Resolve("second", "two");
        Assert.Same(first.Asset, second.Asset);
        var before = part.ToJson();
        var socketFrame = PartPlacement3D.Lift(LayeredFrame2D.Local(new(facing, 1), .2f, new(3, 2), 4), new(.7f, .6f, .4f));
        var drawing = new PuppetDrawing();
        drawing.AddAttachment(first, socketFrame);
        var drawn = drawing.Mesh.Vertices.ToArray();
        Assert.Equal(3, drawn.Length);
        for (var i = 0; i < 3; i++)
        {
            var local = geometry.Mesh!.Vertices[i].XYZ + new Vector3(.4f, .1f, .2f);
            var expected = socketFrame.TransformPoint((local - part.Point(EquipmentPoints.Grip)!.Value.XYZ) * part.Scale)
                + new Vector3(0, 0, .4f + .6f + .7f);
            TestModels.Near(expected, Point(drawn[i].Position), 1e-5f);
        }
        var centre = drawn.Aggregate(Vector3.Zero, (sum, v) => sum + Point(v.Position)) / drawn.Length;
        Assert.Equal(0, PartPicking2D.Distance(first, socketFrame, centre));
        drawing.Clear();
        first.Instance.Layer += 2;
        drawing.AddAttachment(first, socketFrame);
        for (var i = 0; i < 3; i++) TestModels.Near(Point(drawn[i].Position) + new Vector3(0, 0, 2), Point(drawing.Mesh.Vertices[i].Position), 1e-5f);
        Assert.Equal(.7f, second.Instance.Layer);
        Assert.Equal(before, part.ToJson());
        TestModels.Near(socketFrame.Origin, PartPlacement3D.Point(socketFrame, part, part.Point(EquipmentPoints.Grip)!.Value), 1e-5f);
    }

    [Fact]
    public void CollidersCanUseAnArbitraryPartLocation()
    {
        var catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);
        var entity = EntityAsset.FromJson(catalog.EntityAssets["spear-guard"].ToJson());
        var spear = Part.FromJson(catalog.Parts["spear"].ToJson());
        spear.Locations.Add(new() { Id = "blade-centre", X = 1.2f, Z = .08f });
        var collider = entity.Colliders.First(c => c.Prop == spear.Id);
        collider.Point = "blade-centre";
        var resolved = ResolvedEntity.Compile(entity, catalog.Resolve, catalog.Animations.GetValueOrDefault,
            id => id == spear.Id ? spear : catalog.Parts.GetValueOrDefault(id));
        Assert.Equal(new PuppetPoint(1.2f, 0, .08f), resolved.Colliders.First(c => c.Id == collider.Id).PropPoint);
    }
}
