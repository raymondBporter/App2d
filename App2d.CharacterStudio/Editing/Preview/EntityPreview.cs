using App2d.CharacterStudio.Authoring;
using App2d.CharacterStudio.Importing;
using App2d.Core.Animation;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Operations.Bounds;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Physics.Motion;
using System.Numerics;

namespace App2d.CharacterStudio.Editing;

/// <summary>
/// An entity previewed in the Entity workspace at the transport time: its final pose and every region derived from it, as the
/// game would build them. <see cref="Action"/> is the previewed action; <see cref="Weight"/> its masked layer's weight.
/// </summary>
public sealed record EntityPreview(ResolvedEntity Entity, ActorPose Pose, float Time, ResolvedAction? Action, float Weight,
    EntityRegion Movement, IReadOnlyList<(ResolvedHit Hit, EntityRegion Region)> Attacks, IReadOnlyList<PlacedCollider> Colliders);

/// <summary>
/// One of the entity's attached colliders placed for the preview pose, as the runtime would register it: the same
/// placement function, with the enabled state the activation windows, alive state and default give it at that time.
/// </summary>
public sealed record PlacedCollider(ResolvedCollider Collider, EntityRegion Region, bool Enabled)
{
    public string Id => Collider.Id;
    public string Use => Collider.Use;
}
