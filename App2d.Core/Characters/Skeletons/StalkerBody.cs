using App2d.Core.Geometry.Shapes;
using System.Collections.Immutable;
using static App2d.Core.Characters.Paints;
using static App2d.Core.Characters.StalkerSkeleton;

namespace App2d.Core.Characters;

/// <summary>The stalker's drawn body, as attachments on <see cref="StalkerSkeleton"/>.</summary>
public static class StalkerBody
{
    /// <summary>A round body on three two-part legs.</summary>
    public static ImmutableArray<Attachment> Body { get; } =
    [
        new JointPath("stroke", [Hip0, Knee0], Paint("#222b32", stroke: 0.045f)),
        new JointPath("stroke-2", [Knee0, Foot0], Paint("#222b32", stroke: 0.045f)),
        new JointPath("stroke-3", [Hip1, Knee1], Paint("#222b32", stroke: 0.045f)),
        new JointPath("stroke-4", [Knee1, Foot1], Paint("#222b32", stroke: 0.045f)),
        new JointPath("stroke-5", [Hip2, Knee2], Paint("#222b32", stroke: 0.045f)),
        new JointPath("stroke-6", [Knee2, Foot2], Paint("#222b32", stroke: 0.045f)),
        new MountedShape("ellipse", BodyUpright, Ellipse2D.Builder.Create(new(0.475f, 0.19f)), Paint("#c9e0b8", 0.045f), -0.05f),
    ];
}
