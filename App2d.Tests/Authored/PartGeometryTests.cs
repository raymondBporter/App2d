using App2d.CharacterSeeds;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Drawing;
using App2d.Core.Characters.Geometry;
using System.Numerics;
using System.Text.Json.Nodes;

namespace App2d.Tests.Authored;

public sealed class PartGeometryTests
{
    [Fact]
    public void MeshesFollowAnimatedBonesFitShowAndPickingWithoutChangingSharedGeometry()
    {
        var tail = PartSeeds.Tail();
        var tuft = tail.Geometry[1];
        tuft.Geometry = null;
        tuft.Mesh = new()
        {
            Outline = [new(-.08f, -.04f, .3f), new(.08f, -.04f, .3f), new(.08f, .04f, .3f), new(-.08f, .04f, .3f)],
            Thickness = .1f,
        };
        tuft.OffsetX = .12f;
        tuft.Depth = -.2f;
        tail.Validate();
        var original = tail.ToJson();
        var model = PersonTemplate.Model();
        ModelAuthoring.AddAttachment(model, tail.Id, Humanoid.BodySocket, id => tail, "tail");
        var resolved = ResolvedModel.From(model, partOf: id => tail);
        var clip = ClipAuthoring.New(resolved, "mesh-motion", "Mesh motion", loop: false);
        ClipAuthoring.SetKey(clip, new(MotionClip.FitKind, "tail/tuft"), 0, new(1, 0, 0));
        ClipAuthoring.SetKey(clip, new(MotionClip.ShowKind, "tail/tuft"), 0, Vector3.UnitX);
        ClipAuthoring.SetKey(clip, new(MotionClip.ShowKind, "tail/tuft"), .5f, Vector3.Zero);
        clip.Validate(resolved);

        var evaluated = PoseEvaluator.Sample(resolved, clip, .2).Attachments["tail"];
        var fitted = evaluated.Pose.Fitted(evaluated.Attachment.Parts[1]);
        Assert.Equal(2, fitted.ScaleX);
        var frame = PartAttachment2D.FrameOf(fitted, evaluated.World, evaluated.Frame);
        var center = frame.At(Vector2.Zero);
        Assert.Equal(0, PartPicking2D.Distance(fitted, evaluated.World, center, evaluated.Frame), 4);
        var contour = PartContours2D.Contour(fitted, evaluated.World, evaluated.Frame);
        Assert.All(contour, p => Assert.Equal(frame.Origin.Z + .3f, p.Z, 4));

        var drawing = new PuppetDrawing();
        drawing.Build(tail.Ink, tail.LineWidth, [fitted], evaluated.World, evaluated.Frame);
        Assert.True(drawing.Mesh.Count > 0);
        // A physical thickness of .1 survives the planar drawing-layer offset and the X-only fit.
        var depths = drawing.Mesh.Vertices.ToArray().Select(v => v.Position.Z).ToArray();
        Assert.InRange(depths.Max() - depths.Min(), .099f, .101f);
        var hidden = PoseEvaluator.Sample(resolved, clip, .6).Attachments["tail"];
        Assert.DoesNotContain(hidden.DrawnParts(), p => p.Id == "tuft");
        Assert.Equal(original, tail.ToJson());
        var again = PoseEvaluator.Sample(resolved, clip, .2).Attachments["tail"];
        TestModels.Near(evaluated.World("tip"), again.World("tip"));
    }

    [Fact]
    public void LegacyConversionPreservesMeshCoordinatesMaterialsAndStableEntryIds()
    {
        const string legacy = """
            { "format":"app2d-part", "version":2, "id":"mixed", "name":"Mixed",
              "parts":[{ "id":"mesh-1", "a":"origin", "geometry":{"kind":"circle", "center":{"x":0,"y":0}, "radius":0.1}, "material":{"fill":"#abcdef"} }],
              "solids":[{ "vertices":[{"x":0,"y":0,"z":0.3},{"x":1,"y":0,"z":0.4},{"x":0,"y":1,"z":0.5}], "triangles":[0,1,2], "material":{"fill":"#102030"} }]
            }
            """;
        Assert.Throws<System.Text.Json.JsonException>(() => Part.FromJson(legacy));
        var node = JsonNode.Parse(legacy)!.AsObject();
        PartGeometryConversion.Upgrade(node);
        var part = Part.FromJson(node.ToJsonString());
        Assert.Equal(2, part.Geometry.Count);
        Assert.Equal("mesh-1", part.Geometry[0].Id);
        Assert.NotEqual("mesh-1", part.Geometry[1].Id);
        Assert.Equal("origin", part.Geometry[1].Frame);
        Assert.Equal("#102030", part.Geometry[1].Material!.Fill);
        Assert.Equal([.3f, .4f, .5f], part.Geometry[1].Mesh!.Vertices.Select(v => v.Z));
        Assert.False(node.ContainsKey("parts") || node.ContainsKey("solids"));
        Assert.Equal(part.ToJson(), Part.FromJson(part.ToJson()).ToJson());
    }

    [Fact]
    public void MeshPickingUsesIndexedTrianglesRatherThanTreatingVertexOrderAsAnOutline()
    {
        var entry = new PartGeometry
        {
            Id = "islands", A = "origin", Material = new() { Fill = "#ffffff" },
            Mesh = new()
            {
                Vertices = [new(0, 0), new(1, 0), new(0, 1), new(3, 0), new(4, 0), new(3, 1)],
                Triangles = [0, 1, 2, 3, 4, 5],
            },
        };
        entry.Validate(id => id == "origin");
        Assert.Equal(0, PartPicking2D.Distance(entry, _ => Vector3.Zero, new(.1f, .1f, 0)), 4);
        Assert.Equal(0, PartPicking2D.Distance(entry, _ => Vector3.Zero, new(3.1f, .1f, 0)), 4);
        Assert.True(PartPicking2D.Distance(entry, _ => Vector3.Zero, new(2, .1f, 0)) > 1);
        entry.Hidden = true;
        Assert.Equal(float.PositiveInfinity, PartPicking2D.Distance(entry, _ => Vector3.Zero, new(.1f, .1f, 0)));
    }
}
