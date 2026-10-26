using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Geometry;
using App2d.Core.Mathematics.Transforms;
using App2d.Tests.Authored;
using System.Numerics;

namespace App2d.Tests.Geometry;

/// <summary>
/// P1a of the character-system migration: the one composable placement value. Frames multiply, layers add, a matrix
/// joins with no layer offset, and actor placement converts local, entity and world without touching layers.
/// </summary>
public sealed class LayeredFrame2DTests
{
    [Fact]
    public void IdentityAndDefaultAreExplicit()
    {
        var point = new Vector3(2, -1, .3f);
        Assert.Equal(point, LayeredFrame2D.Identity.At(point));
        Assert.Equal(new Vector3(0, 0, .3f), default(LayeredFrame2D).At(point));
        var placed = LayeredFrame2D.Local(new(2, .5f), .4f, new(1, 2), -.1f);
        Assert.Equal(placed, placed.Then(LayeredFrame2D.Identity));
        Assert.Equal(placed, LayeredFrame2D.Identity.Then(placed));
        Assert.Equal(0, LayeredFrame2D.FromMatrix(Matrix3x2.CreateRotation(1) * Matrix3x2.CreateScale(3)).Layer);
    }

    [Fact]
    public void CompositionMultipliesFramesAndAddsLayers()
    {
        var local = LayeredFrame2D.Local(new(2, .5f), .4f, new(1, 2), -.1f);
        var parent = new LayeredFrame2D(new Frame2D(new(12, -3), new(-2, 0), new(.5f, 3)), .25f);
        var combined = local.Then(parent);
        var point = new Vector2(4, 2);
        TestModels.Near(parent.At(local.At(point)), combined.At(point), 1e-5f, "a composed placement lands where the parent places the local result");
        Assert.Equal(local.Frame.Then(parent.Frame), combined.Frame);
        Assert.Equal(.15f, combined.Layer, 6);
        // A further local layer offset adds on top, and XY rotation, scale or reflection never leak into the layer.
        Assert.Equal(.15f + .02f, combined.At(new Vector3(point, .02f)).Z, 6);
        Assert.Equal(.15f, local.Then(parent with { Frame = new Frame2D(Matrix3x2.CreateScale(-7, 7) * Matrix3x2.CreateRotation(2)) }).Layer, 6);
    }

    [Fact]
    public void LayerOrderSurvivesWorldScalingAndMirroring()
    {
        var near = LayeredFrame2D.FromRotation(.3f, new(1, 1, -.2f));
        var far = LayeredFrame2D.FromRotation(-.5f, new(1, 1, .1f));
        foreach (var world in new[] { LayeredFrame2D.FromMatrix(Matrix3x2.CreateScale(48)), LayeredFrame2D.FromMatrix(Matrix3x2.CreateScale(-48, 48) * Matrix3x2.CreateTranslation(640, 360)) })
        {
            Assert.True(near.Then(world).Layer < far.Then(world).Layer);
            Assert.Equal(near.Layer, near.Then(world).Layer);
            Assert.Equal(far.Layer, far.Then(world).Layer);
        }
    }

    [Fact]
    public void InverseAndLocalCoordinatesUndoThePlacementIncludingTheLayer()
    {
        var placement = LayeredFrame2D.Local(new(2, .5f), .4f, new(1, 2), -.1f).Then(LayeredFrame2D.FromRotation(1.2f, new(3, 4, .5f)));
        var local = new Vector3(.3f, -.7f, .05f);
        var world = placement.At(local);
        Assert.True(placement.TryToLocal(world, out var restored));
        TestModels.Near(local, restored, 1e-5f, "local coordinates");
        Assert.True(placement.TryInverse(out var inverse));
        TestModels.Near(local, inverse.At(world), 1e-5f, "inverse placement");
        Assert.Equal(-placement.Layer, inverse.Layer);
        Assert.False(new LayeredFrame2D(new Frame2D(Vector2.Zero, Vector2.UnitX, Vector2.UnitX), 0).TryToLocal(world, out _));
    }

