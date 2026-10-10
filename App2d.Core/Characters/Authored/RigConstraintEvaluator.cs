using App2d.Core.Kinematics;
using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Core.Characters.Authored;

/// <summary>A sampled IK goal in model/world XY and character depth. Bend is +1 or -1.</summary>
public readonly record struct RigIkTarget(string Constraint, Vector3 Position, int Bend);
/// <summary>A contact pin applied after its IK goal. Release blends toward that goal; a releasing pin is not reported as held.</summary>
public readonly record struct RigContactPin(string Constraint, Vector3 Position, float Release = 0);

/// <summary>
/// Shared, graphics-free IK/contact evaluation for one pose. Apply targets in model order, then contact pins in clip order.
/// This object owns no simulation clock or persistent physics state; create one for each sampled pose.
/// </summary>
public sealed class RigConstraintEvaluator(ResolvedModel model, EvaluatedPose pose)
{
    private readonly Dictionary<string, (RigIkTarget Target, int Index)> _targets = new(StringComparer.Ordinal);
    // A target exactly at the root has no direction. Retain the FK axis so reflection and repeated pins are stable.
    private readonly Dictionary<string, Vector2> _foldDirections = model.IkConstraints.Where(c => c.Solver == ModelChain.BoneSolver)
        .ToDictionary(c => c.Id, c => new Vector2(pose.Bones[c.Root].M11, pose.Bones[c.Root].M12), StringComparer.Ordinal);

    public void Apply(RigIkTarget target)
    {
        if (target.Bend is not (-1 or 1)) throw new ArgumentOutOfRangeException(nameof(target), "Bend must be -1 or 1.");
        if (_targets.ContainsKey(target.Constraint)) throw new InvalidOperationException($"Constraint '{target.Constraint}' already has a target in this pose.");
        var result = Solve(target.Constraint, target.Position, target.Bend);
        _targets.Add(target.Constraint, (target, pose.Chains.Count));
        pose.Chains.Add(result);
    }

    public void Apply(RigContactPin pin)
    {
        if (!float.IsFinite(pin.Release) || pin.Release is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(pin), "Contact release must be between 0 and 1.");
        if (!_targets.TryGetValue(pin.Constraint, out var goal)) throw new InvalidOperationException($"Contact '{pin.Constraint}' needs an IK target first.");
        var target = pin.Release > 0 ? Vector3.Lerp(pin.Position, goal.Target.Position, pin.Release) : pin.Position;
        var result = Solve(pin.Constraint, target, goal.Target.Bend);
        pose.Chains[goal.Index] = result;
        if (pin.Release == 0) pose.Contacts.Add(new(pin.Constraint, target, result.Residual));
    }

    /// <summary>Converts a sampled offset into a goal. Point constraints retain the original rotation-only frame convention.</summary>
    public static Vector3 TargetPosition(ResolvedModel model, EvaluatedPose pose, ModelChain chain, Vector3 delta)
    {
        var locomotion = chain.Frame == CharacterModel.Locomotion;
        var point = locomotion ? pose.Locomotion : pose.Points[chain.Frame];
        var rest = locomotion ? Vector3.Zero : model.Rest[chain.Frame];
        if (chain.Solver == ModelChain.PointSolver || locomotion)
            return point + Rotation2D.ApplyXY(model.Rest[chain.End] - rest + delta, locomotion ? 0 : pose.Angles[chain.Frame]);
        var (setupInverse, frame) = TargetFrames(model, pose, chain);
        var local = Vector2.Transform(XY(model.Rest[chain.End]), setupInverse) + XY(delta);
        return new(Vector2.Transform(local, frame), point.Z + model.Rest[chain.End].Z - rest.Z + delta.Z);
    }

    /// <summary>Inverse of TargetPosition, before reference-measure scaling. Used by dragging as well as other authoring hosts.</summary>
    public static Vector3 TargetDelta(ResolvedModel model, EvaluatedPose pose, ModelChain chain, Vector3 world)
    {
        var locomotion = chain.Frame == CharacterModel.Locomotion;
        var point = locomotion ? pose.Locomotion : pose.Points[chain.Frame];
        var rest = locomotion ? Vector3.Zero : model.Rest[chain.Frame];
        if (chain.Solver == ModelChain.PointSolver || locomotion)
            return Rotation2D.ApplyXY(world - point, locomotion ? 0 : -pose.Angles[chain.Frame]) - (model.Rest[chain.End] - rest);
        var (setupInverse, frame) = TargetFrames(model, pose, chain);
        Matrix3x2.Invert(frame, out var inverse);
        var delta = Vector2.Transform(XY(world), inverse) - Vector2.Transform(XY(model.Rest[chain.End]), setupInverse);
        return new(delta, world.Z - point.Z - model.Rest[chain.End].Z + rest.Z);
    }

    private static (Matrix3x2 SetupInverse, Matrix3x2 Frame) TargetFrames(ResolvedModel model, EvaluatedPose pose, ModelChain chain)
    {
        var frame = pose.Bones[chain.Frame];
        if (!Matrix3x2.Invert(model.RestTransforms[chain.Frame], out var setupInverse) || !Matrix3x2.Invert(frame, out _))
            throw new InvalidDataException($"Bone constraint '{chain.Id}': target frame '{chain.Frame}' is collapsed.");
        return (setupInverse, frame);
    }

    private ChainResult Solve(string id, Vector3 target, int bend)
    {
        if (!float.IsFinite(target.X) || !float.IsFinite(target.Y) || !float.IsFinite(target.Z))
            throw new InvalidDataException($"Constraint '{id}': target must be finite.");
        if (!model.Chains.TryGetValue(id, out var chain)) throw new InvalidDataException($"Unknown IK constraint '{id}'.");
        return chain.Solver == ModelChain.BoneSolver ? BoneFrameIk.Solve(model, pose, chain, target, bend, _foldDirections[id]) : SolvePoints(chain, target, bend);
    }

    private ChainResult SolvePoints(ModelChain chain, Vector3 target, int bend)
    {
        // Preserve the old point solver exactly: rest segment lengths, translated descendants, unchanged angles.
        var root = pose.Points[chain.Root];
        var solved = TwoBoneIk2D.Solve(XY(root), XY(target), model.Length(chain.Root, chain.Joint), model.Length(chain.Joint, chain.End), bend);
        var joint = new Vector3(solved.Joint, pose.Points[chain.Joint].Z);
        var end = new Vector3(solved.End, target.Z);
        MoveDescendants(chain.Joint, joint - pose.Points[chain.Joint], chain.End);
        MoveDescendants(chain.End, end - pose.Points[chain.End], null);
        Place(chain.Joint, joint); Place(chain.End, end);
        return new(chain.Id, Vector2.Distance(solved.End, XY(target)), solved.ReachesTarget);

        void Place(string id, Vector3 point)
        {
            pose.Points[id] = point;
            var frame = pose.Bones[id]; frame.M31 = point.X; frame.M32 = point.Y; pose.Bones[id] = frame;
        }
        void MoveDescendants(string id, Vector3 delta, string? except)
        {
            foreach (var child in model.Children[id])
            {
                if (child == except) continue;
                Place(child, pose.Points[child] + delta); MoveDescendants(child, delta, null);
            }
        }
    }

    private static Vector2 XY(Vector3 v) => new(v.X, v.Y);
}
