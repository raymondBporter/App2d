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
/// One animated part instance evaluated inside its wearer's pose (P7a): its own evaluated pose in the asset's space, its
/// placement in the character frame, the expression it shows, whether it is hidden and the layout fit its controls were
/// repositioned by. Drawing, picking, contours and editor handles read the nested pose through <see cref="World"/>,
/// <see cref="Frame"/>, <see cref="Rest"/> and <see cref="RestFrame"/>, which carry it into the wearer's frame.
/// </summary>
public sealed record EvaluatedAttachment(ResolvedAttachment Attachment, EvaluatedPose Pose, LayeredFrame2D Placement, string? Expression, bool Hidden, Vector2 LayoutFit)
{
    public string Id => Attachment.Id;
    public ResolvedModel Rig => Attachment.Rig!;

    private Vector3 Fitted(Vector3 point) => new(point.X * LayoutFit.X, point.Y * LayoutFit.Y, point.Z);

    /// <summary>A rig control's origin in the wearer's frame.</summary>
    public Vector3 World(string control) => Placement.At(Fitted(Pose.Points[control]));
    /// <summary>A rig control's placement in the wearer's frame: the artwork carrier of a bone-attached piece.</summary>
    public LayeredFrame2D Frame(string control)
    {
        var local = Pose.Placement(control);
        return (LayoutFit == Vector2.One ? local : local.WithOrigin(Fitted(local.Origin))).Then(Placement);
    }

    public Vector3 Rest(string control) => Placement.At(Fitted(Rig.Rest[control]));
    public LayeredFrame2D RestFrame(string control)
    {
        var local = Rig.RestPlacement(control);
        return (LayoutFit == Vector2.One ? local : local.WithOrigin(Fitted(local.Origin))).Then(Placement);
    }

    /// <summary>The pieces drawn for this instance: fitted and visible per the nested pose.</summary>
    public IEnumerable<PartGeometry> DrawnParts() => Attachment.Parts.Where(Pose.Visible).Select(Pose.Drawn);

    /// <summary>A wearer-side namespaced name for one of this instance's targets.</summary>
    public string Qualify(string target) => Id + Part.Namespace + target;
}
