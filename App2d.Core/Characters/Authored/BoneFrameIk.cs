using App2d.Core.Kinematics;
using System.Numerics;

namespace App2d.Core.Characters.Authored;

/// <summary>Two rigid bone segments with similarity frames: uniform scale and reflection, but no shear or collapse.</summary>
internal static class BoneFrameIk
{
    internal static void ValidateSetup(CharacterModel model)
    {
        if (!model.IkChains.Any(c => c.Solver == ModelChain.BoneSolver)) return;
        var controls = model.Controls.ToDictionary(c => c.Id, StringComparer.Ordinal);
        var frames = new Dictionary<string, Matrix3x2>(StringComparer.Ordinal);
        Matrix3x2 Frame(string id)
        {
            if (frames.TryGetValue(id, out var frame)) return frame;
            var c = controls[id];
            return frames[id] = c.Transform is null
                ? Matrix3x2.CreateRotation(c.RestAngle) * Matrix3x2.CreateTranslation(c.Rest.X, c.Rest.Y)
                : c.Transform.Matrix * (c.Parent is null ? Matrix3x2.Identity : Frame(c.Parent));
        }
        foreach (var c in controls.Values) Frame(c.Id);
        ValidateFrames(controls, model.IkChains, frames);
    }

    internal static void ValidateFrames(IReadOnlyDictionary<string, ModelControl> controls, IEnumerable<ModelChain> chains, IReadOnlyDictionary<string, Matrix3x2> frames)
    {
        foreach (var c in chains.Where(c => c.Solver == ModelChain.BoneSolver))
        {
            Segment(c, controls[c.Root], frames[c.Root], frames[c.Joint]);
            Segment(c, controls[c.Joint], frames[c.Joint], frames[c.End]);
            if (c.Frame != CharacterModel.Locomotion && !Matrix3x2.Invert(frames[c.Frame], out _))
                throw new InvalidDataException($"Bone constraint '{c.Id}': target frame '{c.Frame}' is collapsed.");
        }
    }

    private static float Segment(ModelChain chain, ModelControl bone, Matrix3x2 frame, Matrix3x2 child)
    {
        var x = new Vector2(frame.M11, frame.M12); var y = new Vector2(frame.M21, frame.M22);
        var size = x.LengthSquared(); var length = bone.Length * MathF.Sqrt(size);
        if (!float.IsFinite(length) || length < .01f || !float.IsFinite(y.LengthSquared())
            || MathF.Abs(size - y.LengthSquared()) > size * 1e-4f || MathF.Abs(Vector2.Dot(x, y)) > size * 1e-4f)
        {
            throw new InvalidDataException($"Bone constraint '{chain.Id}': '{bone.Id}' needs a uniform, unsheared frame and a world segment of at least 0.01; collapsed or nonuniform animated scales are unsupported.");
        }

        var tip = new Vector2(frame.M31, frame.M32) + x * bone.Length;
        var actual = new Vector2(child.M31, child.M32);
        if (!float.IsFinite(actual.X) || !float.IsFinite(actual.Y) || Vector2.Distance(tip, actual) > MathF.Max(1e-5f, length * 1e-4f))
            throw new InvalidDataException($"Bone constraint '{chain.Id}': the child of '{bone.Id}' must sit at its +X tip (length {bone.Length}); move the child or change the bone length.");
        return length;
    }

    internal static ChainResult Solve(ResolvedModel model, EvaluatedPose pose, ModelChain chain, Vector3 target, int bend, Vector2 zeroTargetDirection)
    {
        var root = pose.Bones[chain.Root]; var joint = pose.Bones[chain.Joint];
        var first = Segment(chain, model.Controls[chain.Root], root, joint);
        var second = Segment(chain, model.Controls[chain.Joint], joint, pose.Bones[chain.End]);
        // Bend is in the bone frame's handedness, so mirroring a rig mirrors its elbow too.
        var handedness = root.GetDeterminant() < 0 ? -1 : 1;
        var solved = TwoBoneIk2D.SolveExact(new(root.M31, root.M32), new(target.X, target.Y), first, second, bend * handedness, zeroTargetDirection);
        var firstDirection = solved.Joint - new Vector2(root.M31, root.M32);
        TransformSubtree(chain.Root, pose.Points[chain.Root], Angle(firstDirection) - MathF.Atan2(root.M12, root.M11));
        joint = pose.Bones[chain.Joint];
        TransformSubtree(chain.Joint, new(solved.Joint, pose.Points[chain.Joint].Z), Angle(solved.End - solved.Joint) - MathF.Atan2(joint.M12, joint.M11));
        TransformSubtree(chain.End, new(solved.End, target.Z), 0);
        return new(chain.Id, Vector2.Distance(solved.End, new(target.X, target.Y)), solved.ReachesTarget);

        static float Angle(Vector2 v) => MathF.Atan2(v.Y, v.X);
        void TransformSubtree(string id, Vector3 at, float rotation)
        {
            var origin = pose.Points[id];
            var depth = at.Z - origin.Z;
            var delta = Matrix3x2.CreateTranslation(-origin.X, -origin.Y) * Matrix3x2.CreateRotation(rotation) * Matrix3x2.CreateTranslation(at.X, at.Y);
            void Apply(string bone)
            {
                var frame = pose.Bones[bone] * delta;
                pose.Bones[bone] = frame;
                pose.Points[bone] = new(frame.M31, frame.M32, pose.Points[bone].Z + depth);
                pose.Angles[bone] = MathF.Atan2(frame.M12, frame.M11);
                foreach (var child in model.Children[bone]) Apply(child);
            }
            Apply(id);
        }
    }
}
