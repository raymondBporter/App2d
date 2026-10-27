using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Geometry;
using App2d.Core.Mathematics;
using App2d.Core.Mathematics.Transforms;
using System.Numerics;

namespace App2d.Tests.Authored;

public sealed class PartConsolidationTests
{
    private static Vector3 Point(Microsoft.Xna.Framework.Vector3 p) => new(p.X, p.Y, p.Z);
    private static Part Tool() => new()
    {
        Id = "tool", Name = "Tool", Scale = 1.7f, Pivot = EquipmentPoints.Grip,
        Locations = [new() { Id = EquipmentPoints.Grip, X = .2f, Y = -.1f, Z = .03f }],
        Geometry = [new()
        {
            Id = "mesh-1", A = "origin", Frame = "origin", Material = new() { Fill = "#cccccc" },
            Mesh = new() { Vertices = [new(0, 0, .1f), new(.4f, 0, .1f), new(0, .3f, .1f)], Triangles = [0, 1, 2] }
        }]
    };
    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void TiltedInstancesShareGeometryButKeepPhysicalDepthSeparateFromLayers(int facing)
    {
        var part = Tool();
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
        var entity = Part.FromJson(catalog.Parts["spear-guard"].ToJson());
        var spear = Part.FromJson(catalog.Parts["spear"].ToJson());
        spear.Locations.Add(new() { Id = "blade-centre", X = 1.2f, Z = .08f });
        var collider = entity.Geometry.First(c => c.Prop == spear.Id);
        collider.Point = "blade-centre";
        var resolved = ResolvedEntity.Compile(entity, catalog.Resolve, catalog.Animations.GetValueOrDefault,
            id => id == spear.Id ? spear : catalog.Parts.GetValueOrDefault(id));
        Assert.Equal(new PuppetPoint(1.2f, 0, .08f), resolved.Colliders.First(c => c.Id == collider.Id).PropPoint);
    }
}
