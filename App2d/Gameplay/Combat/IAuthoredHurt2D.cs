using App2d.Core.Geometry.Primitives;
using System.Numerics;

namespace App2d.Gameplay.Combat;

/// <summary>
/// Pose-derived damage geometry, independent of the stable terrain movement body. When <see cref="HurtRegistered"/> is set
/// the geometry is attached colliders in the collision system and the ordinary broad phase finds it; otherwise it is the
/// legacy hurt layout, which <see cref="CombatSystem2D"/> still scans separately (retired with the last unconverted entity).
/// </summary>
public interface IAuthoredHurt2D
{
    bool HurtRegistered { get; }
    bool OverlapsHurt(Rect2D hit);
    Vector2? HurtContact(Rect2D hit);
}
