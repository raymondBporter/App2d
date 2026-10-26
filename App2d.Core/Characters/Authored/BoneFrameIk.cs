using App2d.Core.Constraints;
using App2d.Core.Kinematics;
using App2d.Core.Mathematics;
using App2d.Core.Mathematics.Transforms;
using System.Numerics;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// The canonical two-bone IK constraint (<see cref="ModelChain.BoneSolver"/>): two rigid segments whose similarity frames
/// turn with the solve, carrying their descendants and attachments. Uniform scale and reflection are supported; shear and
/// collapse are rejected by name. Joint limits (P4b) make the solve limit-aware: the analytic pose is checked against each
/// limited node's parent-local rotation and, when a bound is crossed, re-solved with that rotation held at the bound and the
/// remaining rotation aimed at the target, so the result is the closest in-range pose with an honest residual. A pose that
/// was clamped, folded or held never reports the target as reached (<see cref="ChainResult.Of"/>).
/// </summary>
internal static class BoneFrameIk
{
    /// <summary>Checks every bone constraint's segments on the resolved nodes: a bound or scaled length is the node's, not the authored control's.</summary>
    internal static void ValidateFrames(IReadOnlyDictionary<string, RigNode> nodes, IEnumerable<ModelChain> chains)
    {
        foreach (var c in chains.Where(c => c.Solver == ModelChain.BoneSolver))
        {
            Segment(c, nodes[c.Root], nodes[c.Root].RestFrame, nodes[c.Joint].RestFrame);
            Segment(c, nodes[c.Joint], nodes[c.Joint].RestFrame, nodes[c.End].RestFrame);
            if (c.Frame != CharacterModel.Locomotion && !Matrix3x2.Invert(nodes[c.Frame].RestFrame, out _))
            {
                throw new InvalidDataException($"Bone constraint '{c.Id}': target frame '{c.Frame}' is collapsed.");
            }
        }
    }

    private static float Segment(ModelChain chain, RigNode bone, Matrix3x2 frame, Matrix3x2 child)
    {
        var (x, y, _) = frame.GetRows();
        var size = x.LengthSquared();
        var length = bone.Length * MathF.Sqrt(size);
        if (!float.IsFinite(length) || length < .01f || !float.IsFinite(y.LengthSquared())
            || MathF.Abs(size - y.LengthSquared()) > size * 1e-4f || MathF.Abs(Vector2.Dot(x, y)) > size * 1e-4f)
        {
            throw new InvalidDataException($"Bone constraint '{chain.Id}': '{bone.Id}' needs a uniform, unsheared frame and a world segment of at least 0.01; collapsed or nonuniform animated scales are unsupported.");
        }

        var tip = Vector2.Transform(new(bone.Length, 0), frame);
        var actual = child.Translation;
        if (!actual.IsFinite() || Vector2.Distance(tip, actual) > MathF.Max(1e-5f, length * 1e-4f))
        {
            throw new InvalidDataException($"Bone constraint '{chain.Id}': the child of '{bone.Id}' must sit at its +X tip (length {bone.Length}); move the child or change the bone length.");
        }

        return length;
    }

    internal static ChainResult Solve(ResolvedModel model, EvaluatedPose pose, ModelChain chain, Vector3 target, int bend, Vector2 zeroTargetDirection)
    {
        var root = pose.Bones[chain.Root];
        var joint = pose.Bones[chain.Joint];
        var first = Segment(chain, model.Nodes[chain.Root], root, joint);
        var second = Segment(chain, model.Nodes[chain.Joint], joint, pose.Bones[chain.End]);
        var rootOrigin = new Vector2(root.M31, root.M32);
        var goal = new Vector2(target.X, target.Y);

        // Bend is in the bone frame's handedness, so mirroring a rig mirrors its elbow too.
        var handedness = root.GetDeterminant() < 0 ? -1 : 1;
        var solved = TwoBoneIk2D.SolveExact(rootOrigin, goal, first, second, bend * handedness, zeroTargetDirection);
        var firstAngle = Angle(solved.Joint - rootOrigin);
        var secondAngle = Angle(solved.End - solved.Joint);
        var limited = Limit(model, pose, chain, rootOrigin, goal, first, second, handedness, ref firstAngle, ref secondAngle);

        var jointPosition = rootOrigin + first * Direction(firstAngle);
        var endPosition = jointPosition + second * Direction(secondAngle);
        TransformSubtree(chain.Root, pose.Points[chain.Root], firstAngle - Angle(root));
        joint = pose.Bones[chain.Joint];
        TransformSubtree(chain.Joint, new(jointPosition, pose.Points[chain.Joint].Z), secondAngle - Angle(joint));
        TransformSubtree(chain.End, new(endPosition, target.Z), 0);
        return ChainResult.Of(chain.Id, Vector2.Distance(endPosition, goal), limited);

        void TransformSubtree(string id, Vector3 at, float rotation)
        {
            var origin = pose.Points[id];
            var depth = at.Z - origin.Z;
            Apply(id, Matrix3x2.CreateTranslation(-origin.X, -origin.Y) * Matrix3x2.CreateRotation(rotation) * Matrix3x2.CreateTranslation(at.X, at.Y));
            void Apply(string bone, Matrix3x2 delta)
            {
                // A character-oriented node rides its parent's origin and keeps the character axes: the solve carries it
                // and its subtree by translation only, so its artwork, sockets and children keep their authored turn.
                var oriented = model.Nodes[bone].CharacterOriented;
                if (oriented)
                {
                    var was = new Vector2(pose.Points[bone].X, pose.Points[bone].Y);
                    delta = Matrix3x2.CreateTranslation(Vector2.Transform(was, delta) - was);
                }

                var frame = pose.Bones[bone] * delta;
                pose.Place(bone, frame, pose.Points[bone].Z + depth, oriented ? pose.Angles[bone] : Angle(frame));
                foreach (var child in model.Children[bone])
                {
                    Apply(child, delta);
                }
            }
        }
    }

