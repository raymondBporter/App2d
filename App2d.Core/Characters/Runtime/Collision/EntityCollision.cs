using App2d.Core.Characters.Geometry;
using App2d.Core.Mathematics.Transforms;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// Movement, hurt and attack regions from one placed final pose, as values: the attack and hurt regions are the same
/// placement the actors' registered colliders use (<see cref="ResolvedCollider.Place"/>). Graphics-free.
/// </summary>
public static class EntityCollision
{

    /// <summary>Where a shot leaves: the first equipped prop with a muzzle, and its unit axis in world XY (facing included).</summary>
    public static (Vector3f Point, Vector2f Axis) Muzzle(ResolvedEntity entity, ActorPose pose)
    {
        var gun = entity.Equipment.FirstOrDefault(e => e.Prop.Point(EquipmentPoints.Muzzle) is not null) ?? throw new InvalidOperationException($"Entity '{entity.Id}' equips no prop with a muzzle.");
        var frame = pose.Socket(gun.Socket);
        // Gameplay stays in XY; foreshortening must not change projectile speed. A head-on barrel has no XY aim.
        return (PartPlacement3D.Point(frame, gun.Prop, gun.Prop.Point(EquipmentPoints.Muzzle)!.Value), PartPlacement3D.ProjectAxis(frame, pose.Facing));
    }
}
