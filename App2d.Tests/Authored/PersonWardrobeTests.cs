using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Rendering.Characters;
using System.Numerics;

namespace App2d.Tests.Authored;

public sealed class PersonWardrobeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConcaveCutoutsPreserveTheNotchAndHaveClosedOutwardFaces(bool reverse)
    {
        // A square with the upper-right quarter removed. Fan triangulation would fill the notch.
        PuppetPoint[] outline = [new(0, 0), new(2, 0), new(2, 1), new(1, 1), new(1, 2), new(0, 2)];
        var solid = PropGeometry.Extrude(reverse ? outline.Reverse() : outline, .02f, "#aaaaaa");
        var area = 0f; var edges = new Dictionary<(int, int), int>();
        for (var i = 0; i < solid.Triangles.Count; i += 3)
        {
            var a = solid.Triangles[i]; var b = solid.Triangles[i + 1]; var c = solid.Triangles[i + 2];
            var pa = solid.Vertices[a].XYZ; var pb = solid.Vertices[b].XYZ; var pc = solid.Vertices[c].XYZ;
            if (pa.Z < 0 && pb.Z < 0 && pc.Z < 0)
            {
                var cross = Vector3.Cross(pb - pa, pc - pa); Assert.True(cross.Z < 0);
                area += -cross.Z / 2;
                var center = (pa + pb + pc) / 3;
                Assert.False(center.X > 1 && center.Y > 1);
            }
            foreach (var (u, v) in new[] { (a, b), (b, c), (c, a) })
            {
                var edge = u < v ? (u, v) : (v, u); edges[edge] = edges.GetValueOrDefault(edge) + 1;
            }
        }
        Assert.Equal(3, area, 5);
        Assert.All(edges.Values, count => Assert.Equal(2, count));
    }

    [Fact]
    public void HairStaysWhenUnarmedAndChangesForTheBackView()
    {
        var clip = new MotionClip { Markers = [new() { Id = PersonLoadout.BackViewMarker, Time = .2f }] };
        Assert.Equal((PersonWardrobe.ShortHair, PersonWardrobe.HeadSocket), Assert.Single(PersonLoadout.Dressed(clip, 0, PersonGear.None)));
        Assert.Equal((PersonWardrobe.ShortHairBack, PersonWardrobe.HeadSocket), Assert.Single(PersonLoadout.Dressed(clip, .3f, PersonGear.None)));
    }

    [Fact]
    public void HeadArtUsesTheSameScreenAxesAsTheDrawnHeadDuringDeath()
    {
        var catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);
        var entity = catalog.Entities["maul-brute"];
        var pose = new ActorPose(PoseEvaluator.Sample(entity.Model, catalog.Animations["person-death"], .6f), Vector2.Zero, 1);
        var socket = pose.Socket(entity.Sockets[PersonWardrobe.HeadSocket]);
        TestModels.Near(Vector3.UnitX, socket.Along3);
        TestModels.Near(Vector3.UnitY, socket.Across3);
        TestModels.Near(pose.World("head"), socket.Origin);
    }

    [Fact]
    public void GarmentsFollowTheActualTorsoDuringTheHammerSlamInBothFacings()
    {
        var catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot); Assert.Empty(catalog.Errors);
        var entity = catalog.Entities["maul-brute"]; var clip = catalog.Animations["person-hammer-slam"];
        var socket = entity.Sockets[PersonWardrobe.BodySocket];
        foreach (var facing in new[] { -1, 1 }) foreach (var at in new[] { 0, .55, .78, 1.1 })
        {
            var pose = new ActorPose(PoseEvaluator.Sample(entity.Model, clip, at), Vector2.Zero, facing);
            var expected = pose.World("chest") - pose.World("hips"); expected.Z = 0;
            TestModels.Near(Vector3.Normalize(expected), pose.Socket(socket).Across3);
            TestModels.Near(pose.World("hips"), pose.Socket(socket).Origin);
        }
    }

    [Fact]
    public void PackagedWardrobeCompilesAndDrawsWithExistingEnemyActions()
    {
        var catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot); Assert.Empty(catalog.Errors);
        var drawing = new PuppetDrawing();
        foreach (var id in new[] { "maul-brute", "cinder-gunner" })
        {
            var entity = catalog.Entities[id];
            Assert.Equal(3, entity.Equipment.Count(e => e.Socket.Id is PersonWardrobe.HeadSocket or PersonWardrobe.BodySocket));
            foreach (var clip in entity.Roles.Values.Select(r => r.Clip).Concat(entity.Actions.Values.Select(a => a.Clip)))
                for (var frame = 0; frame <= 12; frame++)
                {
                    drawing.Build(entity, PoseEvaluator.Sample(entity.Model, clip, frame * clip.Duration / 12));
                    Assert.True(drawing.Mesh.Count > 0);
                    Assert.InRange(drawing.Mesh.Max.X - drawing.Mesh.Min.X, .1f, 10);
                }
        }
        foreach (var prop in PersonWardrobe.Props()) prop.Validate();
    }

    [Fact]
    public void TunicPaintStaysOnTheTorsoAndClipsToItsSilhouette()
    {
        var part = new PuppetPart
        {
            A = "origin", Kind = "ellipse", Width = 2, Height = 1, OutlineWidth = 0,
            Paint = [new() { Fill = "#ff0000", Points = [new(-2, -2), new(2, -2), new(2, 2), new(-2, 2)] }],
        };
        var drawing = new PuppetDrawing(); drawing.Build("#000000", .04f, [part], _ => Vector3.Zero);
        var red = drawing.Mesh.Vertices.ToArray().Where(v => v.Color.R == 255 && v.Color.G == 0).ToArray();
        Assert.NotEmpty(red);
        foreach (var vertex in red)
        {
            var p = vertex.Position;
            Assert.InRange(p.X * p.X + 4 * p.Y * p.Y, 0, 1.00001f);
            Assert.InRange(p.Z, -.0001f, 0);
        }
        // Resizing the body also resizes the paint, without changing a clothing socket or prop scale.
        drawing.Build("#000000", .04f, [part with { Width = 4 }], _ => Vector3.Zero);
        Assert.InRange(drawing.Mesh.Max.X, 1.999f, 2.001f);
    }

    [Fact]
    public void EnemyOutfitsDifferAndTheWrapEnclosesBothHipStrokes()
    {
        var catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);
        foreach (var id in new[] { "maul-brute", "cinder-gunner" })
        {
            var entity = catalog.Entities[id];
            var body = entity.Model.Parts.Single(p => p.Id == "body");
            if (id == "maul-brute")
            {
                Assert.Empty(body.Paint!);
                Assert.Equal(entity.Model.Parts.Single(p => p.Id == "head").Fill, body.Fill);
            }
            else Assert.NotEmpty(body.Paint!);
            Assert.DoesNotContain(entity.Equipment, e => e.Prop.Id.EndsWith("-tunic", StringComparison.Ordinal));
            Assert.Null(entity.Model.Parts.Single(p => p.Id == "head").OutlineWidth);
            Assert.All(entity.Equipment.Where(e => e.Prop.Usage != "prop"), e => Assert.Equal(entity.Model.Base.LineWidth, e.Prop.LineWidth));
            var wrap = entity.Equipment.Single(e => e.Prop.Id.EndsWith("-hide-wrap", StringComparison.Ordinal));
            var shell = wrap.Prop.Solids[0];
            var layers = PersonWardrobeDepths.From(entity.Model);
            Assert.True(layers.NearArm < layers.FrontDetail - .0005f); // include the ink bias
            Assert.True(layers.FrontDetail + layers.DetailThickness < layers.WrapFront);
            Assert.True(layers.WrapFront < layers.NearLeg);
            Assert.True(layers.NearLeg < layers.FarLeg);
            Assert.True(layers.FarLeg < layers.WrapBack && layers.WrapBack < layers.FarArm);
            Assert.All(wrap.Prop.Solids.SelectMany(s => s.Vertices), p => Assert.True(p.Z - .0005f > layers.NearArm));
            foreach (var clip in entity.Roles.Values.Select(r => r.Clip).Concat(entity.Actions.Values.Select(a => a.Clip)))
                for (var i = 0; i <= 12; i++)
                {
                    var pose = new ActorPose(PoseEvaluator.Sample(entity.Model, clip, clip.Duration * i / 12), Vector2.Zero, 1);
                    var vertices = shell.Vertices.Select(p => ActorPose.PropPoint(pose.Socket(wrap.Socket), wrap.Prop, p)).ToArray();
                    foreach (var side in new[] { "left", "right" })
                    {
                        var z = pose.World(side + "-hip").Z;
                        Assert.True(vertices.Min(p => p.Z) < z - .0225f);
                        Assert.True(vertices.Max(p => p.Z) > z + .0225f);
                    }
                }
        }
    }
}