    /// <summary>
    /// The limit-aware step. The root's parent-local rotation is <c>parentHand * (firstAngle - parentAngle)</c> and the joint's
    /// is <c>rootHand * (secondAngle - firstAngle)</c>, each known modulo a turn and unwrapped into its declared range. A joint
    /// bound that is crossed fixes the triangle's shape, which is then aimed at the target as a whole; a root bound that is
    /// crossed fixes the upper segment, and the lower one aims at the target from there. The held rotations are recorded in
    /// the pose's limit states. Returns whether any bound shaped the pose.
    /// </summary>
    private static bool Limit(ResolvedModel model, EvaluatedPose pose, ModelChain chain, Vector2 rootOrigin, Vector2 goal, float first, float second, int rootHand, ref float firstAngle, ref float secondAngle)
    {
        var rootNode = model.Nodes[chain.Root];
        var jointNode = model.Nodes[chain.Joint];
        if (rootNode.Limit is null && jointNode.Limit is null)
        {
            return false;
        }

        var parentAngle = rootNode.Parent is null ? 0 : Angle(pose.Bones[rootNode.Parent]);
        var parentHand = rootNode.Parent is null ? 1 : pose.Bones[rootNode.Parent].GetDeterminant() < 0 ? -1 : 1;
        float RootLocal(float a) => parentHand * Wrap(a - parentAngle);
        float JointLocal(float a1, float a2) => rootHand * Wrap(a2 - a1);
        var rootState = rootNode.Limit is { } r0 ? r0.Evaluate(r0.Unwrap(RootLocal(firstAngle))) : (ConstraintLimitEvaluation1D?)null;
        var jointState = jointNode.Limit is { } j0 ? j0.Evaluate(j0.Unwrap(JointLocal(firstAngle, secondAngle))) : (ConstraintLimitEvaluation1D?)null;
        var limited = false;
        if (jointState is { IsSatisfied: false } heldJoint)
        {
            limited = true;
            // The elbow is held at its bound: the two segments form a fixed shape that turns as one about the root to face the target.
            var phi = rootHand * heldJoint.Target;
            var shape = new Vector2(first + second * MathF.Cos(phi), second * MathF.Sin(phi));
            var aim = goal - rootOrigin;
            if (aim.LengthSquared() > 1e-12f)
            {
                firstAngle = Angle(aim) - Angle(shape);
            }

            if (rootNode.Limit is { } rootLimit)
            {
                firstAngle = parentAngle + parentHand * rootLimit.Evaluate(rootLimit.Unwrap(RootLocal(firstAngle))).Target;
            }

            secondAngle = firstAngle + phi;
        }
        else if (rootState is { IsSatisfied: false } heldRoot)
        {
            limited = true;
            // The upper segment is held at its bound; the lower one aims at the target from the fixed elbow.
            firstAngle = parentAngle + parentHand * heldRoot.Target;
            var aim = goal - (rootOrigin + first * Direction(firstAngle));
            if (aim.LengthSquared() > 1e-12f)
            {
                secondAngle = Angle(aim);
            }

            if (jointNode.Limit is { } jointLimit)
            {
                secondAngle = firstAngle + rootHand * jointLimit.Evaluate(jointLimit.Unwrap(JointLocal(firstAngle, secondAngle))).Target;
            }
        }

        if (rootNode.Limit is { } rl)
        {
            pose.Limits[chain.Root] = rl.Evaluate(rl.Unwrap(RootLocal(firstAngle)));
        }

        if (jointNode.Limit is { } jl)
        {
            pose.Limits[chain.Joint] = jl.Evaluate(jl.Unwrap(JointLocal(firstAngle, secondAngle)));
        }

        return limited;
    }

    private static float Angle(Vector2 v) => MathF.Atan2(v.Y, v.X);
    private static float Angle(Matrix3x2 frame) => MathF.Atan2(frame.M12, frame.M11);
    private static Vector2 Direction(float angle) => new(MathF.Cos(angle), MathF.Sin(angle));
    /// <summary>An angle wrapped into (-pi, pi].</summary>
    private static float Wrap(float angle) => MathF.IEEERemainder(angle, MathF.Tau);
}
