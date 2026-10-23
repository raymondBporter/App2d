using App2d.Core.Characters.Geometry;
using System.Numerics;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// The legacy affine adapter (character-migration-baseline.md, section 7.3): evaluates a <see cref="RigNodeKinds.Affine"/>
/// node's full parent-local <see cref="Mathematics.Transforms.Affine2D"/> setup with its animated scale and shear channels,
/// exactly as the explicit-bone path did before P3. The canonical similarity rig cannot express inherited nonuniform scale
/// or shear, so conversion of these nodes is reported, never approximated.
/// Callers: the native wheel fixture (<c>TestModels.Wheel</c>, NativeRigTests, the wheel reference cases) and curve-face's
/// <c>left-eye</c>/<c>right-eye</c> (scale tracks squash the pupils; their artwork-fit replacement is P7b). Studio edits them
/// through the "Legacy affine setup" inspector section. Retire in P11 with a scoped deformation or an explicit decision to drop
/// native inherited affine.
/// </summary>
internal static class LegacyAffineRig
{
    public static void Evaluate(EvaluatedPose pose, RigNode node, Vector3 translate, float rotate, Vector3 scale, Vector3 shear, float layer, LayeredFrame2D carrier)
    {
        var setup = node.Affine!;
        var local = new LayeredFrame2D(new(setup.EvaluateMatrix(
            new(setup.X + translate.X, setup.Y + translate.Y),
            setup.Rotation + rotate,
            new(setup.ScaleX * (1 + scale.X), setup.ScaleY * (1 + scale.Y)),
            new(setup.ShearX + shear.X, setup.ShearY + shear.Y))), layer);
        var placed = local.Then(carrier);
        pose.Place(node.Id, placed, MathF.Atan2(placed.XAxis.Y, placed.XAxis.X));
    }
}
