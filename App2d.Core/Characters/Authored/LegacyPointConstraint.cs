using App2d.Core.Kinematics;
using System.Numerics;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// The legacy point-constraint adapter (plan rule 7; character-migration-baseline.md, section 7.2): a two-bone IK constraint
/// declared with <see cref="ModelChain.PointSolver"/> moves its joint and end to the solved positions and translates their
/// descendants by the same amount; no frame turns, segment lengths are the rest distances, and the historical reach inset
/// of <see cref="TwoBoneIk2D.Solve"/> is kept. A linked joint leaves its setup link for the duration of the solve.
/// Callers (every shipped chain is declared this way): person (left-arm, right-arm, left-leg, right-leg), stalker (three
/// legs), quadruped, triceratops and baby-triceratops-rig (four legs each); the <c>Creature</c>, <c>Linkage</c> and tripod
/// fixtures in tests and the editor smoke. Converting a bundle to the canonical <see cref="ModelChain.BoneSolver"/> is an
/// intentional difference recorded per bundle (angles of the solved controls and any socket framed by the root change);
/// retire this adapter in P11 when the last bundle converts.
/// </summary>
internal static class LegacyPointConstraint
{
    public static ChainResult Solve(ResolvedModel model, EvaluatedPose pose, ModelChain chain, Vector3 target, int bend)
    {
        var root = pose.Points[chain.Root];
        var solved = TwoBoneIk2D.Solve(XY(root), XY(target), model.Length(chain.Root, chain.Joint), model.Length(chain.Joint, chain.End), bend);
        var joint = new Vector3(solved.Joint, pose.Points[chain.Joint].Z);
        var end = new Vector3(solved.End, target.Z);
        MoveDescendants(chain.Joint, joint - pose.Points[chain.Joint], chain.End);
        MoveDescendants(chain.End, end - pose.Points[chain.End], null);
        pose.Translate(chain.Joint, joint);
        pose.Translate(chain.End, end);
        return ChainResult.Of(chain.Id, Vector2.Distance(solved.End, XY(target)));

        void MoveDescendants(string id, Vector3 delta, string? except)
        {
            foreach (var child in model.Children[id])
            {
                if (child == except)
                {
                    continue;
                }

                pose.Translate(child, pose.Points[child] + delta);
                MoveDescendants(child, delta, null);
            }
        }
    }

    private static Vector2 XY(Vector3 v) => new(v.X, v.Y);
}
