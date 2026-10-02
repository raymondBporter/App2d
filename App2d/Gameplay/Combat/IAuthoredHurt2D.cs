using App2d.Core.Geometry;
using System.Numerics;

namespace App2d.Gameplay.Combat;

/// <summary>Pose-derived damage geometry, independent of the stable terrain movement body.</summary>
public interface IAuthoredHurt2D
{
    bool OverlapsHurt(Rect2D hit);
    Vector2? HurtContact(Rect2D hit);
}