    [Fact]
    public void SimilarityCarrierWithNonuniformLeafScaleAddsNoShear()
    {
        var leaf = LayeredFrame2D.Local(new(3, .25f), .7f, new(.2f, .1f), 0);
        var carrier = new LayeredFrame2D(new Frame2D(Matrix3x2.CreateScale(-1.5f, 1.5f) * Matrix3x2.CreateRotation(2.1f) * Matrix3x2.CreateTranslation(4, 5)), .3f);
        var placed = leaf.Then(carrier);
        Assert.InRange(Vector2.Dot(placed.XAxis, placed.YAxis), -1e-5f, 1e-5f);
        Assert.Equal(3 * 1.5f, placed.XAxis.Length(), 4);
        Assert.Equal(.25f * 1.5f, placed.YAxis.Length(), 4);
    }

    [Fact]
    public void ActorPlacementMirrorsXKeepsLayersAndRoundTrips()
    {
        foreach (var facing in new[] { 1, -1 })
        {
            var actor = new ActorPose(new EvaluatedPose(), new(10, 2), facing);
            var local = new Vector3(.4f, 1.1f, -.12f);
            var world = actor.Place(local);
            Assert.Equal(new Vector3(10 + .4f * facing, 3.1f, -.12f), world);
            TestModels.Near(local, actor.ToLocal(world), 1e-6f, "round trip");
            Assert.Equal(facing < 0, actor.Placement.Then(LayeredFrame2D.Identity).Frame.Matrix.GetDeterminant() < 0);
            Assert.Equal(world, actor.Placement3D.TransformPoint(local));
        }
    }

    [Fact]
    public void LayerOffsetsReproduceAbsoluteRestDepthAndAnimatedParentsCarryChildren()
    {
        var catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);
        foreach (var id in new[] { "person", "quadruped", "pterosaur", "curve-face" })
        {
            var model = catalog.Resolve(id);
            var rest = PoseEvaluator.Rest(model);
            foreach (var control in model.Order)
            {
                var summed = 0f;
                for (var c = control; c is not null; c = c.Parent is null ? null : model.Controls[c.Parent])
                {
                    summed += model.LayerOffsets[c.Id];
                }

                Assert.Equal(model.Rest[control.Id].Z, summed, 5);
                Assert.Equal(model.Rest[control.Id].Z, rest.World(control.Id).Z, 5);
                Assert.Equal(model.Rest[control.Id].Z, model.RestPlacement(control.Id).Layer);
                Assert.Equal(rest.World(control.Id), rest.Placement(control.Id).Origin);
            }
        }

        // The same local meaning for point controls and explicit bones: a parent's translate-Z key is a layer delta its children inherit.
        foreach (var bones in new[] { false, true })
        {
            var model = new CharacterModel
            {
                Id = "layers",
                Name = "Layers",
                Controls = bones
                ?
                [
                    new() { Id = "root", Node = RigNodeKinds.Bone, Local = new() { Y = 1, Layer = .1f } },
                    new() { Id = "child", Parent = "root", Node = RigNodeKinds.Bone, Local = new() { X = .5f, Layer = -.3f } },
                    new() { Id = "leaf", Parent = "child", Node = RigNodeKinds.Bone, Local = new() { X = .5f, Layer = -.05f } },
                ]
                :
                [
                    new() { Id = "root", Rest = new(0, 1, .1f) },
                    new() { Id = "child", Parent = "root", Rest = new(.5f, 1, -.2f) },
                    new() { Id = "leaf", Parent = "child", Rest = new(1, 1, -.25f) },
                ],
            };
            model.Validate();
            var resolved = ResolvedModel.From(model);
            Assert.Equal(.1f, resolved.LayerOffsets["root"], 6);
            Assert.Equal(-.3f, resolved.LayerOffsets["child"], 6);
            Assert.Equal(-.05f, resolved.LayerOffsets["leaf"], 6);
            var clip = ClipAuthoring.New(resolved, "layers-lift", "Lift");
            ClipAuthoring.SetKey(clip, new(MotionClip.TranslateKind, "root"), 0, new(0, 0, .5f));
            ClipAuthoring.SetKey(clip, new(MotionClip.TranslateKind, "leaf"), 0, new(0, 0, .02f));
            clip.Validate(resolved);
            var pose = PoseEvaluator.Sample(resolved, clip, 0);
            Assert.Equal(.6f, pose.World("root").Z, 5);
            Assert.Equal(.3f, pose.World("child").Z, 5);
            Assert.Equal(.27f, pose.World("leaf").Z, 5);
        }
    }
}
