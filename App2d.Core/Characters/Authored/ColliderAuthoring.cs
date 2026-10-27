using App2d.Core.Geometry.Primitives;
using App2d.Core.Mathematics;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// Collider definitions authored from a model's rest pose (P8c, P11b): the one derivation the seeds, the entity template and
/// the former conversion share, so generated content declares exactly what Studio and the conversion wrote.
/// </summary>
public static class ColliderAuthoring
{
    /// <summary>
    /// A hurt collider holding the rest-pose box of <paramref name="controls"/> inflated by <paramref name="pad"/>, bound to the
    /// region's anchor control (its first control whose parent lies outside the region) in the screen frame, so the box follows
    /// that control and keeps its size (a hairline capsule when the box is degenerate). Sizes and offsets are rounded to five decimals.
    /// </summary>
    /// <summary>Radius, in authored units, given to a hurt box whose controls enclose no area.</summary>
    public const float DegenerateHurtRadius = .005f;

    public static PartGeometry HurtBox(ResolvedModel model, string id, IReadOnlyList<string> controls, float pad)
    {
        if (controls.Count == 0)
        {
            throw new InvalidDataException($"Hurt collider '{id}': at least one control is needed.");
        }

        var rest = PoseEvaluator.Rest(model);
        var inRegion = controls.ToHashSet(StringComparer.Ordinal);
        var anchor = controls.First(c => model.Controls[c].Parent is not { } parent || !inRegion.Contains(parent));
        var anchorRest = rest.Points[anchor].XY;
        var bounds = Rect2D.FromPoints(controls.Select(c => rest.Points[c].XY)).InflatedBy(pad);
        var definition = new PartGeometry { Id = id, A = anchor, Frame = CharacterModel.Locomotion, Collision = new() };
        if (bounds.Width > 0 && bounds.Height > 0)
        {
            definition.Geometry = Rectangle2D.Builder.FromSize(new Vector2(MathF.Round(bounds.Width, 5), MathF.Round(bounds.Height, 5)));
            var offset = bounds.Center - anchorRest;
            definition.OffsetX = MathF.Round(offset.X, 5);
            definition.OffsetY = MathF.Round(offset.Y, 5);
        }
        else
        {
            definition.Geometry = Capsule2D.Builder.Create(bounds.Min - anchorRest, bounds.Max - anchorRest, DegenerateHurtRadius);
        }

        return definition;
    }

    /// <summary>What <see cref="HurtBox"/> declared, for a report.</summary>
    public static string Describe(PartGeometry hurt) => hurt.Geometry is Rectangle2D box
        ? $"box {box.Size.X:0.###} x {box.Size.Y:0.###} at offset ({hurt.OffsetX:0.###}, {hurt.OffsetY:0.###})"
        : hurt.Geometry is Capsule2D capsule ? $"hairline capsule from ({capsule.Start.X:0.###}, {capsule.Start.Y:0.###}) to ({capsule.End.X:0.###}, {capsule.End.Y:0.###})" : hurt.Geometry!.GetType().Name;
}
