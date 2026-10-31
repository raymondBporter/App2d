using App2d.Core.Animation;
using App2d.Core.Characters.Geometry;
using App2d.Core.Constraints;
using App2d.Core.Mathematics;
using App2d.Core.Mathematics.Progress;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Timing;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// One constraint's outcome. <see cref="Reached"/> is residual-based (P4b): the end lies within <see cref="ReachTolerance"/> of
/// the target; a solve that clamped to full reach, a minimum fold or a joint limit reports the gap it left instead of claiming
/// the target. <see cref="Limited"/> says a joint limit shaped the pose.
/// </summary>
public sealed record ChainResult(string Chain, float Residual, bool Reached, bool Limited = false)
{
    /// <summary>The position tolerance of the reference set: authored chains reach to ~3e-8, so anything above this is a real gap.</summary>
    public const float ReachTolerance = 1e-4f;

    public static ChainResult Of(string chain, float residual, bool limited = false) => new(chain, residual, residual <= ReachTolerance, limited);
}
public sealed record ContactResult(string Chain, Vector3 Target, float Residual);
