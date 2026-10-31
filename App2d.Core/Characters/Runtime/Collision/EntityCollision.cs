using App2d.Core.Characters.Geometry;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics;
using App2d.Core.Mathematics.Transforms;
using System.Numerics;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// Movement, hurt and attack regions from one placed final pose, as values: the attack and hurt regions are the same
/// placement the actors' registered colliders use (<see cref="ResolvedCollider.Place"/>). Graphics-free.
/// </summary>
public static class EntityCollision
{

    /// <summary>The movement shape placed at the feet, mirrored to the facing.</summary>
    public static EntityRegion Movement(ResolvedEntity entity, Vector2 position, int facing) =>
        new(ColliderUses.Movement, entity.MovementShape, Similarity2D.FromAxis(position, new(facing, 0), mirror: facing < 0));

    /// <summary>The hurt regions at a pose: the entity's enabled-by-default hurt colliders placed.</summary>
    public static List<EntityRegion> Hurt(ResolvedEntity entity, ActorPose pose) =>
        [.. entity.Colliders.Where(c => c.Use == ColliderUses.Hurt && c.DefaultEnabled).Select(c => c.Region(pose))];

    /// <summary>Where a shot leaves: the first equipped prop with a muzzle, and its unit axis in world XY (facing included).</summary>
    public static (Vector3 Point, Vector2 Axis) Muzzle(ResolvedEntity entity, ActorPose pose)
    {
        var gun = entity.Equipment.FirstOrDefault(e => e.Prop.Point(EquipmentPoints.Muzzle) is not null) ?? throw new InvalidOperationException($"Entity '{entity.Id}' equips no prop with a muzzle.");
        var frame = pose.Socket(gun.Socket);
        // Gameplay stays in XY; foreshortening must not change projectile speed. A head-on barrel has no XY aim.
        return (PartPlacement3D.Point(frame, gun.Prop, gun.Prop.Point(EquipmentPoints.Muzzle)!.Value), PartPlacement3D.ProjectAxis(frame, pose.Facing));
    }

    /// <summary>
    /// The window's attack collider placed for the pose, named by the window (the hit group): the one collider placement,
    /// origin pushed along the anchor's axis, +X along that axis (the facing when the axis is foreshortened away) and +Y
    /// toward the frame's across direction so mirroring keeps up in the up direction.
    /// </summary>
    public static EntityRegion Attack(ActorPose pose, ResolvedHit hit) =>
        new(hit.Window.Id, hit.Shape, hit.Collider.Place(pose));
}
