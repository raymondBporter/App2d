using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
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
        var capsule = (RigCapsuleShape2D)rig.AddShape("Capsule", child);
        capsule.Purpose = RigShapePurpose.Collision;
        var polygon = (RigPolygonShape2D)rig.AddShape("Polygon", child);
        polygon.LocalY = -7;

        var model = RigAuthoredBridge2D.Export(rig, "check-rig", "Check rig");
        var resolved = ResolvedModel.From(CharacterModel.FromJson(model.ToJson()));
        Compare(rig, PoseEvaluator.Rest(resolved));
        if (!resolved.Parts.Single(p => p.Id == $"part-{capsule.Id}").Hidden)
            throw new InvalidOperationException("Collision-only geometry should not be drawn.");
        if (resolved.Parts.Single(p => p.Id == $"part-{polygon.Id}").Points?.Count != 5)
            throw new InvalidOperationException("Polygon vertices were lost.");

        // A rotate track on a bone origin must carry all child bones and attachments with it.
        var clip = new MotionClip { Id = "check-rotation", Name = "Rotation", Model = model.Id };
        clip.Tracks.Add(new ClipTrack { Kind = MotionClip.RotateKind, Target = RigAuthoredBridge2D.BoneControl(root),
            Keys = [new ClipKey { Time = 0 }, new ClipKey { Time = 1, Angle = .4f }] });
        clip.Tracks.Add(new ClipTrack { Kind = MotionClip.RotateKind, Target = RigAuthoredBridge2D.BoneControl(child),
            Keys = [new ClipKey { Time = 0 }, new ClipKey { Time = 1, Angle = -.2f }] });
        clip.Validate(resolved);
        root.AngleDegrees += .4f * 180 / MathF.PI;
        child.AngleDegrees -= .2f * 180 / MathF.PI;
        Compare(rig, PoseEvaluator.Sample(resolved, clip, 1));
    }

    private static void Compare(RigDocument2D rig, EvaluatedPose pose)
    {
        foreach (var bone in rig.Bones)
        {
            var frame = RigDocument2D.GetWorldTransform(bone);
            var id = RigAuthoredBridge2D.BoneControl(bone);
            Near(Vector2.Transform(Vector2.Zero, frame), pose.World(id), id);
            Near(Vector2.Transform(new Vector2(bone.Length, 0), frame), pose.World($"{id}-tip"), $"{id}-tip");
        }
        foreach (var shape in rig.Shapes)
        {
            var frame = RigDocument2D.GetWorldTransform(shape.AttachedBone);
            var center = Vector2.Transform(new Vector2(shape.LocalX, shape.LocalY), frame);
            var up = Vector2.TransformNormal(Vector2.UnitY,
                Matrix3x2.CreateRotation(MathF.PI / 180f * shape.AngleDegrees) * frame);
            var id = RigAuthoredBridge2D.ShapeControl(shape);
            Near(center, pose.World(id), id);
            Near(center + up, pose.World($"{id}-up"), $"{id}-up");
            var part = new PuppetPart { A = id, B = $"{id}-up" };
            var authored = PartGeometry.FrameOf(part, pose.World);
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
