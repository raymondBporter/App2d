using App2d.Core.Characters.Geometry;
using App2d.Core.Json;
using App2d.Core.Mathematics.Transforms;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// One rig node. <see cref="Node"/> declares how its setup is authored (<see cref="RigNodeKinds"/>): a point control's
/// <see cref="Rest"/>/<see cref="RestAngle"/> are character-frame values the resolver turns into a parent-local similarity;
/// a bone node authors <see cref="Local"/>, <see cref="Length"/> and an optional <see cref="Link"/> and derives its rest.
/// Scale names the measure this control's animated translation scales with.
/// </summary>
public sealed record ModelControl
{
    public string Id { get; set; } = "";
    public string? Parent { get; set; }
    public string? Name { get; set; }
    /// <summary>The declared setup kind, one of <see cref="RigNodeKinds"/>. Never inferred from the other members.</summary>
    public string Node { get; set; } = RigNodeKinds.Point;
    /// <summary>Canonical parent-local similarity setup of a <see cref="RigNodeKinds.Bone"/> node (null is the identity).</summary>
    public RigSetup? Local { get; set; }
    /// <summary>Binds a bone node's origin to a position along its parent bone instead of <see cref="Local"/>'s translation.</summary>
    public RigLink? Link { get; set; }
    /// <summary>Optional angular joint limits of a bone node (P4b): bounds on its parent-local rotation. A point node has none.</summary>
    public RigLimit? Limit { get; set; }
    /// <summary>Character-frame rest of a point node. Derived, and so omitted, for a bone node.</summary>
    public PuppetPoint Rest { get; set; }
    /// <summary>Character-frame XY direction of a point node's frame in rest, in radians. Derived for a bone node.</summary>
    public float RestAngle { get; set; }
    /// <summary>Bone length along local +X. Zero is an ordinary control, still useful as a handle, socket or target.</summary>
    public float Length { get; set; }
    public string Scale { get; set; } = CharacterModel.Unit;
}
