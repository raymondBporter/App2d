using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Mathematics;
using App2d.Rendering.Characters;
using System.Numerics;

namespace App2d.Tests.Authored;

public sealed class BoneAuthoringTests
{
    [Fact]
    public void BonesAndAttachedShapesShareOneFrameThroughRestAndAnimation()
    {
        var model = ModelAuthoring.Empty("bones", "Bones");
        var root = ModelAuthoring.AddBone(model, null, "root");
        var child = ModelAuthoring.AddBone(model, root.Id, "child");
        var shape = ModelAuthoring.AddPartToBone(model, PuppetPartKinds.Box, child.Id);
        shape.Angle = .15f;
        ModelAuthoring.RotateRestBone(model, root.Id, .3f);

        var reloaded = CharacterModel.FromJson(model.ToJson());
        Assert.Equal(2, reloaded.Controls.Count);
        Assert.Equal(child.Id, reloaded.Parts.Single().Frame);
        var resolved = ResolvedModel.From(reloaded);
        var rest = PoseEvaluator.Rest(resolved);
        Near(new(.5f * MathF.Cos(.3f), .5f * MathF.Sin(.3f)), rest.World(child.Id));
        Assert.Equal(.3f, rest.Angles[child.Id], 4);

        var clip = new MotionClip
        {
            Id = "bones-turn",
            Name = "Turn",
            Model = model.Id,
            Tracks =
            [
                new() { Kind = MotionClip.RotateKind, Target = root.Id, Keys = [new() { Time = 1, Angle = .2f }] },
                new() { Kind = MotionClip.RotateKind, Target = child.Id, Keys = [new() { Time = 1, Angle = -.1f }] }
            ]
        };
        clip.Validate(resolved);
        var pose = PoseEvaluator.Sample(resolved, clip, 1);
        Near(new(.5f * MathF.Cos(.5f), .5f * MathF.Sin(.5f)), pose.World(child.Id));
        Assert.Equal(.4f, pose.Angles[child.Id], 4);
        var frame = PartGeometry.FrameOf(shape, pose.World, id => pose.Angles[id]);
        var expected = new Vector2(pose.World(child.Id).X, pose.World(child.Id).Y) + Rotation2D.Apply(new(.25f, 0), .4f);
        Near(expected, frame.Origin);
        Near(Rotation2D.Apply(Vector2.UnitX, .55f), new Vector3(frame.Right, 0));
        var drawing = new PuppetDrawing();
        drawing.Build(resolved, pose);
        Assert.True(drawing.Mesh.Count > 0);
    }

    [Fact]
    public void BoneFrameAndTowardControlCannotCompeteForAShape()
    {
        var model = ModelAuthoring.Empty("bones", "Bones");
        var bone = ModelAuthoring.AddBone(model, null, "root");
        var other = ModelAuthoring.AddBone(model, bone.Id, "child");
        var shape = ModelAuthoring.AddPartToBone(model, PuppetPartKinds.Box, bone.Id);
        shape.B = other.Id;
        Assert.Throws<InvalidDataException>(model.Validate);
    }

    [Fact]
    public void PointIkCannotSilentlyTreatExplicitBoneLengthsAsJointDistances()
    {
        var model = ModelAuthoring.Empty("bones", "Bones");
        var root = ModelAuthoring.AddBone(model, null, "root");
        var joint = ModelAuthoring.AddBone(model, root.Id, "joint");
        var end = ModelAuthoring.AddBone(model, joint.Id, "end");
        Assert.Throws<InvalidDataException>(() => ModelAuthoring.AddChain(model, end.Id));
    }

    private static void Near(Vector2 expected, Vector3 actual) =>
        Assert.InRange(Vector2.Distance(expected, new(actual.X, actual.Y)), 0, 1e-4f);
}
