using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Operations.Vertices;
using App2d.Core.Mathematics;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Tests.Authored;

/// <summary>
/// P1b of the character-system migration: artwork, picking, editor inverse placement, sockets and handles all read one
/// <see cref="LayeredFrame2D"/>; no consumer composes its own offset and rotation. The reference set pins the numbers;
/// these tests pin the route.
/// </summary>
public sealed class PlacementRoutingTests
{
    private static readonly AuthoredCatalog Catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);

    [Fact]
    public void ArtworkIsOneAttachmentOnItsCarrier()
    {
        var model = Catalog.Resolve("person");
        var pose = PoseEvaluator.Sample(model, Catalog.Animations["person-walk"], .3, true);
        foreach (var part in model.Parts.Where(p => !p.Hidden))
        {
            var frame = PartAttachment2D.FrameOf(part, model, pose);
            Assert.Equal(PartAttachment2D.Attachment(part).Then(PartAttachment2D.Carrier(part, pose.World, pose.Placement)), frame);
            Assert.Equal(pose.World(part.A).Z + part.Depth, frame.Layer, 6);
            if (part.Geometry is IShape2D shape)
            {
                var vertices = new Vector2[shape.GetOutlineVertCount(48)];
                shape.GetOutlineVerts(vertices, 48);
                Assert.Equal(frame.At(vertices[0]), PartContours2D.Contour(part, model, pose)[0]);
                var inside = frame.At(Vector2.Zero);
                Assert.True(PartPicking2D.Distance(part, model, pose, inside) <= 1, part.Id);
                Assert.True(PartAttachment2D.ArtworkPoint(part, 0, inside.XY, model, pose).Length() < 1e-5f, part.Id);
            }
            else if (part.Geometry is ICurve2D)
            {
                // A curve's carrier spans A to B, so its first and last samples sit on the controls.
                var path = PartContours2D.Contour(part, model, pose);
                TestModels.Near(pose.World(part.A) + new Vector3(0, 0, part.Depth), path[0], 1e-5f, part.Id);
                TestModels.Near(pose.World(part.B!) + new Vector3(0, 0, part.Depth), path[^1], 1e-4f, part.Id);
            }
        }
    }

    [Fact]
    public void BoneAttachedArtworkUsesTheControlPlacementAtItsOrigin()
    {
        var model = ModelAuthoring.Empty("bones", "Bones");
        var root = ModelAuthoring.AddBone(model, null, "root");
        var shape = ModelAuthoring.AddPartToBone(model, RoundedRectangle2D.Builder.FromSize(.4f, .4f, .05f), root.Id);
        shape.OffsetX = .25f;
        shape.Angle = .15f;
        shape.ScaleY = 2;
        var resolved = ResolvedModel.From(model);
        var pose = PoseEvaluator.Rest(resolved);
        var frame = PartAttachment2D.FrameOf(shape, resolved, pose);
        Assert.Equal(PartAttachment2D.Attachment(shape).Then(pose.Placement(root.Id)), frame);
        Assert.Equal(pose.Placement(root.Id).At(new Vector2(root.Length, 0)), new Vector3(Vector2.Transform(new(root.Length, 0), pose.Bones[root.Id]), pose.World(root.Id).Z));
        Assert.InRange(Vector2.Dot(frame.XAxis, frame.YAxis), -1e-6f, 1e-6f);
    }

    [Fact]
    public void SocketsArePlanarPlacementsLiftedForProps()
    {
        var hero = Catalog.Entities["hero"];
        var clip = Catalog.Animations["player-sword-side-cut"];
        var strike = clip.Markers.First(m => m.Id == "strike").Time;
        foreach (var facing in new[] { 1, -1 })
        {
            var pose = new ActorPose(PoseEvaluator.Sample(hero.Model, clip, strike, false, new() { InPlace = true }), new(3, .5f), facing);
            foreach (var socket in hero.Model.Base.Sockets)
            {
                var planar = pose.SocketPlacement(socket);
                var lifted = pose.Socket(socket);
                TestModels.Near(planar.Origin, lifted.Origin, 1e-6f, socket.Id);
                Assert.Equal(pose.Local.Points[socket.Control].Z + socket.OffsetZ, planar.Layer, 6);
                Assert.Equal(PropPlacement3D.Lift(planar, pose.Local.SocketAngles.GetValueOrDefault(socket.Id)), lifted);
                Assert.Equal(facing < 0, planar.Frame.Matrix.GetDeterminant() < 0);
                if (pose.Local.SocketAngles.GetValueOrDefault(socket.Id) == Vector3.Zero)
                {
                    TestModels.Near(new(planar.XAxis, 0), lifted.XAxis, 1e-6f, socket.Id);
                    TestModels.Near(new(planar.YAxis, 0), lifted.YAxis, 1e-6f, socket.Id);
                    Assert.Equal(Vector3.UnitZ, lifted.ZAxis);
                }
            }

            // The sword hand is tilted and twisted at the strike: a genuinely 3D frame whose turn the editor handle reads.
            var hand = hero.Sockets["sword-hand"];
            var orientation = pose.Local.SocketAngles[hand.Id];
            Assert.NotEqual(0f, orientation.X + orientation.Y);
            var local = new ActorPose(pose.Local, Vector2.Zero, 1);
            var turn = local.SocketTurn(hand);
            var planarTurn = MathF.Atan2(local.SocketPlacement(hand).XAxis.Y, local.SocketPlacement(hand).XAxis.X);
            Assert.Equal(planarTurn + orientation.Z, turn, 5);
            Assert.True(MathF.Abs(local.Socket(hand).ZAxis.Z) < .999f, "the lifted frame leaves the screen plane");
        }
    }
}
