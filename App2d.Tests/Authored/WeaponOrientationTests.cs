using App2d.Core.Characters.Authored;
using System.Numerics;
using App2d.Core.Characters;
using App2d.Core.Characters.Editing;
using App2d.Rendering.Characters;

namespace App2d.Tests.Authored;

public sealed class WeaponOrientationTests
{
    [Fact]
    public void FacetedBladeIsClosedOutwardWoundAndEndsAtItsTip()
    {
        var mesh = PropGeometry.Blade(.1f, .65f, .82f, .047f, .014f, "#dce5e7");
        new PropAsset { Id = "blade", Name = "Blade", Solids = [mesh] }.Validate();
        var center = new Vector3(.4f, 0, 0); var edges = new Dictionary<(int, int), int>();
        for (var i = 0; i < mesh.Triangles.Count; i += 3)
        {
            var indices = mesh.Triangles.Skip(i).Take(3).ToArray();
            var a = mesh.Vertices[indices[0]].XYZ; var b = mesh.Vertices[indices[1]].XYZ; var c = mesh.Vertices[indices[2]].XYZ;
            Assert.True(Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3 - center) > 0);
            for (var j = 0; j < 3; j++)
            {
                var u = indices[j]; var v = indices[(j + 1) % 3]; var edge = (Math.Min(u, v), Math.Max(u, v));
                edges[edge] = edges.GetValueOrDefault(edge) + 1;
            }
        }
        Assert.All(edges.Values, count => Assert.Equal(2, count));
        Assert.Equal(new PuppetPoint(.82f, 0, 0), mesh.Vertices.MaxBy(v => v.X));
    }

    private static (ResolvedModel Model, ModelSocket Socket, MotionClip Clip) Setup()
    {
        var model = ResolvedModel.From(PersonTemplate.Model()); var socket = model.Base.Sockets.First();
        return (model, socket, ClipAuthoring.New(model, "weapon-test", "Weapon test", 1, false));
    }

    [Fact]
    public void TwistTurnsWidthIntoDepthWithoutMovingTheGripOrChangingLength()
    {
        var (model, socket, clip) = Setup();
        var rest = new ActorPose(PoseEvaluator.Rest(model), Vector2.Zero, 1).Socket(socket);
        ClipAuthoring.SetKey(clip, new(MotionClip.OrientKind, socket.Id), 0, new(MathF.PI / 2, 0, 0));
        clip.Validate(model);
        var turned = new ActorPose(PoseEvaluator.Sample(model, clip, 0), Vector2.Zero, 1).Socket(socket);
        TestModels.Near(rest.Origin, turned.Origin, 1e-6f);
        TestModels.Near(rest.Along3, turned.Along3, 1e-6f);
        Assert.InRange(MathF.Abs(turned.Across3.Z), .999f, 1.001f);
        Assert.InRange(turned.Across.Length(), 0, 1e-6f);
        Assert.InRange(Vector3.Distance(turned.At(2, 0), turned.Origin), 1.999f, 2.001f);
    }

    [Fact]
    public void TiltForeshortensAndFacingMirrorsEveryLocalAxisIncludingThickness()
    {
        var (model, socket, clip) = Setup();
        ClipAuthoring.SetKey(clip, new(MotionClip.OrientKind, socket.Id), 0, new(.7f, .6f, .4f));
        var local = PoseEvaluator.Sample(model, clip, 0); var origin = new Vector2(4, 2);
        var right = new ActorPose(local, origin, 1).Socket(socket); var left = new ActorPose(local, origin, -1).Socket(socket);
        var prop = new PropAsset { Grip = new(.2f, -.1f, .03f), Scale = 2 };
        foreach (var p in new[] { prop.Grip, new PuppetPoint(1, .2f, -.04f), new PuppetPoint(.3f, -.3f, .2f) })
        {
            var r = ActorPose.PropPoint(right, prop, p); var l = ActorPose.PropPoint(left, prop, p);
            TestModels.Near(new(8 - r.X, r.Y, r.Z), l, 1e-5f);
            Assert.InRange(Vector3.Distance(r, right.Origin) - Vector3.Distance(p.XYZ, prop.Grip.XYZ) * 2, -1e-5f, 1e-5f);
        }
        Assert.True(right.Axis.Length() < .9f);
    }

    [Fact]
    public void FullTurnsInterpolateThroughHalfATurnAndRoundTripWithTimelineEdits()
    {
        var (model, socket, clip) = Setup(); var channel = new Channel(MotionClip.OrientKind, socket.Id);
        ClipAuthoring.SetKey(clip, channel, 0, Vector3.Zero); ClipAuthoring.SetKey(clip, channel, 1, new(MathF.Tau, 0, 0));
        clip = MotionClip.FromJson(clip.ToJson()); clip.Validate(model);
        Assert.Equal(MathF.PI, PoseEvaluator.Sample(model, clip, .5).SocketAngles[socket.Id].X, 5);
        ClipAuthoring.MoveKeys(clip, 1, .75f, [channel]); ClipAuthoring.Retime(clip, 2);
        Assert.Equal(new[] { 0f, 1.5f }, ClipAuthoring.KeyTimes(clip, [channel]));
        ClipAuthoring.DeleteKeys(clip, 1.5f, [channel]); Assert.Single(clip.Tracks[0].Keys);
    }

    [Fact]
    public void SocketOrientationBlendsWithTheArmLayerAndRejectsMissingSockets()
    {
        var (model, socket, clip) = Setup(); var channel = new Channel(MotionClip.OrientKind, socket.Id);
        ClipAuthoring.SetKey(clip, channel, 0, new(.2f, 0, 0));
        var overlay = ClipAuthoring.Duplicate(clip, "overlay", "Overlay"); ClipAuthoring.SetKey(overlay, channel, 0, new(1, 0, 0));
        var result = PoseEvaluator.Sample(model, clip, 0, input: new() { Overlay = new(overlay, 0, new HashSet<string> { socket.Frame ?? socket.Control }, .5f) });
        Assert.Equal(.6f, result.SocketAngles[socket.Id].X, 5);
        overlay.Tracks.Clear();
        result = PoseEvaluator.Sample(model, clip, 0, input: new() { Overlay = new(overlay, 0, new HashSet<string> { socket.Frame ?? socket.Control }) });
        Assert.Equal(Vector3.Zero, result.SocketAngles[socket.Id]);
        clip.Tracks[0].Target = "missing-socket"; Assert.Contains("unknown socket", Assert.Throws<InvalidDataException>(() => clip.Validate(model)).Message);
    }

    [Fact]
    public void ObjAcceptsNegativeIndicesAndWeldsPositionsButRequiresTriangles()
    {
        var mesh = PropGeometry.ImportObj("v 0 0 0\nv 1 0 0\nv 0 1 0\nv 0 0 0\nf -1/1/1 -2/2/1 -3/3/1", 2);
        Assert.Equal(3, mesh.Vertices.Count); Assert.Equal(new[] { 0, 2, 1 }, mesh.Triangles);
        Assert.Equal(2, mesh.Vertices[1].X);
        Assert.Throws<InvalidDataException>(() => PropGeometry.ImportObj("v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3 1"));
        Assert.Throws<InvalidDataException>(() => PropGeometry.ImportObj("v NaN 0 0"));
        Assert.Throws<InvalidDataException>(() => PropGeometry.ImportObj("v 0 0 0\nf 1 1 1"));
    }

    [Fact]
    public void SolidBladeKeepsAThinVisibleEdgeAndDoesNotLoseItsReverseFace()
    {
        var prop = new PropAsset { Id = "blade", Name = "Blade", LineWidth = .008f,
            Solids = [PropGeometry.Extrude([new(0, -.1f), new(1, -.1f), new(1, .1f), new(0, .1f)], .02f, "#cccccc")] };
        var drawing = new PuppetDrawing();
        float Width(float angle)
        {
            drawing.Mesh.Clear(); var matrix = Matrix4x4.CreateRotationX(angle);
            drawing.AddProp(prop, new(Vector3.Zero, Vector3.UnitX, Vector3.Transform(Vector3.UnitY, matrix), Vector3.Transform(Vector3.UnitZ, matrix)));
            Assert.True(drawing.Mesh.TriangleCount > 12);
            return drawing.Mesh.Max.Y - drawing.Mesh.Min.Y;
        }
        var broad = Width(0); var thin = Width(MathF.PI / 2); var reverse = Width(MathF.PI);
        Assert.True(thin > .019f && thin < broad / 3);
        Assert.Equal(broad, reverse, 5);
    }

    [Fact]
    public void WeaponPoseSupportsPendingKeysUndoSaveAndReopen()
    {
        var root = Path.Combine(Path.GetTempPath(), "weapon-editor-" + Guid.NewGuid().ToString("N"));
        try
        {
            PersonTemplate.WriteStudies(root); var session = new EditorSession(AuthoringWorkspace.Open(root));
            session.Open("person-thrust"); session.PreviewSocket = "right-grip"; session.AutoKey = false; session.Seek(.2f);
            session.PoseWeapon(new(.7f, .4f, 0)); Assert.True(session.HasPendingPose);
            Assert.DoesNotContain(session.ClipDocument!.Asset.Tracks, t => t.Kind == MotionClip.OrientKind);
            Assert.Equal(.7f, session.Scene()[0].Pose.SocketAngles["right-grip"].X);
            session.KeyPose(); Assert.False(session.HasPendingPose);
            session.Undo(); Assert.DoesNotContain(session.ClipDocument.Asset.Tracks, t => t.Kind == MotionClip.OrientKind);
            session.Redo(); Assert.True(session.Save(session.ClipDocument), session.Message);
            var reopened = AuthoringWorkspace.Open(root).Clip("person-thrust")!;
            Assert.Equal(.7f, reopened.Asset.Tracks.Single(t => t.Kind == MotionClip.OrientKind).Keys.Single().X);
            session.PoseWeapon(new(1.2f, 0, 0)); session.Seek(.4f); Assert.False(session.HasPendingPose);
            Assert.Equal(.7f, session.Scene()[0].Pose.SocketAngles["right-grip"].X);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
