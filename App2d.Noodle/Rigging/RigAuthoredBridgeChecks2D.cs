using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Noodle.Rigging;

internal static class RigAuthoredBridgeChecks2D
{
    public static void Run()
    {
        // The real editor sample must produce a valid, reloadable runtime asset.
        var starter = RigAuthoredBridge2D.Export(RigDocument2D.CreateStarterPerson(), "starter-rig", "Starter rig");
        _ = ResolvedModel.From(CharacterModel.FromJson(starter.ToJson()));

        var rig = new RigDocument2D();
        var root = rig.AddBone(name: "root");
        root.LocalX = 12; root.LocalY = -8; root.AngleDegrees = 25; root.Length = 40;
        var child = rig.AddBone(root, "child");
        child.LocalX = 31; child.LocalY = 6; child.AngleDegrees = -35; child.Length = 24;
        var circle = (RigCircleShape2D)rig.AddShape("Circle", root);
        circle.LocalX = 10; circle.LocalY = 4;
        var rectangle = (RigRectangleShape2D)rig.AddShape("Rectangle", child);
        rectangle.AngleDegrees = 17;
        rectangle.CornerRadius = 8;
        var capsule = (RigCapsuleShape2D)rig.AddShape("Capsule", child);
        capsule.Purpose = RigShapePurpose.Collision;
        var polygon = (RigPolygonShape2D)rig.AddShape("Polygon", child);
        polygon.LocalY = -7;

        var model = RigAuthoredBridge2D.Export(rig, "check-rig", "Check rig");
        var resolved = ResolvedModel.From(CharacterModel.FromJson(model.ToJson()));
        if (resolved.Controls.Count != rig.Bones.Count)
            throw new InvalidOperationException("The exported rig should need one control per bone.");
        Compare(rig, resolved, PoseEvaluator.Rest(resolved));
        if (!resolved.Parts.Single(p => p.Id == $"part-{capsule.Id}").Hidden)
            throw new InvalidOperationException("Collision-only geometry should not be drawn.");
        if (resolved.Parts.Single(p => p.Id == $"part-{rectangle.Id}").Geometry is not RoundedRectangleShapeDefinition2D)
            throw new InvalidOperationException("Noodle's corner radius should export as a rounded rectangle.");
        if (resolved.Parts.Single(p => p.Id == $"part-{polygon.Id}").Geometry is not ConvexPolygonShapeDefinition2D { Vertices.Count: 5 })
            throw new InvalidOperationException("Polygon vertices were lost.");

        // A rotate track on a bone origin must carry all child bones and attachments with it.
        var clip = new MotionClip { Id = "check-rotation", Name = "Rotation", Model = model.Id };
        clip.Tracks.Add(new ClipTrack
        {
            Kind = MotionClip.RotateKind,
            Target = RigAuthoredBridge2D.BoneControl(root),
            Keys = [new ClipKey { Time = 0 }, new ClipKey { Time = 1, Angle = .4f }]
        });
        clip.Tracks.Add(new ClipTrack
        {
            Kind = MotionClip.RotateKind,
            Target = RigAuthoredBridge2D.BoneControl(child),
            Keys = [new ClipKey { Time = 0 }, new ClipKey { Time = 1, Angle = -.2f }]
        });
        clip.Validate(resolved);
        root.AngleDegrees += .4f * 180 / MathF.PI;
        child.AngleDegrees -= .2f * 180 / MathF.PI;
        Compare(rig, resolved, PoseEvaluator.Sample(resolved, clip, 1));
        CheckSharedBoneIk();
    }

    private static void CheckSharedBoneIk()
    {
        var rig = new RigDocument2D();
        var root = rig.AddBone(name: "beam"); root.Length = 60;
        var joint = rig.AddBone(root, "hinge"); joint.LocalX = 60; joint.LocalY = 0; joint.AngleDegrees = 0; joint.Length = 40;
        var end = rig.AddBone(joint, "tool"); end.LocalX = 40; end.LocalY = 0; end.AngleDegrees = 0; end.Length = 10;
        rig.AddShape("Rectangle", root); rig.AddShape("Capsule", joint); rig.AddShape("Circle", end);
        var model = RigAuthoredBridge2D.Export(rig, "ik-bridge", "IK bridge");
        var constraint = ModelAuthoring.AddBoneIk(model, RigAuthoredBridge2D.BoneControl(end)); constraint.Bend = -1;
        var resolved = ResolvedModel.From(CharacterModel.FromJson(model.ToJson()));
        var clip = ClipAuthoring.New(resolved, "ik-bridge-aim", "Aim");
        ClipAuthoring.Pose(resolved, clip, PoseEvaluator.Rest(resolved), 0, constraint.End, new(.6f, .4f, 0));
        clip.Validate(resolved);
        // The exported Noodle bones and artwork use exactly the Studio constraint evaluator.
        joint.AngleDegrees = 90;
        Compare(rig, resolved, PoseEvaluator.Sample(resolved, clip, 0));
    }

    private static void Compare(RigDocument2D rig, ResolvedModel model, EvaluatedPose pose)
    {
        foreach (var bone in rig.Bones)
        {
            var frame = RigDocument2D.GetWorldTransform(bone);
            var id = RigAuthoredBridge2D.BoneControl(bone);
            Near(Vector2.Transform(Vector2.Zero, frame), pose.World(id), id);
            var authored = new BoneFrame2D(new(pose.World(id).X, pose.World(id).Y), pose.Angles[id], model.Controls[id].Length);
            Near(Vector2.Transform(new Vector2(bone.Length, 0), frame), new Vector3(authored.Tip, 0), $"{id}-tip");
        }
        foreach (var shape in rig.Shapes)
        {
            var frame = RigDocument2D.GetWorldTransform(shape.AttachedBone);
            var center = Vector2.Transform(new Vector2(shape.LocalX, shape.LocalY), frame);
            var up = Vector2.TransformNormal(Vector2.UnitY,
                Matrix3x2.CreateRotation(MathF.PI / 180f * shape.AngleDegrees) * frame);
            var id = $"part-{shape.Id}";
            var part = model.Parts.Single(p => p.Id == id);
            var authored = PartGeometry.FrameOf(part, pose.World, control => pose.Angles[control]);
            Near(center, authored.Origin, id);
            if (Vector2.Distance(authored.Up, up) > 1e-4f)
                throw new InvalidOperationException($"{id} orientation differs from the Noodle attachment.");
        }
    }

    private static void Near(Vector2 pixels, Vector3 authored, string id)
    {
        var expected = pixels * RigAuthoredBridge2D.UnitsPerPixel;
        if (Vector2.Distance(expected, new Vector2(authored.X, authored.Y)) > 1e-4f)
            throw new InvalidOperationException($"{id}: expected {expected}, got {authored}.");
    }
}
